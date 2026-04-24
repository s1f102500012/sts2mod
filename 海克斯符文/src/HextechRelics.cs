using Godot;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HextechRunes;

public abstract class HextechRelicBase : RelicModel
{
	private static readonly string PlaceholderIconPath = ImageHelper.GetImagePath("powers/missing_power.png");

	public sealed override RelicRarity Rarity => RelicRarity.Starter;

	public override string PackedIconPath => GetResolvedIconPath();

	protected override string PackedIconOutlinePath => GetResolvedIconPath();

	protected override string BigIconPath => GetResolvedIconPath();

	public virtual bool IsAvailableForPlayer(Player player) => true;

	protected static int FloorToInt(decimal value)
	{
		return (int)decimal.Floor(value);
	}

	protected bool IsOwnedCard(CardModel? card)
	{
		return card?.Owner == Owner;
	}

	protected bool IsOwnedAttack(CardModel? card)
	{
		return card != null && card.Owner == Owner && card.Type == CardType.Attack;
	}

	protected bool IsOwnedSkill(CardModel? card)
	{
		return card != null && card.Owner == Owner && card.Type == CardType.Skill;
	}

	protected bool IsOwnedNonXCardWithCostAtLeast(CardModel? card, decimal minimumCost)
	{
		return card != null
			&& card.Owner == Owner
			&& !card.EnergyCost.CostsX
			&& card.EnergyCost.GetAmountToSpend() >= minimumCost;
	}

	protected bool IsOwnerOrPet(Creature? dealer)
	{
		return dealer == Owner?.Creature || dealer?.PetOwner == Owner;
	}

	protected bool IsDamageFromOwner(Creature? dealer, CardModel? cardSource)
	{
		if (Owner == null)
		{
			return false;
		}

		if (IsOwnerOrPet(dealer))
		{
			return true;
		}

		return cardSource?.Owner == Owner;
	}

	protected bool IsDefectPlayer(Player player)
	{
		return player.Character.Id == ModelDb.GetId<Defect>();
	}

	protected bool IsDefectOwner => Owner != null && IsDefectPlayer(Owner);

	private string GetResolvedIconPath()
	{
		string? customPath = ModInfo.TryGetCustomRelicIconPath(this);
		if (!string.IsNullOrEmpty(customPath) && ResourceLoader.Exists(customPath))
		{
			return customPath;
		}

		return PlaceholderIconPath;
	}

	protected bool TryGetOwnedEnemyDebuffTarget(PowerModel power, decimal amount, Creature? applier, out Creature? target)
	{
		target = power.Owner;
		return amount > 0m
			&& target?.Side == CombatSide.Enemy
			&& applier == Owner?.Creature
			&& power.GetTypeForAmount(amount) == PowerType.Debuff
			&& power is not ITemporaryPower;
	}
}

public abstract class LimitedDebuffProcRelicBase : HextechRelicBase
{
	private int _procsThisTurn;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedProcsThisTurn
	{
		get => _procsThisTurn;
		set
		{
			_procsThisTurn = Math.Max(0, value);
			UpdateDisplay();
		}
	}

	protected virtual int MaxProcsPerTurn => 3;

	public override bool ShowCounter => CombatManager.Instance?.IsInProgress == true && !IsCanonical;

	public override int DisplayAmount => !IsCanonical ? Math.Max(0, MaxProcsPerTurn - _procsThisTurn) : 0;

	public override Task BeforeCombatStart()
	{
		ResetProcs();
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		ResetProcs();
		return Task.CompletedTask;
	}

	public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, CombatState combatState)
	{
		if (Owner != null && side == Owner.Creature.Side)
		{
			ResetProcs();
		}

		return Task.CompletedTask;
	}

	public override async Task AfterPowerAmountChanged(PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
	{
		if (!TryGetOwnedEnemyDebuffTarget(power, amount, applier, out Creature? target) || _procsThisTurn >= MaxProcsPerTurn)
		{
			return;
		}

		_procsThisTurn++;
		UpdateDisplay();
		Flash(target == null ? Array.Empty<Creature>() : [target]);
		await OnEnemyDebuffApplied(target!);
	}

	protected abstract Task OnEnemyDebuffApplied(Creature target);

	private void ResetProcs()
	{
		_procsThisTurn = 0;
		UpdateDisplay();
	}

	private void UpdateDisplay()
	{
		Status = _procsThisTurn == MaxProcsPerTurn - 1 ? RelicStatus.Active : RelicStatus.Normal;
		InvokeDisplayAmountChanged();
	}
}

public sealed class JudicatorRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new EnergyVar(1),
		new DynamicVar("DamageMultiplier", 1.2m)
	];

	public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		if (!IsDamageFromOwner(dealer, cardSource) || target?.Side != CombatSide.Enemy || target.CurrentHp * 2 >= target.MaxHp)
		{
			return 1m;
		}

		return DynamicVars["DamageMultiplier"].BaseValue;
	}

	public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature target, bool wasRemovalPrevented, float deathAnimLength)
	{
		if (wasRemovalPrevented || Owner == null || Owner.Creature.IsDead || target.Side == Owner.Creature.Side)
		{
			return;
		}

		Flash(Array.Empty<Creature>());
		if (Owner.PlayerCombatState != null)
		{
			await PlayerCmd.SetEnergy(Owner.PlayerCombatState.MaxEnergy, Owner);
		}
	}
}

public sealed class SlapRune : LimitedDebuffProcRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<StrengthPower>(1m)
	];

	protected override Task OnEnemyDebuffApplied(Creature target)
	{
		return PowerCmd.Apply<StrengthPower>(Owner!.Creature, DynamicVars.Strength.BaseValue, Owner!.Creature, null);
	}
}

public sealed class TankEngineRune : HextechRelicBase
{
	private int _stacks;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedStacks
	{
		get => _stacks;
		set
		{
			_stacks = Math.Max(0, value);
			InvokeDisplayAmountChanged();
		}
	}

	public override bool ShowCounter => true;

	public override int DisplayAmount => !IsCanonical ? _stacks : 0;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("HpGainPercent", 0.05m),
		new DynamicVar("ScalePercent", 5m)
	];

	public override Task AfterObtained()
	{
		Grow();
		return Task.CompletedTask;
	}

	public override Task AfterRoomEntered(AbstractRoom room)
	{
		Grow();
		return Task.CompletedTask;
	}

	public override async Task AfterCombatVictory(CombatRoom room)
	{
		if (Owner == null || Owner.Creature.IsDead)
		{
			return;
		}

		SavedStacks++;
		Flash(Array.Empty<Creature>());
		int hpGain = Math.Max(1, FloorToInt(Owner.Creature.MaxHp * DynamicVars["HpGainPercent"].BaseValue));
		await CreatureCmd.GainMaxHp(Owner.Creature, hpGain);
		Grow();
	}

	private void Grow()
	{
		if (Owner == null)
		{
			return;
		}

		float size = 1f + _stacks * 0.05f;
		NCombatRoom.Instance?.GetCreatureNode(Owner.Creature)?.SetDefaultScaleTo(size, 0f);
	}
}

public sealed class EurekaRune : HextechRelicBase
{
	public override decimal ModifyMaxEnergy(Player player, decimal amount)
	{
		if (player != Owner)
		{
			return amount;
		}

		return amount + FloorToInt(player.Relics.Count / 4m);
	}
}

public sealed class InfiniteLoopRune : HextechRelicBase
{
	private int _combatVictories;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedCombatVictories
	{
		get => _combatVictories;
		set => _combatVictories = Math.Max(0, value);
	}

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new EnergyVar(1),
		new DynamicVar("Combats", 5m)
	];

	public override decimal ModifyMaxEnergy(Player player, decimal amount)
	{
		if (player != Owner)
		{
			return amount;
		}

		return amount + DynamicVars.Energy.BaseValue + FloorToInt(_combatVictories / 5m);
	}

	public override Task AfterCombatVictory(CombatRoom room)
	{
		if (Owner != null && !Owner.Creature.IsDead)
		{
			SavedCombatVictories++;
		}

		return Task.CompletedTask;
	}
}

public sealed class AstralBodyRune : HextechRelicBase
{
	public override bool HasUponPickupEffect => true;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new MaxHpVar(50m),
		new DynamicVar("DamageMultiplier", 0.9m)
	];

	public override Task AfterObtained()
	{
		return CreatureCmd.GainMaxHp(Owner!.Creature, DynamicVars.MaxHp.BaseValue);
	}

	public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		if (!IsDamageFromOwner(dealer, cardSource))
		{
			return 1m;
		}

		return DynamicVars["DamageMultiplier"].BaseValue;
	}
}

public sealed class AncientWineRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new HealVar(1m)
	];

	public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		if (!IsOwnedSkill(cardPlay.Card))
		{
			return Task.CompletedTask;
		}

		Flash();
		return CreatureCmd.Heal(Owner!.Creature, DynamicVars.Heal.BaseValue);
	}
}

