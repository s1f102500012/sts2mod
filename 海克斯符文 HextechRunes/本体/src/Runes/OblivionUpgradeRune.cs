using HarmonyLib;
using MegaCrit.Sts2.Core.Hooks;

namespace HextechRunes;

public sealed class OblivionUpgradeRune : CardUpgradeRuneBase<Oblivion>
{
	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromCard<Oblivion>(),
		HoverTipFactory.FromPower<OblivionPower>()
	];

	protected override bool IsAvailableForCharacter(Player player) => IsNecrobinderPlayer(player);

	[HarmonyPatch(typeof(OblivionPower), nameof(OblivionPower.AfterSideTurnEnd), typeof(PlayerChoiceContext), typeof(CombatSide), typeof(IEnumerable<Creature>))]
	[HextechPatch("rune.oblivion", "升级遗忘", Rune = typeof(OblivionUpgradeRune))]
	private static class OblivionPatch
	{
		[HarmonyPrefix]
		[HarmonyPriority(Priority.Low)]
		private static bool Prefix(OblivionPower __instance, ref Task __result)
		{
			if (__instance.Applier?.Player?.GetRelic<OblivionUpgradeRune>() == null)
			{
				return true;
			}

			__result = Task.CompletedTask;
			return false;
		}
	}
}
