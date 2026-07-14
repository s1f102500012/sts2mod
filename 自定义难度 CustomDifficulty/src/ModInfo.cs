namespace CustomDifficulty;

internal static class ModInfo
{
	public const string Id = "CustomDifficulty";
	public const string Name = "自定义难度";
	public const string Version = "0.2.0";
#if STS2_108_OR_NEWER
	public const string TargetGameVersion = "0.108.0";
#else
	public const string TargetGameVersion = "0.107.1";
#endif
}
