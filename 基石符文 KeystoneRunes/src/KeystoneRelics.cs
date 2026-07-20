using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

#if STS2_104_OR_NEWER
using TurnCombatState = MegaCrit.Sts2.Core.Combat.ICombatState;
#else
using TurnCombatState = MegaCrit.Sts2.Core.Combat.CombatState;
#endif

namespace KeystoneRunes;

public abstract class Keystone_RelicBase : RelicModel
{
	public sealed override RelicRarity Rarity => RelicRarity.Starter;

	public override string PackedIconPath => GetIconPath();

	protected override string PackedIconOutlinePath => PackedIconPath;

	protected override string BigIconPath => PackedIconPath;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public bool KeystoneRunes_SelectionHandled { get; set; }

#if STS2_106_OR_NEWER
	public virtual Task AfterSideTurnStart(CombatSide side, TurnCombatState combatState)
	{
		return Task.CompletedTask;
	}

	public sealed override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, TurnCombatState combatState)
	{
		return AfterSideTurnStart(side, combatState);
	}

	public virtual Task AfterTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)
	{
		return Task.CompletedTask;
	}

	public sealed override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
	{
		return AfterTurnEnd(choiceContext, side);
	}
#endif

	protected abstract string GetIconPath();

	protected static int RoundToInt(decimal value)
	{
		return (int)decimal.Round(value, 0, MidpointRounding.AwayFromZero);
	}

	protected bool IsOwnedCard(CardModel? card)
	{
		return card?.Owner == Owner;
	}

	protected bool IsOwnedAttack(CardModel? card)
	{
		return card != null && card.Owner == Owner && card.Type == CardType.Attack;
	}

	protected CardModel? TryFindOwnedCombatCard(SerializableCard? savedCard)
	{
		if (savedCard?.Id == null || Owner?.PlayerCombatState == null)
		{
			return null;
		}

		IEnumerable<CardModel> candidates = Owner.PlayerCombatState.PlayPile.Cards
			.Concat(Owner.PlayerCombatState.AllCards);
		return candidates.FirstOrDefault(card => IsSameSavedCard(card, savedCard));
	}

	protected int GetCurrentActBonus()
	{
		return Math.Max(1, (Owner?.RunState.CurrentActIndex ?? 0) + 1);
	}

	protected bool TryGetOwnedEnemyDebuffTarget(PowerModel power, decimal amount, Creature? applier, out Creature? target)
	{
		target = power.Owner;
		return amount != 0m
			&& power.GetTypeForAmount(amount) == PowerType.Debuff
			&& target?.Side == CombatSide.Enemy
			&& applier == Owner?.Creature
			&& power is not ITemporaryPower;
	}

	private static bool IsSameSavedCard(CardModel card, SerializableCard savedCard)
	{
		SerializableCard current = card.ToSerializable();
		return Equals(current.Id, savedCard.Id)
			&& current.CurrentUpgradeLevel == savedCard.CurrentUpgradeLevel
			&& Equals(current.Enchantment, savedCard.Enchantment);
	}
}

public sealed class Keystone_ElectrocuteRune : Keystone_RelicBase
{
	private const int RequiredHits = 3;

	private const int BaseDamage = 5;

	private const decimal CurrentHpRatio = 0.10m;

	private int _consecutiveHitsThisTurn;

	private int _trackedTargetCombatId = -1;

	private CardModel? _currentTrackedCard;

	private bool _currentTrackedCardHadHit;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedConsecutiveHitsThisTurn
	{
		get => _consecutiveHitsThisTurn;
		set
		{
			_consecutiveHitsThisTurn = Math.Max(0, value);
			RefreshVisualState();
		}
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedTrackedTargetCombatId
	{
		get => _trackedTargetCombatId;
		set => _trackedTargetCombatId = value;
	}

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("Hits", RequiredHits),
		new DynamicVar("BaseDamage", BaseDamage),
		new DynamicVar("HpPercent", CurrentHpRatio * 100m)
	];

	public override bool ShowCounter => CombatManager.Instance?.IsInProgress == true && !IsCanonical;

	public override int DisplayAmount => !IsCanonical ? _consecutiveHitsThisTurn : 0;

	protected override string GetIconPath() => ModInfo.ElectrocuteIconPath;

	public override Task BeforeCombatStart()
	{
		ResetTracking();
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		ResetTracking();
		return Task.CompletedTask;
	}

	public override Task AfterSideTurnStart(CombatSide side, TurnCombatState combatState)
	{
		if (side == CombatSide.Player)
		{
			ResetTracking();
		}

		return Task.CompletedTask;
	}

	public override Task BeforeCardPlayed(CardPlay cardPlay)
	{
		if (!IsOwnedCard(cardPlay.Card))
		{
			return Task.CompletedTask;
		}

		_currentTrackedCard = cardPlay.Card;
		_currentTrackedCardHadHit = false;
		if (!IsPotentialElectrocuteCard(cardPlay.Card))
		{
			ResetTracking();
		}

		return Task.CompletedTask;
	}

	public override async Task AfterDamageGiven(
		PlayerChoiceContext choiceContext,
		Creature? dealer,
		DamageResult result,
		ValueProp props,
		Creature target,
		CardModel? cardSource)
	{
		if (!IsValidElectrocuteHit(dealer, result, props, target, cardSource))
		{
			return;
		}

		if (ReferenceEquals(cardSource, _currentTrackedCard))
		{
			_currentTrackedCardHadHit = true;
		}

		int targetCombatId = target.CombatId.HasValue ? checked((int)target.CombatId.Value) : -1;
		if (_trackedTargetCombatId >= 0 && targetCombatId == _trackedTargetCombatId)
		{
			_consecutiveHitsThisTurn++;
		}
		else
		{
			_trackedTargetCombatId = targetCombatId;
			_consecutiveHitsThisTurn = 1;
		}

		RefreshVisualState();
		if (_consecutiveHitsThisTurn < RequiredHits || !target.IsAlive)
		{
			return;
		}

		int bonusDamage = BaseDamage + RoundToInt((decimal)target.CurrentHp * CurrentHpRatio);
		ResetTracking();
		Flash([target]);
		await CreatureCmd.Damage(
			choiceContext,
			target,
			bonusDamage,
			ValueProp.Unpowered | ValueProp.SkipHurtAnim,
			Owner!.Creature,
			cardSource: null);
	}

	public override Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		if (!IsOwnedCard(cardPlay.Card) || !ReferenceEquals(cardPlay.Card, _currentTrackedCard))
		{
			return Task.CompletedTask;
		}

		if (!_currentTrackedCardHadHit)
		{
			ResetTracking();
		}

		if (cardPlay.IsLastInSeries)
		{
			_currentTrackedCard = null;
			_currentTrackedCardHadHit = false;
		}

