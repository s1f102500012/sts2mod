using MegaCrit.Sts2.addons.mega_text;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

internal static partial class HextechCombatHooks
{
	private static FieldInfo? HealthBarCreatureField;
	private static FieldInfo? HealthBarHpLabelField;

	private static void EnsureNearDeathFeastFields()
	{
		HealthBarCreatureField ??= RequireField(typeof(NHealthBar), "_creature");
		HealthBarHpLabelField ??= RequireField(typeof(NHealthBar), "_hpLabel");
	}


	private static void NearDeathFeastKillPrefix(Creature creature)
	{
		NearDeathFeastRune.ForceDeathThresholdForKill(creature);
		HextechEnemyNearDeath.ForceDeathThresholdForKill(creature);
	}


	[HarmonyPatch(typeof(Creature), nameof(Creature.LoseHpInternal), typeof(decimal), typeof(ValueProp))]
	[HextechPatch("combat.near-death-feast.lose-hp", "濒死狂宴", Rune = typeof(NearDeathFeastRune))]
	private static class NearDeathFeastLoseHpPatch
	{
		[HarmonyPrefix]
		[HarmonyPriority(Priority.Low)]
		private static bool Prefix(Creature __instance, decimal amount, ValueProp props, ref DamageResult __result)
		{
			if (NearDeathFeastRune.ShouldInterceptLoseHp(__instance, amount))
			{
				__result = NearDeathFeastRune.LoseHpAllowingDying(__instance, amount, props);
				return false;
			}

			if (HextechEnemyNearDeath.ShouldInterceptLoseHp(__instance, amount))
			{
				__result = HextechEnemyNearDeath.LoseHpAllowingDying(__instance, amount, props);
				return false;
			}

			return true;
		}
	}

	[HarmonyPatch(typeof(Creature), nameof(Creature.CurrentHp), MethodType.Setter)]
	[HextechPatch("combat.near-death-feast.current-hp", "濒死狂宴", Rune = typeof(NearDeathFeastRune))]
	private static class NearDeathFeastCurrentHpPatch
	{
		[HarmonyPrefix]
		[HarmonyPriority(Priority.Low)]
		private static bool Prefix(Creature __instance, int value)
		{
			if (value >= 0)
			{
				// 敌方转阶段/接续/复活把 HP 设回正值:清掉残留的濒死状态。
				// 注意只认 >1:濒死维持本身就是把 HP 写成 1(LoseHpAllowingDying 内部的
				// SetCurrentHpInternal 会走本 setter),按 1 清会把刚记下的负血债务当场抹掉。
				if (value > 1)
				{
					HextechEnemyNearDeath.ClearIfRecovered(__instance, value);
				}

				return true;
			}

			if (NearDeathFeastRune.HasDyingState(__instance))
			{
				NearDeathFeastRune.PreserveNegativeHpAsDyingState(__instance, value);
				return false;
			}

			if (HextechEnemyNearDeath.HasDyingState(__instance))
			{
				HextechEnemyNearDeath.PreserveNegativeHpAsDyingState(__instance, value);
				return false;
			}

			return true;
		}
	}

	[HarmonyPatch(typeof(Creature), nameof(Creature.IsAlive), MethodType.Getter)]
	[HextechPatch("combat.near-death-feast.is-alive", "濒死狂宴", Rune = typeof(NearDeathFeastRune))]
	private static class NearDeathFeastIsAlivePatch
	{
		[HarmonyPostfix]
		private static void Postfix(Creature __instance, ref bool __result)
		{
			if (!__result && (NearDeathFeastRune.IsDyingButAlive(__instance) || HextechEnemyNearDeath.IsDyingButAlive(__instance)))
			{
				__result = true;
			}
		}
	}

	[HarmonyPatch(typeof(Creature), nameof(Creature.IsDead), MethodType.Getter)]
	[HextechPatch("combat.near-death-feast.is-dead", "濒死狂宴", Rune = typeof(NearDeathFeastRune))]
	private static class NearDeathFeastIsDeadPatch
	{
		[HarmonyPostfix]
		private static void Postfix(Creature __instance, ref bool __result)
		{
			if (__result && (NearDeathFeastRune.IsDyingButAlive(__instance) || HextechEnemyNearDeath.IsDyingButAlive(__instance)))
			{
				__result = false;
			}
		}
	}

