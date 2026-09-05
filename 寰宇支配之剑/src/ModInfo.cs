using System.Reflection;

namespace UniversalDominionSword;

internal static class ModInfo
{
	public const string Id = "UniversalDominionSword";

	public const string DisplayName = "寰宇支配之剑";

	public const string HarmonyId = "Natsuki.UniversalDominionSword";

	/// <summary>csproj 按编译目标写入的程序集元数据键;加载器据此核对变体与目录名一致。</summary>
	public const string CompatTargetMetadataKey = "UniversalDominionSwordCompatibilityTarget";

	public const string ImageRoot = "res://UniversalDominionSword/images/";

	public const string RelicIconPath = ImageRoot + "relics/universal_dominion_sword.png";

	public const string CardPortraitPath = ImageRoot + "cards/universal_dominion_sword_card.png";

	public const string Layer0Path = ImageRoot + "relics/infinity_sword_layer_0.png";

	public const string Layer1Path = ImageRoot + "relics/infinity_sword_layer_1.png";

	public const string MaskPath = ImageRoot + "relics/infinity_sword_mask.png";

	public const int CosmicFrameCount = 10;

	public static string CosmicPath(int index) => $"{ImageRoot}relics/cosmic_{index}.png";

	public static string TargetGameVersion { get; } =
		typeof(ModInfo).Assembly
			.GetCustomAttributes<AssemblyMetadataAttribute>()
			.FirstOrDefault(attribute => string.Equals(attribute.Key, CompatTargetMetadataKey, StringComparison.Ordinal))
			?.Value
		?? "unknown";
}
