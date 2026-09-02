namespace HextechRunes;

public sealed class InstantDeathRune : HextechRelicBase
{
	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<DoomPower>()
	];

	public override bool IsAvailableForPlayer(Player player)
	{
		return IsNecrobinderPlayer(player);
	}

	public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
	{
		if (power is DoomPower doomPower && amount > 0m)
		{
			await KillIfDoomExceedsHp(doomPower.Owner);
		}
	}

	public override async Task AfterCurrentHpChanged(Creature creature, decimal delta)
	{
		if (delta < 0m)
		{
			await KillIfDoomExceedsHp(creature);
		}
	}

	private async Task KillIfDoomExceedsHp(Creature creature)
	{
		if (Owner == null
			|| Owner.Creature.IsDead
			|| creature.Side != CombatSide.Enemy
			|| !creature.IsAlive
			|| creature.GetPowerAmount<DoomPower>() <= creature.CurrentHp)
		{
			return;
		}

		Flash([creature]);

		// 血肉戏法/疫情响应链内不能同步处决:死亡处理与进行中的 power hook 链
		// 撞车会卡死游戏(灾厄→血肉戏法反伤→即死达标的链路)。挂账,链退出后补杀。
		if (HextechCombatHooks.IsResolvingSleightOfFleshPowerDebuffResponse
			|| HextechCombatHooks.IsResolvingOutbreakPowerPoisonResponse)
		{
			HextechCombatHooks.QueueInstantDeathDoomKill(creature);
			return;
		}

		await DoomPower.DoomKill([creature]);
	}
}