public sealed class SlowCookRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("BurnPercent", 6m)
	];

	public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (player != Owner)
		{
			return;
		}

		int burnAmount = FloorToInt(player.Creature.MaxHp * (DynamicVars["BurnPercent"].BaseValue / 100m));
		if (burnAmount <= 0)
		{
			return;
		}

		CombatState? combatState = player.Creature.CombatState;
		if (combatState == null)
		{
			return;
		}

		IReadOnlyList<Creature> enemies = combatState.HittableEnemies.ToList();
		if (enemies.Count == 0)
		{
			return;
		}

		Flash(enemies);
		foreach (Creature enemy in enemies)
		{
			await PowerCmd.Apply<HextechBurnPower>(enemy, burnAmount, player.Creature, null);
		}
	}
}

public sealed class NoNonsenseRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<StrengthPower>(1m)
	];

	public async Task HandlePreventedNonHandDraw(int drawsPrevented)
	{
		if (Owner == null || Owner.Creature.IsDead || drawsPrevented <= 0)
		{
			return;
		}

		Flash();
		await Cmd.CustomScaledWait(0.05f, 0.1f);
	}
}

public abstract class AttributeConversionRelicBase : HextechRelicBase
{
	private bool _isConverting;
	private decimal? _pendingAmount;
	private Creature? _pendingApplier;
	private CardModel? _pendingCardSource;

	protected abstract bool ShouldConvert(PowerModel canonicalPower);

	protected abstract bool ShouldConvertAppliedPower(PowerModel power);

	protected abstract Task ApplyConvertedPower(decimal amount, Creature? applier, CardModel? cardSource);

	protected abstract Task RevertOriginalPower(PowerModel power, decimal amount, Creature? applier, CardModel? cardSource);

	public override Task AfterCombatEnd(CombatRoom room)
	{
		_pendingAmount = null;
		_pendingApplier = null;
		_pendingCardSource = null;
		_isConverting = false;
		return Task.CompletedTask;
	}

	public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target, decimal amount, Creature? applier, out decimal modifiedAmount)
	{
		modifiedAmount = amount;
		if (_isConverting || Owner == null || target != Owner.Creature || amount == 0m || !ShouldConvert(canonicalPower))
		{
			return false;
		}

		// Replace the original stat change with the converted one after the hook pipeline finishes.
		_pendingAmount = amount;
		_pendingApplier = applier;
		_pendingCardSource = null;
		modifiedAmount = 0m;
		return true;
	}

	public override async Task AfterModifyingPowerAmountReceived(PowerModel power)
	{
		if (_pendingAmount is not decimal amount)
		{
			return;
		}

		Creature? applier = _pendingApplier;
		CardModel? cardSource = _pendingCardSource;
		_pendingAmount = null;
		_pendingApplier = null;
		_pendingCardSource = null;

		_isConverting = true;
		try
		{
			Flash();
			await ApplyConvertedPower(amount, applier, cardSource);
		}
		finally
		{
			_isConverting = false;
		}
	}

	public override async Task AfterPowerAmountChanged(PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
	{
		if (_isConverting || Owner == null || amount == 0m || power.Owner != Owner.Creature || !ShouldConvertAppliedPower(power))
		{
			return;
		}

		_isConverting = true;
		try
		{
			Flash();
			await RevertOriginalPower(power, amount, applier, cardSource);
			await ApplyConvertedPower(amount, applier, cardSource);
		}
		finally
		{
			_isConverting = false;
		}
	}
}

public sealed class DexterityToStrengthRune : AttributeConversionRelicBase
{

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<StrengthPower>(1m)
	];

	public override Task AfterRoomEntered(AbstractRoom room)
	{
		if (room is not CombatRoom || Owner == null)
		{
			return Task.CompletedTask;
		}

		return PowerCmd.Apply<StrengthPower>(Owner.Creature, DynamicVars.Strength.BaseValue, Owner.Creature, null);
	}

	protected override bool ShouldConvert(PowerModel canonicalPower)
	{
		return canonicalPower is DexterityPower;
	}

	protected override bool ShouldConvertAppliedPower(PowerModel power)
	{
		return power is DexterityPower;
	}

	protected override Task ApplyConvertedPower(decimal amount, Creature? applier, CardModel? cardSource)
	{
		return PowerCmd.Apply<StrengthPower>(Owner!.Creature, amount, applier, cardSource);
	}

	protected override Task RevertOriginalPower(PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
	{
		return PowerCmd.Apply<DexterityPower>(Owner!.Creature, -amount, applier, cardSource);
	}
}

public sealed class StrengthToDexterityRune : AttributeConversionRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<DexterityPower>(1m)
	];

	public override Task AfterRoomEntered(AbstractRoom room)
	{
		if (room is not CombatRoom || Owner == null)
		{
			return Task.CompletedTask;
		}

		return PowerCmd.Apply<DexterityPower>(Owner.Creature, DynamicVars.Dexterity.BaseValue, Owner.Creature, null);
	}

	protected override bool ShouldConvert(PowerModel canonicalPower)
	{
		return canonicalPower is StrengthPower;
	}

	protected override bool ShouldConvertAppliedPower(PowerModel power)
	{
		return power is StrengthPower;
	}

	protected override Task ApplyConvertedPower(decimal amount, Creature? applier, CardModel? cardSource)
	{
		return PowerCmd.Apply<DexterityPower>(Owner!.Creature, amount, applier, cardSource);
	}

	protected override Task RevertOriginalPower(PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
	{
		return PowerCmd.Apply<StrengthPower>(Owner!.Creature, -amount, applier, cardSource);
	}
}

public sealed class DexterityStrengthToFocusRune : AttributeConversionRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<FocusPower>(1m)
	];

	public override bool IsAvailableForPlayer(Player player)
	{
		return IsDefectPlayer(player);
	}

	public override Task AfterRoomEntered(AbstractRoom room)
	{
		if (room is not CombatRoom || Owner == null || !IsDefectOwner)
		{
			return Task.CompletedTask;
		}

		return PowerCmd.Apply<FocusPower>(Owner.Creature, DynamicVars["FocusPower"].BaseValue, Owner.Creature, null);
	}

	protected override bool ShouldConvert(PowerModel canonicalPower)
	{
		return IsDefectOwner && (canonicalPower is DexterityPower || canonicalPower is StrengthPower);
	}

	protected override bool ShouldConvertAppliedPower(PowerModel power)
	{
		return IsDefectOwner && (power is DexterityPower || power is StrengthPower);
	}

	protected override Task ApplyConvertedPower(decimal amount, Creature? applier, CardModel? cardSource)
	{
		return PowerCmd.Apply<FocusPower>(Owner!.Creature, amount, applier, cardSource);
	}

	protected override Task RevertOriginalPower(PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
	{
		if (Owner == null)
		{
			return Task.CompletedTask;
		}

		if (power is DexterityPower)
		{
			return PowerCmd.Apply<DexterityPower>(Owner.Creature, -amount, applier, cardSource);
		}

		if (power is StrengthPower)
		{
			return PowerCmd.Apply<StrengthPower>(Owner.Creature, -amount, applier, cardSource);
		}

		return Task.CompletedTask;
	}
}

public sealed class SuperBrainRune : HextechRelicBase
{
	public override Task AfterRoomEntered(AbstractRoom room)
	{
		if (room is not CombatRoom || Owner == null)
		{
			return Task.CompletedTask;
		}

		int plating = Owner.Deck.Cards.Count / 3;
		if (plating <= 0)
		{
			return Task.CompletedTask;
		}

		Flash();
		return PowerCmd.Apply<PlatingPower>(Owner.Creature, plating, Owner.Creature, null);
	}
}

public sealed class GiantSlayerRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new CardsVar(2),
		new DynamicVar("HpGap", 50m),
		new DynamicVar("DamagePerStepPercent", 0.1m),
		new DynamicVar("MaxBonusPercent", 0.5m)
	];

	public override decimal ModifyHandDraw(Player player, decimal count)
	{
		if (player != Owner)
		{
			return count;
		}

		return count + DynamicVars.Cards.BaseValue;
	}

	public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		if (Owner == null || target?.Side != CombatSide.Enemy || !IsDamageFromOwner(dealer, cardSource))
		{
			return 1m;
		}

		int hpGap = target.MaxHp - Owner.Creature.MaxHp;
		if (hpGap <= 0)
		{
			return 1m;
		}

		int steps = hpGap / 50;
		decimal bonus = Math.Min(steps * DynamicVars["DamagePerStepPercent"].BaseValue, DynamicVars["MaxBonusPercent"].BaseValue);
		return 1m + bonus;
	}
}

public sealed class NimbleRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new CardsVar(1)
	];

	public override decimal ModifyHandDraw(Player player, decimal count)
	{
		if (player != Owner)
		{
			return count;
		}

		return count + DynamicVars.Cards.BaseValue;
	}
}

