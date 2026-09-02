using System.Text;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

/// <summary>
/// 事件驱动的战斗特效派发器。符文在其触发点(战斗逻辑、各端一致执行)调用这里的方法,
/// 由本类把可视节点延迟挂到对应 <see cref="NCreature"/> 上。纯表现层:只新建可视节点、不读写任何
/// gameplay/同步状态;取不到节点时安全跳过。<see cref="HextechCreatureNodeRegistry"/> 提供 entity→node 桥。
/// 延迟异步入口均为 fire-and-forget,对应 Run* 方法必须在顶层捕获并记录异常。
/// </summary>
internal static class HextechCombatVfxHooks
{

	/// <summary>
	/// 吞噬灵魂的特效在死亡动画开始的瞬间派发(真死亡分支必经点),而不是等 rune 的 AfterDeath:
	/// Hook.AfterDeath 是逐监听器顺序 await 的链条,排在前面的监听器等待死亡动画会让魂"卡一下"
	/// 才飞出(最后一只怪死亡时链条提前收尾所以不卡)。此处仅派发表现,数值仍在 rune 内结算。
	/// </summary>
	[HarmonyPatch(typeof(NCreature), nameof(NCreature.StartDeathAnim), typeof(bool))]
	[HextechPatch("visual.combat-vfx.soul-drain", "吞噬灵魂特效")]
	private static class StartDeathAnimPatch
	{
		[HarmonyPostfix]
		private static void Postfix(NCreature __instance) => StartDeathAnimPostfix(__instance);
	}

	private static void StartDeathAnimPostfix(NCreature __instance)
	{
		try
		{
			Creature? dead = __instance.Entity;
			if (dead is not { Side: CombatSide.Enemy } || dead.CombatState is not { } combatState
				|| !HextechMonsterInteractionPolicy.IsTrueCombatDeath(dead))
			{
				return;
			}

			foreach (Player player in combatState.Players)
			{
				if (player.Creature is { IsDead: false } collector && player.GetRelic<SoulEaterRune>() != null)
				{
					// 缕数必须在此刻(死亡瞬间)按身份算好:特效延迟一帧执行时死者已被移出战斗,
					// CombatState 为 null、按小怪兜底,导致精英/BOSS 也只掉 1-2 缕。
					HextechCombatVfx.SoulDrain(dead, collector, HextechCombatVfx.GetSoulWispCount(dead));
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][CombatVfx] Soul drain dispatch on death anim failed: {ex.Message}");
		}
	}

}

/// <summary>entity → 屏幕节点映射,由战斗节点生命周期 hook 填充;新战斗重建,取用时校验有效性。</summary>
internal static class HextechCreatureNodeRegistry
{
	private static readonly Dictionary<Creature, NCreature> Nodes = new();

	internal static void Clear()
	{
		Nodes.Clear();
	}

	internal static void Register(NCreature? node)
	{
		if (!GodotObject.IsInstanceValid(node) || node!.Entity == null)
		{
			return;
		}

		Nodes[node.Entity] = node;
	}

	/// <summary>AddCreature postfix 专用:GetCreatureNode 在战斗构建/召唤同步链上,异常不能外泄。</summary>
	internal static NCreature? SafeGetCreatureNode(NCombatRoom room, Creature creature)
	{
		try
		{
			return room.GetCreatureNode(creature);
		}
		catch (Exception ex)
		{
			if (HextechRunLogBudget.TryConsume("combat.creature-node-safe-get-failure", 5))
			{
				Log.Error($"[{ModInfo.Id}][Mayhem] GetCreatureNode failed in AddCreature postfix: {ex}");
			}

			return null;
		}
	}

	internal static NCreature? TryGet(Creature? creature)
	{
		if (creature != null && Nodes.TryGetValue(creature, out NCreature? node) && GodotObject.IsInstanceValid(node))
		{
			return node;
		}

		return null;
	}
}

internal static partial class HextechCombatVfx
{
	private enum MissileStyle
	{
		MagicMissile,
		TwinFlames
	}

	internal const float MagicMissileLaunchIntervalSeconds = 0.055f;
	internal const float MagicMissileBaseFlightSeconds = 0.28f;
	internal const float MagicMissileFlightStepSeconds = 0.025f;

