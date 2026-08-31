namespace HextechRunes;

/// <summary>
/// 万剑归宗(棱彩,仅储君):你的君王之剑打出后会被消耗;每当抽牌堆洗牌时,打出你消耗牌堆里的所有君王之剑。
/// 打出→消耗→洗牌时齐射→再次消耗,形成"剑冢"滚雪球:每把亲手打出过的剑永久加入后续齐射。
/// </summary>
public sealed class MyriadSwordsRune : HextechRelicBase
{
	private bool _discharging;

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromCard<SovereignBlade>()
	];

	public override bool IsAvailableForPlayer(Player player)
	{
		return IsRegentPlayer(player);
	}

	// 君王之剑打出后进消耗堆。去向 None(复制品等)不抢改,防幽灵实体(同八分门)。
	public override (PileType, CardPilePosition) ModifyCardPlayResultPileTypeAndPositionCompat(CardModel card, bool isAutoPlay, ResourceInfo resources, PileType pileType, CardPilePosition position)
	{
		return pileType is not PileType.None && card.Owner == Owner && card is SovereignBlade
			? (PileType.Exhaust, position)
			: (pileType, position);
	}

	public override async Task AfterShuffle(PlayerChoiceContext choiceContext, Player shuffler)
	{
		if (_discharging
			|| Owner == null
			|| shuffler != Owner
			|| Owner.Creature.IsDead
			|| Owner.PlayerCombatState == null
			|| Owner.Creature.CombatState == null)
		{
			return;
		}

		// 快照:齐射打出的剑经上面的去向改写回到消耗堆,不会在本次洗牌触发内重复打出。
		List<SovereignBlade> blades = PileType.Exhaust.GetPile(Owner).Cards
			.OfType<SovereignBlade>()
			.Where(blade => blade.Owner == Owner)
			.ToList();
		if (blades.Count == 0)
		{
			return;
		}

		_discharging = true;
		try
		{
			Flash();
			HextechMyriadSwordsVfx.Play(Owner.Creature);
			foreach (SovereignBlade blade in blades)
			{
				if (CombatManager.Instance.IsOverOrEnding || Owner.Creature.IsDead)
				{
					break;
				}

				// 目标与瓦库代打同口径:单体取首个可命中敌人(两端确定),全体(寻锋刃)传 null。
				Creature? target = blade.TargetType == TargetType.AnyEnemy
					? Owner.Creature.CombatState.HittableEnemies
						.OrderBy(static enemy => enemy.CombatId ?? uint.MaxValue)
						.FirstOrDefault()
					: null;
				if (blade.TargetType == TargetType.AnyEnemy && target == null)
				{
					break;
				}

				try
				{
					await HextechAutoPlayHelper.AutoPlayOrMoveToResultPile(choiceContext, blade, target, skipXCapture: true);
				}
				finally
				{
					if (blade.Pile?.Type == PileType.Play)
					{
						// 致死一击会提前结束战斗；原版自动打牌收尾偶尔因此把主机的牌留在 Play，
						// 而客机已按本海克斯规则送入 Exhaust。即使自动打牌被取消也要收敛结果堆。
						await CardPileCmd.Add(blade, PileType.Exhaust, CardPilePosition.Bottom, this);
					}
				}
				HextechSovereignBladeVfxSync.Reconcile(Owner);
			}
		}
		finally
		{
			if (Owner?.PlayerCombatState != null && Owner.Creature.CombatState != null)
			{
				HextechSovereignBladeVfxSync.Reconcile(Owner);
			}
			_discharging = false;
		}
	}
}
