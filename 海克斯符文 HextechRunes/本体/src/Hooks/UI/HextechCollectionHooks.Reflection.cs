using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Screens.RelicCollection;

namespace HextechRunes;

internal static partial class HextechCollectionHooks
{
	private static void ApplyCustomHeaderText(
		NRelicCollectionCategory subCategory,
		string localizationKey,
		string zhHeader,
		string zhBody,
		string enHeader,
		string enBody)
	{
		if (HeaderLabelField!.GetValue(subCategory) is not MegaRichTextLabel headerLabel)
		{
			return;
		}

		string fallback = new LocString("relic_collection", localizationKey).GetRawText();
		headerLabel.SetTextAutoSize(FormatLikeStarterHeader(_starterHeaderTemplate, fallback, zhHeader, zhBody, enHeader, enBody));
	}

	private static string FormatLikeStarterHeader(
		string? starterTemplate,
		string fallback,
		string zhHeader,
		string zhBody,
		string enHeader,
		string enBody)
	{
		if (string.IsNullOrWhiteSpace(starterTemplate))
		{
			return fallback;
		}

		string formatted = starterTemplate
			.Replace(StarterHeaderZh, zhHeader)
			.Replace(StarterHeaderZhBody, zhBody)
			.Replace(StarterHeaderEn, enHeader)
			.Replace(StarterHeaderEnBody, enBody);

		return formatted == starterTemplate ? fallback : formatted;
	}

	private static List<NRelicCollectionCategory> GetSubCategories(NRelicCollectionCategory category)
	{
		return (List<NRelicCollectionCategory>)SubCategoriesField!.GetValue(category)!;
	}

	private static bool CanUseSubcategoryHooks()
	{
		return HeaderLabelField != null
			&& SubCategoriesField != null
			&& CreateForSubcategoryMethod != null
			&& LoadSubcategoryMethod != null;
	}

	private static IEnumerable<string> GetMissingSubcategoryDependencies()
	{
		if (HeaderLabelField == null)
		{
			yield return "NRelicCollectionCategory._headerLabel";
		}

		if (SubCategoriesField == null)
		{
			yield return "NRelicCollectionCategory._subCategories";
		}

		if (CreateForSubcategoryMethod == null)
		{
			yield return "NRelicCollectionCategory.CreateForSubcategory";
		}

		if (LoadSubcategoryMethod == null)
		{
			yield return "NRelicCollectionCategory.LoadSubcategory";
		}
	}
}
