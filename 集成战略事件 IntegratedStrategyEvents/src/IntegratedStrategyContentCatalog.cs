namespace IntegratedStrategyEvents;

internal static partial class IntegratedStrategyContentCatalog
{
	public static Type[] ModelTypes =>
	[
		.. EventTypes,
		.. EventRelicTypes,
		.. CardTypes,
		.. EncounterTypes,
		.. PowerTypes,
		typeof(Map.IntegratedStrategyMapLifecycle)
	];
}