public sealed class OverflowRune : HextechRelicBase
{
	public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
	{
		modifiedCost = originalCost;
		if (Owner == null || card.Owner != Owner || card.Pile?.Type != PileType.Hand || card.EnergyCost.CostsX)
		{
			return false;
		}

		modifiedCost = originalCost + 1m;
		return true;
	}

	public override decimal ModifyBlockMultiplicative(Creature target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay)
	{
		return target == Owner?.Creature ? 2m : 1m;
	}

	public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		return IsOwnerOrPet(dealer) || cardSource?.Owner == Owner ? 2m : 1m;
	}
}

public sealed class SturdyRune : HextechRelicBase
{
	public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (player != Owner)
		{
			return Task.CompletedTask;
		}

		decimal percent = player.Creature.CurrentHp < player.Creature.MaxHp * 0.5m ? 0.05m : 0.02m;
		int healAmount = Math.Max(1, FloorToInt(player.Creature.MaxHp * percent));
		Flash();
		return CreatureCmd.Heal(player.Creature, healAmount);
	}
}

public sealed class LoopRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new EnergyVar(1)
	];

	public override decimal ModifyMaxEnergy(Player player, decimal amount)
	{
		return player == Owner ? amount + DynamicVars.Energy.BaseValue : amount;
	}
}

public sealed class RedEnvelopeRune : HextechRelicBase
{
	public override Task AfterCombatVictory(CombatRoom room)
	{
		if (Owner == null || Owner.Creature.IsDead)
		{
			return Task.CompletedTask;
		}

		Flash(Array.Empty<Creature>());
		if (Owner.PlayerRng.Rewards.NextInt(100) < 80)
		{
			room.AddExtraReward(Owner, new GoldReward(10, 20, Owner));
		}
		else
		{
			room.AddExtraReward(Owner, new RelicReward(Owner));
		}

		return Task.CompletedTask;
	}
}

public sealed class EscapePlanRune : HextechRelicBase
{
	private bool _pendingTrigger;
	private bool _triggeredThisCombat;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public bool SavedPendingTrigger
	{
		get => _pendingTrigger;
		set => _pendingTrigger = value;
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public bool SavedTriggeredThisCombat
	{
		get => _triggeredThisCombat;
		set => _triggeredThisCombat = value;
	}

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("ThresholdPercent", 50m),
		new DynamicVar("BlockPercent", 60m)
	];

	public override Task BeforeCombatStart()
	{
		_pendingTrigger = false;
		_triggeredThisCombat = false;
		Status = RelicStatus.Normal;
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		_pendingTrigger = false;
		_triggeredThisCombat = false;
		Status = RelicStatus.Normal;
		return Task.CompletedTask;
	}

	public override Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		if (Owner == null
			|| target != Owner.Creature
			|| result.UnblockedDamage <= 0
			|| _triggeredThisCombat
			|| _pendingTrigger
			|| target.CurrentHp >= target.MaxHp * 0.5m)
		{
			return Task.CompletedTask;
		}

		_pendingTrigger = true;
		_triggeredThisCombat = true;
		Status = RelicStatus.Active;
		Flash();
		return Task.CompletedTask;
	}

	public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (player != Owner || !_pendingTrigger)
		{
			return;
		}

		_pendingTrigger = false;
		Status = RelicStatus.Normal;
		int blockAmount = FloorToInt(player.Creature.MaxHp * 0.6m);
		Flash();
		if (blockAmount > 0)
		{
			await CreatureCmd.GainBlock(player.Creature, blockAmount, ValueProp.Unpowered, null);
		}

		await PowerCmd.Apply<ShrinkPower>(player.Creature, 1m, player.Creature, null);
	}
}

public sealed class MindPurificationRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("DamagePercent", 30m)
	];

	public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature target, bool wasRemovalPrevented, float deathAnimLength)
	{
		if (Owner == null || wasRemovalPrevented || target.Side == Owner.Creature.Side || target.CombatState == null)
		{
			return;
		}

		List<(Creature creature, int damage)> toDamage = target.CombatState.Enemies
			.Where(enemy => enemy != target && enemy.IsAlive)
			.Select(enemy => (enemy, FloorToInt(enemy.CurrentHp * 0.3m)))
			.Where(pair => pair.Item2 > 0)
			.ToList();
		if (toDamage.Count == 0)
		{
			return;
		}

		Flash(toDamage.Select(static pair => pair.creature));
		foreach ((Creature creature, int damage) in toDamage)
		{
			await CreatureCmd.Damage(choiceContext, creature, damage, ValueProp.Unpowered, Owner.Creature, null);
		}
	}
}

public sealed class BadTasteRune : LimitedDebuffProcRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new HealVar(1m)
	];

	protected override Task OnEnemyDebuffApplied(Creature target)
	{
		return CreatureCmd.Heal(Owner!.Creature, DynamicVars.Heal.BaseValue);
	}
}

public sealed class CourageOfColossusRune : LimitedDebuffProcRelicBase
{
	protected override int MaxProcsPerTurn => 2;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("Plating", 3m)
	];

	protected override Task OnEnemyDebuffApplied(Creature target)
	{
		return PowerCmd.Apply<PlatingPower>(Owner!.Creature, DynamicVars["Plating"].BaseValue, Owner!.Creature, null);
	}
}

public sealed class EndlessRecoveryRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new HealVar(3m)
	];

	public override Task AfterRoomEntered(AbstractRoom room)
	{
		if (Owner == null || Owner.Creature.IsDead)
		{
			return Task.CompletedTask;
		}

		Flash();
		return CreatureCmd.Heal(Owner.Creature, DynamicVars.Heal.BaseValue);
	}
}

public sealed class GlassCannonRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("DamageMultiplier", 1.4m),
		new DynamicVar("HealCapPercent", 0.7m)
	];

	public decimal HealCapPercent => DynamicVars["HealCapPercent"].BaseValue;

	public override async Task AfterObtained()
	{
		if (Owner?.Creature == null)
		{
			return;
		}

		int hpCap = Math.Max(1, FloorToInt(Owner.Creature.MaxHp * HealCapPercent));
		if (Owner.Creature.CurrentHp > hpCap)
		{
			await CreatureCmd.SetCurrentHp(Owner.Creature, hpCap);
		}
	}

	public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		if (!IsDamageFromOwner(dealer, cardSource))
		{
			return 1m;
		}

		return DynamicVars["DamageMultiplier"].BaseValue;
	}
}

public sealed class MadScientistRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("OrbSlots", 5m),
		new DynamicVar("OrbCount", 5m)
	];

	public override async Task AfterObtained()
	{
		if (Owner == null)
		{
			return;
		}

		ElicitCard card = (ElicitCard)ModelDb.Card<ElicitCard>().ToMutable();
		card.Owner = Owner;
		if (Owner.PlayerCombatState != null && CombatManager.Instance.IsInProgress)
		{
			await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, addedByPlayer: true);
		}
		else
		{
			card.FloorAddedToDeck = Owner.RunState.TotalFloor;
			Owner.Deck.AddInternal(card);
		}
		SaveManager.Instance.MarkCardAsSeen(card);
	}

	public override async Task AfterSideTurnStart(CombatSide side, CombatState combatState)
	{
		if (Owner == null || side != Owner.Creature.Side || combatState.RoundNumber > 1)
		{
			return;
		}

		Flash();
		await OrbCmd.AddSlots(Owner, DynamicVars["OrbSlots"].IntValue);
		for (int i = 0; i < DynamicVars["OrbCount"].IntValue; i++)
		{
			OrbModel orb = OrbModel.GetRandomOrb(Owner.RunState.Rng.CombatOrbGeneration).ToMutable();
			await OrbCmd.Channel(new BlockingPlayerChoiceContext(), orb, Owner);
		}
	}
}

public sealed class ElicitCard : CardModel
{
	public override CardPoolModel Pool => Owner?.Character.CardPool ?? ModelDb.CardPool<ColorlessCardPool>();

	public override CardPoolModel VisualCardPool => Pool;

	public override OrbEvokeType OrbEvokeType => OrbEvokeType.All;

	public override string PortraitPath => ModelDb.Card<Shatter>().PortraitPath;

	public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.Static(StaticHoverTip.Evoke)
	];

	public override IEnumerable<CardKeyword> CanonicalKeywords =>
	[
		CardKeyword.Innate,
		CardKeyword.Retain,
		CardKeyword.Exhaust
	];

	public ElicitCard()
		: base(0, CardType.Skill, CardRarity.Token, TargetType.Self)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		int orbCount = Owner.PlayerCombatState?.OrbQueue.Orbs.Count ?? 0;
		for (int i = 0; i < orbCount; i++)
		{
			await OrbCmd.EvokeNext(choiceContext, Owner);
		}
	}
}

