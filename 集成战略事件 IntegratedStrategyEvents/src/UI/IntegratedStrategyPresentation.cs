using MegaCrit.Sts2.Core.Logging;

namespace IntegratedStrategyEvents;

internal static class IntegratedStrategyPresentation
{
	internal static void Run(Action action, string operation)
	{
		try { action(); }
		catch (Exception ex) { Log.Warn($"{ModInfo.LogPrefix}[Presentation] {operation}: {ex.GetBaseException().Message}"); }
	}

	internal static async Task RunAsync(Func<Task> action, string operation)
	{
		try { await action(); }
		catch (Exception ex) { Log.Warn($"{ModInfo.LogPrefix}[Presentation] {operation}: {ex.GetBaseException().Message}"); }
	}
}