		return Task.CompletedTask;
	}

	private bool IsValidElectrocuteHit(
		Creature? dealer,
		DamageResult result,
		ValueProp props,
		Creature target,
		CardModel? cardSource)
	{
		return dealer == Owner?.Creature
			&& target.Side == CombatSide.Enemy
			&& result.TotalDamage > 0
			&& !props.HasFlag(ValueProp.Unpowered)
			&& IsPotentialElectrocuteCard(cardSource);
	}

	private bool IsPotentialElectrocuteCard(CardModel? card)
	{
		return card != null
			&& IsOwnedCard(card)
			&& card.TargetType == TargetType.AnyEnemy;
	}

	private void ResetTracking()
	{
		_consecutiveHitsThisTurn = 0;
		_trackedTargetCombatId = -1;
		RefreshVisualState();
	}

	private void RefreshVisualState()
	{
		Status = _consecutiveHitsThisTurn >= RequiredHits - 1 ? RelicStatus.Active : RelicStatus.Normal;
		InvokeDisplayAmountChanged();
	}
}

public sealed class Keystone_FirstStrikeRune : Keystone_RelicBase
{
	private bool _hasDuplicatedFirstAttack;

	private int _firstTurnDamage;

	private CardModel? _trackedFirstAttackCard;

	private CardModel? _activeFirstAttackCard;

	private SerializableCard? _savedTrackedFirstAttackCard;

	private SerializableCard? _savedActiveFirstAttackCard;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public bool SavedHasDuplicatedFirstAttack
	{
		get => _hasDuplicatedFirstAttack;
		set => _hasDuplicatedFirstAttack = value;
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedFirstTurnDamage
	{
		get => _firstTurnDamage;
		set
		{
			_firstTurnDamage = Math.Max(0, value);
			InvokeDisplayAmountChanged();
		}
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public SerializableCard? SavedTrackedFirstAttackCard
	{
		get => _trackedFirstAttackCard?.ToSerializable() ?? _savedTrackedFirstAttackCard;
		set
		{
			_trackedFirstAttackCard = null;
			_savedTrackedFirstAttackCard = value;
		}
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public SerializableCard? SavedActiveFirstAttackCard
	{
		get => _activeFirstAttackCard?.ToSerializable() ?? _savedActiveFirstAttackCard;
		set
		{
			_activeFirstAttackCard = null;
			_savedActiveFirstAttackCard = value;
		}
	}

	public override bool ShowCounter => CombatManager.Instance?.IsInProgress == true && !IsCanonical;

	public override int DisplayAmount => !IsCanonical ? _firstTurnDamage : 0;

	protected override string GetIconPath() => ModInfo.FirstStrikeIconPath;

	public override Task BeforeCombatStart()
	{
		ResetTracking();
		return Task.CompletedTask;
	}

	public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount)
	{
		ResolveSavedFirstAttackCards();
		if (_hasDuplicatedFirstAttack || !IsOwnedAttack(card))
		{
			return playCount;
		}

		return playCount + 1;
	}

	public override Task AfterModifyingCardPlayCount(CardModel card)
	{
		ResolveSavedFirstAttackCards();
		if (_hasDuplicatedFirstAttack || !IsOwnedAttack(card))
		{
			return Task.CompletedTask;
		}

		_hasDuplicatedFirstAttack = true;
		_trackedFirstAttackCard = card;
		_savedTrackedFirstAttackCard = null;
		Status = RelicStatus.Active;
		Flash(Array.Empty<Creature>());
		return Task.CompletedTask;
	}

	public override Task BeforeCardPlayed(CardPlay cardPlay)
	{
		ResolveSavedFirstAttackCards();
		if (ReferenceEquals(cardPlay.Card, _trackedFirstAttackCard))
		{
			_activeFirstAttackCard = cardPlay.Card;
			_savedActiveFirstAttackCard = null;
		}

		return Task.CompletedTask;
	}

	public override Task AfterDamageGiven(
		PlayerChoiceContext choiceContext,
		Creature? dealer,
		DamageResult result,
		ValueProp props,
		Creature target,
		CardModel? cardSource)
	{
		Player? owner = Owner;
		if (owner == null || !IsOwnerDamageDealer(owner, dealer) || target.Side != CombatSide.Enemy)
		{
			return Task.CompletedTask;
		}

		if (IsActiveFirstAttackDamage(cardSource))
		{
			_firstTurnDamage += result.TotalDamage;
			InvokeDisplayAmountChanged();
		}

		return Task.CompletedTask;
	}

	public override Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ResolveSavedFirstAttackCards();
		if (ReferenceEquals(cardPlay.Card, _activeFirstAttackCard) && cardPlay.IsLastInSeries)
		{
			_trackedFirstAttackCard = null;
			_activeFirstAttackCard = null;
			_savedTrackedFirstAttackCard = null;
			_savedActiveFirstAttackCard = null;
		}

		return Task.CompletedTask;
	}

	public override Task AfterCombatVictory(CombatRoom room)
	{
		if (Owner != null && _firstTurnDamage > 0)
		{
			room.AddExtraReward(Owner, new GoldReward(_firstTurnDamage, Owner));
			Flash(Array.Empty<Creature>());
			_firstTurnDamage = 0;
		}

		return Task.CompletedTask;
	}

	private bool IsActiveFirstAttackDamage(CardModel? cardSource)
	{
		ResolveSavedFirstAttackCards();
		if (!ReferenceEquals(cardSource, _activeFirstAttackCard))
		{
			return false;
		}

		var history = CombatManager.Instance?.History;
		if (history == null)
		{
			return false;
		}

		int started = 0;
		foreach (var entry in history.CardPlaysStarted)
		{
			if (ReferenceEquals(entry.CardPlay.Card, _activeFirstAttackCard))
			{
				started++;
			}
		}

		int finished = 0;
		foreach (var entry in history.CardPlaysFinished)
		{
			if (ReferenceEquals(entry.CardPlay.Card, _activeFirstAttackCard))
			{
				finished++;
			}
		}

		return started > finished;
	}

	private static bool IsOwnerDamageDealer(Player owner, Creature? dealer)
	{
		return dealer == owner.Creature
			|| dealer == owner.Osty
			|| (dealer?.IsPet == true && dealer.PetOwner == owner);
	}

	private void ResolveSavedFirstAttackCards()
	{
		if (_trackedFirstAttackCard == null && _savedTrackedFirstAttackCard != null)
		{
			_trackedFirstAttackCard = TryFindOwnedCombatCard(_savedTrackedFirstAttackCard);
			if (_trackedFirstAttackCard != null)
			{
				_savedTrackedFirstAttackCard = null;
			}
		}

		if (_activeFirstAttackCard == null && _savedActiveFirstAttackCard != null)
		{
			_activeFirstAttackCard = TryFindOwnedCombatCard(_savedActiveFirstAttackCard);
			if (_activeFirstAttackCard != null)
			{
				_savedActiveFirstAttackCard = null;
			}
		}
	}

	private void ResetTracking()
	{
		_hasDuplicatedFirstAttack = false;
		_firstTurnDamage = 0;
		_trackedFirstAttackCard = null;
		_activeFirstAttackCard = null;
		_savedTrackedFirstAttackCard = null;
		_savedActiveFirstAttackCard = null;
		Status = RelicStatus.Normal;
		InvokeDisplayAmountChanged();
	}
}

public sealed class Keystone_UndyingGraspRune : Keystone_RelicBase
{
	private const int CardsPerCharge = 4;

