using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.RelicCollection;
using MegaCrit.Sts2.Core.Unlocks;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

internal static partial class HextechCollectionHooks
{
	private readonly record struct SubcategoryHeaderText(string ZhHeader, string ZhBody, string EnHeader, string EnBody);

	private const float GenericToCharacterPoolSpacing = 96f;

	private const string StarterHeaderZh = "初始：";

	private const string StarterHeaderZhBody = "角色们开始游戏时自身携带的遗物。";

	private const string HextechHeaderZh = "海克斯：";

	private const string HextechHeaderZhBody = "来自海克斯符文池的自定义遗物。";

	private const string ForgeHeaderZh = "属性锻造器：";

	private const string ForgeHeaderZhBody = "来自属性锻造系统的自定义遗物。";

	private const string StarterHeaderEn = "Starter:";

	private const string StarterHeaderEnBody = "Relics that characters start the game with.";

	private const string HextechHeaderEn = "Hextech:";

	private const string HextechHeaderEnBody = "Custom relics from the Hextech rune pool.";

	private const string ForgeHeaderEn = "Stat Forgers:";

	private const string ForgeHeaderEnBody = "Custom relics from the stat forging system.";

	private static readonly IReadOnlyDictionary<string, SubcategoryHeaderText> CharacterHeaderTexts = new Dictionary<string, SubcategoryHeaderText>
	{
		["CHARACTER.IRONCLAD"] = new("铁甲战士海克斯：", "仅铁甲战士可抽取的海克斯符文。", "Ironclad Hexes:", "Hextech runes only available to Ironclad."),
		["CHARACTER.SILENT"] = new("静默猎手海克斯：", "仅静默猎手可抽取的海克斯符文。", "Silent Hexes:", "Hextech runes only available to Silent."),
		["CHARACTER.REGENT"] = new("储君海克斯：", "仅储君可抽取的海克斯符文。", "Regent Hexes:", "Hextech runes only available to Regent."),
		["CHARACTER.DEFECT"] = new("故障机器人海克斯：", "仅故障机器人可抽取的海克斯符文。", "Defect Hexes:", "Hextech runes only available to Defect."),
		["CHARACTER.NECROBINDER"] = new("亡灵契约师海克斯：", "仅亡灵契约师可抽取的海克斯符文。", "Necrobinder Hexes:", "Hextech runes only available to Necrobinder.")
	};

	private static readonly FieldInfo? HeaderLabelField = TryGetField(
		typeof(NRelicCollectionCategory),
		"_headerLabel",
		BindingFlags.Instance | BindingFlags.NonPublic,
		warnIfMissing: false);

	private static readonly FieldInfo? SubCategoriesField = TryGetField(
		typeof(NRelicCollectionCategory),
		"_subCategories",
		BindingFlags.Instance | BindingFlags.NonPublic,
		warnIfMissing: false);

	private static readonly FieldInfo? RelicsContainerField = TryGetField(
		typeof(NRelicCollectionCategory),
		"_relicsContainer",
		BindingFlags.Instance | BindingFlags.NonPublic,
		warnIfMissing: false);

	private static readonly MethodInfo? CreateForSubcategoryMethod = TryGetMethod(
		typeof(NRelicCollectionCategory),
		"CreateForSubcategory",
		BindingFlags.Instance | BindingFlags.NonPublic,
		warnIfMissing: false);

	private static readonly MethodInfo? LoadSubcategoryMethod = TryGetMethod(
		typeof(NRelicCollectionCategory),
		"LoadSubcategory",
		BindingFlags.Instance | BindingFlags.NonPublic,
		warnIfMissing: false,
		typeof(NRelicCollection),
		typeof(LocString),
		typeof(IEnumerable<RelicModel>),
		typeof(HashSet<RelicModel>),
		typeof(HashSet<RelicModel>));

	private static readonly MethodInfo? LoadRelicsMethod = TryGetMethod(
		typeof(NRelicCollectionCategory),
		"LoadRelics",
		BindingFlags.Instance | BindingFlags.Public,
		warnIfMissing: false,
		typeof(RelicRarity),
		typeof(NRelicCollection),
		typeof(LocString),
		typeof(HashSet<RelicModel>),
		typeof(UnlockState),
		typeof(HashSet<RelicModel>));

