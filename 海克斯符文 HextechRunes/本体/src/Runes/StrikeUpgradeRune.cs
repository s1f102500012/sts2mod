namespace HextechRunes;

/// <summary>
/// 升级：打击(棱彩,重做)——你的基础打击可以无限升级(6+3n);战斗胜利后,升级你本场打出过的打击(牌库本体,逐张)。
/// 无限升级由 HextechStarterUpgradeHooks 放开 MaxUpgradeLevel 实现;
/// 标题"打击+n"与升级公式(每级+3)都是原版 MaxUpgradeLevel>1 时的原生行为,无需额外处理。
/// 结算挂在 AfterCombatEnd:原版在 AfterCombatVictory 前就清空打出历史,见 HextechStarterUpgradeHelper。
/// </summary>
public sealed class StrikeUpgradeRune : HextechRelicBase
{
	public override bool IsAvailableForPlayer(Player player)
	{
		return player.Deck.Cards.Any(IsBasicStrike);
	}

	internal static bool IsBasicStrike(CardModel card)
	{
		return card.Rarity == CardRarity.Basic && card.Tags.Contains(CardTag.Strike);
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		return HextechStarterUpgradeHelper.UpgradePlayedBasicCards(this, Owner, room, IsBasicStrike);
	}
}
