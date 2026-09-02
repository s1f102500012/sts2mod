namespace HextechRunes;

public sealed class KakaRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<RitualPower>(1m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<RitualPower>()
	];

	internal static bool BlocksAttack(CardModel card)
	{
		Player? owner = card.Owner;
		return owner != null
			&& IllusoryWeaponRune.IsAttackForEffects(card, owner)
			&& owner.GetRelic<KakaRune>() != null
			&& owner.Creature.CombatState?.RoundNumber == 1;
	}

	// 第 1 回合禁止攻击牌:走原版 Hook.ShouldPlay,CanPlay 会带上 BlockedByHook 与本遗物作为 preventer。
	public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
	{
		return autoPlayType != AutoPlayType.None || card.Owner != Owner || !BlocksAttack(card);
	}

	public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (player != Owner || Owner == null || Owner.Creature.IsDead || Owner.Creature.CombatState?.RoundNumber != 2)
		{
			return;
		}

		int act = GetPlayerActNumberForScaling();
		Flash();
		await PowerCmd.Apply<RitualPower>(Owner.Creature, act, Owner.Creature, null);
	}
}
