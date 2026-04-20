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

namespace KeystoneRunes;

public abstract class KeystoneRelicBase : RelicModel
{
	public sealed override RelicRarity Rarity => RelicRarity.Starter;

	public override string PackedIconPath => GetIconPath();

	protected override string PackedIconOutlinePath => PackedIconPath;

	protected override string BigIconPath => PackedIconPath;

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
}

public sealed class ElectrocuteRune : KeystoneRelicBase
{
	private const int RequiredHits = 3;

	private const int BaseDamage = 5;

	private const decimal CurrentHpRatio = 0.10m;

	private int _consecutiveHitsThisTurn;

	private uint? _trackedTargetCombatId;

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
	public uint? SavedTrackedTargetCombatId
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

	public override bool ShowCounter => false;

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

	public override Task AfterSideTurnStart(CombatSide side, CombatState combatState)
	{
		if (side == CombatSide.Player)
		{
			ResetTracking();
		}

		return Task.CompletedTask;
	}

	public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		if (!IsValidEnemyTargetedPlay(cardPlay))
		{
			ResetTracking();
			return;
		}

		Creature target = cardPlay.Target!;
		uint? targetCombatId = target.CombatId;
		if (_trackedTargetCombatId.HasValue && targetCombatId == _trackedTargetCombatId)
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
			context,
			target,
			bonusDamage,
			ValueProp.Unpowered | ValueProp.SkipHurtAnim,
			Owner!.Creature,
			cardSource: null);
	}

	private bool IsValidEnemyTargetedPlay(CardPlay cardPlay)
	{
		return cardPlay.Target is { Side: CombatSide.Enemy }
			&& cardPlay.Card.Owner == Owner
			&& cardPlay.Card.TargetType == TargetType.AnyEnemy;
	}

	private void ResetTracking()
	{
		_consecutiveHitsThisTurn = 0;
		_trackedTargetCombatId = null;
		RefreshVisualState();
	}

	private void RefreshVisualState()
	{
		Status = _consecutiveHitsThisTurn >= RequiredHits - 1 ? RelicStatus.Active : RelicStatus.Normal;
		InvokeDisplayAmountChanged();
	}
}

public sealed class FirstStrikeRune : KeystoneRelicBase
{
	private bool _hasDuplicatedFirstAttack;

	private int _firstTurnDamage;

	private CardModel? _trackedFirstAttackCard;

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
		if (_hasDuplicatedFirstAttack || !IsOwnedAttack(card) || target?.Side != CombatSide.Enemy)
		{
			return playCount;
		}

		_hasDuplicatedFirstAttack = true;
		_trackedFirstAttackCard = card;
		Status = RelicStatus.Active;
		return playCount + 1;
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
		if (owner == null || dealer != owner.Creature || target.Side != CombatSide.Enemy)
		{
			return Task.CompletedTask;
		}

		if (ReferenceEquals(cardSource, _trackedFirstAttackCard))
		{
			_firstTurnDamage += result.TotalDamage;
			InvokeDisplayAmountChanged();
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

	private void ResetTracking()
	{
		_hasDuplicatedFirstAttack = false;
		_firstTurnDamage = 0;
		_trackedFirstAttackCard = null;
		Status = RelicStatus.Normal;
		InvokeDisplayAmountChanged();
	}
}

public sealed class UndyingGraspRune : KeystoneRelicBase
{
	private const int CardsPerCharge = 4;

	private const decimal BonusDamageRatio = 0.04m;

	private const decimal HealRatio = 0.02m;

	private int _cardsPlayedTowardCharge;

	private int _charges;