public sealed class SpeedsterRune : HextechRelicBase
{
	public override decimal ModifyHandDraw(Player player, decimal count)
	{
		if (player != Owner)
		{
			return count;
		}

		return count + (player.PlayerCombatState?.MaxEnergy ?? 0) / 2;
	}
}

public sealed class SpeedDemonRune : HextechRelicBase
{
	private bool _triggeredThisTurn;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public bool SavedTriggeredThisTurn
	{
		get => _triggeredThisTurn;
		set => _triggeredThisTurn = value;
	}

	public override Task BeforeCombatStart()
	{
		_triggeredThisTurn = false;
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		_triggeredThisTurn = false;
		return Task.CompletedTask;
	}

	public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, CombatState combatState)
	{
		if (Owner != null && side == Owner.Creature.Side)
		{
			_triggeredThisTurn = false;
		}

		return Task.CompletedTask;
	}

	public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
	{
		if (_triggeredThisTurn
			|| Owner == null
			|| target.Side != CombatSide.Enemy
			|| result.UnblockedDamage <= 0
			|| (!IsOwnerOrPet(dealer) && cardSource?.Owner != Owner))
		{
			return;
		}

		_triggeredThisTurn = true;
		Flash([target]);
		await CardPileCmd.Draw(choiceContext, 2m, Owner);
	}
}

public sealed class JeweledGauntletRune : HextechRelicBase
{
	public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount)
	{
		if (Owner == null || card.Owner != Owner)
		{
			return playCount;
		}

		return Owner.RunState.Rng.Niche.NextInt(100) < 25 ? playCount + 1 : playCount;
	}

	public override Task AfterModifyingCardPlayCount(CardModel card)
	{
		if (Owner != null && card.Owner == Owner)
		{
			Flash();
		}

		return Task.CompletedTask;
	}
}

public sealed class SoulEaterRune : HextechRelicBase
{
	private int _debuffsThisCombat;
	private int _hpGainedThisCombat;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedDebuffsThisCombat
	{
		get => _debuffsThisCombat;
		set => _debuffsThisCombat = Math.Max(0, value);
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedHpGainedThisCombat
	{
		get => _hpGainedThisCombat;
		set => _hpGainedThisCombat = Math.Max(0, value);
	}

	public override Task BeforeCombatStart()
	{
		_debuffsThisCombat = 0;
		_hpGainedThisCombat = 0;
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		_debuffsThisCombat = 0;
		_hpGainedThisCombat = 0;
		return Task.CompletedTask;
	}

	public override async Task AfterPowerAmountChanged(PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
	{
		if (!TryGetOwnedEnemyDebuffTarget(power, amount, applier, out _))
		{
			return;
		}

		_debuffsThisCombat++;
		if (Owner == null || _hpGainedThisCombat >= 5 || _debuffsThisCombat % 5 != 0)
		{
			return;
		}

		_hpGainedThisCombat++;
		Flash();
		await CreatureCmd.GainMaxHp(Owner.Creature, 1m);
	}
}

public sealed class InfernalConduitRune : HextechRelicBase
{
	private int _pendingEnergy;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedPendingEnergy
	{
		get => _pendingEnergy;
		set => _pendingEnergy = Math.Max(0, value);
	}

	public override Task BeforeCombatStart()
	{
		_pendingEnergy = 0;
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		_pendingEnergy = 0;
		return Task.CompletedTask;
	}

	public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		if (Owner == null || cardPlay.Card.Owner != Owner || cardPlay.Target is not { Side: CombatSide.Enemy } enemy)
		{
			return;
		}

		await PowerCmd.Apply<HextechBurnPower>(enemy, 1m, Owner.Creature, cardPlay.Card);
	}

	public override Task BeforeTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)
	{
		if (Owner == null || side != Owner.Creature.Side || Owner.Creature.CombatState == null)
		{
			return Task.CompletedTask;
		}

		_pendingEnergy = Owner.Creature.CombatState.Enemies
			.Where(enemy => enemy.IsAlive)
			.Sum(enemy => Math.Max(0, enemy.GetPowerAmount<HextechBurnPower>()) / 5);
		return Task.CompletedTask;
	}

	public override async Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
	{
		if (player != Owner || _pendingEnergy <= 0)
		{
			return;
		}

		int energy = _pendingEnergy;
		_pendingEnergy = 0;
		Flash();
		await PlayerCmd.GainEnergy(energy, player);
	}
}

public sealed class DualWieldRune : HextechRelicBase
{
	public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount)
	{
		return IsOwnedAttack(card) ? playCount + 1 : playCount;
	}

	public override Task AfterModifyingCardPlayCount(CardModel card)
	{
		if (IsOwnedAttack(card))
		{
			Flash();
		}

		return Task.CompletedTask;
	}

	public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		return IsDamageFromOwner(dealer, cardSource) ? 0.6m : 1m;
	}
}

public sealed class GoliathRune : HextechRelicBase
{
	private int _baseMaxHp;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedBaseMaxHp
	{
		get => _baseMaxHp;
		set => _baseMaxHp = Math.Max(0, value);
	}

	public int BaseMaxHp
	{
		get => _baseMaxHp;
		set => _baseMaxHp = Math.Max(1, value);
	}

	public override bool HasUponPickupEffect => true;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("HpGainPercent", 0.4m),
		new DynamicVar("DamageMultiplier", 1.2m),
		new DynamicVar("Scale", 1.4m)
	];

	public override async Task AfterObtained()
	{
		if (Owner == null)
		{
			return;
		}

		EnsureBaseMaxHpInitialized(assumeAlreadyScaled: false);
		await CreatureCmd.SetMaxHp(Owner.Creature, BaseMaxHp);
		await CreatureCmd.Heal(Owner.Creature, Owner.Creature.MaxHp - Owner.Creature.CurrentHp);
		Grow();
	}

	public override Task AfterRoomEntered(AbstractRoom room)
	{
		if (Owner != null)
		{
			EnsureBaseMaxHpInitialized(assumeAlreadyScaled: true);
		}

		Grow();
		return Task.CompletedTask;
	}

	public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		return IsDamageFromOwner(dealer, cardSource) ? DynamicVars["DamageMultiplier"].BaseValue : 1m;
	}

	private void Grow()
	{
		if (Owner == null)
		{
			return;
		}

		NCombatRoom.Instance?.GetCreatureNode(Owner.Creature)?.SetDefaultScaleTo((float)DynamicVars["Scale"].BaseValue, 0f);
	}

	public void EnsureBaseMaxHpInitialized(bool assumeAlreadyScaled = true)
	{
		if (Owner != null && _baseMaxHp <= 0)
		{
			_baseMaxHp = assumeAlreadyScaled
				? Math.Max(1, FloorToInt(Owner.Creature.MaxHp / DynamicVars["Scale"].BaseValue))
				: Owner.Creature.MaxHp;
		}
	}

	public int GetScaledMaxHp()
	{
		return FloorToInt(BaseMaxHp * DynamicVars["Scale"].BaseValue);
	}
}

public sealed class DonationRune : HextechRelicBase
{
	public override bool HasUponPickupEffect => true;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new GoldVar(1000)
	];

	public override Task AfterObtained()
	{
		return PlayerCmd.GainGold(DynamicVars.Gold.BaseValue, Owner!);
	}
}

public sealed class HeavyHitterRune : HextechRelicBase
{
	public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		if (Owner == null || !IsDamageFromOwner(dealer, cardSource))
		{
			return 1m;
		}

		return 1m + Math.Min(30m, Math.Floor(Owner.Creature.MaxHp / 6m)) / 100m;
	}
}

public sealed class TwiceThriceRune : HextechRelicBase
{
	private int _attacksPlayedThisCombat;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedAttacksPlayedThisCombat
	{
		get => _attacksPlayedThisCombat;
		set => _attacksPlayedThisCombat = Math.Max(0, value);
	}

	public override Task BeforeCombatStart()
	{
		_attacksPlayedThisCombat = 0;
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		_attacksPlayedThisCombat = 0;
		return Task.CompletedTask;
	}

	public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount)
	{
		if (!IsOwnedAttack(card))
		{
			return playCount;
		}

		_attacksPlayedThisCombat++;
		return _attacksPlayedThisCombat % 3 == 0 ? playCount + 1 : playCount;
	}

	public override Task AfterModifyingCardPlayCount(CardModel card)
	{
		if (IsOwnedAttack(card))
		{
			Flash();
		}

		return Task.CompletedTask;
	}
}

public sealed class FirebrandRune : HextechRelicBase
{
	public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
	{
		if (Owner == null || target.Side != CombatSide.Enemy || !props.IsPoweredAttack() || !IsDamageFromOwner(dealer, cardSource))
		{
			return;
		}

		await PowerCmd.Apply<HextechBurnPower>(target, 2m, Owner.Creature, cardSource);
	}
}

