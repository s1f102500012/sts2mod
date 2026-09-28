namespace HextechRunes;

// 0.108.0 起伤害管线加 CardPlay 参数:游戏侧 override 按版本二选一,子类统一重写版本无关的
// Compat 虚方法(见各基类内的同名段落)。
public abstract class HextechPowerBase : PowerModel
{
	// 自有 Power 的本地 UI 事件不能截断后续共享命令；不拦截原版或第三方 Power。
	protected new void Flash()
	{
		try
		{
			base.Flash();
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][PowerVisual] Flash failed for {GetType().Name}: {ex.Message}");
		}
	}

	protected new void InvokeDisplayAmountChanged()
	{
		try
		{
			base.InvokeDisplayAmountChanged();
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][PowerVisual] Counter refresh failed for {GetType().Name}: {ex.Message}");
		}
	}

	public virtual decimal ModifyDamageMultiplicativeCompat(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		return 1m;
	}

#if STS2_108_OR_NEWER
	public sealed override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
	{
		return ModifyDamageMultiplicativeCompat(target, amount, props, dealer, cardSource);
	}
#else
	public sealed override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		return ModifyDamageMultiplicativeCompat(target, amount, props, dealer, cardSource);
	}
#endif

	public virtual Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, HextechCombatState combatState)
	{
		return Task.CompletedTask;
	}

	public sealed override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, IReadOnlyList<Creature> participants, HextechCombatState combatState)
	{
		return BeforeSideTurnStart(choiceContext, side, combatState);
	}

	public virtual Task AfterSideTurnStart(CombatSide side, HextechCombatState combatState)
	{
		return Task.CompletedTask;
	}

	/// <summary>
	/// 需要按 participants 判定"持有者是否参与本次回合开始"的能力(额外回合只带单个玩家重入回合开始 Hook 时)
	/// 覆盖此方法;默认转发到不带参与者的版本,不改变其它能力的既有语义。
	/// </summary>
	public virtual Task AfterSideTurnStartForParticipants(CombatSide side, IReadOnlyList<Creature> participants, HextechCombatState combatState)
	{
		return AfterSideTurnStart(side, combatState);
	}

	public sealed override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, HextechCombatState combatState)
	{
		return AfterSideTurnStartForParticipants(side, participants, combatState);
	}

	public virtual Task BeforeTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)
	{
		return Task.CompletedTask;
	}

	// 按持有者回合结算的能力在此检查参与者；默认保留阵营级能力的既有转发语义。
	public virtual Task BeforeTurnEndForParticipants(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
	{
		return BeforeTurnEnd(choiceContext, side);
	}

	public sealed override Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
	{
		return BeforeTurnEndForParticipants(choiceContext, side, participants);
	}

	public virtual Task AfterTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)
	{
		return Task.CompletedTask;
	}

	public sealed override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
	{
		return AfterTurnEnd(choiceContext, side);
	}
}

internal abstract class HextechModifierBase : ModifierModel
{
	public virtual decimal ModifyDamageMultiplicativeCompat(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		return 1m;
	}

#if STS2_108_OR_NEWER
	public sealed override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
	{
		return ModifyDamageMultiplicativeCompat(target, amount, props, dealer, cardSource);
	}
#else
	public sealed override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		return ModifyDamageMultiplicativeCompat(target, amount, props, dealer, cardSource);
	}
#endif

	// 0.109.0 起卡牌打出去向从 (PileType, CardPilePosition) 元组改为 CardLocation:同 HextechRelicBase 的桥。
	public virtual (PileType, CardPilePosition) ModifyCardPlayResultPileTypeAndPositionCompat(CardModel card, bool isAutoPlay, ResourceInfo resources, PileType pileType, CardPilePosition position)
	{
		return (pileType, position);
	}

#if STS2_109_OR_NEWER
	public sealed override CardLocation ModifyCardPlayResultLocation(CardModel card, bool isAutoPlay, ResourceInfo resources, CardLocation cardLocation)
	{
		(PileType pileType, CardPilePosition position) = ModifyCardPlayResultPileTypeAndPositionCompat(card, isAutoPlay, resources, cardLocation.pileType, cardLocation.position);
		cardLocation.pileType = pileType;
		cardLocation.position = position;
		return cardLocation;
	}
#else
	public sealed override (PileType, CardPilePosition) ModifyCardPlayResultPileTypeAndPosition(CardModel card, bool isAutoPlay, ResourceInfo resources, PileType pileType, CardPilePosition position)
	{
		return ModifyCardPlayResultPileTypeAndPositionCompat(card, isAutoPlay, resources, pileType, position);
	}
#endif

	public virtual Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, HextechCombatState combatState)
	{
		return Task.CompletedTask;
	}

	public sealed override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, IReadOnlyList<Creature> participants, HextechCombatState combatState)
	{
		return BeforeSideTurnStart(choiceContext, side, combatState);
	}

	public virtual Task AfterSideTurnStart(CombatSide side, HextechCombatState combatState)
	{
		return Task.CompletedTask;
	}

	public sealed override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, HextechCombatState combatState)
	{
		return AfterSideTurnStart(side, combatState);
	}

	public virtual Task BeforeTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)
	{
		return Task.CompletedTask;
	}

	public sealed override Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
	{
		return BeforeTurnEnd(choiceContext, side);
	}

	public virtual Task AfterTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)
	{
		return Task.CompletedTask;
	}

	public sealed override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
	{
		return AfterTurnEnd(choiceContext, side);
	}
}