	private const decimal BonusDamageRatio = 0.05m;

	private const decimal HealRatio = 0.02m;

	private const int FirstTriggerMaxHpGain = 1;

	private int _cardsPlayedTowardCharge;

	private int _charges;

	private CardModel? _lastTriggeredCard;

	private SerializableCard? _savedLastTriggeredCard;

	private bool _hasGainedMaxHpThisCombat;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedCardsPlayedTowardCharge
	{
		get => _cardsPlayedTowardCharge;
		set => _cardsPlayedTowardCharge = Math.Max(0, value);
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedCharges
	{
		get => _charges;
		set
		{
			_charges = Math.Max(0, value);
			RefreshVisualState();
		}
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public bool SavedHasGainedMaxHpThisCombat
	{
		get => _hasGainedMaxHpThisCombat;
		set => _hasGainedMaxHpThisCombat = value;
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public SerializableCard? SavedLastTriggeredCard
	{
		get => _lastTriggeredCard?.ToSerializable() ?? _savedLastTriggeredCard;
		set
		{
			_lastTriggeredCard = null;
			_savedLastTriggeredCard = value;
		}
	}

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("CardsPerCharge", CardsPerCharge),
		new DynamicVar("BonusDamagePercent", BonusDamageRatio * 100m),
		new DynamicVar("HealPercent", HealRatio * 100m),
		new DynamicVar("MaxHpGain", FirstTriggerMaxHpGain)
	];

	public override bool ShowCounter => CombatManager.Instance?.IsInProgress == true;

	public override int DisplayAmount
	{
		get
		{
			if (IsCanonical)
			{
				return 0;
			}

			return _charges > 0 ? CardsPerCharge : _cardsPlayedTowardCharge;
		}
	}

	protected override string GetIconPath() => ModInfo.GraspIconPath;

	public override Task BeforeCombatStart()
	{
		ResetTracking();
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		ResetTracking();
		return Task.CompletedTask;
	}

	public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		ResolveSavedLastTriggeredCard();
		if (ReferenceEquals(cardPlay.Card, _lastTriggeredCard))
		{
			_lastTriggeredCard = null;
			_savedLastTriggeredCard = null;
			return Task.CompletedTask;
		}

		if (!IsOwnedAttack(cardPlay.Card))
		{
			return Task.CompletedTask;
		}

		if (_charges > 0)
		{
			return Task.CompletedTask;
		}

		_cardsPlayedTowardCharge++;
		if (_cardsPlayedTowardCharge >= CardsPerCharge)
		{
			_cardsPlayedTowardCharge -= CardsPerCharge;
			_charges++;
			Flash(Array.Empty<Creature>());
		}
		RefreshVisualState();

		return Task.CompletedTask;
	}

	public override async Task AfterDamageGiven(
		PlayerChoiceContext choiceContext,
		Creature? dealer,
		DamageResult result,
		ValueProp props,
		Creature target,
		CardModel? cardSource)
	{
		ResolveSavedLastTriggeredCard();
		if (_charges <= 0
			|| dealer != Owner?.Creature
			|| target.Side != CombatSide.Enemy
			|| !IsOwnedAttack(cardSource)
			|| ReferenceEquals(cardSource, _lastTriggeredCard))
		{
			return;
		}

		_charges--;
		_lastTriggeredCard = cardSource;
		_savedLastTriggeredCard = null;
		_cardsPlayedTowardCharge = 1;
		RefreshVisualState();

		Player owner = Owner!;
		int bonusDamage = RoundToInt((decimal)owner.Creature.MaxHp * BonusDamageRatio);
		int healAmount = Math.Max(1, RoundToInt((decimal)owner.Creature.MaxHp * HealRatio));
		Flash([target]);

		if (!_hasGainedMaxHpThisCombat)
		{
			_hasGainedMaxHpThisCombat = true;
			await CreatureCmd.GainMaxHp(owner.Creature, FirstTriggerMaxHpGain);
		}

		if (bonusDamage > 0 && target.IsAlive)
		{
			await CreatureCmd.Damage(
				choiceContext,
				target,
				bonusDamage,
				ValueProp.Unpowered | ValueProp.SkipHurtAnim,
				owner.Creature,
				cardSource: null);
		}

		await CreatureCmd.Heal(owner.Creature, healAmount, playAnim: true);
	}

	private void ResolveSavedLastTriggeredCard()
	{
		if (_lastTriggeredCard != null || _savedLastTriggeredCard == null)
		{
			return;
		}

		_lastTriggeredCard = TryFindOwnedCombatCard(_savedLastTriggeredCard);
		if (_lastTriggeredCard != null)
		{
			_savedLastTriggeredCard = null;
		}
	}

	private void ResetTracking()
	{
		_cardsPlayedTowardCharge = 0;
		_charges = 0;
		_lastTriggeredCard = null;
		_savedLastTriggeredCard = null;
		_hasGainedMaxHpThisCombat = false;
		RefreshVisualState();
	}

	private void RefreshVisualState()
	{
		Status = _charges > 0 ? RelicStatus.Active : RelicStatus.Normal;
		InvokeDisplayAmountChanged();
	}
}

public sealed class Keystone_ConquerorTemporaryStrengthPower : TemporaryStrengthPower
{
	public override AbstractModel OriginModel => ModelDb.Relic<Keystone_ConquerorRune>();

	protected override bool IsVisibleInternal => false;
}

public sealed class Keystone_ConquerorRune : Keystone_RelicBase
{
	private const int AttacksPerStrength = 2;

	private const int MaxStrengthPerTurn = 3;

	private const int HealPerAttack = 1;

	private int _attacksPlayedThisTurn;

	private int _strengthGrantedThisTurn;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedAttacksPlayedThisTurn
	{
		get => _attacksPlayedThisTurn;
		set => _attacksPlayedThisTurn = Math.Max(0, value);
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedStrengthGrantedThisTurn
	{
		get => _strengthGrantedThisTurn;
		set
		{
			_strengthGrantedThisTurn = Math.Clamp(value, 0, MaxStrengthPerTurn);
			RefreshVisualState();
		}
	}

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("AttacksPerStrength", AttacksPerStrength),
		new DynamicVar("MaxStrength", MaxStrengthPerTurn),
		new DynamicVar("HealPerAttack", HealPerAttack)
	];

	public override bool ShowCounter => CombatManager.Instance?.IsInProgress == true;