public sealed class NightstalkingRune : HextechRelicBase
{
	public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature target, bool wasRemovalPrevented, float deathAnimLength)
	{
		if (Owner == null || wasRemovalPrevented || target.Side == Owner.Creature.Side)
		{
			return;
		}

		Flash();
		await PowerCmd.Apply<IntangiblePower>(Owner.Creature, 1m, Owner.Creature, null);
	}
}

public sealed class MasterOfDualityRune : HextechRelicBase
{
	public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		if (Owner == null || cardPlay.Card.Owner != Owner)
		{
			return;
		}

		if (cardPlay.Card.Type == CardType.Skill)
		{
			Flash();
			await PowerCmd.Apply<HextechTemporaryStrengthPower>(Owner.Creature, 1m, Owner.Creature, null);
		}
		else if (cardPlay.Card.Type == CardType.Attack)
		{
			Flash();
			await PowerCmd.Apply<HextechTemporaryDexterityPower>(Owner.Creature, 1m, Owner.Creature, null);
		}
	}
}

public sealed class BigStrengthRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<StrengthPower>(2m)
	];

	public override Task AfterRoomEntered(AbstractRoom room)
	{
		if (room is not CombatRoom || Owner == null)
		{
			return Task.CompletedTask;
		}

		Flash();
		return PowerCmd.Apply<StrengthPower>(Owner.Creature, DynamicVars.Strength.BaseValue, Owner.Creature, null);
	}
}

public sealed class HandOfBaronRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("DamageMultiplier", 1.2m),
		new DynamicVar("Shrink", 2m)
	];

	public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		return IsDamageFromOwner(dealer, cardSource) ? DynamicVars["DamageMultiplier"].BaseValue : 1m;
	}

	public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, CombatState combatState)
	{
		if (Owner == null || side != Owner.Creature.Side)
		{
			return;
		}

		Flash();
		await PowerCmd.Apply<ShrinkPower>(combatState.HittableEnemies, DynamicVars["Shrink"].BaseValue, Owner.Creature, null);
	}
}

public sealed class CantTouchThisRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<BufferPower>(3m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<BufferPower>()
	];

	public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		if (Owner == null
			|| cardPlay.Card.Owner != Owner
			|| cardPlay.Card.EnergyCost.CostsX
			|| cardPlay.Card.EnergyCost.GetAmountToSpend() < 3)
		{
			return;
		}

		Flash();
		await PowerCmd.Apply<BufferPower>(Owner.Creature, DynamicVars["BufferPower"].BaseValue, Owner.Creature, null);
	}
}

public sealed class TormentorRune : LimitedDebuffProcRelicBase
{
	private bool _applyingBurnProc;

	protected override int MaxProcsPerTurn => 5;

	public override async Task AfterPowerAmountChanged(PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
	{
		if (_applyingBurnProc)
		{
			return;
		}

		await base.AfterPowerAmountChanged(power, amount, applier, cardSource);
	}

	protected override async Task OnEnemyDebuffApplied(Creature target)
	{
		try
		{
			_applyingBurnProc = true;
			await PowerCmd.Apply<HextechBurnPower>(target, 1m, Owner!.Creature, null);
		}
		finally
		{
			_applyingBurnProc = false;
		}
	}
}

public sealed class AdamantRune : LimitedDebuffProcRelicBase
{
	protected override int MaxProcsPerTurn => 5;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new BlockVar(3m, ValueProp.Unpowered)
	];

	protected override Task OnEnemyDebuffApplied(Creature target)
	{
		return CreatureCmd.GainBlock(Owner!.Creature, DynamicVars.Block, null);
	}
}

public sealed class GetExcitedRune : HextechRelicBase
{
	private int _pendingEnergy;
	private int _pendingDraw;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedPendingEnergy
	{
		get => _pendingEnergy;
		set => _pendingEnergy = Math.Max(0, value);
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedPendingDraw
	{
		get => _pendingDraw;
		set => _pendingDraw = Math.Max(0, value);
	}

	public override Task BeforeCombatStart()
	{
		_pendingEnergy = 0;
		_pendingDraw = 0;
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		_pendingEnergy = 0;
		_pendingDraw = 0;
		return Task.CompletedTask;
	}

	public override Task AfterDeath(PlayerChoiceContext choiceContext, Creature target, bool wasRemovalPrevented, float deathAnimLength)
	{
		if (Owner == null || wasRemovalPrevented || target.Side == Owner.Creature.Side)
		{
			return Task.CompletedTask;
		}

		_pendingEnergy += 2;
		_pendingDraw += 2;
		Flash();
		return Task.CompletedTask;
	}

	public override async Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
	{
		if (player != Owner)
		{
			return;
		}

		int energy = _pendingEnergy;
		int draw = _pendingDraw;
		_pendingEnergy = 0;
		_pendingDraw = 0;
		if (energy > 0)
		{
			await PlayerCmd.GainEnergy(energy, player);
		}

		if (draw > 0)
		{
			await CardPileCmd.Draw(choiceContext, draw, player);
		}
	}
}

public sealed class QueenRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<FrailPower>(2m),
		new PowerVar<WeakPower>(2m),
		new PowerVar<VulnerablePower>(2m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<FrailPower>(),
		HoverTipFactory.FromPower<WeakPower>(),
		HoverTipFactory.FromPower<VulnerablePower>()
	];

	public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, CombatState combatState)
	{
		if (Owner == null || side != Owner.Creature.Side)
		{
			return;
		}

		Flash();
		await PowerCmd.Apply<FrailPower>(combatState.HittableEnemies, DynamicVars["FrailPower"].BaseValue, Owner.Creature, null);
		await PowerCmd.Apply<WeakPower>(combatState.HittableEnemies, DynamicVars.Weak.BaseValue, Owner.Creature, null);
		await PowerCmd.Apply<VulnerablePower>(combatState.HittableEnemies, DynamicVars.Vulnerable.BaseValue, Owner.Creature, null);
	}
}

public sealed class MountainSoulRune : HextechRelicBase
{
	private bool _tookUnblockedDamageSinceLastTurn;
	private bool _hasPreviousTurn;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public bool SavedTookUnblockedDamageSinceLastTurn
	{
		get => _tookUnblockedDamageSinceLastTurn;
		set => _tookUnblockedDamageSinceLastTurn = value;
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public bool SavedHasPreviousTurn
	{
		get => _hasPreviousTurn;
		set => _hasPreviousTurn = value;
	}

	public override Task BeforeCombatStart()
	{
		_tookUnblockedDamageSinceLastTurn = false;
		_hasPreviousTurn = false;
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		_tookUnblockedDamageSinceLastTurn = false;
		_hasPreviousTurn = false;
		return Task.CompletedTask;
	}

	public override Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		if (Owner != null && target == Owner.Creature && result.UnblockedDamage > 0)
		{
			_tookUnblockedDamageSinceLastTurn = true;
		}

		return Task.CompletedTask;
	}

	public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (player != Owner)
		{
			return;
		}

		if (_hasPreviousTurn && !_tookUnblockedDamageSinceLastTurn)
		{
			Flash();
			decimal block = Math.Max(1, FloorToInt(player.Creature.MaxHp * 0.1m));
			await CreatureCmd.GainBlock(player.Creature, block, ValueProp.Unpowered, null);
		}

		_hasPreviousTurn = true;
		_tookUnblockedDamageSinceLastTurn = false;
	}
}

public sealed class SwiftAndSafeRune : HextechRelicBase
{
	private int _cardsDrawnThisCombat;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedCardsDrawnThisCombat
	{
		get => _cardsDrawnThisCombat;
		set
		{
			_cardsDrawnThisCombat = Math.Max(0, value);
			InvokeDisplayAmountChanged();
		}
	}

	public override bool ShowCounter => CombatManager.Instance?.IsInProgress == true && !IsCanonical;

	public override int DisplayAmount
	{
		get
		{
			if (IsCanonical)
			{
				return 0;
			}

			int remainder = _cardsDrawnThisCombat % 10;
			return remainder == 0 ? 10 : 10 - remainder;
		}
	}

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("CardsNeeded", 10m),
		new PowerVar<ArtifactPower>(1m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<ArtifactPower>()
	];

	public override Task BeforeCombatStart()
	{
		_cardsDrawnThisCombat = 0;
		InvokeDisplayAmountChanged();
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		_cardsDrawnThisCombat = 0;
		InvokeDisplayAmountChanged();
		return Task.CompletedTask;
	}

	public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
	{
		if (card.Owner != Owner)
		{
			return;
		}

		_cardsDrawnThisCombat++;
		InvokeDisplayAmountChanged();
		if (Owner == null || _cardsDrawnThisCombat % 10 != 0)
		{
			return;
		}

		Flash();
		await PowerCmd.Apply<ArtifactPower>(Owner.Creature, DynamicVars["ArtifactPower"].BaseValue, Owner.Creature, null);
	}
}

public sealed class SacrificeRune : HextechRelicBase
{
	private int _countThisCombat;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedCountThisCombat
	{
		get => _countThisCombat;
		set
		{
			_countThisCombat = Math.Max(0, value);
			InvokeDisplayAmountChanged();
		}
	}

