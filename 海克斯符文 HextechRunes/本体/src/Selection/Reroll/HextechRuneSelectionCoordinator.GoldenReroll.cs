namespace HextechRunes;

internal static partial class HextechRuneSelectionCoordinator
{
	private static HextechGoldenRerollSession CreateGoldenRerollSession(
		Player player,
		int actIndex,
		int choiceOrdinal,
		IReadOnlyList<RelicModel> options)
	{
		HextechRarityTier rarity = GetRarityForOptions(options);
		bool hasUpgradedCandidates =
			HextechGoldenRerollRules.TryGetUpgradedRarity(rarity, out HextechRarityTier upgradedRarity)
			&& BuildSelectableRunePool(
				player,
				upgradedRarity,
				(RunState)player.RunState).Count > 0;
		HextechGoldenRerollSession session = HextechGoldenRerollSession.Create(
			(RunState)player.RunState,
			player,
			actIndex,
			choiceOrdinal,
			rarity,
			hasUpgradedCandidates);
		if (session.IsActive)
		{
			HextechLog.Info(
				$"[{ModInfo.Id}][Mayhem] Golden reroll active: act={actIndex} choice={choiceOrdinal} " +
				$"player={player.NetId} rarity={rarity} upgraded={session.UpgradedRarity}");
		}

		return session;
	}

	private static HextechRarityTier? GetGoldenRerollOverride(
		HextechGoldenRerollSession goldenReroll)
	{
		return goldenReroll.IsActive ? goldenReroll.UpgradedRarity : null;
	}
}
