using MegaCrit.Sts2.Core.Map;

namespace HextechRunes;

internal abstract class HextechEnemyHexEffect
{
	internal abstract MonsterHexKind Kind { get; }

	internal virtual bool AffectsPlayerAttackCostPreview => false;

	internal virtual int PersistentOrder => 0;

	internal virtual int EnemyHealOrder => 0;

	internal virtual void ResetRunScopedState()
	{
	}

	internal virtual decimal ModifyDamageMultiplicative(HextechEnemyHexContext context, Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		return 1m;
	}

	internal virtual decimal ModifyBlockMultiplicative(HextechEnemyHexContext context, Creature target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay)
	{
		return 1m;
	}

	internal virtual decimal ModifyEnemyHealMultiplicative(HextechEnemyHexContext context, Creature creature, decimal amount)
	{
		return 1m;
	}

	internal virtual decimal ModifyEnemyHealAmount(HextechEnemyHexContext context, Creature creature, decimal amount)
	{
		return amount;
	}

	internal virtual decimal ModifyHpLostAfterOsty(HextechEnemyHexContext context, Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		return amount;
	}

	internal virtual decimal ModifyHandDraw(HextechEnemyHexContext context, Player player, decimal count)
	{
		return count;
	}

	internal virtual bool ShouldDraw(HextechEnemyHexContext context, Player player, bool fromHandDraw)
	{
		return true;
	}

	internal virtual bool ShouldPlay(HextechEnemyHexContext context, CardModel card, AutoPlayType autoPlayType)
	{
		return true;
	}

	internal virtual bool ShouldFlush(HextechEnemyHexContext context, Player player)
	{
		return true;
	}

	internal virtual bool ShouldEtherealTrigger(HextechEnemyHexContext context, CardModel card)
	{
		return true;
	}

	internal virtual decimal ModifyPlayerAttackEnergyCostMultiplier(HextechEnemyHexContext context, CardModel card, decimal originalCost)
	{
		return 1m;
	}

	internal virtual (PileType, CardPilePosition)? ModifyCardPlayResultPileTypeAndPosition(
		HextechEnemyHexContext context,
		CardModel card,
		bool isAutoPlay,
		ResourceInfo resources,
		PileType pileType,
		CardPilePosition position)
	{
		return null;
	}

	internal virtual Task ApplyPersistentToEnemy(HextechEnemyHexContext context, Creature creature, int? maxHpBaseOverride, bool replayOneShotPowers)
	{
		return Task.CompletedTask;
	}

	internal virtual Task ApplyCombatStartToEnemy(HextechEnemyHexContext context, Creature enemy, CombatRoom room)
	{
		return Task.CompletedTask;
	}

	// 只在首次 BeforeCombatStart 与明确的首领阶段重放中分发；后续普通敌人入场不会进入此钩子。
	internal virtual Task ApplyOpeningCombatStartToEnemy(
		HextechEnemyHexContext context,
		Creature enemy,
		CombatRoom room,
		bool replayOneShotPowers)
	{
		return Task.CompletedTask;
	}

	internal virtual Task ApplyCombatStartPlayerDebuffs(HextechEnemyHexContext context, CombatRoom room, IReadOnlyList<Creature> players)
	{
		return Task.CompletedTask;
	}

	internal virtual ActMap ModifyGeneratedMapLate(HextechEnemyHexContext context, IRunState runState, ActMap map, int actIndex)
	{
		return map;
	}

	internal virtual Task BeforeSideTurnStart(HextechEnemyHexContext context, PlayerChoiceContext choiceContext, CombatSide side, HextechCombatState combatState)
	{
		return Task.CompletedTask;
	}

	internal virtual Task BeforePlayerSideTurnStart(HextechEnemyHexContext context, HextechCombatState combatState, IReadOnlyList<Creature> players)
	{
		return Task.CompletedTask;
	}

	internal virtual Task BeforeEnemySideTurnStart(HextechEnemyHexContext context, HextechCombatState combatState, IReadOnlyList<Creature> players, IReadOnlyList<Creature> enemies)
	{
		return Task.CompletedTask;
	}

	internal virtual Task AfterCardPlayed(HextechEnemyHexContext context, PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		return Task.CompletedTask;
	}

	internal virtual Task AfterShuffle(HextechEnemyHexContext context, PlayerChoiceContext choiceContext, Player shuffler)
	{
		return Task.CompletedTask;
	}

