namespace HextechRunes;

public sealed class FeyMagicRune : HextechRelicBase
{
	internal const int MinimumCardCost = 3;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public bool SavedTriggeredThisTurn
	{
		get => false;
		set { } // 旧存档仍可能包含该字段；新版效果不再有每回合次数限制。
	}

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("MinCost", MinimumCardCost)
	];

	public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
	{
		if (Owner == null
			|| target.Side != CombatSide.Enemy
			|| result.TotalDamage <= 0m
			|| !IsOwnedCardWithEffectiveCostAtLeast(cardSource, DynamicVars["MinCost"].BaseValue))
		{
			return;
		}

		Flash([target]);
		await CreatureCmd.Stun(target, null);
	}
}
