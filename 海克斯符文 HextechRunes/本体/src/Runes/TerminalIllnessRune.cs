using HarmonyLib;
using MegaCrit.Sts2.Core.Hooks;

namespace HextechRunes;

public sealed class TerminalIllnessRune : HextechRelicBase
{
	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<PoisonPower>()
	];

	public override bool IsAvailableForPlayer(Player player) => IsSilentPlayer(player);

	public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target, decimal amount, Creature? applier, out decimal modifiedAmount)
	{
		modifiedAmount = amount;
		if (Owner == null
			|| Owner.Creature.IsDead
			|| target.Side != CombatSide.Enemy
			|| canonicalPower is not PoisonPower
			|| amount != -1m
			|| applier != null)
		{
			return false;
		}

		modifiedAmount = 0m;
		return true;
	}

	public override Task AfterModifyingPowerAmountReceived(PowerModel power)
	{
		Flash();
		return Task.CompletedTask;
	}

	[HarmonyPatch(typeof(PoisonPower), nameof(PoisonPower.CalculateTotalDamageNextTurn), new Type[0])]
	[HextechPatch("rune.terminal-illness", "绝症", Rune = typeof(TerminalIllnessRune))]
	private static class TerminalIllnessPatch
	{
		[HarmonyPostfix]
		private static void Postfix(PoisonPower __instance, ref int __result)
		{
			HextechCombatState? combatState = __instance.Owner.CombatState;
			if (combatState == null
				|| __instance.Owner.Side != CombatSide.Enemy
				|| !combatState.Players.Any(static player =>
					player.Creature.IsAlive && player.GetRelic<TerminalIllnessRune>() != null))
			{
				return;
			}

			int triggerCount = Math.Min(
				__instance.Amount,
				1 + combatState
					.GetOpponentsOf(__instance.Owner)
					.Where(static creature => creature.IsAlive)
					.Sum(static creature => creature.GetPowerAmount<AccelerantPower>()));
			decimal totalDamage = 0m;
			for (int i = 0; i < triggerCount; i++)
			{
	#if STS2_108_OR_NEWER
				decimal damage = Hook.ModifyDamage(
					combatState.RunState,
					combatState,
					__instance.Owner,
					null,
					__instance.Amount,
					ValueProp.Unblockable | ValueProp.Unpowered,
					null,
					null,
					ModifyDamageHookType.All,
					CardPreviewMode.None,
					out _);
	#else
				decimal damage = Hook.ModifyDamage(
					combatState.RunState,
					combatState,
					__instance.Owner,
					null,
					__instance.Amount,
					ValueProp.Unblockable | ValueProp.Unpowered,
					null,
					ModifyDamageHookType.All,
					CardPreviewMode.None,
					out _);
	#endif
				totalDamage += damage;
			}

			__result = (int)totalDamage;
		}
	}
}