	public override int DisplayAmount => !IsCanonical ? _strengthGrantedThisTurn : 0;

	protected override string GetIconPath() => ModInfo.ConquerorIconPath;

	public override Task BeforeCombatStart()
	{
		ResetTurnTracking();
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		ResetTurnTracking();
		return Task.CompletedTask;
	}

	public override Task AfterSideTurnStart(CombatSide side, TurnCombatState combatState)
	{
		if (side == CombatSide.Player)
		{
			ResetTurnTracking();
		}

		return Task.CompletedTask;
	}

	public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		if (!IsOwnedAttack(cardPlay.Card) || Owner?.Creature.CombatState?.CurrentSide != CombatSide.Player)
		{
			return;
		}

		bool wasAtStrengthCap = _strengthGrantedThisTurn >= MaxStrengthPerTurn;
		_attacksPlayedThisTurn++;
		int targetStrength = Math.Min(_attacksPlayedThisTurn / AttacksPerStrength, MaxStrengthPerTurn);
		while (_strengthGrantedThisTurn < targetStrength)
		{
			_strengthGrantedThisTurn++;
			await Sts2Compat.ApplyPower<Keystone_ConquerorTemporaryStrengthPower>(Owner!.Creature, 1m, Owner.Creature, cardPlay.Card);
			Flash(Array.Empty<Creature>());
		}

		if (wasAtStrengthCap)
		{
			await CreatureCmd.Heal(Owner!.Creature, HealPerAttack, playAnim: true);
		}

		RefreshVisualState();
	}

	public override Task AfterTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)
	{
		if (side != CombatSide.Player)
		{
			return Task.CompletedTask;
		}

		ResetTurnTracking();
		return Task.CompletedTask;
	}

	private void ResetTurnTracking()
	{
		_attacksPlayedThisTurn = 0;
		_strengthGrantedThisTurn = 0;
		RefreshVisualState();
	}

	private void RefreshVisualState()
	{
		Status = _strengthGrantedThisTurn >= MaxStrengthPerTurn ? RelicStatus.Active : RelicStatus.Normal;
		InvokeDisplayAmountChanged();
	}
}

public sealed class Keystone_SummonAeryRune : Keystone_RelicBase
{
	private const int CardsPerCharge = 3;

	private int _cardsPlayedTowardCharge;

	private int _charges;

	private CardModel? _lastTriggeredCard;

	private bool _isGrantingBonusBlock;

	private bool _isDealingBonusDamage;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedCardsPlayedTowardCharge
	{
		get => _cardsPlayedTowardCharge;
		set => _cardsPlayedTowardCharge = Math.Max(0, value);
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedCharges
	{
		get => _charges;
		set
		{
			_charges = Math.Clamp(value, 0, 1);
			RefreshVisualState();
		}
	}

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("CardsPerCharge", CardsPerCharge),
		new DynamicVar("ActBonus", 1m)
	];

	public override bool ShowCounter => CombatManager.Instance?.IsInProgress == true;

	public override int DisplayAmount => !IsCanonical ? (_charges > 0 ? CardsPerCharge : _cardsPlayedTowardCharge) : 0;

	protected override string GetIconPath() => ModInfo.AeryIconPath;

	public override Task BeforeCombatStart()
	{
		ResetCombatTracking();
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		ResetCombatTracking();
		return Task.CompletedTask;
	}

	public override Task AfterSideTurnStart(CombatSide side, TurnCombatState combatState)
	{
		if (side == CombatSide.Player)
		{
			RefillCharges();
		}

		return Task.CompletedTask;
	}

	public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		if (ReferenceEquals(cardPlay.Card, _lastTriggeredCard))
		{
			_lastTriggeredCard = null;
			return Task.CompletedTask;
		}

		if (!IsOwnedCard(cardPlay.Card))
		{
			return Task.CompletedTask;
		}

		if (_charges > 0)
		{
			return Task.CompletedTask;
		}

		_cardsPlayedTowardCharge++;
		if (_cardsPlayedTowardCharge >= CardsPerCharge)
		{
			_cardsPlayedTowardCharge = 0;
			_charges = 1;
			Flash(Array.Empty<Creature>());
		}

		RefreshVisualState();
		return Task.CompletedTask;
	}

	public override async Task AfterBlockGained(Creature creature, decimal amount, ValueProp props, CardModel? cardSource)
	{
		if (_isGrantingBonusBlock || _charges <= 0 || creature != Owner?.Creature || amount <= 0m)
		{
			return;
		}

		ConsumeCharge(cardSource);
		RefreshVisualState();

		int bonus = GetCurrentActBonus();
		if (bonus <= 0)
		{
			return;
		}

		_isGrantingBonusBlock = true;
		try
		{
			Flash([creature]);
			await CreatureCmd.GainBlock(creature, bonus, ValueProp.Unpowered, cardPlay: null, fast: false);
		}
		finally
		{
			_isGrantingBonusBlock = false;
		}
	}

	public override async Task AfterDamageGiven(
		PlayerChoiceContext choiceContext,
		Creature? dealer,
		DamageResult result,
		ValueProp props,
		Creature target,
		CardModel? cardSource)
	{
		if (_isDealingBonusDamage
			|| _charges <= 0
			|| dealer != Owner?.Creature
			|| target.Side != CombatSide.Enemy
			|| props.HasFlag(ValueProp.Unpowered)
			|| result.TotalDamage <= 0)
		{
			return;
		}

		ConsumeCharge(cardSource);
		RefreshVisualState();

		int bonus = GetCurrentActBonus();
		if (bonus <= 0 || !target.IsAlive)
		{
			return;
		}

		_isDealingBonusDamage = true;
		try
		{
			Flash([target]);
			await CreatureCmd.Damage(
				choiceContext,
				target,
				bonus,
				ValueProp.Unpowered | ValueProp.SkipHurtAnim,
				Owner!.Creature,
				cardSource: null);
		}
		finally
		{
			_isDealingBonusDamage = false;
		}
	}

	private void ResetCombatTracking()
	{
		_cardsPlayedTowardCharge = 0;
		_charges = 0;
		_lastTriggeredCard = null;
		RefreshVisualState();
	}

	private void RefillCharges()
	{
		_cardsPlayedTowardCharge = 0;
		_charges = 1;
		_lastTriggeredCard = null;
		RefreshVisualState();
	}

	private void ConsumeCharge(CardModel? cardSource)
	{
		_lastTriggeredCard = cardSource;
		_charges = 0;
		_cardsPlayedTowardCharge = IsOwnedCard(cardSource) ? 1 : 0;
	}

	private void RefreshVisualState()
	{
		Status = _charges > 0 ? RelicStatus.Active : RelicStatus.Normal;
		InvokeDisplayAmountChanged();
	}
}

public sealed class Keystone_LethalTempoRune : Keystone_RelicBase
{
	private const int StartingAttacksRequired = 3;

	private const int MinimumAttacksRequired = 1;

