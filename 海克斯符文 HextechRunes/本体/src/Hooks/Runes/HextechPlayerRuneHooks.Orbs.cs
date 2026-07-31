using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Orbs;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

// Orb 容量/布局相关的玩家符文 hook:疯狂科学家(扩容)、
// 大容量环形布局、电动力学(闪电球溅射)。从 HextechPlayerRuneHooks 主文件拆出,
// 自带 orb 布局常量与反射字段,便于独立维护。
internal static partial class HextechPlayerRuneHooks
{
	private const int OrbLayoutRadiusSoftCapSlots = 10;
	private const float OrbLayoutRangeDegrees = 125f;
	private const float OrbLayoutAngleOffsetDegrees = -25f;
	private const float OrbLayoutMaxRadius = 300f;
	private const float OrbLayoutTweenSpeed = 0.45f;

	private static FieldInfo? OrbManagerOrbsField;
	private static FieldInfo? OrbManagerCreatureField;
	private static FieldInfo? OrbManagerCurrentTweenField;

	private static void EnsureOrbLayoutFields()
	{
		OrbManagerOrbsField ??= RequireField(typeof(NOrbManager), "_orbs");
		OrbManagerCreatureField ??= RequireField(typeof(NOrbManager), "_creatureNode");
		OrbManagerCurrentTweenField ??= RequireField(typeof(NOrbManager), "_curTween");
	}

	private static bool OrbAddSlotsPrefix(Player player, int amount, ref Task __result)
	{
		if (player.GetRelic<MadScientistRune>() == null)
		{
			return true;
		}

		if (CombatManager.Instance.IsOverOrEnding || amount <= 0)
		{
			__result = Task.CompletedTask;
			return false;
		}

		if (player.PlayerCombatState == null)
		{
			return true;
		}

		player.PlayerCombatState.OrbQueue.AddCapacity(amount);
		NCombatRoom.Instance?.GetCreatureNode(player.Creature)?.OrbManager?.AddSlotAnim(amount);
		__result = Task.CompletedTask;
		return false;
	}

	private static bool OrbTweenLayoutPrefix(NOrbManager __instance)
	{
		try
		{
			return OrbTweenLayoutPrefixCore(__instance);
		}
		catch (Exception ex)
		{
			// 纯布局表现层,异常回退原版布局,不能向调用方外泄。
			Log.Warn($"[{ModInfo.Id}][Mayhem] Orb layout override failed; falling back to vanilla layout: {ex.Message}");
			return true;
		}
	}

	private static bool OrbTweenLayoutPrefixCore(NOrbManager __instance)
	{
		if (!TryGetOrbLayoutState(__instance, out List<NOrb> orbs, out int capacity)
			|| capacity <= OrbLayoutRadiusSoftCapSlots)
		{
			return true;
		}

		if (orbs.Count == 0)
		{
			return false;
		}

		float angle = OrbLayoutRangeDegrees;
		float angleStep = OrbLayoutRangeDegrees / Math.Max(1, capacity - 1);
		float radius = OrbLayoutMaxRadius;
		if (!__instance.IsLocal)
		{
			radius *= 0.75f;
		}

		((Tween?)OrbManagerCurrentTweenField?.GetValue(__instance))?.Kill();
		Tween tween = __instance.CreateTween().SetParallel();
		OrbManagerCurrentTweenField?.SetValue(__instance, tween);

		int layoutCount = Math.Min(capacity, orbs.Count);
		for (int i = 0; i < layoutCount; i++)
		{
			float radians = (OrbLayoutAngleOffsetDegrees - angle) * MathF.PI / 180f;
			Vector2 position = new(-MathF.Cos(radians) * radius, MathF.Sin(radians) * radius);
			tween.TweenProperty(orbs[i], "position", position, OrbLayoutTweenSpeed)
				.SetEase(Tween.EaseType.InOut)
				.SetTrans(Tween.TransitionType.Sine);
			angle -= angleStep;
		}

		return false;
	}

	private static bool TryGetOrbLayoutState(NOrbManager manager, out List<NOrb> orbs, out int capacity)
	{
		orbs = (List<NOrb>?)OrbManagerOrbsField?.GetValue(manager) ?? new List<NOrb>();
		NCreature? creature = (NCreature?)OrbManagerCreatureField?.GetValue(manager);
		capacity = creature?.Entity.Player?.PlayerCombatState?.OrbQueue.Capacity ?? 0;
		return capacity > 0;
	}

	private static bool LightningApplyDamagePrefix(LightningOrb __instance, decimal value, Creature? target, PlayerChoiceContext choiceContext, ref Task<IEnumerable<Creature>> __result)
	{
		if (__instance.Owner?.GetRelic<ElectrodynamicsRune>() == null)
		{
			return true;
		}

		__result = ApplyElectrodynamicsLightningDamage(__instance, value, choiceContext);
		return false;
	}

	private static async Task<IEnumerable<Creature>> ApplyElectrodynamicsLightningDamage(LightningOrb orb, decimal value, PlayerChoiceContext choiceContext)
	{
		List<Creature> targets = orb.CombatState.GetOpponentsOf(orb.Owner.Creature)
			.Where(static enemy => enemy.IsHittable)
			.ToList();
		if (targets.Count == 0)
		{
			return Array.Empty<Creature>();
		}

		foreach (Creature target in targets)
		{
			VfxCmd.PlayOnCreature(target, "vfx/vfx_attack_lightning");
		}

		await CreatureCmd.Damage(choiceContext, targets, value, ValueProp.Unpowered, orb.Owner.Creature);
		return targets;
	}
}
