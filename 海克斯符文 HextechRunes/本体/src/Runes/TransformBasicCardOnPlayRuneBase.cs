using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace HextechRunes;

// 「打出某类基础卡后,把它在本场战斗中变化为另一张卡」的共用基类。
// 时机:卡打出后会从 PileType.Play 移入弃牌堆。不能在 AfterCardPlayed/Late 里转化(那时卡仍在打出区、
// 之后还要被移堆,中途转化会破坏移堆 → 卡牌卡在空中)。改在 AfterCardChangedPilesLate:卡刚从 Play 落定到
// 弃牌堆那一刻再用 CardPreviewStyle.None 静默转化 —— 即时生效、不卡牌,与「从弃牌堆选牌入手」也相性正常。
// oldPileType == Play 恰好只命中「打出」的卡(手牌弃掉的是 Hand→弃牌堆,不会误伤);Finesse/亮剑非基础卡,
// 转化产生的新卡不会再次触发,无循环。
// 还必须同时检查「新堆不是 Play」:自动打出(乱战 MayhemPower 的 AutoPlayFromDrawPile 等)会先把牌
// 放进 Play 堆,OnPlayWrapper 开头再 Add(Play) 一次,产生 Play→Play 的移堆——只看 oldPileType 会在
// 牌尚未打出时就转化,打出流程随后拿着已移除的旧卡跑完、结果堆结算被跳过,NCard 卡在屏幕中间
// (0.8.3 玩家反馈:升级打击+顺手的事,第一回合乱战自动打出打击卡死)。加上 card.Pile.Type != Play
// 后,该场景推迟到牌真正落入弃牌堆那次移堆再转化,行为与手动打出一致。
public abstract class TransformBasicCardOnPlayRuneBase<TReplacement> : HextechRelicBase
	where TReplacement : CardModel
{
	protected abstract bool ShouldTransform(CardModel card);

	// 泛型模型类的钩子只转发,不直接用类型参数(第三方批量补丁会把它写死,见 AutoPlayFormsAtCombatStartRuneBase)。
	public override Task AfterCardChangedPilesLate(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
	{
		return TransformPlayedCardAsync(card, oldPileType);
	}

	private async Task TransformPlayedCardAsync(CardModel card, PileType oldPileType)
	{
		if (Owner == null
			|| card.Owner != Owner
			|| oldPileType != PileType.Play
			|| card.Pile == null
			|| card.Pile.Type == PileType.Play
			|| !ShouldTransform(card)
			|| card.CombatState is not { } combatState)
		{
			return;
		}

		Flash();
		CardModel replacement = combatState.CreateCard<TReplacement>(Owner);
		CardTransformUpgradeHelper.PreserveUpgradeLevel(card, replacement);
		await CardCmd.Transform(card, replacement, CardPreviewStyle.None);
	}
}
