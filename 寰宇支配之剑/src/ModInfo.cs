namespace UniversalDominionSword;

internal static class ModInfo
{
	public const string Id = "UniversalDominionSword";

	public const string DisplayName = "寰宇支配之剑";

#if STS2_107_1
	public const string TargetGameVersion = "0.107.1";
#elif STS2_110_0
	public const string TargetGameVersion = "0.110.0";
#else
#error Unsupported Slay the Spire 2 compatibility target.
#endif

	public const string RelicIconPath = "res://UniversalDominionSword/images/relics/universal_dominion_sword.png";

	public const string CardPortraitPath = "res://UniversalDominionSword/images/cards/universal_dominion_sword_card.png";

	public const string Layer0Path = "res://UniversalDominionSword/images/relics/infinity_sword_layer_0.png";

	public const string Layer1Path = "res://UniversalDominionSword/images/relics/infinity_sword_layer_1.png";

	public const string MaskPath = "res://UniversalDominionSword/images/relics/infinity_sword_mask.png";

	public static string CosmicPath(int index) =>
		$"res://UniversalDominionSword/images/relics/cosmic_{index}.png";
}
