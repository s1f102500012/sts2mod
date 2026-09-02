namespace HextechRunes;

internal sealed partial class HextechMayhemModifier
{
	public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
	{
		await HextechEnemyHexDispatcher.ForEachActive(
			this,
			(effect, context) => effect.AfterPowerAmountChanged(context, power, amount, applier, cardSource));

		bool hasMonsterDebuffTrigger = HextechEnemyPowerTriggerHelper.TryGetMonsterDebuffTrigger(power, amount, applier, out Creature? target, out Creature? source);
		bool suppressMonsterDebuffDuplicate = hasMonsterDebuffTrigger
			&& HextechEnemyTriggerGuard.ShouldSuppressMonsterDebuffDuplicate(_combatTracking, power, amount, source, cardSource);
		if (hasMonsterDebuffTrigger && !suppressMonsterDebuffDuplicate)
		{
			await HextechEnemyHexDispatcher.ForEachActive(
				this,
				(effect, context) => effect.AfterMonsterDebuffApplied(context, power, amount, target!, source!, cardSource));
		}

		Creature? courageSource = null;
		bool hasCourageTrigger = false;
		if (hasMonsterDebuffTrigger && !suppressMonsterDebuffDuplicate)
		{
			courageSource = source;
			hasCourageTrigger = courageSource != null;
		}
		else if (HextechEnemyPowerTriggerHelper.TryGetMonsterSelfBuffTrigger(power, amount, applier, out Creature? buffSource))
		{
			courageSource = buffSource;
			hasCourageTrigger = true;
		}

		if (hasCourageTrigger)
		{
			await HextechEnemyHexDispatcher.ForEachActive(
				this,
				(effect, context) => effect.AfterCourageTrigger(context, courageSource!));
		}
	}
}
