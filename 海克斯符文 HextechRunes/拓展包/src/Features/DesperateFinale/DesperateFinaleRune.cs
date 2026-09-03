using HextechRunes;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HextechRunesSponsorPack;

public sealed class DesperateFinaleRune : HextechRelicBase, IHextechHealingMultiplierProvider
{
	private readonly HashSet<uint> _ownerKilledChoraleCombatIds = [];
	private readonly HashSet<uint> _ownerDoomedChoraleCombatIds = [];
	private readonly HashSet<uint> _rewardedChoraleCombatIds = [];
	private readonly List<int> _pendingProjectionChoraleHp = [];
	private bool _pendingFinalChoraleRewards;
	private int _stacks;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedFinalChoraleKills
	{
		get => _stacks;
		set
		{
			_stacks = Math.Max(0, value);
			InvokeDisplayAmountChanged();
		}
	}

	public override bool HasUponPickupEffect => true;

	public override bool ShowCounter => !IsCanonical;

	public override int DisplayAmount => !IsCanonical ? _stacks : 0;

	private decimal BonusMultiplier => 1m + _stacks * DynamicVars["BonusPercent"].BaseValue / 100m;

	public override bool IsAvailableForPlayer(Player player)
	{
		_ = player;
		return IntegratedStrategyEventsBridge.IsAvailable;
	}

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("BonusPercent", 20m),
		new DynamicVar("ChoraleHpBonus", 300m)
	];

	public override Task AfterObtained()
	{
		return Owner != null
			? IntegratedStrategyEventsBridge.ObtainProphecyProjection(Owner)
			: Task.CompletedTask;
	}

	// 主模组 0.108 适配后基类 ModifyDamageMultiplicative 被 sealed(版本签名转发),子类改写 Compat 变体。
	public override decimal ModifyDamageMultiplicativeCompat(
		Creature? target,
		decimal amount,
		ValueProp props,
		Creature? dealer,
		CardModel? cardSource)
	{
		return IsDamageFromOwnerToEnemyOrPreview(target, dealer, cardSource) ? BonusMultiplier : 1m;
	}

	public override decimal ModifyBlockMultiplicative(
		Creature target,
		decimal block,
		ValueProp props,
		CardModel? cardSource,
		CardPlay? cardPlay)
	{
		return target == Owner?.Creature ? BonusMultiplier : 1m;
	}

	public decimal ModifyHealingMultiplicative(Player player, Creature creature, decimal amount)
	{
		return player == Owner && creature == Owner?.Creature ? BonusMultiplier : 1m;
	}

	public override Task AfterDamageGiven(
		PlayerChoiceContext choiceContext,
		Creature? dealer,
		DamageResult result,
		ValueProp props,
		Creature target,
		CardModel? cardSource)
	{
		_ = choiceContext;
		_ = props;

		if (Owner == null
			|| !result.WasTargetKilled
			|| target.CombatId is not uint combatId
			|| !IntegratedStrategyEventsBridge.IsFinalChorale(target)
			|| !IsDamageFromOwner(dealer, cardSource))
		{
			return Task.CompletedTask;
		}

		_ownerKilledChoraleCombatIds.Add(combatId);
		return Task.CompletedTask;
	}

	public override Task AfterPowerAmountChanged(
		PlayerChoiceContext choiceContext,
		PowerModel power,
		decimal amount,
		Creature? applier,
		CardModel? cardSource)
	{
		_ = choiceContext;
		_ = cardSource;

		if (Owner != null
			&& amount > 0m
			&& power is DoomPower
			&& applier == Owner.Creature
			&& power.Owner.CombatId is uint combatId
			&& IntegratedStrategyEventsBridge.IsFinalChorale(power.Owner))
		{
			_ownerDoomedChoraleCombatIds.Add(combatId);
		}

		return Task.CompletedTask;
	}

	public override Task BeforeDeath(Creature target)
	{
		if (Owner != null
			&& target.CombatId is uint combatId
			&& target.GetPower<DoomPower>()?.Applier == Owner.Creature
			&& IntegratedStrategyEventsBridge.IsFinalChorale(target))
		{
			_ownerDoomedChoraleCombatIds.Add(combatId);
		}

		return Task.CompletedTask;
	}

	public override Task AfterDeath(
		PlayerChoiceContext choiceContext,
		Creature target,
		bool wasRemovalPrevented,
		float deathAnimLength)
	{
		_ = choiceContext;
		_ = deathAnimLength;

		if (Owner == null
			|| wasRemovalPrevented
			|| target.CombatId is not uint combatId
			|| !_ownerKilledChoraleCombatIds.Contains(combatId)
			|| !IntegratedStrategyEventsBridge.IsFinalChorale(target))
		{
			return Task.CompletedTask;
		}

		QueueFinalChoraleReward(target, combatId);
		return Task.CompletedTask;
	}

	public override Task AfterDiedToDoom(
		PlayerChoiceContext choiceContext,
		IReadOnlyList<Creature> creatures)
	{
		_ = choiceContext;

		if (Owner == null)
		{
			return Task.CompletedTask;
		}

		foreach (Creature creature in creatures)
		{
			if (!creature.IsDead
				|| creature.CombatId is not uint combatId
				|| !_ownerDoomedChoraleCombatIds.Contains(combatId)
				|| !IntegratedStrategyEventsBridge.IsFinalChorale(creature))
			{
				continue;
			}

			QueueFinalChoraleReward(creature, combatId);
		}

		return Task.CompletedTask;
	}

	private void QueueFinalChoraleReward(Creature target, uint combatId)
	{
		if (Owner == null || !_rewardedChoraleCombatIds.Add(combatId))
		{
			return;
		}

		SavedFinalChoraleKills++;
		_pendingProjectionChoraleHp.Add(Math.Max(1, target.MaxHp + DynamicVars["ChoraleHpBonus"].IntValue));
		_pendingFinalChoraleRewards = true;
		Flash([Owner.Creature]);
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		_ = room;
		_ownerKilledChoraleCombatIds.Clear();
		_ownerDoomedChoraleCombatIds.Clear();
		_rewardedChoraleCombatIds.Clear();
		return Task.CompletedTask;
	}

	public override async Task AfterCombatVictory(CombatRoom room)
	{
		if (Owner != null && _pendingProjectionChoraleHp.Count > 0)
		{
			if (_pendingFinalChoraleRewards)
			{
				IntegratedStrategyEventsBridge.AddFinalChoraleRewardsIfMissing(room);
			}

			foreach (int choraleHp in _pendingProjectionChoraleHp)
			{
				await IntegratedStrategyEventsBridge.ObtainProphecyProjection(Owner, choraleHp);
			}
		}

		_ownerKilledChoraleCombatIds.Clear();
		_ownerDoomedChoraleCombatIds.Clear();
		_rewardedChoraleCombatIds.Clear();
		_pendingProjectionChoraleHp.Clear();
		_pendingFinalChoraleRewards = false;
	}
}
