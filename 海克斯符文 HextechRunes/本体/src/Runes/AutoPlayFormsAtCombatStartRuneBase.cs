namespace HextechRunes;

/// <summary>
/// 「升级：XX形态」系共用基类:每场战斗开始时,自动打出你所有的目标能力牌(TCard),
/// 类似注能附魔——形态不再占用手牌与抽牌流。触发时机照抄固态时间:首个玩家回合开始后
/// (牌堆与打出管线就绪),以每场一次标志防重复;普通形态牌合并结算最终 Power,
/// 仅有克隆附魔的牌也可合并;其他附魔/负面附着牌回退原版逐张路径,整批只派发一次打出牌钩子。
/// 补卡/无刷新门槛由 CardUpgradeRuneBase 提供。
/// </summary>
public abstract class AutoPlayFormsAtCombatStartRuneBase<TCard> : CardUpgradeRuneBase<TCard>
	where TCard : CardModel
{
	private bool _startedThisCombat;
	private bool _autoPlaying;

	public override Task BeforeCombatStart()
	{
		_startedThisCombat = false;
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		_startedThisCombat = false;
		return Task.CompletedTask;
	}

	public override async Task AfterPlayerTurnStartLate(PlayerChoiceContext choiceContext, Player player)
	{
		if (_startedThisCombat
			|| _autoPlaying
			|| player != Owner
			|| Owner == null
			|| Owner.Creature.IsDead
			|| Owner.PlayerCombatState == null
			|| !IsAvailableForCharacter(Owner))
		{
			return;
		}

		_startedThisCombat = true;

		// "你所有的 XX 形态":战斗牌堆(抽牌/手牌/弃牌)里的全部,含引魂/固有等把牌送进
		// 非常规起始位置的情况;消耗堆与已移除的不算。
		List<TCard> cards = Owner.PlayerCombatState.AllCards
			.OfType<TCard>()
			.Where(card => card.Owner == Owner
				&& card.Pile?.Type is PileType.Draw or PileType.Hand or PileType.Discard)
			.ToList();
		if (cards.Count == 0)
		{
			return;
		}

		_autoPlaying = true;
		try
		{
			Flash();
			// 整批从进场动画起共用展开布局;安全场景只结算一次合计 Power,兼容场景仍走
			// 原版逐张路径。虚空形态的 EndTurn 在同一确定性作用域内压掉,不吃掉首回合。
			using (HextechFormAutoPlayHooks.BeginCardPlayBatch(cards))
			using (HextechFormAutoPlayHooks.BeginEndTurnSuppression())
			{
				IReadOnlyList<CardPileAddResult> moveResults = await CardPileCmd.Add(
					cards,
					PileType.Play,
					CardPilePosition.Bottom);
				List<CardModel> cardsInPlay = moveResults
					.Where(result => result.success && result.cardAdded.Pile?.Type == PileType.Play)
					.Select(result => result.cardAdded)
					.ToList();

				await HextechFormAutoPlayHooks.PlayCardBatchVfx(cardsInPlay);

				if (await HextechFormAutoPlayHooks.TryPlayCombinedFinalEffect(choiceContext, cardsInPlay))
				{
					return;
				}

				foreach (CardModel card in cardsInPlay)
				{
					if (card.Pile?.Type != PileType.Play)
					{
						continue;
					}

					await HextechAutoPlayHelper.AutoPlayOrMoveToResultPile(
						choiceContext,
						card,
						target: null,
						skipCardPileVisuals: true);
				}
			}
		}
		finally
		{
			_autoPlaying = false;
		}
	}
}