	// 死亡之环改用 LoL 卡尔萨斯式幽绿光环色调(原血色已弃用)。
	private static readonly Color DeathRingColor = new(0.24f, 0.96f, 0.45f);
	private static readonly Color DeathFlashColor = new(0.62f, 1f, 0.64f);
	private static readonly Color DivineRingColor = new(1f, 0.9f, 0.55f);
	private static readonly Color DivineFlashColor = new(1f, 0.97f, 0.82f);
	// 吞噬灵魂:幽青色亡魂。
	private static readonly Color SoulColor = new(0.42f, 0.95f, 0.82f);
	private static readonly Color SoulCoreColor = new(0.78f, 1f, 0.95f);
	// 神圣干预:天降光柱与光尘。
	private static readonly Color DivineShaftColor = new(1f, 0.93f, 0.62f);
	private static readonly Color DivineDustColor = new(1f, 0.95f, 0.75f);

	// 仅表现层随机(路径弧度/粒子错落),不触碰联机决定论。
	private static readonly Random VisualRng = new();
	private static readonly Dictionary<string, Texture2D?> VanillaTextureCache = [];

	private static Texture2D? _glowTexture;
	private static Texture2D? _ringTexture;

	/// <summary>加载原版 PCK 内贴图;失败返回 null(调用方回退程序化纹理)。</summary>
	private static Texture2D? LoadVanillaTexture(string resPath)
	{
		if (VanillaTextureCache.TryGetValue(resPath, out Texture2D? cached))
		{
			return cached != null && GodotObject.IsInstanceValid(cached) ? cached : null;
		}

		Texture2D? texture = ResourceLoader.Load(resPath) as Texture2D;
		VanillaTextureCache[resPath] = texture;
		return texture;
	}

	private static Vector2 Bezier(Vector2 from, Vector2 control, Vector2 to, float t)
	{
		return from.Lerp(control, t).Lerp(control.Lerp(to, t), t);
	}

	/// <summary>同色相提满明度:比原色亮一档但不发白,保持色彩纯度。</summary>
	private static Color Brighten(Color color)
	{
		color.ToHsv(out float hue, out float saturation, out float value);
		return Color.FromHsv(hue, saturation, 1f) with { A = color.A };
	}

	/// <summary>
	/// 魂的缕数:小怪 1-2、精英 3-4、BOSS 5-6;召唤物/随从(非主要敌人)按小怪算。
	/// 必须在死亡瞬间调用——死者被移出战斗后 CombatState 为 null,只会按小怪兜底。
	/// </summary>
	internal static int GetSoulWispCount(Creature source)
	{
		MegaCrit.Sts2.Core.Rooms.RoomType roomType =
			source.CombatState?.Encounter?.RoomType ?? MegaCrit.Sts2.Core.Rooms.RoomType.Monster;
		return roomType switch
		{
			MegaCrit.Sts2.Core.Rooms.RoomType.Boss when source.IsPrimaryEnemy => 5 + VisualRng.Next(2),
			MegaCrit.Sts2.Core.Rooms.RoomType.Elite when source.IsPrimaryEnemy => 3 + VisualRng.Next(2),
			_ => 1 + VisualRng.Next(2)
		};
	}

	/// <summary>
	/// 把特效节点插到父容器中最后一个 <see cref="NCreature"/> 之后:画在所有角色之上、
	/// 但不盖住同容器后续的战斗结算等 UI 节点(追加在末尾会盖过它们)。
	/// </summary>
	private static void PlaceAboveCreatures(Node parent, Node node)
	{
		int lastCreatureIndex = -1;
		int count = parent.GetChildCount();
		for (int i = 0; i < count; i++)
		{
			if (parent.GetChild(i) is NCreature)
			{
				lastCreatureIndex = i;
			}
		}

		if (lastCreatureIndex >= 0)
		{
			parent.MoveChild(node, Math.Min(lastCreatureIndex + 1, parent.GetChildCount() - 1));
		}
	}

	// 死者立绘平均色缓存(按怪物类型;null=算不出,回退默认魂色)。
	private static readonly Dictionary<Type, Color?> MonsterTintCache = [];

	/// <summary>魂色=死者 Spine 立绘贴图的 alpha 加权平均色(抬亮压灰,魂要发光);失败回退幽青。</summary>
	private static Color GetSoulTint(Creature source)
	{
		try
		{
			if (source.Monster is not { } monster)
			{
				return SoulColor;
			}

			Type type = monster.GetType();
			if (!MonsterTintCache.TryGetValue(type, out Color? tint))
			{
				tint = ComputeMonsterAverageColor(type.Name);
				MonsterTintCache[type] = tint;
			}

			return tint ?? SoulColor;
		}
		catch
		{
			return SoulColor;
		}
	}