	private const int CardsDrawn = 1;

	private int _attacksPlayedTowardDraw;

	private int _attacksRequiredForDraw = StartingAttacksRequired;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("StartingAttacks", StartingAttacksRequired),
		new DynamicVar("MinimumAttacks", MinimumAttacksRequired),
		new DynamicVar("CardsDrawn", CardsDrawn)
	];

	public override bool ShowCounter => CombatManager.Instance?.IsInProgress == true && !IsCanonical;

	public override int DisplayAmount => !IsCanonical ? Math.Max(0, _attacksRequiredForDraw - _attacksPlayedTowardDraw) : 0;

	protected override string GetIconPath() => ModInfo.LethalTempoIconPath;

	public override Task BeforeCombatStart()
	{
		ResetTurnTracking();
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		ResetTurnTracking();
		return Task.CompletedTask;
	}

	public override Task AfterSideTurnStart(CombatSide side, TurnCombatState combatState)
	{
		if (side == CombatSide.Player)
		{
			ResetTurnTracking();
		}

		return Task.CompletedTask;
	}

	public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		if (!IsOwnedAttack(cardPlay.Card) || Owner?.Creature.CombatState?.CurrentSide != CombatSide.Player)
		{
			return;
		}

		_attacksPlayedTowardDraw++;
		if (_attacksPlayedTowardDraw < _attacksRequiredForDraw)
		{
			RefreshVisualState();
			return;
		}

		_attacksPlayedTowardDraw = 0;
		_attacksRequiredForDraw = Math.Max(MinimumAttacksRequired, _attacksRequiredForDraw - 1);
		RefreshVisualState();

		if (Owner == null)
		{
			return;
		}

		Flash(Array.Empty<Creature>());
		await CardPileCmd.Draw(new BlockingPlayerChoiceContext(), CardsDrawn, Owner, fromHandDraw: false);
	}

	private void ResetTurnTracking()
	{
		_attacksPlayedTowardDraw = 0;
		_attacksRequiredForDraw = StartingAttacksRequired;
		RefreshVisualState();
	}

	private void RefreshVisualState()
	{
		Status = _attacksRequiredForDraw - _attacksPlayedTowardDraw <= 1 ? RelicStatus.Active : RelicStatus.Normal;
		InvokeDisplayAmountChanged();
	}
}

public sealed class Keystone_PhaseRushRune : Keystone_RelicBase
{
	private CardType _lastPlayedType = CardType.None;

	private int _sameTypeStreakThisTurn;

	private bool _triggeredThisTurn;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public CardType SavedLastPlayedType
	{
		get => _lastPlayedType;
		set => _lastPlayedType = value;
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedSameTypeStreakThisTurn
	{
		get => _sameTypeStreakThisTurn;
		set
		{
			_sameTypeStreakThisTurn = Math.Max(0, value);
			InvokeDisplayAmountChanged();
		}
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public bool SavedTriggeredThisTurn
	{
		get => _triggeredThisTurn;
		set
		{
			_triggeredThisTurn = value;
			RefreshVisualState();
		}
	}

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("Cards", 3m),
		new EnergyVar(1),
		new DynamicVar("Draw", 1m)
	];

	public override bool ShowCounter => CombatManager.Instance?.IsInProgress == true;

	public override int DisplayAmount => !IsCanonical ? _sameTypeStreakThisTurn : 0;

	protected override string GetIconPath() => ModInfo.PhaseRushIconPath;

	public override Task BeforeCombatStart()
	{
		ResetTurnTracking();
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		ResetTurnTracking();
		return Task.CompletedTask;
	}

	public override Task AfterSideTurnStart(CombatSide side, TurnCombatState combatState)
	{
		if (side == CombatSide.Player)
		{
			ResetTurnTracking();
		}

		return Task.CompletedTask;
	}

	public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		if (!IsOwnedCard(cardPlay.Card))
		{
			return;
		}

		if (_lastPlayedType == cardPlay.Card.Type)
		{
			_sameTypeStreakThisTurn++;
		}
		else
		{
			_lastPlayedType = cardPlay.Card.Type;
			_sameTypeStreakThisTurn = 1;
		}

		if (_triggeredThisTurn || _sameTypeStreakThisTurn < 3)
		{
			RefreshVisualState();
			return;
		}

		_triggeredThisTurn = true;
		RefreshVisualState();
		Flash(Array.Empty<Creature>());
		await PlayerCmd.GainEnergy(1m, Owner!);
		await CardPileCmd.Draw(new BlockingPlayerChoiceContext(), 1m, Owner!, fromHandDraw: false);
	}

	private void ResetTurnTracking()
	{
		_lastPlayedType = CardType.None;
		_sameTypeStreakThisTurn = 0;
		_triggeredThisTurn = false;
		RefreshVisualState();
	}

	private void RefreshVisualState()
	{
		Status = !_triggeredThisTurn ? RelicStatus.Active : RelicStatus.Normal;
		InvokeDisplayAmountChanged();
	}
}

public sealed class Keystone_UnsealedSpellbookRune : Keystone_RelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("Options", 3m)
	];

	protected override string GetIconPath() => ModInfo.UnsealedSpellbookIconPath;

	public override async Task BeforeCombatStart()
	{
		if (Owner == null)
		{
			return;
		}

		List<CardModel> options = BuildSpellbookOptions();
		if (options.Count == 0)
		{
			return;
		}

		CardModel? selectedCard = await CardSelectCmd.FromChooseACardScreen(
			new BlockingPlayerChoiceContext(),
			options,
			Owner,
			canSkip: false);
		if (selectedCard == null)
		{
			return;
		}

		Flash(Array.Empty<Creature>());
		selectedCard.SetToFreeThisCombat();
		await Sts2Compat.AddGeneratedCardToCombat(selectedCard, PileType.Hand, Owner!, CardPilePosition.Bottom);
	}

	private List<CardModel> BuildSpellbookOptions()
	{
		if (Owner == null)
		{
			return new List<CardModel>();
		}

		return CardFactory.GetDistinctForCombat(
				Owner,
				from c in Owner.Character.CardPool.GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint)
				where c.Type == CardType.Power
				select c,
				3,
				Owner.RunState.Rng.CombatCardGeneration)
			.ToList();
	}
}

public sealed class Keystone_HailOfBladesRune : Keystone_RelicBase
{
	private const int MaxBuffedAttacksPerTurn = 3;

