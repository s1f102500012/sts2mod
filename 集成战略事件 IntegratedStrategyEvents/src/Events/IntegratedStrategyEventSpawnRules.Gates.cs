using IntegratedStrategyEvents.Map;
using IntegratedStrategyEvents.TreeHoles;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace IntegratedStrategyEvents.Events;

internal static partial class IntegratedStrategyEventSpawnRules
{
	private const int EntrustAdventurerMinimumGold = 50;
	private const int AllComersWelcomeMinimumGold = 200;
	private const int NorthernWizardArenaMinimumGold = 120;
	private const int LiuerMinimumGold = 2;
	private const int TrappedPersonMinimumGold = 1;
	private const int TrappedPersonMaximumGold = 100;
	private const int FortuneFlowsMinimumGold = 150;
	private const int ForSurvivalMinimumStartingGold = 15;
	private const int GoodsFromTheMouthMinimumGold = 150;

	private const int WastefulRevelryMinimumHp = 19;
	private const int FatefulMeetingMinimumHp = 9;
	private const int PathOfSufferingMinimumHp = 25;
	private const int HundredMileEncampmentMinimumHp = 9;

	private const int TimidThievesMinimumMaxHp = 3;
	private const int FutureHunterCardsToRemove = 2;

	private static readonly IReadOnlyDictionary<Type, Func<IRunState, bool>> AllowRules =
		new Dictionary<Type, Func<IRunState, bool>>
		{
			[typeof(EntrustAdventurerEvent)] = static runState =>
				AnyPlayerHasGold(runState, EntrustAdventurerMinimumGold),
			[typeof(AllComersWelcomeEvent)] = static runState =>
				AnyPlayerHasGold(runState, AllComersWelcomeMinimumGold),
			[typeof(NorthernWizardArenaEvent)] = static runState =>
				AnyPlayerHasGold(runState, NorthernWizardArenaMinimumGold),
			[typeof(ForwardForestEvent)] = static _ => false,
			[typeof(GlimpseEvent)] = static _ => false,
			[typeof(ShiftingCityEvent)] = static _ => false,
			[typeof(StoryToBeToldEvent)] = static _ => false,
			[typeof(TruthToBeToldEvent)] = static runState =>
				runState is RunState state &&
				IntegratedStrategySecretMapNodeController.IsAtProphetHornSecretNode(state),
			[typeof(VoidPortentEvent)] = static runState =>
				IsSecondActOpeningBranchAvailable(runState),
			[typeof(ChangeEvent)] = static runState =>
				IsSecondActOpeningBranchAvailable(runState),
			[typeof(PrimordialDivergenceEvent)] = static runState =>
				IsSecondActOpeningBranchAvailable(runState),
			[typeof(AnomalousReportEvent)] = static runState =>
				runState is RunState state &&
				IntegratedStrategyTreeHoleController.IsAtProphetHornFragmentEventPoint(state),
			[typeof(BeginningEvent)] = static runState =>
				IsSecondActOpeningBranchAvailable(runState),
			[typeof(LiberationEvent)] = static runState =>
				IsSecondActOpeningBranchAvailable(runState),
			[typeof(SublimationEvent)] = static runState =>
				runState is RunState state &&
				IntegratedStrategyTreeHoleController.IsAtAbyssalJungleSublimationEventPoint(state),
			[typeof(OdeEvent)] = static runState =>
				runState is RunState state &&
				IntegratedStrategyTreeHoleController.IsAtAbyssalJungleOdeEventPoint(state),
			[typeof(ReconstructionEvent)] = static runState =>
				runState is RunState state &&
				IntegratedStrategyTreeHoleController.IsAtEternalDustFirstEventPoint(state),
			[typeof(ExplorerSmallStepEvent)] = static runState =>
				runState is RunState state &&
				IntegratedStrategyTreeHoleController.IsAtEternalDustSecondEventPoint(state),
			[typeof(LiuerEvent)] = static runState =>
				AllPlayersHaveGold(runState, LiuerMinimumGold),
			[typeof(TrappedPersonEvent)] = static runState =>
				AllPlayersHaveGoldBetween(runState, TrappedPersonMinimumGold, TrappedPersonMaximumGold),
			[typeof(FortuneFlowsEvent)] = static runState =>
				AllPlayersHaveGold(runState, FortuneFlowsMinimumGold),
			[typeof(ForSurvivalEvent)] = static runState =>
				AnyPlayerHasGold(runState, ForSurvivalMinimumStartingGold),
			[typeof(GoodsFromTheMouthEvent)] = static runState =>
				AnyPlayerHasGold(runState, GoodsFromTheMouthMinimumGold),
			[typeof(TransmissionEvent)] = static runState =>
				runState.Players.Any(HasTransformableStrikeOrDefend),
			[typeof(WastefulRevelryEvent)] = static runState =>
				AnyPlayerCanLoseHp(runState, WastefulRevelryMinimumHp - 1),
			[typeof(FatefulMeetingEvent)] = static runState =>
				AnyPlayerCanLoseHp(runState, FatefulMeetingMinimumHp - 1),
			[typeof(PathOfSufferingEvent)] = static runState =>
				AnyPlayerCanLoseHp(runState, PathOfSufferingMinimumHp - 1),
			[typeof(HundredMileEncampmentEvent)] = static runState =>
				AllPlayersCanLoseHp(runState, HundredMileEncampmentMinimumHp - 1),
			[typeof(TimidThievesEvent)] = static runState =>
				AnyPlayerCanLoseMaxHp(runState, TimidThievesMinimumMaxHp - 1),
			[typeof(RoyalDisputeEvent)] = static runState =>
				runState.Players.All(HasOffColorCardPool),
			[typeof(FutureHunterEvent)] = static runState =>
				runState.Players.All(static player =>
					CountRemovableDeckCards(player, CardType.Attack) >= FutureHunterCardsToRemove
					|| CountRemovableDeckCards(player, CardType.Skill) >= FutureHunterCardsToRemove)
		};