	[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.GainBlock), typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(CardPlay), typeof(bool))]
	[HextechPatch("combat.near-death-feast.gain-block", "濒死狂宴", Rune = typeof(NearDeathFeastRune))]
	private static class NearDeathFeastGainBlockPatch
	{
		[HarmonyPrefix]
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(Creature creature, ref Task<decimal> __result)
		{
			if (!NearDeathFeastRune.ShouldPreventSustain(creature) && !HextechEnemyNearDeath.ShouldPreventSustain(creature))
			{
				return true;
			}

			__result = Task.FromResult(0m);
			return false;
		}
	}

	[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.GainBlock), typeof(Creature), typeof(BlockVar), typeof(CardPlay), typeof(bool))]
	[HextechPatch("combat.near-death-feast.gain-block-var", "濒死狂宴", Rune = typeof(NearDeathFeastRune))]
	private static class NearDeathFeastGainBlockVarPatch
	{
		[HarmonyPrefix]
		[HarmonyPriority(Priority.Low)]
		private static bool Prefix(Creature creature, ref Task<decimal> __result)
		{
			return NearDeathFeastGainBlockPatch.Prefix(creature, ref __result);
		}
	}

	[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Kill), typeof(Creature), typeof(bool))]
	[HextechPatch("combat.near-death-feast.kill", "濒死狂宴", Rune = typeof(NearDeathFeastRune))]
	private static class NearDeathFeastKillPatch
	{
		[HarmonyPrefix]
		private static void Prefix(Creature creature) => NearDeathFeastKillPrefix(creature);
	}

	[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Kill), typeof(IReadOnlyCollection<Creature>), typeof(bool))]
	[HextechPatch("combat.near-death-feast.kill-many", "濒死狂宴", Rune = typeof(NearDeathFeastRune))]
	private static class NearDeathFeastKillManyPatch
	{
		[HarmonyPrefix]
		private static void Prefix(IReadOnlyCollection<Creature> creatures)
		{
			foreach (Creature creature in creatures)
			{
				NearDeathFeastRune.ForceDeathThresholdForKill(creature);
				HextechEnemyNearDeath.ForceDeathThresholdForKill(creature);
			}
		}
	}

	[HarmonyPatch(typeof(CreatureCmd), "KillWithoutCheckingWinCondition", typeof(Creature), typeof(bool), typeof(int))]
	[HextechPatch("combat.near-death-feast.kill-no-win-check", "濒死狂宴", Rune = typeof(NearDeathFeastRune))]
	private static class NearDeathFeastKillWithoutWinCheckPatch
	{
		[HarmonyPrefix]
		private static void Prefix(Creature creature) => NearDeathFeastKillPrefix(creature);
	}

	[HarmonyPatch(typeof(NHealthBar), "RefreshText")]
	[HextechPatch("combat.near-death-feast.health-bar-text", "濒死狂宴", Rune = typeof(NearDeathFeastRune))]
	private static class NearDeathFeastHealthBarTextPatch
	{
		[HarmonyPrepare]
		private static bool Prepare()
		{
			EnsureNearDeathFeastFields();
			return true;
		}

		[HarmonyPostfix]
		private static void Postfix(NHealthBar __instance)
		{
			if (HealthBarCreatureField?.GetValue(__instance) is not Creature creature
				|| HealthBarHpLabelField?.GetValue(__instance) is not MegaLabel hpLabel)
			{
				return;
			}

			if (NearDeathFeastRune.TryGetDisplayedHp(creature, out int displayedHp)
				|| HextechEnemyNearDeath.TryGetDisplayedHp(creature, out displayedHp))
			{
				hpLabel.SetTextAutoSize($"{displayedHp}/{creature.MaxHp}");
			}
		}
	}
}
