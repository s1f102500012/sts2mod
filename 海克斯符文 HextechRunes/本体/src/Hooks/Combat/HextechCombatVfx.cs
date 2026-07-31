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
	public static void Install(Harmony harmony)
	{
		harmony.Patch(
			RequireMethod(typeof(NCombatRoom), "_Ready", BindingFlags.Instance | BindingFlags.Public),
			postfix: new HarmonyMethod(typeof(HextechCombatVfxHooks), nameof(CombatRoomReadyPostfix)));
		harmony.Patch(
			RequireMethod(typeof(NCombatRoom), nameof(NCombatRoom.AddCreature), BindingFlags.Instance | BindingFlags.Public, typeof(Creature)),
			postfix: new HarmonyMethod(typeof(HextechCombatVfxHooks), nameof(AddCreaturePostfix)));
		harmony.Patch(
			RequireMethod(typeof(NCreature), "_Ready", BindingFlags.Instance | BindingFlags.Public),
			postfix: new HarmonyMethod(typeof(HextechCombatVfxHooks), nameof(CreatureReadyPostfix)));
		harmony.Patch(
			RequireMethod(typeof(NCreature), nameof(NCreature.StartDeathAnim), BindingFlags.Instance | BindingFlags.Public, typeof(bool)),
			postfix: new HarmonyMethod(typeof(HextechCombatVfxHooks), nameof(StartDeathAnimPostfix)));
		HextechLog.Info($"[{ModInfo.Id}][CombatVfx] Hooks installed.");
	}

	private static void CombatRoomReadyPostfix(NCombatRoom __instance)
	{
		HextechCreatureNodeRegistry.Clear();
		foreach (NCreature creature in __instance.CreatureNodes)
		{
			HextechCreatureNodeRegistry.Register(creature);
		}
	}

	private static void AddCreaturePostfix(NCombatRoom __instance, Creature creature)
	{
		HextechCreatureNodeRegistry.Register(HextechCreatureNodeRegistry.SafeGetCreatureNode(__instance, creature));
	}

	private static void CreatureReadyPostfix(NCreature __instance)
	{
		HextechCreatureNodeRegistry.Register(__instance);
	}

	/// <summary>
	/// 吞噬灵魂的特效在死亡动画开始的瞬间派发(真死亡分支必经点),而不是等 rune 的 AfterDeath:
	/// Hook.AfterDeath 是逐监听器顺序 await 的链条,排在前面的监听器等待死亡动画会让魂"卡一下"
	/// 才飞出(最后一只怪死亡时链条提前收尾所以不卡)。此处仅派发表现,数值仍在 rune 内结算。
	/// </summary>
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

internal static class HextechCombatVfx
{
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

