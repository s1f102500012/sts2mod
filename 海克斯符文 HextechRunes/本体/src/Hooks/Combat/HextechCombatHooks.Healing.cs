using MegaCrit.Sts2.Core.Models.Monsters;

namespace HextechRunes;

internal static partial class HextechCombatHooks
{
	internal const string EndlessModeHarmonyId = "Natsuki.EndlessMode";
	internal const string RitsuLibCoreHarmonyId = "com.ritsukage.sts2-RitsuLib.framework-core";
	internal const string BaseLibHarmonyId = "BaseLib";

	private readonly record struct HealPostState(Player? Player, Creature Creature, int CurrentHpBefore, bool ShouldProcess);


	internal static decimal ClampHealAmountToCap(int currentHp, int maxHp, decimal amount, decimal capPercent)
	{
		if (amount <= 0m)
		{
			return amount;
		}

		int healCap = (int)Math.Floor(maxHp * capPercent);
		return Math.Min(amount, Math.Max(0m, healCap - currentHp));
	}


	private static async Task HealAfterOriginal(Task original, HealPostState state)
	{
		await original;

		Player? player = state.Player;
		Creature creature = state.Creature;
		decimal amount = CalculateActualHealAmount(state.CurrentHpBefore, creature.CurrentHp);
		if (amount <= 0m)
		{
			return;
		}

		if (player?.GetRelic<CircleOfDeathRune>() is CircleOfDeathRune circleOfDeathRune
			&& creature == player.Creature
			&& creature.CombatState != null)
		{
			await circleOfDeathRune.HandleSustainGained(amount);
		}

		// 我们的治疗(仅联机):队友被治疗后镜像给持有者,战斗内外通吃。
		await OurHealingRune.MirrorTeammateHeal(creature, amount);
	}

	internal static decimal CalculateActualHealAmount(int currentHpBefore, int currentHpAfter)
	{
		return Math.Max(0m, currentHpAfter - (decimal)currentHpBefore);
	}

	private static bool IsSkulkingColony(Creature creature)
	{
		return creature.Side == CombatSide.Enemy && creature.Monster is SkulkingColony;
	}

	private static bool IsEnemyReviveHeal(Creature creature, decimal amount)
	{
		return creature.Side == CombatSide.Enemy && creature.IsDead && amount > 0m;
	}

	private static bool TryQueueEnemyHealAsDelayedBlock(
		Creature creature,
		decimal amount,
		RunState? runState,
		HextechMayhemModifier? modifier)
	{
		if (creature.Side != CombatSide.Enemy || amount <= 0m || runState == null)
		{
			return false;
		}

		List<RegenerationSuppressionRune> suppressionRunes = runState.Players
			.Select(static player => player.GetRelic<RegenerationSuppressionRune>())
			.OfType<RegenerationSuppressionRune>()
			.ToList();
		if (!IsSkulkingColony(creature) && suppressionRunes.Count == 0)
		{
			return false;
		}

		modifier ??= ModEntry.EnsureMayhemModifier(runState);
		if (!modifier.QueueEnemyHealingBlock(creature, amount))
		{
			return false;
		}

		foreach (RegenerationSuppressionRune rune in suppressionRunes)
		{
			rune.NotifyEnemyHealSuppressed(creature);
		}

		return true;
	}

	[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Heal), typeof(Creature), typeof(decimal), typeof(bool))]
	[HextechPatch("combat.heal", "治疗修正")]
	private static class HealPatch
	{
		[HarmonyPrefix]
		private static bool Prefix(Creature creature, ref decimal amount, ref Task __result, out HealPostState __state)
		{
			if (NearDeathFeastRune.ShouldPreventSustain(creature) || HextechEnemyNearDeath.ShouldPreventSustain(creature))
			{
				__state = default;
				__result = Task.CompletedTask;
				return false;
			}

			Player? player = creature.Player;
			if (player != null && creature == player.Creature)
			{
				amount *= HextechPlayerCoefficientHelper.GetHealingMultiplier(player);
			}

			if (player?.GetRelic<GlassCannonRune>() is GlassCannonRune glassCannonRune && creature == player.Creature)
			{
				int healCap = (int)Math.Floor(creature.MaxHp * glassCannonRune.HealCapPercent);
				amount = Math.Min(amount, Math.Max(0, healCap - creature.CurrentHp));
				if (amount <= 0m)
				{
					__state = default;
					__result = Task.CompletedTask;
					return false;
				}
			}

			RunState? currentRunState = creature.CombatState?.RunState as RunState;
			HextechMayhemModifier? modifier = null;
			bool isEnemyReviveHeal = IsEnemyReviveHeal(creature, amount);
			if (creature.Side == CombatSide.Enemy
				&& currentRunState != null
				&& !isEnemyReviveHeal
				&& HextechMayhemModifier.FindIn(currentRunState) is HextechMayhemModifier activeModifier)
			{
				modifier = activeModifier;
				amount = modifier.ModifyEnemyHealAmount(creature, amount);
				if (amount <= 0m)
				{
					__state = default;
					__result = Task.CompletedTask;
					return false;
				}
			}

			if (!isEnemyReviveHeal && TryQueueEnemyHealAsDelayedBlock(creature, amount, currentRunState, modifier))
			{
				__state = default;
				__result = Task.CompletedTask;
				return false;
			}

			if (amount <= 0m)
			{
				__state = default;
				__result = Task.CompletedTask;
				return false;
			}

			__state = new HealPostState(player, creature, creature.CurrentHp, ShouldProcess: true);
			return true;
		}

		// 封顶必须是治疗前缀链的最后一环:别的模组(无尽/RitsuLib/BaseLib)的加减乘都算完,再按当前生命封顶。
		// 原版 Heal 对 0 也会播治疗音效与特效,所以"禁止回血"仍由上面的 Prefix 跳过原方法,这里只做数值封顶。
		[HarmonyPrefix]
		[HarmonyPriority(Priority.Last)]
		[HarmonyAfter(EndlessModeHarmonyId, RitsuLibCoreHarmonyId, BaseLibHarmonyId)]
		private static void FinalCapPrefix(Creature creature, ref decimal amount)
		{
			if (amount <= 0m || IsEnemyReviveHeal(creature, amount))
			{
				return;
			}

			decimal? capPercent = null;
			Player? player = creature.Player;
			if (player?.GetRelic<GlassCannonRune>() is GlassCannonRune glassCannonRune
				&& creature == player.Creature)
			{
				capPercent = glassCannonRune.HealCapPercent;
			}
			else if (creature.Side == CombatSide.Enemy
				&& creature.CombatState?.RunState is RunState runState
				&& HextechMayhemModifier.FindIn(runState) is HextechMayhemModifier modifier
				&& modifier.HasActiveMonsterHex(MonsterHexKind.GlassCannon))
			{
				capPercent = GlassCannonEnemyHex.HealCapPercent;
			}

			if (capPercent.HasValue)
			{
				amount = ClampHealAmountToCap(creature.CurrentHp, creature.MaxHp, amount, capPercent.Value);
			}
		}

		[HarmonyPostfix]
		private static void Postfix(HealPostState __state, ref Task __result)
		{
			if (!__state.ShouldProcess)
			{
				return;
			}

			__result = HealAfterOriginal(__result, __state);
		}
	}
}
