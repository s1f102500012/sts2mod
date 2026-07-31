namespace HextechRunes;

// 升级：子程序(仅鸡煲) —— 战斗开始时,不论何处,将所有子程序(Subroutine)放入手牌。
public sealed class SubroutineUpgradeRune : CardUpgradeRuneBase<Subroutine>
{
	private bool _addedThisCombat;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public bool SavedAddedThisCombat
	{
		get => _addedThisCombat;
		set => _addedThisCombat = value;
	}

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromCard<Subroutine>()
	];

	protected override bool IsAvailableForCharacter(Player player) => IsDefectPlayer(player);

	public override Task BeforeCombatStart()
	{
		_addedThisCombat = false;
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		_addedThisCombat = false;
		return Task.CompletedTask;
	}

	public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, HextechCombatState combatState)
	{
		if (player != Owner
			|| Owner?.PlayerCombatState == null
			|| Owner.Creature.IsDead
			|| !TryConsumeCombatStartMove())
		{
			return;
		}

		List<Subroutine> subroutines = Owner.PlayerCombatState.AllCards
			.OfType<Subroutine>()
			.Where(static card => card.Pile?.Type != PileType.Hand)
			.ToList();
		if (subroutines.Count == 0)
		{
			return;
		}

		Flash();
		foreach (Subroutine subroutine in subroutines)
		{
			await CardPileCmd.Add(subroutine, PileType.Hand);
		}
	}

	internal bool TryConsumeCombatStartMove()
	{
		if (_addedThisCombat)
		{
			return false;
		}

		_addedThisCombat = true;
		return true;
	}
}