	private static readonly MethodInfo? CollectionClearRelicsMethod = TryGetMethod(
		typeof(NRelicCollection),
		"ClearRelics",
		BindingFlags.Instance | BindingFlags.NonPublic,
		warnIfMissing: false);

	private static readonly MethodInfo? CollectionLoadRelicsMethod = TryGetMethod(
		typeof(NRelicCollection),
		"LoadRelics",
		BindingFlags.Instance | BindingFlags.NonPublic,
		warnIfMissing: false);

	private static string? _starterHeaderTemplate;

	private static bool _loggedFlatFallback;

	private static bool _loggedMissingFallbackContainer;

	public static void Install(Harmony harmony)
	{
		if (LoadRelicsMethod == null)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Relic collection hooks disabled: missing NRelicCollectionCategory.LoadRelics.");
			return;
		}

		List<string> missingSubcategoryDependencies = GetMissingSubcategoryDependencies().ToList();
		if (missingSubcategoryDependencies.Count > 0)
		{
			if (RelicsContainerField == null)
			{
				Log.Warn($"[{ModInfo.Id}][Mayhem] Relic collection hooks disabled: missing {string.Join(", ", missingSubcategoryDependencies.Append("NRelicCollectionCategory._relicsContainer"))}.");
				return;
			}

			Log.Warn($"[{ModInfo.Id}][Mayhem] Relic collection subcategory hooks unavailable: missing {string.Join(", ", missingSubcategoryDependencies)}; using flat starter-grid fallback.");
		}

		harmony.Patch(
			LoadRelicsMethod!,
			postfix: new HarmonyMethod(typeof(HextechCollectionHooks), nameof(LoadRelicsPostfix)));
	}

	private static void LoadRelicsPostfix(
		NRelicCollectionCategory __instance,
		RelicRarity relicRarity,
		NRelicCollection collection,
		LocString header,
		HashSet<RelicModel> seenRelics,
		UnlockState unlockState,
		HashSet<RelicModel> allUnlockedRelics)
	{
		if (relicRarity != RelicRarity.Starter)
		{
			return;
		}

		_starterHeaderTemplate ??= header.GetRawText();
		if (!CanUseSubcategoryHooks())
		{
			AddFlatFallbackRelics(__instance, collection);
			return;
		}

		AddHextechSubcategory(__instance, collection, seenRelics, allUnlockedRelics);
		AddForgeSubcategory(__instance, collection, seenRelics, allUnlockedRelics);
	}

	public static void RefreshOpenRelicCollections()
	{
		if (CollectionClearRelicsMethod == null || CollectionLoadRelicsMethod == null)
		{
			return;
		}

		Node? root = NGame.Instance?.GetTree()?.Root;
		if (root == null || !GodotObject.IsInstanceValid(root))
		{
			return;
		}

		int refreshed = 0;
		foreach (NRelicCollection collection in EnumerateNodes<NRelicCollection>(root))
		{
			if (!GodotObject.IsInstanceValid(collection) || !collection.IsInsideTree())
			{
				continue;
			}

			try
			{
				CollectionClearRelicsMethod.Invoke(collection, null);
				CollectionLoadRelicsMethod.Invoke(collection, null);
				refreshed++;
			}
			catch (Exception ex)
			{
				Log.Warn($"[{ModInfo.Id}][RuneConfig] Failed to refresh relic collection after config save: {ex.GetType().Name}: {ex.Message}", 2);
			}
		}

		if (refreshed > 0)
		{
			HextechLog.Info($"[{ModInfo.Id}][RuneConfig] Refreshed {refreshed} relic collection screen(s) after config save.");
		}
	}

	private static IEnumerable<TNode> EnumerateNodes<TNode>(Node node)
		where TNode : Node
	{
		if (node is TNode match)
		{
			yield return match;
		}

		foreach (Node child in node.GetChildren())
		{
			foreach (TNode descendant in EnumerateNodes<TNode>(child))
			{
				yield return descendant;
			}
		}
	}

}
