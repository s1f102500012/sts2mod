using Godot;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Screens.RelicCollection;

namespace HextechRunes;

internal static partial class HextechCollectionHooks
{
	private static void AddHextechSubcategory(
		NRelicCollectionCategory self,
		NRelicCollection collection,
		HashSet<RelicModel> seenRelics,
		HashSet<RelicModel> allUnlockedRelics)
	{
		if (collection.Relics.Any(HextechCatalog.IsHextechRelic))
		{
			return;
		}

		IReadOnlyList<RelicModel> genericRunes = HextechCatalog.GetCanonicalGenericVisibleRunes();
		IReadOnlyList<HextechCatalog.RuneSeriesGroup> characterGroups = HextechCatalog.GetCharacterRuneGroups();
		HashSet<RelicModel> visibleHextechRelics = genericRunes
			.Concat(characterGroups.SelectMany(static group => group.Relics))
			.ToHashSet();
		HashSet<RelicModel> seenWithHextech = seenRelics.Concat(visibleHextechRelics).ToHashSet();
		HashSet<RelicModel> unlockedWithHextech = allUnlockedRelics.Concat(visibleHextechRelics).ToHashSet();

		NRelicCollectionCategory subCategory = CreateAndLoadSubcategory(
			self,
			collection,
			HextechAssets.HextechSubcategoryKey,
			genericRunes,
			seenWithHextech,
			unlockedWithHextech);
		ApplyCustomHeaderText(
			subCategory,
			HextechAssets.HextechSubcategoryKey,
			HextechHeaderZh,
			HextechHeaderZhBody,
			HextechHeaderEn,
			HextechHeaderEnBody);

		NRelicCollectionCategory? firstCharacterSubcategory = null;
		foreach (HextechCatalog.RuneSeriesGroup group in characterGroups)
		{
			NRelicCollectionCategory? characterSubcategory = AddCharacterRuneSubcategory(
				subCategory,
				collection,
				group,
				seenWithHextech,
				unlockedWithHextech);
			firstCharacterSubcategory ??= characterSubcategory;
		}

		if (firstCharacterSubcategory != null)
		{
			InsertSpacingBeforeCharacterPools(subCategory, firstCharacterSubcategory);
		}
	}

	private static void AddForgeSubcategory(
		NRelicCollectionCategory self,
		NRelicCollection collection,
		HashSet<RelicModel> seenRelics,
		HashSet<RelicModel> allUnlockedRelics)
	{
		if (collection.Relics.Any(HextechCatalog.IsHextechForgeRelic))
		{
			return;
		}

		HashSet<RelicModel> visibleForgeRelics = HextechCatalog.GetCanonicalForges().ToHashSet();
		HashSet<RelicModel> seenWithForges = seenRelics.Concat(visibleForgeRelics).ToHashSet();
		HashSet<RelicModel> unlockedWithForges = allUnlockedRelics.Concat(visibleForgeRelics).ToHashSet();

		NRelicCollectionCategory subCategory = CreateAndLoadSubcategory(
			self,
			collection,
			HextechAssets.ForgeSubcategoryKey,
			HextechCatalog.GetCanonicalForges(),
			seenWithForges,
			unlockedWithForges);
		ApplyCustomHeaderText(
			subCategory,
			HextechAssets.ForgeSubcategoryKey,
			ForgeHeaderZh,
			ForgeHeaderZhBody,
			ForgeHeaderEn,
			ForgeHeaderEnBody);
	}

	private static NRelicCollectionCategory? AddCharacterRuneSubcategory(
		NRelicCollectionCategory hextechCategory,
		NRelicCollection collection,
		HextechCatalog.RuneSeriesGroup group,
		HashSet<RelicModel> seenRelics,
		HashSet<RelicModel> allUnlockedRelics)
	{
		if (group.Relics.Count == 0)
		{
			return null;
		}

		string localizationKey = $"HEXTECH_{group.LocalizationKey}";
		NRelicCollectionCategory subCategory = CreateAndLoadSubcategory(
			hextechCategory,
			collection,
			localizationKey,
			group.Relics,
			seenRelics,
			allUnlockedRelics);
		if (CharacterHeaderTexts.TryGetValue(group.LocalizationKey, out SubcategoryHeaderText text))
		{
			ApplyCustomHeaderText(subCategory, localizationKey, text.ZhHeader, text.ZhBody, text.EnHeader, text.EnBody);
		}

		return subCategory;
	}

	private static NRelicCollectionCategory CreateAndLoadSubcategory(
		NRelicCollectionCategory parent,
		NRelicCollection collection,
		string localizationKey,
		IEnumerable<RelicModel> relics,
		HashSet<RelicModel> seenRelics,
		HashSet<RelicModel> allUnlockedRelics)
	{
		List<NRelicCollectionCategory> subCategories = GetSubCategories(parent);
		NRelicCollectionCategory subCategory = (NRelicCollectionCategory)CreateForSubcategoryMethod!.Invoke(parent, null)!;
		int insertIndex = ((Control)HeaderLabelField!.GetValue(parent)!).GetIndex() + subCategories.Count + 1;
		subCategories.Add(subCategory);
		parent.AddChild(subCategory);
		parent.MoveChild(subCategory, insertIndex);

		LoadSubcategoryMethod!.Invoke(
			subCategory,
			[
				collection,
				new LocString("relic_collection", localizationKey),
				relics,
				seenRelics,
				allUnlockedRelics
			]);

		return subCategory;
	}

	private static void InsertSpacingBeforeCharacterPools(
		NRelicCollectionCategory hextechCategory,
		NRelicCollectionCategory firstCharacterSubcategory)
	{
		Control spacer = new()
		{
			Name = "HextechGenericToCharacterPoolSpacer",
			CustomMinimumSize = new Vector2(0f, GenericToCharacterPoolSpacing),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		hextechCategory.AddChild(spacer);
		hextechCategory.MoveChild(spacer, firstCharacterSubcategory.GetIndex());
	}
}