	private int _buffedAttacksThisTurn;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedBuffedAttacksThisTurn
	{
		get => _buffedAttacksThisTurn;
		set => _buffedAttacksThisTurn = Math.Max(0, value);
	}

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("Hits", MaxBuffedAttacksPerTurn)
	];

	protected override string GetIconPath() => ModInfo.HailOfBladesIconPath;

	public override Task BeforeCombatStart()
	{
		ResetTurnTracking();
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		ResetTurnTracking();
		return Task.CompletedTask;
	}

	public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		if (!cardPlay.IsFirstInSeries || !IsOwnedAttack(cardPlay.Card) || _buffedAttacksThisTurn >= MaxBuffedAttacksPerTurn)
		{
			return Task.CompletedTask;
		}

		_buffedAttacksThisTurn++;
		Flash(Array.Empty<Creature>());
		return Task.CompletedTask;
	}

	public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
	{
		modifiedCost = originalCost;
		if (!ShouldDiscount(card) || originalCost <= 0m)
		{
			return false;
		}

		decimal discountedCost = Math.Max(0m, originalCost - 1m);
		if (discountedCost == originalCost)
		{
			return false;
		}

		modifiedCost = discountedCost;
		return true;
	}

	private bool ShouldDiscount(CardModel card)
	{
		return _buffedAttacksThisTurn < MaxBuffedAttacksPerTurn
			&& IsOwnedAttack(card)
			&& Owner?.Creature.CombatState?.CurrentSide == CombatSide.Player;
	}

	private void ResetTurnTracking()
	{
		_buffedAttacksThisTurn = 0;
	}
}

public sealed class Keystone_FleetFootworkRune : Keystone_RelicBase
{
	private const int CardsPerCharge = 6;

	private int _cardsPlayedTowardCharge;

	private int _charges;

	private CardModel? _pendingChargedCard;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedCardsPlayedTowardCharge
	{
		get => _cardsPlayedTowardCharge;
		set => _cardsPlayedTowardCharge = Math.Max(0, value);
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedCharges
	{
		get => _charges;
		set
		{
			_charges = Math.Max(0, value);
			RefreshVisualState();
		}
	}

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("CardsPerCharge", CardsPerCharge),
		new EnergyVar(1)
	];

	public override bool ShowCounter => CombatManager.Instance?.IsInProgress == true;

	public override int DisplayAmount
	{
		get
		{
			if (IsCanonical)
			{
				return 0;
			}

			return _charges > 0 ? CardsPerCharge : _cardsPlayedTowardCharge;
		}
	}

	protected override string GetIconPath() => ModInfo.FleetFootworkIconPath;

	public override Task BeforeCombatStart()
	{
		ResetTracking();
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		ResetTracking();
		return Task.CompletedTask;
	}

	public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount)
	{
		if (_charges <= 0 || !IsOwnedCard(card))
		{
			return playCount;
		}

		_pendingChargedCard ??= card;
		if (!ReferenceEquals(_pendingChargedCard, card))
		{
			return playCount;
		}

		return playCount + 1;
	}

	public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		if (ReferenceEquals(cardPlay.Card, _pendingChargedCard) && cardPlay.IsFirstInSeries)
		{
			_charges--;
			_pendingChargedCard = null;
			_cardsPlayedTowardCharge = 1;
			RefreshVisualState();
			Flash(Array.Empty<Creature>());
			await PlayerCmd.GainEnergy(1m, Owner!);
			return;
		}

		if (!IsOwnedCard(cardPlay.Card) || _charges > 0)
		{
			return;
		}

		_cardsPlayedTowardCharge++;
		if (_cardsPlayedTowardCharge >= CardsPerCharge)
		{
			_cardsPlayedTowardCharge -= CardsPerCharge;
			_charges++;
			Flash(Array.Empty<Creature>());
		}

		RefreshVisualState();
	}

	private void ResetTracking()
	{
		_cardsPlayedTowardCharge = 0;
		_charges = 0;
		_pendingChargedCard = null;
		RefreshVisualState();
	}

	private void RefreshVisualState()
	{
		Status = _charges > 0 ? RelicStatus.Active : RelicStatus.Normal;
		InvokeDisplayAmountChanged();
	}
}

public sealed class Keystone_ArcaneCometRune : Keystone_RelicBase
{
	private bool _triggeredThisTurn;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public bool SavedTriggeredThisTurn
	{
		get => _triggeredThisTurn;
		set => _triggeredThisTurn = value;
	}

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("DamagePerRelic", 2m)
	];

	protected override string GetIconPath() => ModInfo.ArcaneCometIconPath;

	public override Task BeforeCombatStart()
	{
		ResetTurnTracking();
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		ResetTurnTracking();
		return Task.CompletedTask;
	}

	public override Task AfterSideTurnStart(CombatSide side, TurnCombatState combatState)
	{
		if (side == CombatSide.Player)
		{
			ResetTurnTracking();
		}

		return Task.CompletedTask;
	}

	public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		if (_triggeredThisTurn
			|| !cardPlay.IsFirstInSeries
			|| !IsOwnedCard(cardPlay.Card)
			|| cardPlay.Target is not { Side: CombatSide.Enemy } target
			|| cardPlay.Card.TargetType != TargetType.AnyEnemy)
		{
			return;
		}

		_triggeredThisTurn = true;
		int damage = (Owner?.Relics.Count ?? 0) * 2;
		Flash([target]);
		await CreatureCmd.Damage(
			context,
			target,
			damage,
			ValueProp.Unpowered | ValueProp.SkipHurtAnim,
			Owner!.Creature,
			cardSource: null);
	}

	private void ResetTurnTracking()
	{
		_triggeredThisTurn = false;
	}
}

public abstract class Keystone_PowerBase : PowerModel
{
#if STS2_106_OR_NEWER
	public virtual Task AfterSideTurnStart(CombatSide side, TurnCombatState combatState)
	{
		return Task.CompletedTask;
	}

	public sealed override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, TurnCombatState combatState)
	{
		return AfterSideTurnStart(side, combatState);
	}
#endif
}

public sealed class Keystone_TemporarySlowPower : Keystone_PowerBase, ITemporaryPower
{
	private bool _shouldIgnoreNextInstance;

	public override PowerType Type => PowerType.Debuff;

	public override PowerStackType StackType => PowerStackType.Counter;

	protected override bool IsVisibleInternal => false;

	public AbstractModel OriginModel => ModelDb.Relic<Keystone_GlacialAugmentRune>();

	public PowerModel InternallyAppliedPower => ModelDb.Power<SlowPower>();

	public override LocString Title => ModelDb.Power<SlowPower>().Title;

	public override LocString Description => ModelDb.Power<SlowPower>().Description;

	public void IgnoreNextInstance()
	{
		_shouldIgnoreNextInstance = true;
	}

