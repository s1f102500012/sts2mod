namespace HextechRunes;

internal static class HextechMonsterSustainHelper
{
	public static decimal GetProteinShakeSustainMultiplier(Creature creature, int playerCount = 1)
	{
		return ResolveProteinShakeSustainMultiplier(creature.MaxHp, playerCount);
	}

	internal static decimal ResolveProteinShakeSustainMultiplier(decimal maxHp, int playerCount = 1)
	{
		decimal hpPerPercent = 5m * Math.Clamp(playerCount, 1, 16);
		decimal bonusPercent = Math.Min(100m, Math.Max(0m, Math.Floor(maxHp / hpPerPercent)));
		return 1m + bonusPercent / 100m;
	}
}
