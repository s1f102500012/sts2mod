using MegaCrit.Sts2.Core.Localization;

namespace HextechRunes;

/// <summary>选择界面卡片底部的小字提示。</summary>
internal interface IHextechSelectionFooterProvider
{
	string? GetSelectionFooterText();
}

// 普通「升级：XX」获得时固定加入 1 张目标牌；双形态升级不送牌，由子类显式关闭。
// 补卡说明以选择界面 footer 小字呈现。覆盖 AfterObtained 的子类必须先调用基类。
public abstract class CardUpgradeRuneBase<TCard> : HextechRelicBase, IHextechSelectionFooterProvider
	where TCard : CardModel
{
	public override bool HasUponPickupEffect => GrantsCardOnPickup;

	internal virtual bool GrantsCardOnPickup => true;

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromCard<TCard>()
	];

	public sealed override bool IsAvailableForPlayer(Player player)
	{
		return IsAvailableForCharacter(player)
			&& MeetsCardAvailabilityRequirement(player.Deck.Cards);
	}

	internal virtual bool MeetsCardAvailabilityRequirement(IEnumerable<CardModel> deckCards)
	{
		return true;
	}

	public override Task AfterObtained()
	{
		return GrantsCardOnPickup
			? AddCardCopiesToDeckOrHand<TCard>(1)
			: Task.CompletedTask;
	}

	protected abstract bool IsAvailableForCharacter(Player player);

	public virtual string? GetSelectionFooterText()
	{
		if (!GrantsCardOnPickup)
		{
			return null;
		}

		try
		{
			// 占位符必须走 LocString 变量机制:裸 {0} 会被 SmartFormat 当变量解析,
			// 找不到时把内部字典 ToString 吐进文本。
			LocString footer = new("relics", "hextechUpgradeRune.selectionFooter");
			footer.Add("CardName", ModelDb.Card<TCard>().Title);
			return footer.GetFormattedText();
		}
		catch
		{
			return null;
		}
	}
}