	public override async Task BeforeApplied(Creature target, decimal amount, Creature? applier, CardModel? cardSource)
	{
		if (_shouldIgnoreNextInstance)
		{
			_shouldIgnoreNextInstance = false;
		}
		else
		{
			await Sts2Compat.ApplyPower<SlowPower>(target, amount, applier, cardSource, silent: true);
		}
	}

#if STS2_104_OR_NEWER
	public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
#else
	public override async Task AfterPowerAmountChanged(PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
#endif
	{
		if (power == this && amount != Amount)
		{
			if (_shouldIgnoreNextInstance)
			{
				_shouldIgnoreNextInstance = false;
			}
			else
			{
				await Sts2Compat.ApplyPower<SlowPower>(Owner, amount, applier, cardSource, silent: true);
			}
		}
	}

	public override async Task AfterSideTurnStart(CombatSide side, TurnCombatState combatState)
	{
		if (side != Owner.Side)
		{
			return;
		}

		await PowerCmd.Remove(this);
		await Sts2Compat.ApplyPower<SlowPower>(Owner, -Amount, Owner, null, silent: true);
	}
}

public sealed class Keystone_GlacialAugmentRune : Keystone_RelicBase
{
	private bool _triggeredThisTurn;

	private bool _isApplyingBonusDebuff;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public bool SavedTriggeredThisTurn
	{
		get => _triggeredThisTurn;
		set => _triggeredThisTurn = value;
	}

	protected override string GetIconPath() => ModInfo.GlacialAugmentIconPath;

	public override Task BeforeCombatStart()
	{
		ResetTurnTracking();
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		ResetTurnTracking();
		return Task.CompletedTask;
	}

	public override Task AfterSideTurnStart(CombatSide side, TurnCombatState combatState)
	{
		if (side == CombatSide.Player)
		{
			ResetTurnTracking();
		}

		return Task.CompletedTask;
	}

#if STS2_104_OR_NEWER
	public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
#else
	public override async Task AfterPowerAmountChanged(PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
#endif
	{
		if (_isApplyingBonusDebuff
			|| _triggeredThisTurn
			|| !TryGetOwnedEnemyDebuffTarget(power, amount, applier, out Creature? target))
		{
			return;
		}

		_triggeredThisTurn = true;
		_isApplyingBonusDebuff = true;
		try
		{
			Flash([target!]);
			await Sts2Compat.ApplyPower<WeakPower>(target!, 1m, Owner!.Creature, cardSource);
			await Sts2Compat.ApplyPower<Keystone_TemporarySlowPower>(target!, 1m, Owner!.Creature, cardSource);
		}
		finally
		{
			_isApplyingBonusDebuff = false;
		}
	}

	private void ResetTurnTracking()
	{
		_triggeredThisTurn = false;
	}
}

public sealed class Keystone_AftershockRune : Keystone_RelicBase
{
	private const int BlockMultiplier = 4;

	private const decimal MaxHpDamageRatio = 0.10m;

	private bool _triggeredThisTurn;

	private bool _shockDamagePending;

	private int _dexterityGrantedThisTurn;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public bool SavedTriggeredThisTurn
	{
		get => _triggeredThisTurn;
		set
		{
			_triggeredThisTurn = value;
			RefreshVisualState();
		}
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedDexterityGrantedThisTurn
	{
		get => _dexterityGrantedThisTurn;
		set => _dexterityGrantedThisTurn = Math.Max(0, value);
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public bool SavedShockDamagePending
	{
		get => _shockDamagePending;
		set => _shockDamagePending = value;
	}

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("ActBonus", 1m),
		new DynamicVar("BlockMultiplier", BlockMultiplier),
		new DynamicVar("MaxHpDamagePercent", MaxHpDamageRatio * 100m)
	];

	protected override string GetIconPath() => ModInfo.AftershockIconPath;

	public override bool ShowCounter => CombatManager.Instance?.IsInProgress == true;

	public override int DisplayAmount => !IsCanonical ? GetCurrentActBonus() : 0;

	public override Task BeforeCombatStart()
	{
		ResetTracking();
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		ResetTracking();
		return Task.CompletedTask;
	}

	public override Task AfterSideTurnStart(CombatSide side, TurnCombatState combatState)
	{
		if (side == CombatSide.Player)
		{
			_triggeredThisTurn = false;
			RefreshVisualState();
		}

		return Task.CompletedTask;
	}

	public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (player != Owner || !_shockDamagePending)
		{
			return;
		}

		await DealPendingShockDamage(choiceContext);
	}

#if STS2_104_OR_NEWER
	public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
#else
	public override async Task AfterPowerAmountChanged(PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
#endif
	{
		if (_triggeredThisTurn
			|| !TryGetOwnedEnemyDebuffTarget(power, amount, applier, out Creature? target))
		{
			return;
		}

		_triggeredThisTurn = true;
		_shockDamagePending = true;
		RefreshVisualState();

		Player owner = Owner!;
		int bonus = GetCurrentActBonus();
		if (bonus <= 0)
		{
			return;
		}

		Flash([target!]);
		await CreatureCmd.GainBlock(owner.Creature, bonus * BlockMultiplier, ValueProp.Unpowered, cardPlay: null, fast: false);
		await Sts2Compat.ApplyPower<DexterityPower>(owner.Creature, bonus, owner.Creature, cardSource);
		_dexterityGrantedThisTurn += bonus;
	}

	public override async Task AfterTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)
	{
		if (side != CombatSide.Player || Owner?.Creature == null || _dexterityGrantedThisTurn <= 0)
		{
			return;
		}

		int currentDexterity = Owner.Creature.GetPowerAmount<DexterityPower>();
		int updatedDexterity = Math.Max(0, currentDexterity - _dexterityGrantedThisTurn);
		await Sts2Compat.SetPowerAmount<DexterityPower>(Owner.Creature, updatedDexterity, Owner.Creature, null);
		_dexterityGrantedThisTurn = 0;
	}

	private void ResetTracking()
	{
		_triggeredThisTurn = false;
		_shockDamagePending = false;
		_dexterityGrantedThisTurn = 0;
		RefreshVisualState();
	}

	private async Task DealPendingShockDamage(PlayerChoiceContext choiceContext)
	{
		Creature? ownerCreature = Owner?.Creature;
		if (ownerCreature?.CombatState == null)
		{
			_shockDamagePending = false;
			return;
		}

		List<Creature> enemies = ownerCreature.CombatState.HittableEnemies
			.Where(static enemy => enemy.IsAlive)
			.ToList();
		if (enemies.Count == 0)
		{
			_shockDamagePending = false;
			return;
		}

		int damage = Math.Max(1, RoundToInt((decimal)ownerCreature.MaxHp * MaxHpDamageRatio));
		_shockDamagePending = false;
		Flash(enemies);
		foreach (Creature enemy in enemies)
		{
			await CreatureCmd.Damage(
				choiceContext,
				enemy,
				damage,
				ValueProp.Unpowered | ValueProp.SkipHurtAnim,
				ownerCreature,
				cardSource: null);
		}
	}

	private void RefreshVisualState()
	{
		Status = !_triggeredThisTurn ? RelicStatus.Active : RelicStatus.Normal;
		InvokeDisplayAmountChanged();
	}
}

public sealed class Keystone_GuardianRune : Keystone_RelicBase
{
	private bool _triggeredThisTurn;

	private bool _isGrantingGuardianBlock;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public bool SavedTriggeredThisTurn
	{
		get => _triggeredThisTurn;
		set
		{
			_triggeredThisTurn = value;
			RefreshVisualState();
		}
	}

