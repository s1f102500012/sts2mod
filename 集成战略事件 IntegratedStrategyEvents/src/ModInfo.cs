namespace IntegratedStrategyEvents;

internal static class ModInfo
{
	public const string ModId = "IntegratedStrategyEvents";
	public const string DisplayName = "集成战略事件";
	public const string HarmonyId = "Natsuki.IntegratedStrategyEvents";
	public const string RitsuLibCoreHarmonyId = "com.ritsukage.sts2-RitsuLib.framework-core";
	public const string LogPrefix = "[IntegratedStrategyEvents]";
	public const string Version = "0.5.5";
#if STS2_110_OR_NEWER
	public const string TargetGameVersion = "0.110.1";
#elif STS2_109_OR_NEWER
	public const string TargetGameVersion = "0.109.0";
#else
	public const string TargetGameVersion = "0.107.1";
#endif
}
