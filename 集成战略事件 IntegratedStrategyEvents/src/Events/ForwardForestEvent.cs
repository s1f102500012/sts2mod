using IntegratedStrategyEvents.TreeHoles;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Runs;

namespace IntegratedStrategyEvents.Events;

public sealed partial class ForwardForestEvent : IntegratedStrategyEventModel
{
	private const int TreeTopMaxHpLoss = 8;
	private const int GoldReward = 30;
	private const int MaxHpReward = 2;
	private const int HealReward = 4;

	public override bool IsShared => true;

	internal static bool CanEnterTreeHoleForAllPlayers(IRunState state)
	{
		return state.Players.All(player =>
			IntegratedStrategyEventEffects.CanLoseMaxHp(player, TreeTopMaxHpLoss));
	}

	protected override IReadOnlyList<EventOption> GenerateInitialOptions()
	{
		Player owner = OwnerOrThrow;
		return
		[
			CanLoseMaxHp(owner, TreeTopMaxHpLoss) && CanEnterTreeHoleForAllPlayers(owner.RunState)
				? Choice(ClimbIntoTreeHole, "TREE_TOP")
				: LockedChoice("TREE_TOP_LOCKED"),
			Choice(ChopForest, "CHOP_FOREST")
		];
	}

	private async Task ClimbIntoTreeHole()
	{
		await LoseMaxHp(TreeTopMaxHpLoss);
		Finish(InitialPage);
		await IntegratedStrategyTreeHoleController.EnterFromEvent(OwnerOrThrow);
	}

	private Task ChopForest()
	{
		ForestOutcome outcome = DrawOutcome();
		ShowPage(outcome.PageKey, [Choice(() => CollectReward(outcome), outcome.OptionKey, outcome.PageKey)]);
		return Task.CompletedTask;
	}

	private ForestOutcome DrawOutcome()
	{
		return Rng.NextInt(4) switch
		{
			0 => new ForestOutcome("CHOP_GOLD", "CLAIM_GOLD", static eventModel => eventModel.GainGold(GoldReward)),
			1 => new ForestOutcome("CHOP_MAX_HP", "CLAIM_MAX_HP", static eventModel => eventModel.GainMaxHp(MaxHpReward)),
			2 => new ForestOutcome("CHOP_HEAL", "CLAIM_HEAL", static eventModel => eventModel.Heal(HealReward)),
			_ => new ForestOutcome("CHOP_RELIC", "CLAIM_RELIC", static eventModel => eventModel.ObtainRandomRelic())
		};
	}

	private async Task CollectReward(ForestOutcome outcome)
	{
		await outcome.Apply(this);
		Finish(outcome.PageKey);
	}

	private sealed record ForestOutcome(
		string PageKey,
		string OptionKey,
		Func<ForwardForestEvent, Task> Apply);
}
