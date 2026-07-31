namespace HextechRunes;

public sealed class StormUpgradeRune : CardUpgradeRuneBase<Storm>
{
	protected override bool IsAvailableForCharacter(Player player) => IsDefectPlayer(player);

	internal static bool ShouldTrigger(CardType cardType, bool hasUpgradeRune)
	{
		return cardType == CardType.Power || hasUpgradeRune;
	}
}
