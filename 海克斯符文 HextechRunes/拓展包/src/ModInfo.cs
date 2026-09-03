using System.Reflection;

namespace HextechRunesSponsorPack;

internal static class ModInfo
{
	public const string Id = "HextechRunesSponsorPack";

	// 唯一版本来源是 csproj 的 <Version>;手写常量会与 manifest / csproj 漂移。
	public static string Version { get; } =
		typeof(ModInfo).Assembly
			.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
			?.Split('+')[0]
		?? typeof(ModInfo).Assembly.GetName().Version?.ToString(3)
		?? "0.0.0";

	// 本变体编译时对准的游戏版本,来自 csproj 的 AssemblyMetadata(加载器也按这个键选变体)。
	public static string TargetGameVersion { get; } =
		typeof(ModInfo).Assembly
			.GetCustomAttributes<AssemblyMetadataAttribute>()
			.FirstOrDefault(static attribute => attribute.Key == "HextechSponsorCompatibilityTarget")
			?.Value
		?? "unknown";
}