	private CardModel? _lastTriggeredCard;

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
		new DynamicVar("BonusDamagePercent", BonusDamageRatio * 100m),
		new DynamicVar("HealPercent", HealRatio * 100m)
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
		if (ReferenceEquals(cardPlay.Card, _lastTriggeredCard))
		{
			_lastTriggeredCard = null;
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
		_cardsPlayedTowardCharge = 1;
		RefreshVisualState();

		Player owner = Owner!;
		int bonusDamage = RoundToInt((decimal)owner.Creature.MaxHp * BonusDamageRatio);
		int healAmount = Math.Max(1, RoundToInt((decimal)owner.Creature.MaxHp * HealRatio));
		Flash([target]);

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

	private void ResetTracking()
	{
		_cardsPlayedTowardCharge = 0;
		_charges = 0;
		_lastTriggeredCard = null;
		RefreshVisualState();
	}

	private void RefreshVisualState()
	{
		Status = _charges > 0 ? RelicStatus.Active : RelicStatus.Normal;
		InvokeDisplayAmountChanged();
	}
}

public sealed class ConquerorRune : KeystoneRelicBase
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

	public override Task AfterSideTurnStart(CombatSide side, CombatState combatState)
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

		_attacksPlayedThisTurn++;
		int targetStrength = Math.Min(_attacksPlayedThisTurn / AttacksPerStrength, MaxStrengthPerTurn);
		while (_strengthGrantedThisTurn < targetStrength)
		{
			_strengthGrantedThisTurn++;
			await PowerCmd.Apply<StrengthPower>(Owner!.Creature, 1m, Owner.Creature, cardPlay.Card);
			Flash(Array.Empty<Creature>());
		}

		if (_strengthGrantedThisTurn >= MaxStrengthPerTurn)
		{
			await CreatureCmd.Heal(Owner!.Creature, HealPerAttack, playAnim: true);
		}

		RefreshVisualState();
	}

	public override async Task AfterTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)
	{
		if (side != CombatSide.Player || Owner?.Creature == null || _strengthGrantedThisTurn <= 0)
		{
			return;
		}

		int currentStrength = Owner.Creature.GetPowerAmount<StrengthPower>();
		int updatedStrength = Math.Max(0, currentStrength - _strengthGrantedThisTurn);
		await PowerCmd.SetAmount<StrengthPower>(Owner.Creature, updatedStrength, Owner.Creature, null);
		ResetTurnTracking();
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

public sealed class SummonAeryRune : KeystoneRelicBase
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

	public override Task AfterSideTurnStart(CombatSide side, CombatState combatState)
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

public sealed class PressTheAttackRune : KeystoneRelicBase
{
	private Dictionary<uint, int>? _attackHitCountsThisTurn;

	private Dictionary<uint, int> AttackHitCountsThisTurn => _attackHitCountsThisTurn ??= new Dictionary<uint, int>();

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("Hits", 3m),
		new DynamicVar("DebuffAmount", 1m)
	];

	protected override string GetIconPath() => ModInfo.PressAttackIconPath;

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

	public override Task AfterSideTurnStart(CombatSide side, CombatState combatState)
	{
		if (side == CombatSide.Player)
		{
			ResetTurnTracking();
		}

		return Task.CompletedTask;
	}

	public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		if (!cardPlay.IsFirstInSeries
			|| !IsOwnedCard(cardPlay.Card)
			|| cardPlay.Target is not { Side: CombatSide.Enemy } target
			|| cardPlay.Card.TargetType != TargetType.AnyEnemy
			|| !target.CombatId.HasValue)
		{
			return;
		}

		uint targetId = target.CombatId.Value;
		int newCount = AttackHitCountsThisTurn.TryGetValue(targetId, out int count) ? count + 1 : 1;
		AttackHitCountsThisTurn[targetId] = newCount;

		if (newCount != 3 || !target.IsAlive)
		{
			return;
		}

		Flash([target]);
		await PowerCmd.Apply<WeakPower>(target, 1m, Owner!.Creature, cardPlay.Card);
		await PowerCmd.Apply<VulnerablePower>(target, 1m, Owner.Creature, cardPlay.Card);
	}

	private void ResetTurnTracking()
	{
		AttackHitCountsThisTurn.Clear();
	}
}

public sealed class PhaseRushRune : KeystoneRelicBase
{
	private CardType _lastPlayedType = CardType.None;

	private int _sameTypeStreakThisTurn;