	private static Color? ComputeMonsterAverageColor(string monsterTypeName)
	{
		// 原版约定:类名 MechaKnight ↔ 贴图 res://animations/monsters/mecha_knight/mecha_knight.png。
		string snake = ToSnakeCase(monsterTypeName);
		if (ResourceLoader.Load($"res://animations/monsters/{snake}/{snake}.png") is not Texture2D texture
			|| texture.GetImage() is not { } image)
		{
			return null;
		}

		if (image.IsCompressed())
		{
			image.Decompress();
		}

		const int SampleSize = 32;
		image.Resize(SampleSize, SampleSize, Image.Interpolation.Bilinear);
		float r = 0f, g = 0f, b = 0f, weight = 0f;
		for (int y = 0; y < SampleSize; y++)
		{
			for (int x = 0; x < SampleSize; x++)
			{
				Color pixel = image.GetPixel(x, y);
				r += pixel.R * pixel.A;
				g += pixel.G * pixel.A;
				b += pixel.B * pixel.A;
				weight += pixel.A;
			}
		}

		if (weight < 1f)
		{
			return null;
		}

		new Color(r / weight, g / weight, b / weight).ToHsv(out float hue, out float saturation, out float value);
		return Color.FromHsv(hue, Mathf.Clamp(saturation, 0.3f, 0.8f), Mathf.Max(value, 0.8f));
	}

	private static string ToSnakeCase(string name)
	{
		StringBuilder builder = new(name.Length + 8);
		for (int i = 0; i < name.Length; i++)
		{
			char c = name[i];
			if (char.IsUpper(c))
			{
				if (i > 0)
				{
					builder.Append('_');
				}

				builder.Append(char.ToLowerInvariant(c));
			}
			else
			{
				builder.Append(c);
			}
		}

		return builder.ToString();
	}

	/// <summary>死亡之环:从施法者甩向目标的血色光束 + 目标身上炸开的死亡环与闪光。</summary>
	internal static void DeathRingLash(Creature source, Creature target)
	{
		Callable.From(() => RunDeathRingLash(source, target)).CallDeferred();
	}

	/// <summary>神圣干预:为每个受益玩家罩上一圈金色圣光脉冲与柔光。</summary>
	internal static void DivinePulse(IReadOnlyList<Creature> allies)
	{
		Creature[] snapshot = [.. allies];
		Callable.From(() => RunDivinePulse(snapshot)).CallDeferred();
	}

	/// <summary>吞噬灵魂:幽青色亡魂从死亡的敌人身上被抽离、飘向并汇入施法者。</summary>
	internal static void SoulDrain(Creature source, Creature destination, int wispCount)
	{
		Callable.From(() => RunSoulDrain(source, destination, wispCount)).CallDeferred();
	}

	/// <summary>
	/// 红黑飞弹从玩家飞向目标;返回值只表示弹道是否真实抵达。取不到节点时视为已抵达，
	/// 让 headless/测试和纯逻辑环境继续结算;场景退出导致节点销毁时返回 false，阻止旧战斗补伤害。
	/// </summary>
	internal static Task<bool> PlayMagicMissile(Creature source, Creature target, int missileIndex)
	{
		try
		{
			NCreature? sourceNode = HextechCreatureNodeRegistry.TryGet(source);
			NCreature? targetNode = HextechCreatureNodeRegistry.TryGet(target);
			if (sourceNode == null || targetNode == null)
			{
				return Task.FromResult(true);
			}

			Node? parent = targetNode.GetParent();
			if (!GodotObject.IsInstanceValid(parent))
			{
				return Task.FromResult(true);
			}

			return SpawnMissile(
				parent!,
				CreatureCenter(sourceNode),
				CreatureCenter(targetNode),
				Mathf.Min(CreatureWidth(sourceNode), CreatureWidth(targetNode)),
				missileIndex,
				missileIndex * MagicMissileLaunchIntervalSeconds,
				MissileStyle.MagicMissile);
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][CombatVfx] Magic missile failed: {ex.Message}");
			return Task.FromResult(true);
		}
	}

