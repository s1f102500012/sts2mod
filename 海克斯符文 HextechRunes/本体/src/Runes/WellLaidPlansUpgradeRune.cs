namespace HextechRunes;

// 已退役，不再进入候选池、配置菜单或图鉴。本类型保留是为了让已经持有该海克斯的旧存档仍可读取。
// 旧效果：计划妥当(WellLaidPlans)回合结束保留手牌时，可保留任意张。
// 真正放开上限在 HextechWellLaidPlansHooks；本类仅负责门控、旧存档行为与 hover。
public sealed class WellLaidPlansUpgradeRune : CardUpgradeRuneBase<WellLaidPlans>
{
	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromCard<WellLaidPlans>()
	];

	protected override bool IsAvailableForCharacter(Player player) => IsSilentPlayer(player);

#if STS2_109_OR_NEWER
	// 0.109 原版计划妥当重做为「整手牌全保留」,power 上不再有选牌挂点。选牌不能塞回 flush
	// 窗口(BeforeFlushLate):0.109 把交互从该窗口整体拿掉了,在里面 await 选牌 UI 会卡死回合
	// (玩家实报"打出计划妥当直接卡住")。改挂 BeforeTurnEnd(结束回合命令链,交互开放),
	// 选中的牌拿单回合保留标记,随后的 flush(经 hooks 把 power.ShouldFlush 拉回 true)照常弃掉未选牌。
	public override async Task BeforeTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)
	{
		if (side != CombatSide.Player
			|| Owner == null
			|| Owner.Creature == null
			|| Owner.Creature.IsDead)
		{
			return;
		}

		if (Owner.Creature.GetPower<WellLaidPlansPower>() is not WellLaidPlansPower power)
		{
			return;
		}

		await HextechWellLaidPlansHooks.UnlimitedRetain(power, choiceContext, Owner);
	}
#endif
}