	internal virtual Task AfterCardDrawn(HextechEnemyHexContext context, PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
	{
		return Task.CompletedTask;
	}

	internal virtual Task AfterBlockGained(HextechEnemyHexContext context, Creature creature, decimal amount, ValueProp props, CardModel? cardSource)
	{
		return Task.CompletedTask;
	}

	internal virtual Task AfterCardPlayedLate(HextechEnemyHexContext context, PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		return Task.CompletedTask;
	}

	internal virtual Task AfterPlayerTurnStartLate(HextechEnemyHexContext context, PlayerChoiceContext choiceContext, Player player)
	{
		return Task.CompletedTask;
	}

	internal virtual Task AfterAutoPrePlayPhaseEnteredLate(HextechEnemyHexContext context, PlayerChoiceContext choiceContext, Player player)
	{
		return Task.CompletedTask;
	}

	internal virtual Task BeforeTurnEnd(HextechEnemyHexContext context, PlayerChoiceContext choiceContext, CombatSide side, CombatRoom? combatRoom)
	{
		return Task.CompletedTask;
	}

	internal virtual Task AfterEnemyDamageReceived(HextechEnemyHexContext context, Creature target, uint combatId, DamageResult result, Creature? dealer, CardModel? cardSource)
	{
		return Task.CompletedTask;
	}

	internal virtual Task AfterEnemyDamageReceivedAny(HextechEnemyHexContext context, Creature target, DamageResult result, Creature? dealer, CardModel? cardSource)
	{
		return Task.CompletedTask;
	}

	internal virtual Task AfterEnemyHealthThreshold(HextechEnemyHexContext context, Creature target, uint combatId)
	{
		return Task.CompletedTask;
	}

	internal virtual Task AfterCurrentHpChanged(HextechEnemyHexContext context, Creature creature, decimal delta)
	{
		return Task.CompletedTask;
	}

	internal virtual Task AfterEnemyDamageGivenImmediate(HextechEnemyHexContext context, Creature dealer, DamageResult result, Creature target, CardModel? cardSource)
	{
		return Task.CompletedTask;
	}

	internal virtual Task AfterEnemyDamageGivenPlayerHit(HextechEnemyHexContext context, Creature dealer, Creature target)
	{
		return Task.CompletedTask;
	}

	internal virtual Task AfterPowerAmountChanged(HextechEnemyHexContext context, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
	{
		return Task.CompletedTask;
	}

	internal virtual Task AfterMonsterDebuffApplied(HextechEnemyHexContext context, PowerModel power, decimal amount, Creature target, Creature source, CardModel? cardSource)
	{
		return Task.CompletedTask;
	}

	internal virtual Task AfterCourageTrigger(HextechEnemyHexContext context, Creature source)
	{
		return Task.CompletedTask;
	}

	internal virtual Task BeforeDeath(HextechEnemyHexContext context, Creature creature)
	{
		return Task.CompletedTask;
	}

	internal virtual Task AfterDeath(HextechEnemyHexContext context, PlayerChoiceContext choiceContext, Creature target, HextechCombatState combatState)
	{
		return Task.CompletedTask;
	}

	internal virtual Task AfterCombatVictory(HextechEnemyHexContext context, CombatRoom room)
	{
		return Task.CompletedTask;
	}

	internal virtual Task AfterCombatEnd(HextechEnemyHexContext context, CombatRoom room)
	{
		return Task.CompletedTask;
	}

	internal virtual bool TryModifyRewards(HextechEnemyHexContext context, Player player, List<Reward> rewards, AbstractRoom? room)
	{
		return false;
	}

	internal virtual Task AfterGoldGained(HextechEnemyHexContext context, Player player)
	{
		return Task.CompletedTask;
	}

	internal virtual bool ShouldAllowSelectingMoreCardRewards(HextechEnemyHexContext context, Player player, CardReward cardReward)
	{
		return false;
	}

	internal virtual CardCreationOptions ModifyCardRewardCreationOptions(HextechEnemyHexContext context, Player player, CardCreationOptions options)
	{
		return options;
	}

	internal virtual bool TryModifyCardRewardOptions(HextechEnemyHexContext context, Player player, List<CardCreationResult> cardRewardOptions, CardCreationOptions creationOptions)
	{
		return false;
	}

	internal virtual bool TryModifyCardRewardOptionsLate(HextechEnemyHexContext context, Player player, List<CardCreationResult> cardRewardOptions, CardCreationOptions creationOptions)
	{
		return false;
	}
}