	private static bool AllPlayersHaveGold(IRunState runState, int amount)
	{
		return runState.Players.All(player => player.Gold >= amount);
	}

	private static bool AnyPlayerHasGold(IRunState runState, int amount)
	{
		return runState.Players.Any(player => player.Gold >= amount);
	}

	private static bool IsSecondActOpeningBranchAvailable(IRunState runState)
	{
		return runState is RunState state &&
			IntegratedStrategyFirstEventPatch.ShouldForceSecondActOpeningEvent(state);
	}

	private static bool AllPlayersHaveGoldBetween(IRunState runState, int minimum, int maximum)
	{
		return runState.Players.All(player => player.Gold >= minimum && player.Gold <= maximum);
	}

	private static bool AllPlayersCanLoseHp(IRunState runState, int amount)
	{
		return runState.Players.All(player => IntegratedStrategyEventEffects.CanLoseHp(player, amount));
	}

	private static bool AnyPlayerCanLoseHp(IRunState runState, int amount)
	{
		return runState.Players.Any(player => IntegratedStrategyEventEffects.CanLoseHp(player, amount));
	}

	private static bool AllPlayersCanLoseMaxHp(IRunState runState, int amount)
	{
		return runState.Players.All(player => IntegratedStrategyEventEffects.CanLoseMaxHp(player, amount));
	}

	private static bool AnyPlayerCanLoseMaxHp(IRunState runState, int amount)
	{
		return runState.Players.Any(player => IntegratedStrategyEventEffects.CanLoseMaxHp(player, amount));
	}

	private static bool HasOffColorCardPool(Player player)
	{
		ModelId currentPoolId = player.Character.CardPool.Id;
		return player.UnlockState.CharacterCardPools.Any(pool =>
			!pool.IsColorless
			&& !pool.Id.Equals(currentPoolId)
			&& pool.AllCards.Any(static card => card.CanBeGeneratedByModifiers));
	}

	private static bool HasTransformableStrikeOrDefend(Player player)
	{
		return IntegratedStrategyEventEffects.CountTransformableBasicDeckCards(player, CardTag.Strike) > 0
			|| IntegratedStrategyEventEffects.CountTransformableBasicDeckCards(player, CardTag.Defend) > 0;
	}

	private static int CountRemovableDeckCards(Player player, CardType type)
	{
		return IntegratedStrategyEventEffects.CountRemovableDeckCards(player, type);
	}
}
