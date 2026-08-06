namespace HextechRunes;

internal sealed record HextechRunConfigurationSnapshot(
	int[] PlayerHexCountsByAct,
	int[] EnemyHexCountsByAct,
	int PlayerRuneRerollLimit,
	int MonsterHexRerollLimit,
	HashSet<string> DisabledPlayerRuneIds,
	HashSet<string> DisabledMonsterHexIds,
	HashSet<string> DisabledForgeIds,
	HextechRarityWeights[] RuneRarityWeightsByAct,
	bool PreventConsecutiveSilverRunes,
	int GoldenRerollChancePercent,
	HextechForgeRarityWeights ForgeRarityWeights,
	int RandomForgeShopPrice,
	bool RandomForgeDirectGrant,
	bool ModEnabled)
{
	public HextechRunConfigurationSnapshot Copy()
	{
		return this with
		{
			PlayerHexCountsByAct = PlayerHexCountsByAct.ToArray(),
			EnemyHexCountsByAct = EnemyHexCountsByAct.ToArray(),
			RuneRarityWeightsByAct = RuneRarityWeightsByAct.ToArray(),
			DisabledPlayerRuneIds = DisabledPlayerRuneIds.ToHashSet(StringComparer.Ordinal),
			DisabledMonsterHexIds = DisabledMonsterHexIds.ToHashSet(StringComparer.Ordinal),
			DisabledForgeIds = DisabledForgeIds.ToHashSet(StringComparer.Ordinal)
		};
	}

	public HextechRarityWeights GetRuneRarityWeightsForAct(int actIndex)
	{
		return RuneRarityWeightsByAct[Math.Clamp(actIndex, 0, RuneRarityWeightsByAct.Length - 1)];
	}
}