	protected override string GetIconPath() => ModInfo.GuardianIconPath;

	public override Task BeforeCombatStart()
	{
		ResetTracking();
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		ResetTracking();
		return Task.CompletedTask;
	}

	public override Task AfterSideTurnStart(CombatSide side, TurnCombatState combatState)
	{
		if (side == CombatSide.Player)
		{
			_triggeredThisTurn = false;
			RefreshVisualState();
		}

		return Task.CompletedTask;
	}

	public override async Task AfterBlockGained(Creature creature, decimal amount, ValueProp props, CardModel? cardSource)
	{
		Creature? ownerCreature = Owner?.Creature;
		if (_isGrantingGuardianBlock
			|| _triggeredThisTurn
			|| creature != ownerCreature
			|| amount <= 0m
			|| cardSource == null
			|| ownerCreature?.CombatState == null
			|| ownerCreature.CombatState.CurrentSide != ownerCreature.Side)
		{
			return;
		}

		_triggeredThisTurn = true;
		RefreshVisualState();

		List<Creature> teammates = ownerCreature.CombatState
			.GetTeammatesOf(ownerCreature)
			.Where(static c => c != null && c.IsAlive && c.IsPlayer)
			.Where(c => c != ownerCreature)
			.ToList();

		_isGrantingGuardianBlock = true;
		try
		{
			Flash([creature]);
			if (teammates.Count > 0)
			{
				foreach (Creature teammate in teammates)
				{
					await CreatureCmd.GainBlock(teammate, amount, ValueProp.Unpowered, null);
				}
			}
			else
			{
				await CreatureCmd.GainBlock(creature, amount, ValueProp.Unpowered, null);
			}
		}
		finally
		{
			_isGrantingGuardianBlock = false;
		}
	}

	private void ResetTracking()
	{
		_triggeredThisTurn = false;
		RefreshVisualState();
	}

	private void RefreshVisualState()
	{
		Status = !_triggeredThisTurn ? RelicStatus.Active : RelicStatus.Normal;
		InvokeDisplayAmountChanged();
	}
}

public sealed class Keystone_DarkHarvestRune : Keystone_RelicBase
{
	private const int BonusDamageMultiplier = 2;

	private int _souls;

	private bool _usedBonusThisTurn;

	private bool _isApplyingHarvestBonus;

	private HashSet<uint>? _harvestedTargetCombatIds;

	private string _savedHarvestedTargetCombatIds = string.Empty;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedSouls
	{
		get => _souls;
		set
		{
			_souls = Math.Max(0, value);
			InvokeDisplayAmountChanged();
		}
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public bool SavedUsedBonusThisTurn
	{
		get => _usedBonusThisTurn;
		set => _usedBonusThisTurn = value;
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public string SavedHarvestedTargetCombatIds
	{
		get => _savedHarvestedTargetCombatIds;
		set
		{
			_savedHarvestedTargetCombatIds = value ?? string.Empty;
			_harvestedTargetCombatIds = DeserializeCombatIdSet(_savedHarvestedTargetCombatIds);
		}
	}

	public override bool ShowCounter => !IsCanonical;

	public override int DisplayAmount => !IsCanonical ? _souls : 0;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("DamageMultiplier", BonusDamageMultiplier)
	];

	protected override string GetIconPath() => ModInfo.DarkHarvestIconPath;

	private HashSet<uint> HarvestedTargetCombatIds => _harvestedTargetCombatIds ??= DeserializeCombatIdSet(_savedHarvestedTargetCombatIds);

	public override Task BeforeCombatStart()
	{
		ResetCombatTracking();
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		ResetCombatTracking();
		return Task.CompletedTask;
	}

	public override Task AfterSideTurnStart(CombatSide side, TurnCombatState combatState)
	{
		if (side == CombatSide.Player)
		{
			ResetTurnTracking();
		}

		return Task.CompletedTask;
	}

	public override async Task AfterDamageGiven(
		PlayerChoiceContext choiceContext,
		Creature? dealer,
		DamageResult result,
		ValueProp props,
		Creature target,
		CardModel? cardSource)
	{
		if (dealer == Owner?.Creature && target.Side == CombatSide.Enemy && result.WasTargetKilled)
		{
			_souls++;
			InvokeDisplayAmountChanged();
			Flash([target]);
		}

		if (_isApplyingHarvestBonus
			|| _usedBonusThisTurn
			|| _souls <= 0
			|| dealer != Owner?.Creature
			|| target.Side != CombatSide.Enemy
			|| !IsOwnedAttack(cardSource)
			|| props.HasFlag(ValueProp.Unpowered)
			|| !target.CombatId.HasValue
			|| result.TotalDamage <= 0
			|| !target.IsAlive
			|| !WasTargetBelowHalfHpBeforeDamage(target, result))
		{
			return;
		}

		uint targetCombatId = target.CombatId.Value;
		if (HarvestedTargetCombatIds.Contains(targetCombatId))
		{
			return;
		}

		HarvestedTargetCombatIds.Add(targetCombatId);
		SyncHarvestedTargetCombatIds();
		_usedBonusThisTurn = true;
		_isApplyingHarvestBonus = true;
		try
		{
			Flash([target]);
			await CreatureCmd.Damage(
				choiceContext,
				target,
				_souls * BonusDamageMultiplier,
				ValueProp.Unpowered | ValueProp.SkipHurtAnim,
				Owner!.Creature,
				cardSource: null);
		}
		finally
		{
			_isApplyingHarvestBonus = false;
		}
	}

	private void ResetTurnTracking()
	{
		_usedBonusThisTurn = false;
	}

	private void ResetCombatTracking()
	{
		ResetTurnTracking();
		HarvestedTargetCombatIds.Clear();
		SyncHarvestedTargetCombatIds();
	}

	private void SyncHarvestedTargetCombatIds()
	{
		if (HarvestedTargetCombatIds.Count == 0)
		{
			_savedHarvestedTargetCombatIds = string.Empty;
			return;
		}

		List<uint> orderedIds = [.. HarvestedTargetCombatIds];
		orderedIds.Sort();
		_savedHarvestedTargetCombatIds = string.Join(",", orderedIds);
	}

	private static bool WasTargetBelowHalfHpBeforeDamage(Creature target, DamageResult result)
	{
		int hpBeforeDamage = target.CurrentHp + Math.Max(0, result.UnblockedDamage);
		return hpBeforeDamage * 2 <= target.MaxHp;
	}

	private static HashSet<uint> DeserializeCombatIdSet(string? serialized)
	{
		HashSet<uint> ids = [];
		if (string.IsNullOrWhiteSpace(serialized))
		{
			return ids;
		}

		foreach (string part in serialized.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			if (uint.TryParse(part, out uint id))
			{
				ids.Add(id);
			}
		}

		return ids;
	}
}
