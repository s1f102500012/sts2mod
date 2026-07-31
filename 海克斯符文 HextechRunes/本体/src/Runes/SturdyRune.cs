namespace HextechRunes;

public sealed class SturdyRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("HealPercent", 2m),
		new DynamicVar("LowHpThresholdPercent", 50m),
		new DynamicVar("LowHpHealPercent", 5m)
	];

	public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (player != Owner)
		{
			return Task.CompletedTask;
		}

		int healAmount = CalculateHealAmount(
			player.Creature.MaxHp,
			player.Creature.CurrentHp,
			DynamicVars["HealPercent"].BaseValue,
			DynamicVars["LowHpThresholdPercent"].BaseValue,
			DynamicVars["LowHpHealPercent"].BaseValue);
		Flash();
		return CreatureCmd.Heal(player.Creature, healAmount);
	}

	internal static int CalculateHealAmount(
		int maxHp,
		int currentHp,
		decimal healPercent,
		decimal lowHpThresholdPercent,
		decimal lowHpHealPercent)
	{
		int normalizedMaxHp = Math.Max(0, maxHp);
		decimal percent = currentHp * 100m < normalizedMaxHp * lowHpThresholdPercent
			? lowHpHealPercent
			: healPercent;
		return Math.Max(1, FloorToInt(normalizedMaxHp * Math.Max(0m, percent) / 100m));
	}
}
