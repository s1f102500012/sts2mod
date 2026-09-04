using IntegratedStrategyEvents.Events;

namespace IntegratedStrategyEvents;

internal static partial class IntegratedStrategyContentCatalog
{
	// 事件类型与它的本地化工厂成对登记，缺一边编译就不过；
	// 事件本地化因此不再靠反射去找各自的私有静态方法。
	public static (Type Type, Func<List<(string, string)>?> CreateLocalization)[] EventDefinitions =>
	[
		(typeof(BoundBloodEvent), BoundBloodEvent.CreateLocalization),
		(typeof(EntrustAdventurerEvent), EntrustAdventurerEvent.CreateLocalization),
		(typeof(WastefulRevelryEvent), WastefulRevelryEvent.CreateLocalization),
		(typeof(FatefulMeetingEvent), FatefulMeetingEvent.CreateLocalization),
		(typeof(PathOfSufferingEvent), PathOfSufferingEvent.CreateLocalization),
		(typeof(TreasureChestDanceEvent), TreasureChestDanceEvent.CreateLocalization),
		(typeof(NorthWindWitchEvent), NorthWindWitchEvent.CreateLocalization),
		(typeof(ResolvingDoubtsEvent), ResolvingDoubtsEvent.CreateLocalization),
		(typeof(TurningPointEvent), TurningPointEvent.CreateLocalization),
		(typeof(PopularAttractionEvent), PopularAttractionEvent.CreateLocalization),
		(typeof(AllComersWelcomeEvent), AllComersWelcomeEvent.CreateLocalization),
		(typeof(TransmissionEvent), TransmissionEvent.CreateLocalization),
		(typeof(DepartedGardenEvent), DepartedGardenEvent.CreateLocalization),
		(typeof(LiuerEvent), LiuerEvent.CreateLocalization),
		(typeof(DevoutPersonEvent), DevoutPersonEvent.CreateLocalization),
		(typeof(ForesightEvent), ForesightEvent.CreateLocalization),
		(typeof(TrappedPersonEvent), TrappedPersonEvent.CreateLocalization),
		(typeof(SleepingStatueEvent), SleepingStatueEvent.CreateLocalization),
		(typeof(TimidThievesEvent), TimidThievesEvent.CreateLocalization),
		(typeof(SeabornScholarEvent), SeabornScholarEvent.CreateLocalization),
		(typeof(OdeEvent), OdeEvent.CreateLocalization),
		(typeof(KindlingSparkEvent), KindlingSparkEvent.CreateLocalization),
		(typeof(SecretDoorEvent), SecretDoorEvent.CreateLocalization),
		(typeof(ForSurvivalEvent), ForSurvivalEvent.CreateLocalization),
		(typeof(UrsusEvent), UrsusEvent.CreateLocalization),
		(typeof(SuspicionChainEvent), SuspicionChainEvent.CreateLocalization),
		(typeof(HundredMileEncampmentEvent), HundredMileEncampmentEvent.CreateLocalization),
		(typeof(InviteToPlayEvent), InviteToPlayEvent.CreateLocalization),
		(typeof(FortuneFlowsEvent), FortuneFlowsEvent.CreateLocalization),
		(typeof(CompletionCeremonyEvent), CompletionCeremonyEvent.CreateLocalization),
		(typeof(LostMountainsEvent), LostMountainsEvent.CreateLocalization),
		(typeof(BlackFootprintsEvent), BlackFootprintsEvent.CreateLocalization),
		(typeof(RoyalDisputeEvent), RoyalDisputeEvent.CreateLocalization),
		(typeof(FutureHunterEvent), FutureHunterEvent.CreateLocalization),
		(typeof(DesperateChoiceEvent), DesperateChoiceEvent.CreateLocalization),
		(typeof(BusinessEmpireEvent), BusinessEmpireEvent.CreateLocalization),
		(typeof(SpeciousEvent), SpeciousEvent.CreateLocalization),
		(typeof(SwordInStoneEvent), SwordInStoneEvent.CreateLocalization),
		(typeof(SecretRoomEvent), SecretRoomEvent.CreateLocalization),
		(typeof(DustDevouringSpreadEvent), DustDevouringSpreadEvent.CreateLocalization),
		(typeof(AfterStoryEndsEvent), AfterStoryEndsEvent.CreateLocalization),
		(typeof(SamiLanguageEvent), SamiLanguageEvent.CreateLocalization),
		(typeof(UnfreezingRiverEvent), UnfreezingRiverEvent.CreateLocalization),
		(typeof(NorthernWizardArenaEvent), NorthernWizardArenaEvent.CreateLocalization),
		(typeof(ForwardForestEvent), ForwardForestEvent.CreateLocalization),
		(typeof(GlimpseEvent), GlimpseEvent.CreateLocalization),
		(typeof(ShiftingCityEvent), ShiftingCityEvent.CreateLocalization),
		(typeof(StoryToBeToldEvent), StoryToBeToldEvent.CreateLocalization),
		(typeof(TruthToBeToldEvent), TruthToBeToldEvent.CreateLocalization),
		(typeof(PrimordialDivergenceEvent), PrimordialDivergenceEvent.CreateLocalization),
		(typeof(VoidPortentEvent), VoidPortentEvent.CreateLocalization),
		(typeof(AnomalousReportEvent), AnomalousReportEvent.CreateLocalization),
		(typeof(ChangeEvent), ChangeEvent.CreateLocalization),
		(typeof(BeginningEvent), BeginningEvent.CreateLocalization),
		(typeof(LiberationEvent), LiberationEvent.CreateLocalization),
		(typeof(SublimationEvent), SublimationEvent.CreateLocalization),
		(typeof(ReconstructionEvent), ReconstructionEvent.CreateLocalization),
		(typeof(ExplorerSmallStepEvent), ExplorerSmallStepEvent.CreateLocalization),
		(typeof(ExpressionEvent), ExpressionEvent.CreateLocalization),
		(typeof(GoodsFromTheMouthEvent), GoodsFromTheMouthEvent.CreateLocalization),
		(typeof(PrimalEntertainmentEvent), PrimalEntertainmentEvent.CreateLocalization),
		(typeof(ColorAndFlavorDifferentOriginsEvent), ColorAndFlavorDifferentOriginsEvent.CreateLocalization),
		(typeof(HeavyContractEvent), HeavyContractEvent.CreateLocalization)
	];

	public static Type[] EventTypes => [.. EventDefinitions.Select(static definition => definition.Type)];
}
