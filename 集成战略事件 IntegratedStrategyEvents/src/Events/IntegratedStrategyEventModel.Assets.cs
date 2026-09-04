using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

namespace IntegratedStrategyEvents.Events;

public abstract partial class IntegratedStrategyEventModel
{
	public override IEnumerable<string> GetAssetPaths(IRunState runState)
	{
		IEnumerable<string> original = base.GetAssetPaths(runState);
		string? portrait = CustomInitialPortraitPath;
		if (TestMode.IsOn || string.IsNullOrWhiteSpace(portrait)) return original;
		string vanillaPortrait = $"res://images/events/{Id.Entry.ToLowerInvariant()}.png";
		return original.Where(path => !string.Equals(path, vanillaPortrait, StringComparison.Ordinal))
			.Append(portrait).Distinct(StringComparer.Ordinal).ToArray();
	}
}