	public override bool ShowCounter => CombatManager.Instance?.IsInProgress == true && !IsCanonical;

	public override int DisplayAmount => !IsCanonical ? _countThisCombat : 0;

	public override Task BeforeCombatStart()
	{
		_countThisCombat = 0;
		InvokeDisplayAmountChanged();
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		if (Owner != null && _countThisCombat > 0)
		{
			room.AddExtraReward(Owner, new GoldReward(_countThisCombat, Owner));
		}

		_countThisCombat = 0;
		InvokeDisplayAmountChanged();
		return Task.CompletedTask;
	}

	public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (player != Owner || player.Creature.CombatState == null)
		{
			return Task.CompletedTask;
		}

		_countThisCombat += player.Creature.CombatState.Enemies.Count(static enemy => enemy.IsAlive && enemy.Side == CombatSide.Enemy) * 2;
		InvokeDisplayAmountChanged();
		return Task.CompletedTask;
	}

}

public sealed class GoldrendRune : HextechRelicBase
{
	private int _countThisCombat;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedCountThisCombat
	{
		get => _countThisCombat;
		set
		{
			_countThisCombat = Math.Max(0, value);
			InvokeDisplayAmountChanged();
		}
	}

	public override bool ShowCounter => CombatManager.Instance?.IsInProgress == true && !IsCanonical;

	public override int DisplayAmount => !IsCanonical ? _countThisCombat : 0;

	public override Task BeforeCombatStart()
	{
		_countThisCombat = 0;
		InvokeDisplayAmountChanged();
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		if (Owner != null && _countThisCombat > 0)
		{
			room.AddExtraReward(Owner, new GoldReward(_countThisCombat, Owner));
		}

		_countThisCombat = 0;
		InvokeDisplayAmountChanged();
		return Task.CompletedTask;
	}

	public override Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
	{
		if (target.Side != CombatSide.Enemy || result.TotalDamage <= 0 || !IsDamageFromOwner(dealer, cardSource))
		{
			return Task.CompletedTask;
		}

		_countThisCombat++;
		InvokeDisplayAmountChanged();
		return Task.CompletedTask;
	}

}

public sealed class ProtectiveVeilRune : HextechRelicBase
{
	private int _turnsThisCombat;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedTurnsThisCombat
	{
		get => _turnsThisCombat;
		set => _turnsThisCombat = Math.Max(0, value);
	}

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<ArtifactPower>(1m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<ArtifactPower>()
	];

	public override Task AfterRoomEntered(AbstractRoom room)
	{
		if (room is not CombatRoom || Owner == null)
		{
			return Task.CompletedTask;
		}

		_turnsThisCombat = 0;
		Flash();
		return PowerCmd.Apply<ArtifactPower>(Owner.Creature, DynamicVars["ArtifactPower"].BaseValue, Owner.Creature, null);
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		_turnsThisCombat = 0;
		return Task.CompletedTask;
	}

	public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, CombatState combatState)
	{
		if (Owner == null || side != Owner.Creature.Side)
		{
			return;
		}

		_turnsThisCombat++;
		if (_turnsThisCombat % 2 != 0)
		{
			return;
		}

		Flash();
		await PowerCmd.Apply<ArtifactPower>(Owner.Creature, DynamicVars["ArtifactPower"].BaseValue, Owner.Creature, null);
	}
}

public sealed class RepulsorRune : HextechRelicBase
{
	private bool _pendingTrigger;
	private bool _triggeredThisCombat;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public bool SavedPendingTrigger
	{
		get => _pendingTrigger;
		set => _pendingTrigger = value;
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public bool SavedTriggeredThisCombat
	{
		get => _triggeredThisCombat;
		set => _triggeredThisCombat = value;
	}

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("ThresholdPercent", 50m),
		new PowerVar<SlipperyPower>(2m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<SlipperyPower>()
	];

	public override Task BeforeCombatStart()
	{
		_pendingTrigger = false;
		_triggeredThisCombat = false;
		Status = RelicStatus.Normal;
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		_pendingTrigger = false;
		_triggeredThisCombat = false;
		Status = RelicStatus.Normal;
		return Task.CompletedTask;
	}

	public override Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		if (Owner == null
			|| target != Owner.Creature
			|| result.UnblockedDamage <= 0
			|| _triggeredThisCombat
			|| _pendingTrigger
			|| target.CurrentHp >= target.MaxHp * 0.5m)
		{
			return Task.CompletedTask;
		}

		_pendingTrigger = true;
		_triggeredThisCombat = true;
		Status = RelicStatus.Active;
		Flash();
		return Task.CompletedTask;
	}

	public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (player != Owner || !_pendingTrigger)
		{
			return;
		}

		_pendingTrigger = false;
		Status = RelicStatus.Normal;
		Flash();
		await PowerCmd.Apply<SlipperyPower>(player.Creature, DynamicVars["SlipperyPower"].BaseValue, player.Creature, null);
	}
}

public sealed class ThornmailRune : HextechRelicBase
{
	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<ThornsPower>()
	];

	public override Task AfterRoomEntered(AbstractRoom room)
	{
		if (room is not CombatRoom || Owner == null)
		{
			return Task.CompletedTask;
		}

		Flash();
		int thorns = 2 + Math.Min(3, FloorToInt(Owner.Creature.MaxHp / 40m));
		return PowerCmd.Apply<ThornsPower>(Owner.Creature, thorns, Owner.Creature, null);
	}
}

public sealed class DawnbringersResolveRune : HextechRelicBase
{
	private bool _triggeredThisCombat;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public bool SavedTriggeredThisCombat
	{
		get => _triggeredThisCombat;
		set => _triggeredThisCombat = value;
	}

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("ThresholdPercent", 50m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<RegenPower>()
	];

	public override Task BeforeCombatStart()
	{
		_triggeredThisCombat = false;
		Status = RelicStatus.Normal;
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		_triggeredThisCombat = false;
		Status = RelicStatus.Normal;
		return Task.CompletedTask;
	}

	public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		if (Owner == null
			|| target != Owner.Creature
			|| result.UnblockedDamage <= 0
			|| _triggeredThisCombat
			|| target.CurrentHp >= target.MaxHp * 0.5m)
		{
			return;
		}

		_triggeredThisCombat = true;
		Status = RelicStatus.Active;
		Flash();
		int regen = Math.Max(1, FloorToInt(Owner.Creature.MaxHp * 0.1m));
		await PowerCmd.Apply<RegenPower>(Owner.Creature, regen, Owner.Creature, null);
	}
}

public sealed class ShrinkRayRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<ShrinkPower>(1m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<ShrinkPower>()
	];

	public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
	{
		if (Owner == null
			|| target.Side != CombatSide.Enemy
			|| result.UnblockedDamage <= 0
			|| !IsDamageFromOwner(dealer, cardSource))
		{
			return;
		}

		Flash([target]);
		await PowerCmd.Apply<ShrinkPower>(target, DynamicVars["ShrinkPower"].BaseValue, Owner.Creature, cardSource);
	}
}

public sealed class ZealotRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("RelicsNeeded", 5m),
		new CardsVar(1)
	];

	public override decimal ModifyHandDraw(Player player, decimal count)
	{
		if (player != Owner || player.Creature.CombatState?.RoundNumber > 1)
		{
			return count;
		}

		return count + Math.Floor(player.Relics.Count / 5m);
	}
}

public sealed class ServantMasterRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new SummonVar(4m)
	];

	public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (player != Owner)
		{
			return;
		}

		Flash();
		await OstyCmd.Summon(choiceContext, player, DynamicVars.Summon.BaseValue, this);
	}
}

public sealed class TranscendentEvilRune : HextechRelicBase
{
	private int _stacks;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedStacks
	{
		get => _stacks;
		set
		{
			_stacks = Math.Max(0, value);
			InvokeDisplayAmountChanged();
		}
	}

	public override bool ShowCounter => true;

	public override int DisplayAmount => !IsCanonical ? _stacks : 0;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("StacksPerBonus", 4m),
		new PowerVar<StrengthPower>(1m),
		new PowerVar<FocusPower>(1m)
	];

	public override Task AfterCombatVictory(CombatRoom room)
	{
		if (Owner == null || Owner.Creature.IsDead)
		{
			return Task.CompletedTask;
		}

		SavedStacks++;
		Flash(Array.Empty<Creature>());
		return Task.CompletedTask;
	}

	public override async Task AfterRoomEntered(AbstractRoom room)
	{
		if (room is not CombatRoom || Owner == null)
		{
			return;
		}

		int bonus = FloorToInt(_stacks / 4m);
		if (bonus <= 0)
		{
			return;
		}

		Flash();
		await PowerCmd.Apply<StrengthPower>(Owner.Creature, bonus, Owner.Creature, null);
		await PowerCmd.Apply<FocusPower>(Owner.Creature, bonus, Owner.Creature, null);
	}
}

