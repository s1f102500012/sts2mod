namespace HextechRunes;

public sealed class SnailFormRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("InitialSlow", HextechPlayerSlowPower.LegacySnailCombatStartAmount),
		new DynamicVar("TurnStartSlow", HextechPlayerSlowPower.LegacySnailCombatStartAmount),
		new DynamicVar("CardSlowGain", HextechPlayerSlowPower.CardPlaySlowIncrease)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<HextechPlayerSlowPower>()
	];

	public override async Task BeforeCombatStart()
	{
		if (Owner == null || Owner.Creature.IsDead)
		{
			return;
		}

		Flash();
		await HextechPowerCmdCompat.Apply<HextechPlayerSlowPower>(Owner.Creature, DynamicVars["InitialSlow"].BaseValue, Owner.Creature, null);
	}

	public override async Task AfterSideTurnStart(CombatSide side, HextechCombatState combatState)
	{
		if (Owner == null || side != Owner.Creature.Side || Owner.Creature.IsDead)
		{
			return;
		}

		decimal current = Owner.Creature.GetPowerAmount<HextechPlayerSlowPower>();
		decimal target = DynamicVars["TurnStartSlow"].BaseValue;
		decimal delta = target - current;
		if (delta == 0m)
		{
			return;
		}

		Flash();
		await HextechPowerCmdCompat.Apply<HextechPlayerSlowPower>(Owner.Creature, delta, Owner.Creature, null);
	}

	public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		if (Owner == null || cardPlay.Card.Owner?.Creature != Owner.Creature || Owner.Creature.IsDead)
		{
			return;
		}

		await HextechPowerCmdCompat.Apply<HextechPlayerSlowPower>(
			context,
			Owner.Creature,
			DynamicVars["CardSlowGain"].BaseValue,
			Owner.Creature,
			cardPlay.Card,
			silent: true);
	}
}
