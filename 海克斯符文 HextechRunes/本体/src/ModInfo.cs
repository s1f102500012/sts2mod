namespace HextechRunes;

internal static class ModInfo
{
    public const string Id = "HextechRunes";

    public const string DisplayName = "海克斯符文";

    public const string Version = "0.8.8";

#if STS2_109_OR_NEWER
    public const string TargetGameVersion = "0.109.0";
#elif STS2_108_OR_NEWER
    public const string TargetGameVersion = "0.108.0";
#elif STS2_107_1
    public const string TargetGameVersion = "0.107.1";
#elif STS2_107_OR_NEWER
    public const string TargetGameVersion = "0.107.0";
#elif STS2_106_OR_NEWER
    public const string TargetGameVersion = "0.106.1";
#elif STS2_105_OR_NEWER
    public const string TargetGameVersion = "0.105.1";
#elif STS2_104_OR_NEWER
    public const string TargetGameVersion = "0.104.0";
#elif STS2_103_2
    public const string TargetGameVersion = "0.103.2";
#else
    public const string TargetGameVersion = "0.103.3";
#endif
}