	/// <summary>蓝黄双生火焰沿相反弧线飞向同一随机目标，结算时序与魔法飞弹一致。</summary>
	internal static Task<bool> PlayTwinFlamesMissile(Creature source, Creature target, int missileIndex)
	{
		try
		{
			NCreature? sourceNode = HextechCreatureNodeRegistry.TryGet(source);
			NCreature? targetNode = HextechCreatureNodeRegistry.TryGet(target);
			if (sourceNode == null || targetNode == null)
			{
				return Task.FromResult(true);
			}

			Node? parent = targetNode.GetParent();
			if (!GodotObject.IsInstanceValid(parent))
			{
				return Task.FromResult(true);
			}

			return SpawnMissile(
				parent!,
				CreatureCenter(sourceNode),
				CreatureCenter(targetNode),
				Mathf.Min(CreatureWidth(sourceNode), CreatureWidth(targetNode)),
				missileIndex,
				missileIndex * MagicMissileLaunchIntervalSeconds,
				MissileStyle.TwinFlames);
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][CombatVfx] Twin Flames missile failed: {ex.Message}");
			return Task.FromResult(true);
		}
	}

	// ---- 回力OK镖:镖沿弧线依次扫过所有敌人再飞回 ----
	// 每敌 0.2s 与 CreatureCmd.Damage 内置的每次结算 0.2s 标准尾巴对齐:
	// 逻辑侧只需在首击前等待 FirstArrival,之后连续结算即可与镖同步。
	internal const float BoomerangFirstArrivalSeconds = 0.22f;
	internal const float BoomerangPerTargetSeconds = 0.2f;

	/// <summary>
	/// 回力OK镖:镖体(符文图标)自施法者掷出,弧线依次命中各敌人后飞回。
	/// <paramref name="roundTrip"/> 为 true 时回程逆序再次扫过每个敌人
	/// (最远处打个转折返),供"一来一回各结算一次伤害"的卡牌版对齐节奏。
	/// </summary>
	internal static void BoomerangSweep(Creature owner, IReadOnlyList<Creature> targets, Texture2D? boomerangTexture, bool roundTrip = false)
	{
		Creature[] snapshot = [.. targets];
		Callable.From(() => RunBoomerangSweep(owner, snapshot, boomerangTexture, roundTrip)).CallDeferred();
	}

	/// <summary>欧米伽:全场红色预警后,天降赤红审判光柱依次轰击每个敌人。</summary>
	internal static void OmegaJudgment(IReadOnlyList<Creature> targets)
	{
		Creature[] snapshot = [.. targets];
		Callable.From(() => RunOmegaJudgment(snapshot)).CallDeferred();
	}

	/// <summary>
	/// 飞身踢:处决瞬间的斩击冲击(原版 BigSlash 节点)+目标本体色爆闪;
	/// 约半秒后一缕绿色治疗光从击杀点弧线流回施法者(与尸体横飞的
	/// <see cref="FlyingKickCorpseLaunchDriver"/> 时序互补:踢击在前、横飞居中、光流殿后)。
	/// </summary>
	internal static void FlyingKickStrike(Creature target, Creature owner)
	{
		Callable.From(() => RunFlyingKickStrike(target, owner)).CallDeferred();
	}

	/// <summary>
	/// 尸爆术:尸体位置毒绿脓爆,飞溅的毒液弧线泼向每个存活敌人,命中处小型毒溅。
	/// 位置在调用当下快照(死亡链上节点随时被移除),取不到就退化为目标群中心上方起爆。
	/// </summary>
	internal static void CorpseBloomBurst(Creature source, IReadOnlyList<Creature> targets)
	{
		NCreature? sourceNode = HextechCreatureNodeRegistry.TryGet(source);
		Vector2? sourcePos = sourceNode != null && GodotObject.IsInstanceValid(sourceNode)
			? CreatureCenter(sourceNode)
			: null;
		Creature[] snapshot = [.. targets];
		Callable.From(() => RunCorpseBloomBurst(sourcePos, snapshot)).CallDeferred();
	}

	/// <summary>
	/// 量子计算:蓝紫预警环后量子光柱依次贯穿每个敌人(节拍与逐敌伤害结算对齐),
	/// 随后每个敌人放出一缕青绿数据流汇回施法者(对应吸血治疗)。
	/// </summary>
	internal static void QuantumPulse(Creature owner, IReadOnlyList<Creature> targets)
	{
		Creature[] snapshot = [.. targets];
		Callable.From(() => RunQuantumPulse(owner, snapshot)).CallDeferred();
	}

}
