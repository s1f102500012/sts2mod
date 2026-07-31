using MegaCrit.Sts2.Core.Models.Relics;

namespace HextechRunes;

public sealed class WatchOutGrapefruitRune : HextechRelicBase, IHextechSharedCombatVictoryRune
{
	private static readonly Type[] FoodRelicTypes =
	[
		typeof(Strawberry),
		typeof(Pear),
		typeof(Mango),
		typeof(DragonFruit),
		typeof(LoomingFruit),
		typeof(LeesWaffle),
		typeof(YummyCookie),
		typeof(MeatOnTheBone),
		typeof(PaelsFlesh),
		typeof(IceCream),
		typeof(Bread),
		typeof(NutritiousOyster),
		typeof(VeryHotCocoa),
		typeof(FragrantMushroom),
		typeof(BigMushroom),
		typeof(ChosenCheese),
		typeof(LastingCandy),
		typeof(NutritiousSoup),
		typeof(BoneTea),
		typeof(EmberTea)
	];

	public override Task AfterCombatVictory(CombatRoom room)
	{
		if (IsNetworkMultiplayer())
		{
			return Task.CompletedTask;
		}

		return ApplySharedCombatVictory(room);
	}

	public Task ApplySharedCombatVictory(CombatRoom room)
	{
		if (Owner == null || Owner.Creature.IsDead)
		{
			return Task.CompletedTask;
		}

		IReadOnlyList<Type> candidates = BuildFoodRelicCandidates(
			IsRegentOwner,
			Owner.GetRelic<IceCream>() != null,
			Owner.GetRelic<NutritiousSoup>() != null);
		Type relicType = HextechStableRandom.Pick(
			candidates,
			(RunState)Owner.RunState,
			HextechStableRandom.TypeModelKey,
			"treat-yourself-food-relic",
			HextechStableRandom.PlayerKey(Owner),
			Owner.Relics.Count.ToString());
		RelicModel relic = ModelDb.GetById<RelicModel>(ModelDb.GetId(relicType)).ToMutable();
		Flash(Array.Empty<Creature>());
		room.AddExtraReward(Owner, new RelicReward(relic, Owner));
		return Task.CompletedTask;
	}

	internal static IReadOnlyList<Type> BuildFoodRelicCandidates(
		bool isRegent,
		bool hasIceCream,
		bool hasNutritiousSoup)
	{
		IEnumerable<Type> candidates = FoodRelicTypes;
		if (isRegent)
		{
			candidates = candidates.Append(typeof(LunarPastry));
		}
		if (hasIceCream)
		{
			candidates = candidates.Where(static type => type != typeof(IceCream));
		}
		if (hasNutritiousSoup)
		{
			candidates = candidates.Where(static type => type != typeof(NutritiousSoup));
		}
		return candidates.ToArray();
	}
}