	private bool _triggeredThisTurn;

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

	public override Task AfterSideTurnStart(CombatSide side, CombatState combatState)
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

public sealed class UnsealedSpellbookRune : KeystoneRelicBase
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
		await CardPileCmd.AddGeneratedCardToCombat(selectedCard, PileType.Hand, addedByPlayer: true, position: CardPilePosition.Bottom);
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

public sealed class HailOfBladesRune : KeystoneRelicBase
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
		cardPlay.Card.EnergyCost.AddThisCombat(-1, reduceOnly: true);
		Flash(Array.Empty<Creature>());
		return Task.CompletedTask;
	}

	private void ResetTurnTracking()
	{
		_buffedAttacksThisTurn = 0;
	}
}

public sealed class FleetFootworkRune : KeystoneRelicBase
{
	private const int CardsPerCharge = 6;

	private int _cardsPlayedTowardCharge;

	private int _charges;

	private CardModel? _pendingChargedAttack;

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
		if (_charges <= 0 || !IsOwnedAttack(card) || target?.Side != CombatSide.Enemy)
		{
			return playCount;
		}

		_pendingChargedAttack ??= card;
		if (!ReferenceEquals(_pendingChargedAttack, card))
		{
			return playCount;
		}

		return playCount + 1;
	}

	public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		if (ReferenceEquals(cardPlay.Card, _pendingChargedAttack) && cardPlay.IsFirstInSeries)
		{
			_charges--;
			_pendingChargedAttack = null;
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
		_pendingChargedAttack = null;
		RefreshVisualState();
	}

	private void RefreshVisualState()
	{
		Status = _charges > 0 ? RelicStatus.Active : RelicStatus.Normal;
		InvokeDisplayAmountChanged();
	}
}

public sealed class ArcaneCometRune : KeystoneRelicBase
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

	public override Task AfterSideTurnStart(CombatSide side, CombatState combatState)
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

public sealed class TemporarySlowPower : PowerModel, ITemporaryPower
{
	private bool _shouldIgnoreNextInstance;

	public override PowerType Type => PowerType.Debuff;

	public override PowerStackType StackType => PowerStackType.Counter;

	protected override bool IsVisibleInternal => false;

	public AbstractModel OriginModel => ModelDb.Relic<GlacialAugmentRune>();

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
			await PowerCmd.Apply<SlowPower>(target, amount, applier, cardSource, silent: true);
		}
	}

	public override async Task AfterPowerAmountChanged(PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
	{
		if (power == this && amount != Amount)
		{
			if (_shouldIgnoreNextInstance)
			{
				_shouldIgnoreNextInstance = false;
			}
			else
			{
				await PowerCmd.Apply<SlowPower>(Owner, amount, applier, cardSource, silent: true);
			}
		}
	}

	public override async Task AfterSideTurnStart(CombatSide side, CombatState combatState)
	{
		if (side != Owner.Side)
		{
			return;
		}

		await PowerCmd.Remove(this);
		await PowerCmd.Apply<SlowPower>(Owner, -Amount, Owner, null, silent: true);
	}
}

public sealed class GlacialAugmentRune : KeystoneRelicBase
{
	private bool _triggeredThisTurn;

	private bool _isApplyingBonusDebuff;

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

	public override Task AfterSideTurnStart(CombatSide side, CombatState combatState)
	{
		if (side == CombatSide.Player)
		{
			ResetTurnTracking();
		}

		return Task.CompletedTask;
	}