	private static async Task RunBoomerangSweep(Creature owner, Creature[] targets, Texture2D? boomerangTexture, bool roundTrip = false)
	{
		try
		{
			NCreature? ownerNode = HextechCreatureNodeRegistry.TryGet(owner);
			if (ownerNode == null || targets.Length == 0)
			{
				return;
			}

			Node? parent = ownerNode.GetParent();
			if (!GodotObject.IsInstanceValid(parent))
			{
				return;
			}

			// 位置全部快照:飞行途中敌人会被伤害击杀,节点随时失效。
			Vector2 ownerPos = CreatureCenter(ownerNode);
			List<Vector2> hitPoints = [];
			foreach (Creature target in targets)
			{
				NCreature? node = HextechCreatureNodeRegistry.TryGet(target);
				if (node != null)
				{
					hitPoints.Add(CreatureCenter(node));
				}
			}

			List<Vector2> waypoints = [ownerPos, .. hitPoints];
			int outboundSegments = hitPoints.Count;
			if (roundTrip)
			{
				// 回程:在最远敌人处打个转(自身回环段)后逆序再扫一遍。
				for (int i = hitPoints.Count - 1; i >= 0; i--)
				{
					waypoints.Add(hitPoints[i]);
				}
			}

			waypoints.Add(ownerPos);
			if (waypoints.Count < 3)
			{
				return;
			}

			float width = CreatureWidth(ownerNode);
			Sprite2D boomerang = new()
			{
				Name = "HextechRunes_Boomerang",
				Texture = boomerangTexture ?? GetGlowTexture(),
				Centered = true,
				Modulate = Colors.White
			};
			parent!.AddChildSafely(boomerang);
			PlaceAboveCreatures(parent, boomerang);
			boomerang.GlobalPosition = ownerPos;
			SetSpriteDiameter(boomerang, width * 0.42f);

			Line2D trail = new()
			{
				Name = "HextechRunes_BoomerangTrail",
				Width = width * 0.16f,
				BeginCapMode = Line2D.LineCapMode.Round,
				EndCapMode = Line2D.LineCapMode.Round,
				JointMode = Line2D.LineJointMode.Round,
				WidthCurve = MakeTrailWidthCurve(),
				Gradient = new Gradient
				{
					Offsets = [0f, 0.6f, 1f],
					Colors = [new Color(0.62f, 0.9f, 1f, 0.55f), new Color(0.62f, 0.9f, 1f, 0.25f), new Color(0.62f, 0.9f, 1f, 0f)]
				},
				Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add }
			};
			parent.AddChildSafely(trail);
			PlaceAboveCreatures(parent, trail);

			SceneTree? tree = boomerang.GetTree();
			if (tree == null)
			{
				FreeNode(boomerang);
				FreeNode(trail);
				return;
			}

			// 逐段贝塞尔飞行:段末命中闪光。转速恒定,拖尾逐帧跟随。
			for (int segment = 0; segment + 1 < waypoints.Count; segment++)
			{
				Vector2 from = waypoints[segment];
				Vector2 to = waypoints[segment + 1];
				bool isFirst = segment == 0;
				bool isReturn = segment == waypoints.Count - 2;
				bool isInbound = segment >= outboundSegments;
				float duration = isFirst ? BoomerangFirstArrivalSeconds
					: isReturn ? 0.3f
					: BoomerangPerTargetSeconds;
				Vector2 mid = (from + to) * 0.5f;
				// 回程段(含最远处的折返回环)走下弧,与去程的上弧区分开。
				float liftDirection = isReturn || isInbound ? 1f : -1f;
				Vector2 control = mid + new Vector2(0f, liftDirection * Mathf.Max(60f, from.DistanceTo(to) * 0.25f));

				float elapsed = 0f;
				while (elapsed < duration)
				{
					if (!GodotObject.IsInstanceValid(boomerang))
					{
						FreeNode(trail);
						return;
					}

					float dt = Mathf.Clamp((float)boomerang.GetProcessDeltaTime(), 1f / 240f, 0.05f);
					elapsed += dt;
					float t = Mathf.Clamp(elapsed / duration, 0f, 1f);
					Vector2 position = Bezier(from, control, to, t);
					boomerang.GlobalPosition = position;
					boomerang.Rotation += dt * Mathf.Tau * 2.2f;
					if (GodotObject.IsInstanceValid(trail))
					{
						trail.AddPoint(trail.ToLocal(position), 0);
						if (trail.GetPointCount() > 14)
						{
							trail.RemovePoint(trail.GetPointCount() - 1);
						}
					}

					await boomerang.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
				}

				if (!isReturn && GodotObject.IsInstanceValid(parent))
				{
					SpawnFlash(parent, to, CreatureWidth(ownerNode) * 0.7f, new Color(0.75f, 0.92f, 1f), 0.28f, 0.55f, aboveCreaturesOnly: true);
				}
			}

			FreeNode(boomerang);
			if (GodotObject.IsInstanceValid(trail))
			{
				Tween fade = trail.CreateTween();
				fade.TweenProperty(trail, "modulate:a", 0f, 0.2f);
				fade.TweenCallback(Callable.From(() => FreeNode(trail)));
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][CombatVfx] Boomerang sweep failed: {ex.Message}");
		}
	}

	private static readonly Color OmegaWarnColor = new(1f, 0.22f, 0.16f);
	private static readonly Color OmegaBeamColor = new(1f, 0.32f, 0.2f);
	private static readonly Color OmegaFlashColor = new(1f, 0.78f, 0.62f);

	private static async Task RunOmegaJudgment(Creature[] targets)
	{
		try
		{
			List<(Vector2 Center, Vector2 Bottom, float Width, float Height, Node Parent)> spots = [];
			foreach (Creature target in targets)
			{
				NCreature? node = HextechCreatureNodeRegistry.TryGet(target);
				Node? parent = node?.GetParent();
				if (node == null || !GodotObject.IsInstanceValid(parent))
				{
					continue;
				}

				Vector2 bottom = node.GetBottomOfHitbox();
				float height = Mathf.Max(bottom.Y - node.GetTopOfHitbox().Y, 120f);
				spots.Add((CreatureCenter(node), bottom, CreatureWidth(node), height, parent!));
			}

			if (spots.Count == 0)
			{
				return;
			}

			// 预警:所有敌人脚下同时亮起红色警戒环。
			foreach ((Vector2 center, Vector2 bottom, float width, _, Node parent) in spots)
			{
				SpawnRing(parent, center, width * 1.2f, width * 0.55f, 0.34f, 0.7f, OmegaWarnColor, aboveCreaturesOnly: true);
			}

			SceneTree? tree = (spots[0].Parent as Node2D)?.GetTree() ?? (Engine.GetMainLoop() as SceneTree);
			if (tree == null)
			{
				return;
			}

			await WaitSeconds(tree, 0.3f);

			// 审判:赤红光柱依次砸下,命中爆闪+扩散环。
			Texture2D? rayTexture = LoadVanillaTexture("res://images/vfx/missile/missile_sky_ray.png");
			foreach ((Vector2 center, Vector2 bottom, float width, float height, Node parent) in spots)
			{
				if (rayTexture != null && GodotObject.IsInstanceValid(parent))
				{
					SpawnOmegaBeam(parent, bottom, width, height, rayTexture);
				}

				if (GodotObject.IsInstanceValid(parent))
				{
					SpawnFlash(parent, center, width * 1.2f, OmegaFlashColor, 0.3f, 0.75f, aboveCreaturesOnly: true);
					SpawnRing(parent, center, width * 0.3f, width * 1.35f, 0.4f, 0.85f, OmegaBeamColor, aboveCreaturesOnly: true);
				}

				await WaitSeconds(tree, 0.06f);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][CombatVfx] Omega judgment failed: {ex.Message}");
		}
	}

	private static readonly Color PoisonBurstColor = new(0.5f, 0.9f, 0.3f);
	private static readonly Color QuantumWarnColor = new(0.5f, 0.55f, 1f);
	private static readonly Color QuantumBeamColor = new(0.58f, 0.5f, 1f);
	private static readonly Color QuantumFlashColor = new(0.78f, 0.85f, 1f);
	private static readonly Color QuantumHealColor = new(0.45f, 0.95f, 0.6f);

	private static async Task RunCorpseBloomBurst(Vector2? sourcePos, Creature[] targets)
	{
		try
		{
			List<(Vector2 Center, float Width, Node Parent)> spots = [];
			foreach (Creature target in targets)
			{
				NCreature? node = HextechCreatureNodeRegistry.TryGet(target);
				Node? parent = node?.GetParent();
				if (node == null || !GodotObject.IsInstanceValid(parent))
				{
					continue;
				}

				spots.Add((CreatureCenter(node), CreatureWidth(node), parent!));
			}

			if (spots.Count == 0)
			{
				return;
			}

			Node burstParent = spots[0].Parent;
			float burstWidth = spots.Max(static spot => spot.Width);
			// 尸体节点在死亡链上随时被移除:取不到就从目标群中心上方起爆。
			Vector2 origin = sourcePos ?? new Vector2(
				spots.Average(static spot => spot.Center.X),
				spots.Min(static spot => spot.Center.Y) - burstWidth * 0.4f);

			// 脓爆:毒绿爆闪+双层扩散环。
			SpawnFlash(burstParent, origin, burstWidth * 1.6f, PoisonBurstColor, 0.36f, 0.85f, aboveCreaturesOnly: true);
			SpawnRing(burstParent, origin, burstWidth * 0.4f, burstWidth * 1.8f, 0.42f, 0.85f, PoisonBurstColor, aboveCreaturesOnly: true);
			SpawnRing(burstParent, origin, burstWidth * 0.25f, burstWidth * 1.2f, 0.32f, 0.6f, Brighten(PoisonBurstColor), aboveCreaturesOnly: true);

			SceneTree? tree = (burstParent as Node2D)?.GetTree() ?? (Engine.GetMainLoop() as SceneTree);
			if (tree == null)
			{
				return;
			}

			await WaitSeconds(tree, 0.1f);

			// 毒液飞溅:弧线毒滴逐个泼向存活敌人,命中处小型毒溅。
			int index = 0;
			foreach ((Vector2 center, float width, Node parent) in spots)
			{
				if (!GodotObject.IsInstanceValid(parent))
				{
					continue;
				}

				Vector2 hitCenter = center;
				float hitWidth = width;
				Node hitParent = parent;
				SpawnSoulWisp(
					parent,
					origin,
					center,
					width * 0.3f,
					0.4f,
					index * 0.05f,
					-Mathf.Max(70f, origin.DistanceTo(center) * 0.3f),
					PoisonBurstColor,
					() =>
					{
						if (GodotObject.IsInstanceValid(hitParent))
						{
							SpawnFlash(hitParent, hitCenter, hitWidth * 0.8f, PoisonBurstColor, 0.26f, 0.65f, aboveCreaturesOnly: true);
							SpawnRing(hitParent, hitCenter, hitWidth * 0.2f, hitWidth * 0.8f, 0.3f, 0.6f, PoisonBurstColor, aboveCreaturesOnly: true);
						}
					});
				index++;
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][CombatVfx] Corpse bloom burst failed: {ex.Message}");
		}
	}

	private static async Task RunQuantumPulse(Creature owner, Creature[] targets)
	{
		try
		{
			List<(Vector2 Center, Vector2 Bottom, float Width, float Height, Node Parent)> spots = [];
			foreach (Creature target in targets)
			{
				NCreature? node = HextechCreatureNodeRegistry.TryGet(target);
				Node? parent = node?.GetParent();
				if (node == null || !GodotObject.IsInstanceValid(parent))
				{
					continue;
				}

				Vector2 bottom = node.GetBottomOfHitbox();
				float height = Mathf.Max(bottom.Y - node.GetTopOfHitbox().Y, 120f);
				spots.Add((CreatureCenter(node), bottom, CreatureWidth(node), height, parent!));
			}

			if (spots.Count == 0)
			{
				return;
			}

			// 预警:蓝紫量子警戒环同时亮起。
			foreach ((Vector2 center, _, float width, _, Node parent) in spots)
			{
				SpawnRing(parent, center, width * 1.2f, width * 0.55f, 0.34f, 0.7f, QuantumWarnColor, aboveCreaturesOnly: true);
			}

			SceneTree? tree = (spots[0].Parent as Node2D)?.GetTree() ?? (Engine.GetMainLoop() as SceneTree);
			if (tree == null)
			{
				return;
			}

			await WaitSeconds(tree, 0.3f);

			// 量子光柱逐敌贯穿:0.2s 间隔与逻辑侧逐敌伤害结算的标准尾巴对齐。
			Texture2D? rayTexture = LoadVanillaTexture("res://images/vfx/missile/missile_sky_ray.png");
			foreach ((Vector2 center, Vector2 bottom, float width, float height, Node parent) in spots)
			{
				if (rayTexture != null && GodotObject.IsInstanceValid(parent))
				{
					SpawnOmegaBeam(parent, bottom, width, height, rayTexture, QuantumBeamColor);
				}

				if (GodotObject.IsInstanceValid(parent))
				{
					SpawnFlash(parent, center, width * 1.2f, QuantumFlashColor, 0.3f, 0.75f, aboveCreaturesOnly: true);
					SpawnRing(parent, center, width * 0.3f, width * 1.35f, 0.4f, 0.85f, QuantumBeamColor, aboveCreaturesOnly: true);
				}

				await WaitSeconds(tree, 0.2f);
			}

			// 吸血回流:每敌一缕青绿数据流弧线汇回施法者。
			NCreature? ownerNode = HextechCreatureNodeRegistry.TryGet(owner);
			if (ownerNode == null || !GodotObject.IsInstanceValid(ownerNode))
			{
				return;
			}

			Vector2 ownerPos = CreatureCenter(ownerNode);
			int index = 0;
			foreach ((Vector2 center, _, float width, _, Node parent) in spots)
			{
				if (!GodotObject.IsInstanceValid(parent))
				{
					continue;
				}

				Node flashParent = parent;
				SpawnSoulWisp(
					parent,
					center,
					ownerPos,
					width * 0.3f,
					0.5f,
					index * 0.07f,
					-width * 0.6f,
					QuantumHealColor,
					() =>
					{
						if (GodotObject.IsInstanceValid(flashParent))
						{
							SpawnFlash(flashParent, ownerPos, width * 0.9f, QuantumHealColor.Lightened(0.3f), 0.3f, 0.5f, aboveCreaturesOnly: true);
						}
					});
				index++;
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][CombatVfx] Quantum pulse failed: {ex.Message}");
		}
	}

	/// <summary>欧米伽的赤红审判光柱:窄而急促(0.08s 闪现全亮,0.3s 收束消退)。</summary>
	private static void SpawnOmegaBeam(Node parent, Vector2 bottom, float width, float height, Texture2D rayTexture, Color? beamColor = null)
	{
		float beamHeight = height * 2.4f;
		Sprite2D beam = new()
		{
			Name = "HextechRunes_OmegaBeam",
			Texture = rayTexture,
			Centered = true,
			Modulate = (beamColor ?? OmegaBeamColor) with { A = 0f },
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add }
		};
		parent.AddChildSafely(beam);
		PlaceAboveCreatures(parent, beam);
		beam.GlobalPosition = new Vector2(bottom.X, bottom.Y - beamHeight * 0.5f);
		Vector2 fullScale = new(width * 0.8f / Math.Max(rayTexture.GetWidth(), 1), beamHeight / Math.Max(rayTexture.GetHeight(), 1));
		beam.Scale = fullScale;

		Tween tween = beam.CreateTween();
		tween.TweenProperty(beam, "modulate:a", 1f, 0.08f).SetEase(Tween.EaseType.Out);
		tween.SetParallel(true);
		tween.TweenProperty(beam, "modulate:a", 0f, 0.3f).SetEase(Tween.EaseType.In).SetDelay(0.08f);
		tween.TweenProperty(beam, "scale:x", fullScale.X * 0.25f, 0.3f).SetEase(Tween.EaseType.In).SetDelay(0.08f);
		tween.Chain().TweenCallback(Callable.From(() => FreeNode(beam)));
	}

	private static async Task RunFlyingKickStrike(Creature target, Creature owner)
	{
		try
		{
			NCreature? targetNode = HextechCreatureNodeRegistry.TryGet(target);
			NCreature? ownerNode = HextechCreatureNodeRegistry.TryGet(owner);
			if (targetNode == null)
			{
				return;
			}

			Node? parent = targetNode.GetParent();
			if (!GodotObject.IsInstanceValid(parent))
			{
				return;
			}

			// 击杀点快照:目标马上会被处决并横飞。
			Vector2 strikePos = CreatureCenter(targetNode);
			float width = CreatureWidth(targetNode);
			Color tint = GetSoulTint(target);

			// 踢击:原版大斩击节点 + 目标本体色爆闪。
			NBigSlashVfx.Create(target);
			NBigSlashImpactVfx.Create(target);
			SpawnFlash(parent!, strikePos, width * 1.3f, Brighten(tint), 0.32f, 0.7f, aboveCreaturesOnly: true);
			SpawnRing(parent!, strikePos, width * 0.35f, width * 1.2f, 0.38f, 0.8f, tint, aboveCreaturesOnly: true);

			// 光流殿后:尸体横飞展开后,一缕治疗绿光从击杀点弧线流回施法者。
			SceneTree? tree = (parent as Node2D)?.GetTree() ?? (Engine.GetMainLoop() as SceneTree);
			if (tree == null || ownerNode == null || !GodotObject.IsInstanceValid(ownerNode))
			{
				return;
			}

			await WaitSeconds(tree, 0.5f);
			if (!GodotObject.IsInstanceValid(parent) || !GodotObject.IsInstanceValid(ownerNode))
			{
				return;
			}

			Color healColor = new(0.45f, 0.95f, 0.5f);
			Vector2 ownerPos = CreatureCenter(ownerNode);
			SpawnSoulWisp(parent!, strikePos, ownerPos, width * 0.34f, 0.55f, 0f, -width * 0.7f, healColor, () =>
			{
				if (GodotObject.IsInstanceValid(parent) && GodotObject.IsInstanceValid(ownerNode))
				{
					SpawnFlash(parent!, ownerPos, width * 0.9f, healColor.Lightened(0.3f), 0.3f, 0.5f, aboveCreaturesOnly: true);
				}
			});
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][CombatVfx] Flying kick strike failed: {ex.Message}");
		}
	}

	private static async Task WaitSeconds(SceneTree tree, float seconds)
	{
		await tree.ToSignal(tree.CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	}

	private static void RunDeathRingLash(Creature source, Creature target)
	{
		try
		{
			NCreature? targetNode = HextechCreatureNodeRegistry.TryGet(target);
			if (targetNode == null)
			{
				return;
			}

			Node? parent = targetNode.GetParent();
			if (!GodotObject.IsInstanceValid(parent))
			{
				return;
			}

			Vector2 targetPos = CreatureCenter(targetNode);
			float width = CreatureWidth(targetNode);
			// 敌人受击爆发刻意做得比我方(施法侧)的环更小一点 —— 它只是被动触发的命中,不该盖过你自身的攻击表现。
			SpawnFlash(parent!, targetPos, width * 0.75f, DeathFlashColor, 0.40f, 0.40f);
			SpawnRing(parent!, targetPos, width * 0.3f, width * 0.8f, 0.45f, 0.95f, DeathRingColor);

			NCreature? sourceNode = HextechCreatureNodeRegistry.TryGet(source);
			if (sourceNode != null)
			{
				Vector2 sourcePos = CreatureCenter(sourceNode);
				SpawnBeam(parent!, sourcePos, targetPos, DeathRingColor, 0.30f);
				SpawnRing(parent!, sourcePos, width * 0.25f, width * 0.95f, 0.30f, 0.55f, DeathRingColor);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][CombatVfx] Death ring lash failed: {ex.Message}");
		}
	}

	private static void RunDivinePulse(Creature[] allies)
	{
		try
		{
			foreach (Creature ally in allies)
			{
				NCreature? node = HextechCreatureNodeRegistry.TryGet(ally);
				if (node == null)
				{
					continue;
				}

				Node? parent = node.GetParent();
				if (!GodotObject.IsInstanceValid(parent))
				{
					continue;
				}

				Vector2 pos = CreatureCenter(node);
				float width = CreatureWidth(node);
				Vector2 bottom = node.GetBottomOfHitbox();
				float height = Mathf.Max(bottom.Y - node.GetTopOfHitbox().Y, width);

				// 主角是从天而降的光柱;柔光与单环收敛为落地反馈,光尘自身体升起。
				Texture2D? rayTexture = LoadVanillaTexture("res://images/vfx/missile/missile_sky_ray.png");
				if (rayTexture != null)
				{
					SpawnLightShaft(parent!, bottom, width, height, rayTexture);
				}

				SpawnSparkleRise(parent!, bottom, width, height);
				SpawnFlash(parent!, pos, width * 1.5f, DivineFlashColor, 0.4f, 0.4f);
				SpawnRing(parent!, pos, width * 0.4f, width * 1.5f, 0.6f, 0.7f, DivineRingColor);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][CombatVfx] Divine pulse failed: {ex.Message}");
		}
	}

	/// <summary>神圣干预:金色光柱自天顶罩下(横向展开淡入、驻留、淡出)。</summary>
	private static void SpawnLightShaft(Node parent, Vector2 bottom, float width, float height, Texture2D rayTexture)
	{
		float shaftHeight = height * 2.1f;
		float shaftWidth = width * 1.45f;
		Sprite2D shaft = new()
		{
			Name = "HextechRunes_DivineShaft",
			Texture = rayTexture,
			Centered = true,
			TopLevel = true,
			Modulate = DivineShaftColor with { A = 0f },
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add }
		};
		parent.AddChildSafely(shaft);
		// 光柱底沿落在脚底:贴图光锥自顶向下渐散,顶亮端悬在头顶上空。
		shaft.GlobalPosition = new Vector2(bottom.X, bottom.Y - shaftHeight * 0.5f);
		Vector2 fullScale = new(shaftWidth / Math.Max(rayTexture.GetWidth(), 1), shaftHeight / Math.Max(rayTexture.GetHeight(), 1));
		shaft.Scale = new Vector2(fullScale.X * 0.55f, fullScale.Y);

		Tween tween = shaft.CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(shaft, "modulate:a", 0.8f, 0.16f).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(shaft, "scale:x", fullScale.X, 0.24f)
			.SetEase(Tween.EaseType.Out)
			.SetTrans(Tween.TransitionType.Cubic);
		tween.Chain().TweenInterval(0.26f);
		tween.Chain().TweenProperty(shaft, "modulate:a", 0f, 0.5f).SetEase(Tween.EaseType.In);
		tween.Chain().TweenCallback(Callable.From(() => FreeNode(shaft)));
	}

	/// <summary>神圣干预:金色星光尘自身体缓缓升起(一次性粒子,规定时限后自毁)。</summary>
	private static void SpawnSparkleRise(Node parent, Vector2 bottom, float width, float height)
	{
		CpuParticles2D dust = new()
		{
			Name = "HextechRunes_DivineDust",
			TopLevel = true,
			OneShot = true,
			Emitting = true,
			Amount = 12,
			Lifetime = 1.05f,
			Explosiveness = 0.2f,
			Randomness = 0.6f,
			LocalCoords = false,
			Texture = LoadVanillaTexture("res://images/vfx/characters/regent_sparkle.png") ?? GetGlowTexture(),
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
			EmissionRectExtents = new Vector2(width * 0.45f, height * 0.35f),
			Direction = new Vector2(0f, -1f),
			Spread = 12f,
			InitialVelocityMin = height * 0.22f,
			InitialVelocityMax = height * 0.45f,
			Gravity = new Vector2(0f, -height * 0.1f),
			ScaleAmountMin = 0.5f,
			ScaleAmountMax = 1.1f,
			Modulate = DivineDustColor,
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			ColorRamp = new Gradient
			{
				Offsets = [0f, 0.2f, 0.75f, 1f],
				Colors = [Colors.White with { A = 0f }, Colors.White, Colors.White, Colors.White with { A = 0f }]
			}
		};
		parent.AddChildSafely(dust);
		dust.GlobalPosition = new Vector2(bottom.X, bottom.Y - height * 0.45f);

		Tween tween = dust.CreateTween();
		tween.TweenInterval(2.2f);
		tween.TweenCallback(Callable.From(() => FreeNode(dust)));
	}

	private static void RunSoulDrain(Creature source, Creature destination, int wispCount)
	{
		try
		{
			NCreature? destNode = HextechCreatureNodeRegistry.TryGet(destination);
			if (destNode == null)
			{
				return;
			}

			Node? parent = destNode.GetParent();
			if (!GodotObject.IsInstanceValid(parent))
			{
				return;
			}

			Vector2 destPos = CreatureCenter(destNode);
			float width = CreatureWidth(destNode);
			NCreature? sourceNode = HextechCreatureNodeRegistry.TryGet(source);
			Vector2 sourcePos = sourceNode != null ? CreatureCenter(sourceNode) : destPos;

			// 魂色取死者立绘的平均色(魂是"它的"魂),失败回退幽青。全程纯敌人色,不混白:
			// 需要"更亮"的部件用同色相提满明度(Brighten),保持色彩纯度。
			Color tint = GetSoulTint(source);
			Color brightTint = Brighten(tint);

			if (sourceNode != null)
			{
				// 亡魂自敌人身上被抽离的一瞬。
				SpawnFlash(parent!, sourcePos, width * 0.85f, brightTint, 0.35f, 0.5f, aboveCreaturesOnly: true);
			}

			// 魂的缕数按死者身份分级(在死亡瞬间由调用方算好传入):小怪 1-2、精英 3-4、BOSS 5-6。
			// 主魂大而稳、带到达闪光;其余各缕尺寸/弧线/节奏随机错开,鱼贯飘入。
			SpawnSoulWisp(parent!, sourcePos, destPos, width * 0.5f, 0.62f, 0f, -width * 0.9f, tint, () =>
			{
				if (GodotObject.IsInstanceValid(parent))
				{
					SpawnFlash(parent!, destPos, width * 1.0f, brightTint, 0.35f, 0.5f, aboveCreaturesOnly: true);
					SpawnRing(parent!, destPos, width * 0.2f, width * 1.05f, 0.4f, 0.8f, tint, aboveCreaturesOnly: true);
				}
			});
			for (int i = 1; i < wispCount; i++)
			{
				float diameter = width * (0.24f + VisualRng.NextSingle() * 0.1f);
				float duration = 0.64f + VisualRng.NextSingle() * 0.16f;
				float delay = 0.03f + i * 0.05f;
				float arcLift = -width * (0.5f + VisualRng.NextSingle() * 0.9f);
				SpawnSoulWisp(parent!, sourcePos, destPos, diameter, duration, delay, arcLift, i % 2 == 0 ? tint : brightTint, null);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][CombatVfx] Soul drain failed: {ex.Message}");
		}
	}

	/// <summary>
	/// 一缕亡魂:双层魂头(亮核+外晕)沿上拱的贝塞尔弧线飘向目标,Line2D 拖尾跟随头部渐细渐隐。
	/// 弧高由 arcLift 给定、横向偏移随机,三缕魂各走各的弧线,像魂魄而不是直线弹道。
	/// </summary>
	private static void SpawnSoulWisp(Node parent, Vector2 from, Vector2 to, float diameter, float duration, float delay, float arcLift, Color color, Action? onArrival)
	{
		// 不用 TopLevel:top-level 节点渲染时脱离父绘制树、直接按 canvas 根级项画在最上层,
		// 会盖过战斗结算 UI 且 MoveChild 调整无效;全局定位改用 GlobalPosition setter(自动换算局部)。
		Node2D head = new() { Name = "HextechRunes_SoulWisp" };
		parent.AddChildSafely(head);
		PlaceAboveCreatures(parent, head);
		head.GlobalPosition = from;
		Sprite2D halo = MakeSprite(GetGlowTexture(), color with { A = 0.75f });
		Sprite2D core = MakeSprite(GetGlowTexture(), Brighten(color) with { A = 0.95f });
		head.AddChild(halo);
		head.AddChild(core);
		SetSpriteDiameter(halo, diameter);
		SetSpriteDiameter(core, diameter * 0.45f);

		Line2D trail = new()
		{
			Name = "HextechRunes_SoulTrail",
			Width = diameter * 0.55f,
			BeginCapMode = Line2D.LineCapMode.Round,
			EndCapMode = Line2D.LineCapMode.Round,
			JointMode = Line2D.LineJointMode.Round,
			WidthCurve = MakeTrailWidthCurve(),
			// 陷阱:Line2D 设置了 Gradient 后 DefaultColor 被完全忽略——魂色必须写进 Gradient
			// 本身(之前色标全白导致拖尾恒为白色)。
			Gradient = new Gradient
			{
				Offsets = [0f, 0.55f, 1f],
				Colors = [color with { A = 0.6f }, color with { A = 0.3f }, color with { A = 0f }]
			},
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add }
		};
		parent.AddChildSafely(trail);
		// 拖尾插在魂头之前(先画=垫在头下)。
		PlaceAboveCreatures(parent, trail);

		// 控制点:两点中点上方 arcLift,横向再加一点随机——先上飘、再拐向目标。
		Vector2 mid = (from + to) * 0.5f;
		float sideJitter = (VisualRng.NextSingle() - 0.5f) * (to - from).Length() * 0.3f;
		Vector2 control = mid + new Vector2(sideJitter, arcLift);

		Tween tween = head.CreateTween();
		if (delay > 0f)
		{
			tween.TweenInterval(delay);
		}

		tween.TweenMethod(Callable.From((float t) =>
		{
			if (!GodotObject.IsInstanceValid(head))
			{
				return;
			}

			Vector2 position = Bezier(from, control, to, t);
			head.GlobalPosition = position;
			if (GodotObject.IsInstanceValid(trail))
			{
				// Line2D 点集是局部坐标(非 TopLevel),全局轨迹点须换算。
				trail.AddPoint(trail.ToLocal(position), 0);
				if (trail.GetPointCount() > 16)
				{
					trail.RemovePoint(trail.GetPointCount() - 1);
				}
			}
		}), 0f, 1f, duration)
			.SetEase(Tween.EaseType.InOut)
			.SetTrans(Tween.TransitionType.Sine);
		tween.Chain().TweenCallback(Callable.From(() =>
		{
			onArrival?.Invoke();
			FreeNode(head);
			if (GodotObject.IsInstanceValid(trail))
			{
				Tween fade = trail.CreateTween();
				fade.TweenProperty(trail, "modulate:a", 0f, 0.22f).SetEase(Tween.EaseType.In);
				fade.TweenCallback(Callable.From(() => FreeNode(trail)));
			}
		}));
	}

	// 拖尾宽度:头部(第一个点)全宽,尾端收细。
	private static Curve MakeTrailWidthCurve()
	{
		Curve curve = new();
		curve.AddPoint(new Vector2(0f, 1f));
		curve.AddPoint(new Vector2(1f, 0.08f));
		return curve;
	}

	private static Vector2 CreatureCenter(NCreature node)
	{
		return node.GetTopOfHitbox().Lerp(node.GetBottomOfHitbox(), 0.5f);
	}

	private static float CreatureWidth(NCreature node)
	{
		return Mathf.Clamp(node.Hitbox?.Size.X ?? 180f, 120f, 360f);
	}

	private static void SpawnRing(Node parent, Vector2 globalPos, float startDiameter, float endDiameter, float duration, float startAlpha, Color color, bool aboveCreaturesOnly = false)
	{
		Sprite2D ring = MakeSprite(GetRingTexture(), color with { A = startAlpha });
		parent.AddChildSafely(ring);
		if (aboveCreaturesOnly)
		{
			PlaceAboveCreatures(parent, ring);
		}

		ring.GlobalPosition = globalPos;
		SetSpriteDiameter(ring, startDiameter);
		float endScale = endDiameter / Math.Max(GetRingTexture().GetWidth(), 1);

		Tween tween = ring.CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(ring, "scale", new Vector2(endScale, endScale), duration)
			.SetEase(Tween.EaseType.Out)
			.SetTrans(Tween.TransitionType.Cubic);
		tween.TweenProperty(ring, "modulate:a", 0f, duration).SetEase(Tween.EaseType.In);
		tween.Chain().TweenCallback(Callable.From(() => FreeNode(ring)));
	}

	private static void SpawnFlash(Node parent, Vector2 globalPos, float diameter, Color color, float duration, float peakAlpha, bool aboveCreaturesOnly = false)
	{
		Sprite2D flash = MakeSprite(GetGlowTexture(), color with { A = 0f });
		parent.AddChildSafely(flash);
		if (aboveCreaturesOnly)
		{
			PlaceAboveCreatures(parent, flash);
		}

		flash.GlobalPosition = globalPos;
		SetSpriteDiameter(flash, diameter);

		Tween tween = flash.CreateTween();
		tween.TweenProperty(flash, "modulate:a", peakAlpha, duration * 0.3f).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(flash, "modulate:a", 0f, duration * 0.7f).SetEase(Tween.EaseType.In);
		tween.TweenCallback(Callable.From(() => FreeNode(flash)));
	}

	private static void SpawnBeam(Node parent, Vector2 fromGlobal, Vector2 toGlobal, Color color, float duration)
	{
		Line2D beam = new()
		{
			Name = "HextechRunes_VfxBeam",
			TopLevel = true,
			Width = 6f,
			DefaultColor = color with { A = 0.9f },
			BeginCapMode = Line2D.LineCapMode.Round,
			EndCapMode = Line2D.LineCapMode.Round,
			Points = [fromGlobal, toGlobal],
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add }
		};
		parent.AddChildSafely(beam);

		Tween tween = beam.CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(beam, "modulate:a", 0f, duration).SetEase(Tween.EaseType.In);
		tween.TweenProperty(beam, "width", 1.5f, duration).SetEase(Tween.EaseType.In);
		tween.Chain().TweenCallback(Callable.From(() => FreeNode(beam)));
	}

	private static Sprite2D MakeSprite(Texture2D texture, Color modulate)
	{
		return new Sprite2D
		{
			Texture = texture,
			Centered = true,
			Modulate = modulate,
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add }
		};
	}

	private static void SetSpriteDiameter(Sprite2D sprite, float diameter)
	{
		if (sprite.Texture is { } texture)
		{
			sprite.Scale = Vector2.One * (diameter / Math.Max(texture.GetWidth(), 1));
		}
	}

	private static void FreeNode(Node node)
	{
		if (GodotObject.IsInstanceValid(node))
		{
			node.QueueFree();
		}
	}

	private static Texture2D GetGlowTexture()
	{
		if (_glowTexture != null && GodotObject.IsInstanceValid(_glowTexture))
		{
			return _glowTexture;
		}

		Gradient gradient = new()
		{
			Offsets = [0f, 0.45f, 1f],
			Colors = [new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 0.55f), new Color(1f, 1f, 1f, 0f)]
		};
		_glowTexture = new GradientTexture2D
		{
			Gradient = gradient,
			Width = 256,
			Height = 256,
			Fill = GradientTexture2D.FillEnum.Radial,
			FillFrom = new Vector2(0.5f, 0.5f),
			FillTo = new Vector2(1f, 0.5f)
		};
		return _glowTexture;
	}

	private static Texture2D GetRingTexture()
	{
		if (_ringTexture != null && GodotObject.IsInstanceValid(_ringTexture))
		{
			return _ringTexture;
		}

		Gradient gradient = new()
		{
			Offsets = [0f, 0.60f, 0.80f, 0.93f, 1f],
			Colors =
			[
				new Color(1f, 1f, 1f, 0f),
				new Color(1f, 1f, 1f, 0f),
				new Color(1f, 1f, 1f, 1f),
				new Color(1f, 1f, 1f, 0f),
				new Color(1f, 1f, 1f, 0f)
			]
		};
		_ringTexture = new GradientTexture2D
		{
			Gradient = gradient,
			Width = 256,
			Height = 256,
			Fill = GradientTexture2D.FillEnum.Radial,
			FillFrom = new Vector2(0.5f, 0.5f),
			FillTo = new Vector2(1f, 0.5f)
		};
		return _ringTexture;
	}
}