public sealed class WizardlyThinkingRune : HextechRelicBase
{
	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<FocusPower>()
	];

	public override bool IsAvailableForPlayer(Player player)
	{
		return IsDefectPlayer(player);
	}

	public override async Task AfterRoomEntered(AbstractRoom room)
	{
		if (room is not CombatRoom || Owner == null || !IsDefectOwner)
		{
			return;
		}

		int focus = Owner.RunState.CurrentActIndex + 1;
		Flash();
		await PowerCmd.Apply<FocusPower>(Owner.Creature, focus, Owner.Creature, null);
	}
}

public sealed class UltimateUnstoppableRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
		[
			new DynamicVar("MinCost", 3m),
			new PowerVar<ArtifactPower>(3m)
		];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<ArtifactPower>()
	];

	public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		if (Owner == null || !IsOwnedNonXCardWithCostAtLeast(cardPlay.Card, DynamicVars["MinCost"].BaseValue))
		{
			return;
		}

		Flash();
		await PowerCmd.Apply<ArtifactPower>(Owner.Creature, DynamicVars["ArtifactPower"].BaseValue, Owner.Creature, cardPlay.Card);
	}
}

public sealed class FinalFormRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("MinCost", 3m),
		new DynamicVar("BlockPercent", 0.2m),
		new CardsVar(3)
	];

	public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		if (Owner == null || !IsOwnedNonXCardWithCostAtLeast(cardPlay.Card, DynamicVars["MinCost"].BaseValue))
		{
			return;
		}

		int block = Math.Max(1, FloorToInt(Owner.Creature.MaxHp * DynamicVars["BlockPercent"].BaseValue));
		Flash();
		await CreatureCmd.GainBlock(Owner.Creature, block, ValueProp.Unpowered, cardPlay, fast: false);
		await CardPileCmd.Draw(context, 3m, Owner, fromHandDraw: false);
	}
}

public sealed class HailToTheKingRune : HextechRelicBase
{
	private bool _triggeredThisRun;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public bool SavedTriggeredThisRun
	{
		get => _triggeredThisRun;
		set => _triggeredThisRun = value;
	}

	public override async Task AfterCombatVictory(CombatRoom room)
	{
		if (Owner == null
			|| _triggeredThisRun
			|| room.RoomType != RoomType.Boss
			|| Owner.Creature.IsDead)
		{
			return;
		}

		_triggeredThisRun = true;
		Flash(Array.Empty<Creature>());
		room.AddExtraReward(Owner, new RelicReward(RelicRarity.Rare, Owner));
		await HextechRuneGrantHelper.ObtainRandomRunes(Owner, ModInfo.GetPlayerRuneTypesForRarity(HextechRarityTier.Prismatic), 1);
	}
}

public sealed class ArcanePunchRune : HextechRelicBase
{
	private int _attacksPlayedThisCombat;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedAttacksPlayedThisCombat
	{
		get => _attacksPlayedThisCombat;
		set
		{
			_attacksPlayedThisCombat = Math.Max(0, value);
			InvokeDisplayAmountChanged();
		}
	}

	public override bool ShowCounter => CombatManager.Instance?.IsInProgress == true && !IsCanonical;

	public override int DisplayAmount
	{
		get
		{
			if (IsCanonical)
			{
				return 0;
			}

			int remainder = _attacksPlayedThisCombat % 2;
			return remainder == 0 ? 2 : 1;
		}
	}

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("AttacksPerEnergy", 2m),
		new EnergyVar(1)
	];

	public override Task BeforeCombatStart()
	{
		_attacksPlayedThisCombat = 0;
		InvokeDisplayAmountChanged();
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		_attacksPlayedThisCombat = 0;
		InvokeDisplayAmountChanged();
		return Task.CompletedTask;
	}

	public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		if (!IsOwnedAttack(cardPlay.Card))
		{
			return;
		}

		_attacksPlayedThisCombat++;
		InvokeDisplayAmountChanged();
		if (_attacksPlayedThisCombat % 2 != 0)
		{
			return;
		}

		Flash();
		await PlayerCmd.GainEnergy(1m, Owner!);
	}
}

public sealed class PandorasBoxRune : HextechRelicBase
{
	public override bool HasUponPickupEffect => true;

	public override async Task AfterObtained()
	{
		if (Owner == null)
		{
			return;
		}

		Player player = Owner;
		Flash();
		await HextechRuneGrantHelper.ReplaceOwnedHextechRunesWithRandomRunes(
			player,
			ModInfo.GetPlayerRuneTypesForRarity(HextechRarityTier.Prismatic),
			new HashSet<ModelId> { ModelDb.GetId<PandorasBoxRune>() });
	}
}

public sealed class TapDanceRune : HextechRelicBase
{
	private int _pendingDraw;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedPendingDraw
	{
		get => _pendingDraw;
		set
		{
			_pendingDraw = Math.Max(0, value);
			InvokeDisplayAmountChanged();
		}
	}

	public override bool ShowCounter => CombatManager.Instance?.IsInProgress == true && !IsCanonical;

	public override int DisplayAmount => !IsCanonical ? _pendingDraw : 0;

	public override Task BeforeCombatStart()
	{
		_pendingDraw = 0;
		InvokeDisplayAmountChanged();
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		_pendingDraw = 0;
		InvokeDisplayAmountChanged();
		return Task.CompletedTask;
	}

	public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		if (!IsOwnedAttack(cardPlay.Card))
		{
			return Task.CompletedTask;
		}

		_pendingDraw++;
		InvokeDisplayAmountChanged();
		return Task.CompletedTask;
	}

	public override async Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
	{
		if (player != Owner || _pendingDraw <= 0)
		{
			return;
		}

		int draw = _pendingDraw;
		_pendingDraw = 0;
		InvokeDisplayAmountChanged();
		Flash();
		await CardPileCmd.Draw(choiceContext, draw, player);
	}
}

public sealed class UltimateRefreshRune : HextechRelicBase
{
	private bool _triggeredThisTurn;
	private bool _triggeredLastPlay;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public bool SavedTriggeredThisTurn
	{
		get => _triggeredThisTurn;
		set => _triggeredThisTurn = value;
	}

	public override Task BeforeCombatStart()
	{
		_triggeredThisTurn = false;
		_triggeredLastPlay = false;
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		_triggeredThisTurn = false;
		_triggeredLastPlay = false;
		return Task.CompletedTask;
	}

	public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, CombatState combatState)
	{
		if (Owner != null && side == Owner.Creature.Side)
		{
			_triggeredThisTurn = false;
			_triggeredLastPlay = false;
		}

		return Task.CompletedTask;
	}

	public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount)
	{
		_triggeredLastPlay = false;
		if (_triggeredThisTurn || !IsOwnedNonXCardWithCostAtLeast(card, 3m))
		{
			return playCount;
		}

		_triggeredThisTurn = true;
		_triggeredLastPlay = true;
		return playCount + 1;
	}

	public override Task AfterModifyingCardPlayCount(CardModel card)
	{
		if (_triggeredLastPlay && IsOwnedNonXCardWithCostAtLeast(card, 3m))
		{
			Flash();
			_triggeredLastPlay = false;
		}

		return Task.CompletedTask;
	}
}

internal static class HextechRuneGrantHelper
{
	private static readonly IReadOnlySet<Type> ExcludedRewardRuneTypes = new HashSet<Type>
	{
		typeof(TransmuteChaosRune),
		typeof(TransmutePrismaticRune),
		typeof(TransmuteGoldRune)
	};

	public static async Task ObtainRandomRunes(Player player, IEnumerable<Type> candidateTypes, int count)
	{
		await ObtainRandomRunes(player, candidateTypes, count, blockedIds: null);
	}

	public static async Task ObtainRandomRunes(Player player, IEnumerable<Type> candidateTypes, int count, IReadOnlySet<ModelId>? blockedIds)
	{
		List<Type> pool = candidateTypes
			.Where(type => !ExcludedRewardRuneTypes.Contains(type))
			.Where(type => blockedIds == null || !blockedIds.Contains(ModelDb.GetId(type)))
			.Where(type =>
			{
				ModelId id = ModelDb.GetId(type);
				if (player.Relics.Any(relic => (relic.CanonicalInstance?.Id ?? relic.Id) == id))
				{
					return false;
				}

				RelicModel relic = ModelDb.GetById<RelicModel>(id);
				return ModInfo.IsAvailableForPlayer(relic, player);
			})
			.ToList();
		if (pool.Count == 0)
		{
			return;
		}

		int picks = Math.Min(count, pool.Count);
		for (int i = 0; i < picks; i++)
		{
			int index = player.RunState.Rng.Niche.NextInt(pool.Count);
			Type runeType = pool[index];
			pool.RemoveAt(index);

			RelicModel relic = ModelDb.GetById<RelicModel>(ModelDb.GetId(runeType)).ToMutable();
			SaveManager.Instance.MarkRelicAsSeen(relic);
			await RelicCmd.Obtain(relic, player);
		}
	}

