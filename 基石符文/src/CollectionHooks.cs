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

		headerLabel.SetTextAutoSize(new LocString("relic_collection", ModInfo.KeystoneSubcategoryKey).GetRawText());
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
