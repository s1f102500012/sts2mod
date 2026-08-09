namespace Heartsteel;

internal static class ModInfo
{
	public const string Id = "Heartsteel";

	public const string DisplayName = "心之钢";

	public const string RelicIconPath = "res://Heartsteel/images/relics/heartsteel.png";

	public const string PowerIconPath = "res://Heartsteel/images/powers/heartsteel_devour_power.png";

	public const string OrnnsForgePortraitPath = "res://Heartsteel/images/events/ornns_forge.png";

	public const string RitsuLibVersion = "0.5.10";

	public static string TargetGameVersion => typeof(ModInfo).Assembly
		.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), inherit: false)
		.Cast<System.Reflection.AssemblyMetadataAttribute>()
		.FirstOrDefault(static attribute => attribute.Key == "HeartsteelCompatibilityTarget")
		?.Value ?? "unknown";
}
