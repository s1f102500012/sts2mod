namespace HextechRunes;

internal static class ModInfo
{
	public const string Id = "HextechRunes";

	public const string DisplayName = "海克斯符文";

	public const string Version = "0.9.1";

	// 发布变体只有三个;csproj 的 HextechValidateTarget 拦住其它目标。
#if STS2_111_OR_NEWER
	public const string TargetGameVersion = "0.111.0";
#elif STS2_110_OR_NEWER
	public const string TargetGameVersion = "0.110.0";
#else
	public const string TargetGameVersion = "0.107.1";
#endif
}
