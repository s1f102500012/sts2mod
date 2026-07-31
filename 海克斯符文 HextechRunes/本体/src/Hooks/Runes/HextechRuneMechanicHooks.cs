using HarmonyLib;
using MegaCrit.Sts2.Core.Hooks;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

internal static class HextechRuneMechanicHooks
{
	internal static void InstallPactsEndUpgrade(Harmony harmony)
	{
		harmony.Patch(
			RequireMethod(typeof(PactsEnd), "get_CanDealDamage", BindingFlags.Instance | BindingFlags.NonPublic),
			postfix: new HarmonyMethod(typeof(HextechRuneMechanicHooks), nameof(PactsEndCanDealDamagePostfix)));
	}

	internal static void InstallCorrosiveWaveUpgrade(Harmony harmony)
	{
		harmony.Patch(
			RequireMethod(typeof(CorrosiveWavePower), nameof(CorrosiveWavePower.AfterSideTurnEnd), BindingFlags.Instance | BindingFlags.Public, typeof(PlayerChoiceContext), typeof(CombatSide), typeof(IEnumerable<Creature>)),
			prefix: new HarmonyMethod(typeof(HextechRuneMechanicHooks), nameof(CorrosiveWaveAfterSideTurnEndPrefix)));
	}

	internal static void InstallTerminalIllness(Harmony harmony)
	{
		harmony.Patch(
			RequireMethod(typeof(PoisonPower), nameof(PoisonPower.CalculateTotalDamageNextTurn), BindingFlags.Instance | BindingFlags.Public),
			postfix: new HarmonyMethod(typeof(HextechRuneMechanicHooks), nameof(PoisonCalculateTotalDamageNextTurnPostfix)));
	}

	internal static void InstallBigHammer(Harmony harmony)
	{
		harmony.Patch(
			RequireMethod(typeof(ForgeCmd), nameof(ForgeCmd.Forge), BindingFlags.Static | BindingFlags.Public, typeof(decimal), typeof(Player), typeof(AbstractModel)),
			prefix: new HarmonyMethod(typeof(HextechRuneMechanicHooks), nameof(ForgePrefix)));
	}

	internal static void InstallOblivionUpgrade(Harmony harmony)
	{
		harmony.Patch(
			RequireMethod(typeof(OblivionPower), nameof(OblivionPower.AfterSideTurnEnd), BindingFlags.Instance | BindingFlags.Public, typeof(PlayerChoiceContext), typeof(CombatSide), typeof(IEnumerable<Creature>)),
			prefix: new HarmonyMethod(typeof(HextechRuneMechanicHooks), nameof(OblivionAfterSideTurnEndPrefix)));
	}

	private static void PactsEndCanDealDamagePostfix(PactsEnd __instance, ref bool __result)
	{
		if (!__result && __instance.Owner.GetRelic<PactsEndUpgradeRune>() != null)
		{
			__result = true;
		}
	}

	private static bool CorrosiveWaveAfterSideTurnEndPrefix(CorrosiveWavePower __instance, ref Task __result)
	{
		if (__instance.Owner.Player?.GetRelic<CorrosiveWaveUpgradeRune>() == null)
		{
			return true;
		}

		__result = Task.CompletedTask;
		return false;
	}

	private static void PoisonCalculateTotalDamageNextTurnPostfix(PoisonPower __instance, ref int __result)
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

	private static void ForgePrefix(ref decimal amount, Player player, AbstractModel? source)
	{
		BigHammerRune? rune = player.GetRelic<BigHammerRune>();
		if (rune == null)
		{
			return;
		}

		bool sourceAlreadyIncludesBonus = source is HammerTimePower hammerTime
			&& hammerTime.Owner.Player?.GetRelic<BigHammerRune>() != null;
		decimal modifiedAmount = rune.ApplyForgeBonus(amount, sourceAlreadyIncludesBonus);
		if (modifiedAmount == amount)
		{
			return;
		}

		amount = modifiedAmount;
		rune.Flash();
	}

	private static bool OblivionAfterSideTurnEndPrefix(OblivionPower __instance, ref Task __result)
	{
		if (__instance.Applier?.Player?.GetRelic<OblivionUpgradeRune>() == null)
		{
			return true;
		}

		__result = Task.CompletedTask;
		return false;
	}
}
