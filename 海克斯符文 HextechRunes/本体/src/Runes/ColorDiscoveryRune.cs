using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Localization;

namespace HextechRunes;

public sealed class ColorDiscoveryRune : HextechRelicBase
{
	private ModelId _pendingRewardCardId = ModelId.none;
	private bool _offeredThisCombat;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public ModelId SavedPendingRewardCardId
	{
		get => _pendingRewardCardId;
		set => _pendingRewardCardId = value;
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public bool SavedOfferedThisCombat
	{
		get => _offeredThisCombat;
		set => _offeredThisCombat = value;
	}

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new CardsVar(3),
		new DynamicVar("Selection", 1m)
	];

	public override Task BeforeCombatStart()
	{
		SavedOfferedThisCombat = false;
		SavedPendingRewardCardId = ModelId.none;
		return Task.CompletedTask;
	}

	public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, HextechCombatState combatState)
	{
		if (SavedOfferedThisCombat
			|| player != Owner
			|| Owner == null
			|| Owner.Creature.IsDead
			|| combatState.RoundNumber != 1
			|| PickOptions(combatState).ToList() is not { Count: > 0 } options)
		{
			return;
		}

		SavedOfferedThisCombat = true;
		IEnumerable<CardModel> selected = await CardSelectCmd.FromSimpleGrid(
			choiceContext,
			options,
			Owner,
			new CardSelectorPrefs(new LocString("cards", "colorDiscoveryRune.selectionScreenPrompt"), 1));
		CardModel? card = selected.FirstOrDefault();
		if (card == null)
		{
			return;
		}

		SavedPendingRewardCardId = card.CanonicalInstance?.Id ?? card.Id;
		card.SetToFreeThisCombat();

		Flash();
		await HextechCardGeneration.AddGeneratedCardToCombat(card, PileType.Hand, addedByPlayer: true);
	}

	public override Task AfterCombatVictory(CombatRoom room)
	{
		if (Owner == null || Owner.Creature.IsDead || SavedPendingRewardCardId.Equals(ModelId.none))
		{
			return Task.CompletedTask;
		}

		room.AddExtraReward(Owner, new ColorDiscoveryCardReward(SavedPendingRewardCardId, Owner));
		SavedPendingRewardCardId = ModelId.none;
		Flash();
		return Task.CompletedTask;
	}

	private IEnumerable<CardModel> PickOptions(HextechCombatState combatState)
	{
		if (Owner == null)
		{
			return [];
		}

		List<CardModel> candidates = GetOtherCharacterCards(Owner).ToList();
		List<CardModel> options = [];
		for (int i = 0; i < DynamicVars.Cards.IntValue && candidates.Count > 0; i++)
		{
			CardModel? card = PickStableGeneratedCard(
				combatState,
				candidates,
				out ModelId canonicalCardId,
				"color-discovery-option",
				HextechStableRandom.PlayerKey(Owner),
				combatState.RoundNumber.ToString(),
				i.ToString(),
				HextechStableRandom.CardPileKey(candidates));
			if (card == null)
			{
				break;
			}

			options.Add(card);
			candidates.RemoveAll(candidate => candidate.Id == canonicalCardId);
		}

		return options;
	}

	private static IEnumerable<CardModel> GetOtherCharacterCards(Player player)
	{
		ModelId ownerPoolId = player.Character.CardPool.Id;
		IEnumerable<CardModel> candidates = GetOtherCharacterPools(
			ModelDb.AllCharacters.Select(static character => character.CardPool),
			ownerPoolId)
			.SelectMany(pool => CardFactory.FilterForCombat(
				pool.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)))
			.Where(static card => card.CanBeGeneratedByModifiers)
			.GroupBy(static card => card.Id)
			.Select(static group => group.First());
		return OrderCandidatesForStableSelection(candidates);
	}

	internal static IReadOnlyList<CardModel> OrderCandidatesForStableSelection(
		IEnumerable<CardModel> candidates)
	{
		return candidates
			.OrderBy(HextechStableRandom.CardKey, StringComparer.Ordinal)
			.ToArray();
	}

	internal static IEnumerable<CardPoolModel> GetOtherCharacterPools(
		IEnumerable<CardPoolModel> characterPools,
		ModelId ownerPoolId)
	{
		return characterPools.Where(pool => !pool.Id.Equals(ownerPoolId));
	}
}
