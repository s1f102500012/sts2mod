namespace HextechRunes;

/// <summary>
/// 升级：防御(棱彩,重做)——你的基础防御可以无限升级(5+3n);战斗胜利后,升级你本场打出过的防御(牌库本体,逐张)。
/// 机制说明见 <see cref="StrikeUpgradeRune"/>。
/// </summary>
public sealed class DefendUpgradeRune : HextechRelicBase
{
	public override bool IsAvailableForPlayer(Player player)
	{
		return player.Deck.Cards.Any(IsBasicDefend);
	}

	internal static bool IsBasicDefend(CardModel card)
	{
		return card.Rarity == CardRarity.Basic && card.Tags.Contains(CardTag.Defend);
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		return HextechStarterUpgradeHelper.UpgradePlayedBasicCards(this, Owner, room, IsBasicDefend);
	}
}
