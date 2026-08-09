using MegaCrit.Sts2.Core.Models.Acts;

namespace IntegratedStrategyEvents.Events;

internal static partial class IntegratedStrategyEventSpawnRules
{
	// 事件 → 可刷新的章节类型。注册期由 ModEntry 逐条交给 RitsuLib 的 RegisterActEvent。
	private static readonly IReadOnlyDictionary<Type, Type[]> ActRules =
		new Dictionary<Type, Type[]>
		{
			[typeof(BoundBloodEvent)] = [typeof(Overgrowth), typeof(Underdocks)],
			[typeof(SecretDoorEvent)] = [typeof(Overgrowth), typeof(Underdocks)],
			[typeof(SecretRoomEvent)] = [typeof(Overgrowth), typeof(Underdocks)],
			[typeof(DustDevouringSpreadEvent)] = [typeof(Overgrowth), typeof(Underdocks)],
			[typeof(SamiLanguageEvent)] = [typeof(Overgrowth), typeof(Underdocks)],
			[typeof(BlackFootprintsEvent)] = [typeof(Hive)],
			[typeof(AfterStoryEndsEvent)] = [typeof(Hive)],
			[typeof(DevoutPersonEvent)] = [typeof(Hive)],
			[typeof(PopularAttractionEvent)] = [typeof(Hive)],
			[typeof(SleepingStatueEvent)] = [typeof(Hive)],
			[typeof(SuspicionChainEvent)] = [typeof(Hive)],
			[typeof(TreasureChestDanceEvent)] = [typeof(Hive)],
			[typeof(BusinessEmpireEvent)] = [typeof(Glory)],
			[typeof(HundredMileEncampmentEvent)] = [typeof(Glory)],
			[typeof(InviteToPlayEvent)] = [typeof(Glory)],
			[typeof(NorthWindWitchEvent)] = [typeof(Glory)],
			[typeof(FutureHunterEvent)] = [typeof(Glory)],
			[typeof(PrimalEntertainmentEvent)] = [typeof(Glory)]
		};

	public static IReadOnlyDictionary<Type, Type[]> ActRegistrations => ActRules;
}
