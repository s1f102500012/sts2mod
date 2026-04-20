using System.Collections.Generic;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.RelicCollection;
using MegaCrit.Sts2.Core.Unlocks;
using MonoMod.RuntimeDetour;

namespace KeystoneRunes;

internal static class CollectionHooks
{
	private const string StarterHeaderZh = "初始：";

	private const string StarterHeaderZhBody = "角色们开始游戏时自身携带的遗物。";

	private const string KeystoneHeaderZh = "基石：";

	private const string KeystoneHeaderZhBody = "来自英雄联盟里的符文遗物。";

	private const string StarterHeaderEn = "Starter:";

	private const string StarterHeaderEnBody = "Relics that characters start the game with.";

	private const string KeystoneHeaderEn = "Keystone:";

	private const string KeystoneHeaderEnBody = "Rune relics from League of Legends.";

	private static readonly FieldInfo HeaderLabelField = RequireField(typeof(NRelicCollectionCategory), "_headerLabel");

	private static readonly FieldInfo SubCategoriesField = RequireField(typeof(NRelicCollectionCategory), "_subCategories");

	private static readonly MethodInfo CreateForSubcategoryMethod = RequireMethod(typeof(NRelicCollectionCategory), "CreateForSubcategory", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly MethodInfo LoadSubcategoryMethod = RequireMethod(
		typeof(NRelicCollectionCategory),
		"LoadSubcategory",
		BindingFlags.Instance | BindingFlags.NonPublic,
		typeof(NRelicCollection),
		typeof(LocString),
		typeof(IEnumerable<RelicModel>),
		typeof(HashSet<RelicModel>),
		typeof(HashSet<RelicModel>));

	private static Hook? _loadRelicsHook;

	private static string? _starterHeaderTemplate;

	private delegate void OrigLoadRelics(
		NRelicCollectionCategory self,
		RelicRarity relicRarity,
		NRelicCollection collection,
		LocString header,
		HashSet<RelicModel> seenRelics,
		UnlockState unlockState,
		HashSet<RelicModel> allUnlockedRelics);

	public static void Install()
	{
		_loadRelicsHook = new Hook(
			RequireMethod(
				typeof(NRelicCollectionCategory),
				"LoadRelics",
				BindingFlags.Instance | BindingFlags.Public,
				typeof(RelicRarity),
				typeof(NRelicCollection),
				typeof(LocString),
				typeof(HashSet<RelicModel>),
				typeof(UnlockState),
				typeof(HashSet<RelicModel>)),
			LoadRelicsDetour);
	}

	private static void LoadRelicsDetour(
		OrigLoadRelics orig,
		NRelicCollectionCategory self,
		RelicRarity relicRarity,
		NRelicCollection collection,
		LocString header,
		HashSet<RelicModel> seenRelics,
		UnlockState unlockState,
		HashSet<RelicModel> allUnlockedRelics)
	{
		orig(self, relicRarity, collection, header, seenRelics, unlockState, allUnlockedRelics);

		if (relicRarity != RelicRarity.Starter)
		{
			return;
		}

		_starterHeaderTemplate ??= header.GetRawText();

		AddKeystoneSubcategory(self, collection, seenRelics, allUnlockedRelics);
	}

	private static void AddKeystoneSubcategory(
		NRelicCollectionCategory self,
		NRelicCollection collection,
		HashSet<RelicModel> seenRelics,
		HashSet<RelicModel> allUnlockedRelics)
	{
		List<NRelicCollectionCategory> subCategories = GetSubCategories(self);
		if (collection.Relics.Any(ModInfo.IsKeystoneRelic))
		{
			return;
		}

		NRelicCollectionCategory subCategory = (NRelicCollectionCategory)CreateForSubcategoryMethod.Invoke(self, null)!;
		int insertIndex = ((Control)HeaderLabelField.GetValue(self)!).GetIndex() + subCategories.Count + 1;
		subCategories.Add(subCategory);
		self.AddChild(subCategory);
		self.MoveChild(subCategory, insertIndex);

		LoadSubcategoryMethod.Invoke(
			subCategory,
			[
				collection,
				new LocString("relic_collection", ModInfo.KeystoneSubcategoryKey),
				ModInfo.GetCanonicalRunes(),
				seenRelics,
				allUnlockedRelics
			]);

		ApplyCustomHeaderText(subCategory);
	}

	private static void ApplyCustomHeaderText(NRelicCollectionCategory subCategory)
	{
		if (HeaderLabelField.GetValue(subCategory) is not MegaRichTextLabel headerLabel)
		{
			return;
		}

		string fallback = new LocString("relic_collection", ModInfo.KeystoneSubcategoryKey).GetRawText();
		headerLabel.SetTextAutoSize(FormatLikeStarterHeader(_starterHeaderTemplate, fallback));
	}

	private static string FormatLikeStarterHeader(string? starterTemplate, string fallback)
	{
		if (string.IsNullOrWhiteSpace(starterTemplate))
		{
			return fallback;
		}

		string formatted = starterTemplate
			.Replace(StarterHeaderZh, KeystoneHeaderZh)
			.Replace(StarterHeaderZhBody, KeystoneHeaderZhBody)
			.Replace(StarterHeaderEn, KeystoneHeaderEn)
			.Replace(StarterHeaderEnBody, KeystoneHeaderEnBody);

		return formatted == starterTemplate ? fallback : formatted;
	}

	private static List<NRelicCollectionCategory> GetSubCategories(NRelicCollectionCategory category)
	{
		return (List<NRelicCollectionCategory>)SubCategoriesField.GetValue(category)!;
	}

	private static FieldInfo RequireField(Type type, string name)
	{
		return type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new InvalidOperationException($"Could not find field {type.FullName}.{name}.");
	}

	private static MethodInfo RequireMethod(Type type, string name, BindingFlags flags, params Type[] parameters)
	{
		return type.GetMethod(name, flags, binder: null, parameters, modifiers: null)
			?? throw new InvalidOperationException($"Could not find method {type.FullName}.{name}.");
	}
}
