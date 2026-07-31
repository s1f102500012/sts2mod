using MegaCrit.Sts2.Core.Saves;

namespace HextechRunes;

public abstract partial class HextechRelicBase
{
	// 战斗生成池必须同时满足原版战斗过滤和 modifier 生成许可；稳定排序让调用方
	// 可以安全地把完整池写入随机盐，而不依赖 CardPool 的枚举顺序。
	protected static List<CardModel> BuildStableCombatGenerationPool(
		IEnumerable<CardModel> candidates,
		Func<CardModel, bool>? extraFilter = null)
	{
		IEnumerable<CardModel> filtered = CardFactory.FilterForCombat(candidates);
		if (extraFilter != null)
		{
			filtered = filtered.Where(extraFilter);
		}

		return filtered
			.Where(static card => card.CanBeGeneratedByModifiers)
			.OrderBy(HextechStableRandom.CardKey, StringComparer.Ordinal)
			.ToList();
	}

	protected CardModel? PickStableGeneratedCard(
		HextechCombatState combatState,
		IReadOnlyList<CardModel> pool,
		params string?[] saltParts)
	{
		return PickStableGeneratedCard(combatState, pool, out _, saltParts);
	}

	protected CardModel? PickStableGeneratedCard(
		HextechCombatState combatState,
		IReadOnlyList<CardModel> pool,
		out ModelId canonicalCardId,
		params string?[] saltParts)
	{
		canonicalCardId = ModelId.none;
		if (Owner == null || pool.Count == 0)
		{
			return null;
		}

		CardModel canonicalCard = HextechStableRandom.Pick(
			pool,
			(RunState)Owner.RunState,
			HextechStableRandom.CardKey,
			saltParts);
		canonicalCardId = canonicalCard.Id;
		return combatState.CreateCard(canonicalCard, Owner);
	}

	protected async Task AddCardCopiesToDeckOrHand<TCard>(int count, Action<CardModel>? configureCard = null)
		where TCard : CardModel
	{
		if (Owner == null || count <= 0)
		{
			return;
		}

		HextechCombatState? combatState = Owner.Creature.CombatState;
		if (Owner.PlayerCombatState != null
			&& combatState != null
			&& CombatManager.Instance.IsInProgress
			&& !CombatManager.Instance.IsOverOrEnding)
		{
			List<CardModel> cards = new(count);
			for (int i = 0; i < count; i++)
			{
				CardModel card = combatState.CreateCard<TCard>(Owner);
				configureCard?.Invoke(card);
				cards.Add(card);
			}

			await HextechCardGeneration.AddGeneratedCardsToCombat(cards, PileType.Hand, addedByPlayer: true);

			return;
		}

		List<CardPileAddResult> results = new(count);
		for (int i = 0; i < count; i++)
		{
			CardModel card = Owner.RunState.CreateCard<TCard>(Owner);
			configureCard?.Invoke(card);
			results.Add(await CardPileCmd.Add(card, PileType.Deck));
			SaveManager.Instance.MarkCardAsSeen(card);
		}

		CardCmd.PreviewCardPileAdd(results, 2f);
	}

	protected async Task AddCardCopiesToCombatHand<TCard>(int count, Action<CardModel>? configureCard = null)
		where TCard : CardModel
	{
		if (Owner == null
			|| count <= 0
			|| Owner.PlayerCombatState == null
			|| Owner.Creature.CombatState is not HextechCombatState combatState
			|| !CombatManager.Instance.IsInProgress
			|| CombatManager.Instance.IsOverOrEnding)
		{
			return;
		}

		List<CardModel> cards = new(count);
		for (int i = 0; i < count; i++)
		{
			CardModel card = combatState.CreateCard<TCard>(Owner);
			configureCard?.Invoke(card);
			cards.Add(card);
		}

		await HextechCardGeneration.AddGeneratedCardsToCombat(cards, PileType.Hand, addedByPlayer: true);
	}
}
