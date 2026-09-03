using HextechRunes;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HextechRunesSponsorPack;

public sealed class RegretRune : HextechRelicBase
{
	private bool _pendingPlayerRevive;
	private bool _freeCardsUntilOwnerTurnEnd;
	private int _revivesUsed;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedDamageBonusPercent
	{
		get => 0;
		set
		{
			// Legacy save compatibility: the reworked rune no longer has permanent damage scaling.
		}
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedRevivesUsed
	{
		get => _revivesUsed;
		set
		{
			_revivesUsed = Math.Max(0, value);
			InvokeDisplayAmountChanged();
		}
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public bool SavedFreeCardsUntilOwnerTurnEnd
	{
		get => _freeCardsUntilOwnerTurnEnd;
		set => _freeCardsUntilOwnerTurnEnd = value;
	}

	public override bool ShowCounter => !IsCanonical;

	public override int DisplayAmount => Math.Max(0, DynamicVars["MaxRevives"].IntValue - _revivesUsed);

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<StrengthPower>(2m),
		new PowerVar<DexterityPower>(2m),
		new DynamicVar("ReviveHpPercent", 30m),
		new DynamicVar("MaxRevives", 7m),
		new PowerVar<WeakPower>(2m),
		new PowerVar<VulnerablePower>(2m),
		new PowerVar<IntangiblePower>(1m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<StrengthPower>(),
		HoverTipFactory.FromPower<DexterityPower>(),
		HoverTipFactory.FromPower<WeakPower>(),
		HoverTipFactory.FromPower<VulnerablePower>(),
		HoverTipFactory.FromPower<IntangiblePower>()
	];

	public override async Task BeforeCombatStart()
	{
		_pendingPlayerRevive = false;
		_freeCardsUntilOwnerTurnEnd = false;
		if (Owner == null || Owner.Creature.IsDead)
		{
			return;
		}

		Flash();
		await PowerCmd.Apply<StrengthPower>(Owner.Creature, DynamicVars.Strength.BaseValue, Owner.Creature, null);
		await PowerCmd.Apply<DexterityPower>(Owner.Creature, DynamicVars.Dexterity.BaseValue, Owner.Creature, null);
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		_pendingPlayerRevive = false;
		_freeCardsUntilOwnerTurnEnd = false;
		return Task.CompletedTask;
	}

	public override Task BeforeDeath(Creature creature)
	{
		if (Owner == null
			|| creature != Owner.Creature
			|| _pendingPlayerRevive
			|| _revivesUsed >= DynamicVars["MaxRevives"].IntValue)
		{
			return Task.CompletedTask;
		}

		_pendingPlayerRevive = true;
		return Task.CompletedTask;
	}

	public override bool ShouldDie(Creature creature)
	{
		if (Owner == null)
		{
			return true;
		}

		return creature != Owner.Creature || !_pendingPlayerRevive;
	}

	public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature target, bool wasRemovalPrevented, float deathAnimLength)
	{
		if (Owner == null || target != Owner.Creature || !wasRemovalPrevented || !_pendingPlayerRevive)
		{
			return;
		}

		_pendingPlayerRevive = false;
		SavedRevivesUsed++;
		_freeCardsUntilOwnerTurnEnd = true;
		int reviveHp = Math.Max(1, FloorToInt(Owner.Creature.MaxHp * DynamicVars["ReviveHpPercent"].BaseValue / 100m));
		Flash([Owner.Creature]);
		await CreatureCmd.SetCurrentHp(Owner.Creature, reviveHp);
		await ApplyReviveRewards(choiceContext);
	}

	public override Task BeforeTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)
	{
		if (Owner != null && side == Owner.Creature.Side && _freeCardsUntilOwnerTurnEnd)
		{
			_freeCardsUntilOwnerTurnEnd = false;
		}

		return Task.CompletedTask;
	}

	public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
	{
		modifiedCost = originalCost;
		if (!ShouldMakeCardFree(card))
		{
			return false;
		}

		modifiedCost = 0m;
		return true;
	}

	public override bool TryModifyStarCost(CardModel card, decimal originalCost, out decimal modifiedCost)
	{
		modifiedCost = originalCost;
		if (!ShouldMakeCardFree(card))
		{
			return false;
		}

		modifiedCost = 0m;
		return true;
	}

	private async Task ApplyReviveRewards(PlayerChoiceContext choiceContext)
	{
		if (Owner == null)
		{
			return;
		}

		if (Owner.Creature.CombatState is HextechCombatState combatState)
		{
			await PowerCmd.Apply<WeakPower>(combatState.HittableEnemies, DynamicVars.Weak.BaseValue, Owner.Creature, null);
			await PowerCmd.Apply<VulnerablePower>(combatState.HittableEnemies, DynamicVars.Vulnerable.BaseValue, Owner.Creature, null);
		}

		await PowerCmd.Apply<IntangiblePower>(Owner.Creature, DynamicVars["IntangiblePower"].BaseValue, Owner.Creature, null);
		await DrawUntilHandFull(choiceContext);
	}

	private Task DrawUntilHandFull(PlayerChoiceContext choiceContext)
	{
		if (Owner == null || Owner.PlayerCombatState == null || Owner.Creature.IsDead)
		{
			return Task.CompletedTask;
		}

		int cardsToDraw = Math.Max(0, 10 - PileType.Hand.GetPile(Owner).Cards.Count);
		return cardsToDraw > 0
			? CardPileCmd.Draw(choiceContext, cardsToDraw, Owner, fromHandDraw: false)
			: Task.CompletedTask;
	}

	private bool ShouldMakeCardFree(CardModel card)
	{
		return _freeCardsUntilOwnerTurnEnd
			&& Owner != null
			&& !Owner.Creature.IsDead
			&& card.Owner == Owner
			&& card.Pile?.Type is PileType.Hand or PileType.Play;
	}
}
