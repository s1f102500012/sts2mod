using MegaCrit.Sts2.Core.Models.Monsters;

namespace HextechRunes;

public sealed class DieForYouRune : HextechRelicBase
{
	private int _pendingWishAmount;
	private int _lastRecordedRound = -1;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedPendingWishAmount
	{
		get => _pendingWishAmount;
		set => _pendingWishAmount = Math.Max(0, value);
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedLastRecordedRound
	{
		get => _lastRecordedRound;
		set => _lastRecordedRound = value;
	}

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new SummonVar(5m),
		new CardsVar(1)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromCard(OstyWishCard.CreatePlaceholderPreview())
	];

	public override bool IsAvailableForPlayer(Player player)
	{
		return IsNecrobinderPlayer(player);
	}

	public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (player != Owner || Owner.Creature.IsDead || !IsNecrobinderPlayer(player))
		{
			return;
		}

		Flash();
		await OstyCmd.Summon(choiceContext, player, DynamicVars.Summon.BaseValue, this);
		await AddPendingWishCard(choiceContext, player);
	}

	public override Task BeforeCombatStart()
	{
		ResetCombatState();
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		ResetCombatState();
		return Task.CompletedTask;
	}

	public override Task AfterDeath(PlayerChoiceContext choiceContext, Creature target, bool wasRemovalPrevented, float deathAnimLength)
	{
		HextechCombatState? combatState = target.CombatState;
		if (Owner == null
			|| wasRemovalPrevented
			|| Owner.Creature.IsDead
			|| target.PetOwner != Owner
			|| target.Monster is not Osty
			|| combatState == null)
		{
			return Task.CompletedTask;
		}

		int roundNumber = combatState.RoundNumber;
		if (_lastRecordedRound == roundNumber)
		{
			return Task.CompletedTask;
		}

		int amount = FloorToInt(target.MaxHp);
		if (amount <= 0)
		{
			return Task.CompletedTask;
		}

		_lastRecordedRound = roundNumber;
		_pendingWishAmount = amount;
		Flash();

		return Task.CompletedTask;
	}

	private async Task AddPendingWishCard(PlayerChoiceContext choiceContext, Player player)
	{
		if (_pendingWishAmount <= 0
			|| player.Creature.CombatState is not HextechCombatState combatState
			|| !CombatManager.Instance.IsInProgress
			|| CombatManager.Instance.IsOverOrEnding)
		{
			return;
		}

		int amount = _pendingWishAmount;
		_pendingWishAmount = 0;

		CardModel card = combatState.CreateCard<OstyWishCard>(player);
		OstyWishCard.SetWishAmount(card, amount);
		await HextechCardGeneration.AddGeneratedCardToCombat(card, PileType.Hand, addedByPlayer: true);
	}

	private void ResetCombatState()
	{
		_pendingWishAmount = 0;
		_lastRecordedRound = -1;
	}
}