	public override async Task AfterPowerAmountChanged(PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
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
			await PowerCmd.Apply<WeakPower>(target!, 1m, Owner!.Creature, cardSource);
			await PowerCmd.Apply<TemporarySlowPower>(target!, 1m, Owner!.Creature, cardSource);
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

public sealed class AftershockRune : KeystoneRelicBase
{
	private const int BlockMultiplier = 4;

	private bool _triggeredThisTurn;

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

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("ActBonus", 1m),
		new DynamicVar("BlockMultiplier", BlockMultiplier)
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

	public override Task AfterSideTurnStart(CombatSide side, CombatState combatState)
	{
		if (side == CombatSide.Player)
		{
			_triggeredThisTurn = false;
			RefreshVisualState();
		}

		return Task.CompletedTask;
	}

	public override async Task AfterPowerAmountChanged(PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
	{
		if (_triggeredThisTurn
			|| !TryGetOwnedEnemyDebuffTarget(power, amount, applier, out Creature? target))
		{
			return;
		}

		_triggeredThisTurn = true;
		RefreshVisualState();

		Player owner = Owner!;
		int bonus = GetCurrentActBonus();
		if (bonus <= 0)
		{
			return;
		}

		Flash([target!]);
		await CreatureCmd.GainBlock(owner.Creature, bonus * BlockMultiplier, ValueProp.Unpowered, cardPlay: null, fast: false);
		await PowerCmd.Apply<DexterityPower>(owner.Creature, bonus, owner.Creature, cardSource);
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
		await PowerCmd.SetAmount<DexterityPower>(Owner.Creature, updatedDexterity, Owner.Creature, null);
		_dexterityGrantedThisTurn = 0;
	}

	private void ResetTracking()
	{
		_triggeredThisTurn = false;
		_dexterityGrantedThisTurn = 0;
		RefreshVisualState();
	}

	private void RefreshVisualState()
	{
		Status = !_triggeredThisTurn ? RelicStatus.Active : RelicStatus.Normal;
		InvokeDisplayAmountChanged();
	}
}

public sealed class DarkHarvestRune : KeystoneRelicBase
{
	private int _souls;

	private bool _usedBonusThisTurn;

	private bool _isApplyingHarvestBonus;

	private CardModel? _pendingHarvestCard;

	private uint? _pendingHarvestTargetCombatId;

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
	public uint? SavedPendingHarvestTargetCombatId
	{
		get => _pendingHarvestTargetCombatId;
		set => _pendingHarvestTargetCombatId = value;
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

	public override Task AfterSideTurnStart(CombatSide side, CombatState combatState)
	{
		if (side == CombatSide.Player)
		{
			ResetTurnTracking();
		}

		return Task.CompletedTask;
	}

	public override Task BeforeCardPlayed(CardPlay cardPlay)
	{
		_pendingHarvestCard = null;
		_pendingHarvestTargetCombatId = null;

		if (_souls <= 0
			|| _usedBonusThisTurn
			|| !cardPlay.IsFirstInSeries
			|| !IsOwnedAttack(cardPlay.Card)
			|| cardPlay.Target is not { Side: CombatSide.Enemy } target
			|| !target.CombatId.HasValue)
		{
			return Task.CompletedTask;
		}

		uint targetCombatId = target.CombatId.Value;
		if (HarvestedTargetCombatIds.Contains(targetCombatId))
		{
			return Task.CompletedTask;
		}

		if (target.CurrentHp * 2 <= target.MaxHp)
		{
			_pendingHarvestCard = cardPlay.Card;
			_pendingHarvestTargetCombatId = targetCombatId;
		}

		return Task.CompletedTask;
	}

	public override Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		if (ReferenceEquals(cardPlay.Card, _pendingHarvestCard))
		{
			_pendingHarvestCard = null;
			_pendingHarvestTargetCombatId = null;
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
			|| !ReferenceEquals(cardSource, _pendingHarvestCard)
			|| !IsOwnedAttack(cardSource)
			|| props.HasFlag(ValueProp.Unpowered)
			|| !target.CombatId.HasValue
			|| _pendingHarvestTargetCombatId != target.CombatId.Value)
		{
			return;
		}

		_pendingHarvestCard = null;
		_pendingHarvestTargetCombatId = null;

		uint targetCombatId = target.CombatId.Value;
		if (HarvestedTargetCombatIds.Contains(targetCombatId)
			|| result.TotalDamage <= 0
			|| !target.IsAlive)
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
				_souls,
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
		_pendingHarvestCard = null;
		_pendingHarvestTargetCombatId = null;
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