	public static async Task ReplaceOwnedHextechRunesWithRandomRunes(Player player, IEnumerable<Type> candidateTypes, IReadOnlySet<ModelId>? blockedIds = null)
	{
		List<RelicModel> ownedRunes = player.Relics.Where(ModInfo.IsHextechRelic).ToList();
		if (ownedRunes.Count == 0)
		{
			return;
		}

		foreach (RelicModel relic in ownedRunes)
		{
			await RelicCmd.Remove(relic);
		}

		await ObtainRandomRunes(player, candidateTypes, ownedRunes.Count, blockedIds);
	}

	public static async Task ConsumeAndObtainRandomRunes(RelicModel consumedRune, Player player, IEnumerable<Type> candidateTypes, int count)
	{
		await RelicCmd.Remove(consumedRune);
		await ObtainRandomRunes(player, candidateTypes, count);
	}
}

public sealed class FirstAidKitRune : HextechRelicBase
{
	public override decimal ModifyBlockMultiplicative(Creature target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay)
	{
		return target == Owner?.Creature ? 1.2m : 1m;
	}
}

public sealed class HomeguardRune : HextechRelicBase
{
	private bool _tookUnblockedDamageSinceLastTurn;
	private bool _hasPreviousTurn;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public bool SavedTookUnblockedDamageSinceLastTurn
	{
		get => _tookUnblockedDamageSinceLastTurn;
		set => _tookUnblockedDamageSinceLastTurn = value;
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public bool SavedHasPreviousTurn
	{
		get => _hasPreviousTurn;
		set => _hasPreviousTurn = value;
	}

	public override Task BeforeCombatStart()
	{
		_tookUnblockedDamageSinceLastTurn = false;
		_hasPreviousTurn = false;
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		_tookUnblockedDamageSinceLastTurn = false;
		_hasPreviousTurn = false;
		return Task.CompletedTask;
	}

	public override Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		if (Owner != null && target == Owner.Creature && result.UnblockedDamage > 0)
		{
			_tookUnblockedDamageSinceLastTurn = true;
		}

		return Task.CompletedTask;
	}

	public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (player != Owner)
		{
			return;
		}

		if (_hasPreviousTurn && !_tookUnblockedDamageSinceLastTurn)
		{
			Flash();
			await CardPileCmd.Draw(choiceContext, 2m, player);
		}

		_hasPreviousTurn = true;
		_tookUnblockedDamageSinceLastTurn = false;
	}
}

public sealed class LightEmUpRune : HextechRelicBase
{
	private int _attacksPlayedThisCombat;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedAttacksPlayedThisCombat
	{
		get => _attacksPlayedThisCombat;
		set => _attacksPlayedThisCombat = Math.Max(0, value);
	}

	public override Task BeforeCombatStart()
	{
		_attacksPlayedThisCombat = 0;
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		_attacksPlayedThisCombat = 0;
		return Task.CompletedTask;
	}

	public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount)
	{
		if (!IsOwnedAttack(card))
		{
			return playCount;
		}

		_attacksPlayedThisCombat++;
		return _attacksPlayedThisCombat % 4 == 0 ? playCount + 1 : playCount;
	}

	public override Task AfterModifyingCardPlayCount(CardModel card)
	{
		if (IsOwnedAttack(card))
		{
			Flash();
		}

		return Task.CompletedTask;
	}
}

public sealed class HolyFireRune : HextechRelicBase
{
}

public sealed class ShrinkEngineRune : HextechRelicBase
{
	private int _stacks;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedStacks
	{
		get => _stacks;
		set
		{
			_stacks = Math.Max(0, value);
			InvokeDisplayAmountChanged();
		}
	}

	public override bool ShowCounter => true;

	public override int DisplayAmount => !IsCanonical ? _stacks : 0;

	public override Task AfterObtained()
	{
		Shrink();
		return Task.CompletedTask;
	}

	public override Task AfterRoomEntered(AbstractRoom room)
	{
		Shrink();
		return Task.CompletedTask;
	}

	public override Task AfterCombatVictory(CombatRoom room)
	{
		if (Owner == null || Owner.Creature.IsDead)
		{
			return Task.CompletedTask;
		}

		SavedStacks++;
		Flash(Array.Empty<Creature>());
		Shrink();
		return Task.CompletedTask;
	}

	public override decimal ModifyHandDraw(Player player, decimal count)
	{
		return player == Owner ? count + FloorToInt(_stacks / 4m) : count;
	}

	public override decimal ModifyMaxEnergy(Player player, decimal amount)
	{
		return player == Owner ? amount + FloorToInt(_stacks / 8m) : amount;
	}

	private void Shrink()
	{
		if (Owner == null)
		{
			return;
		}

		float scale = Math.Max(0.2f, 1f - _stacks * 0.02f);
		NCombatRoom.Instance?.GetCreatureNode(Owner.Creature)?.SetDefaultScaleTo(scale, 0f);
	}
}

public sealed class BackToBasicsRune : HextechRelicBase
{
	public override decimal ModifyBlockMultiplicative(Creature target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay)
	{
		return target == Owner?.Creature ? 1.3m : 1m;
	}

	public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		return IsDamageFromOwner(dealer, cardSource) ? 1.3m : 1m;
	}
}

public sealed class DrawYourSwordRune : HextechRelicBase
{
	public override bool HasUponPickupEffect => true;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
		[
			new DynamicVar("HpGainPercent", 0.3m),
			new PowerVar<StrengthPower>(3m),
			new PowerVar<DexterityPower>(3m)
		];

	public override async Task AfterObtained()
	{
		if (Owner == null)
		{
			return;
		}

		int hpGain = Math.Max(1, FloorToInt(Owner.Creature.MaxHp * DynamicVars["HpGainPercent"].BaseValue));
		await CreatureCmd.GainMaxHp(Owner.Creature, hpGain);
	}

	public override decimal ModifyMaxEnergy(Player player, decimal amount)
	{
		return player == Owner ? Math.Max(0m, amount - 1m) : amount;
	}

	public override async Task AfterRoomEntered(AbstractRoom room)
	{
		if (room is not CombatRoom || Owner == null)
		{
			return;
		}

		Flash();
		await PowerCmd.Apply<StrengthPower>(Owner.Creature, DynamicVars.Strength.BaseValue, Owner.Creature, null);
		await PowerCmd.Apply<DexterityPower>(Owner.Creature, DynamicVars.Dexterity.BaseValue, Owner.Creature, null);
	}
}

public sealed class FeelTheBurnRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<WeakPower>(2m),
		new PowerVar<VulnerablePower>(2m),
		new DynamicVar("Burn", 5m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<WeakPower>(),
		HoverTipFactory.FromPower<VulnerablePower>()
	];

	public override async Task AfterPotionUsed(PotionModel potion, Creature? target)
	{
		if (Owner?.Creature?.CombatState == null)
		{
			return;
		}

		List<Creature> enemies = Owner.Creature.CombatState.Enemies.Where(static enemy => enemy.IsAlive).ToList();
		if (enemies.Count == 0)
		{
			return;
		}

		Flash(enemies);
		await PowerCmd.Apply<WeakPower>(enemies, DynamicVars.Weak.BaseValue, Owner.Creature, null);
		await PowerCmd.Apply<VulnerablePower>(enemies, DynamicVars.Vulnerable.BaseValue, Owner.Creature, null);
		await PowerCmd.Apply<HextechBurnPower>(enemies, DynamicVars["Burn"].BaseValue, Owner.Creature, null);
	}
}

public sealed class TransmuteChaosRune : HextechRelicBase
{
	public override bool HasUponPickupEffect => true;

	public override async Task AfterObtained()
	{
		if (Owner == null)
		{
			return;
		}

		Player player = Owner;
		Flash();
		await HextechRuneGrantHelper.ConsumeAndObtainRandomRunes(this, player, ModInfo.GetAllRuneTypes(), 2);
	}
}

public sealed class TransmutePrismaticRune : HextechRelicBase
{
	public override bool HasUponPickupEffect => true;

	public override async Task AfterObtained()
	{
		if (Owner == null)
		{
			return;
		}

		Player player = Owner;
		Flash();
		await HextechRuneGrantHelper.ConsumeAndObtainRandomRunes(this, player, ModInfo.GetPlayerRuneTypesForRarity(HextechRarityTier.Prismatic), 1);
	}
}

public sealed class TransmuteGoldRune : HextechRelicBase
{
	public override bool HasUponPickupEffect => true;

	public override async Task AfterObtained()
	{
		if (Owner == null)
		{
			return;
		}

		Player player = Owner;
		Flash();
		await HextechRuneGrantHelper.ConsumeAndObtainRandomRunes(this, player, ModInfo.GetPlayerRuneTypesForRarity(HextechRarityTier.Gold), 1);
	}
}
