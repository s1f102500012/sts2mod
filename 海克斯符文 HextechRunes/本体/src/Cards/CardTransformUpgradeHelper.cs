using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Random;

namespace HextechRunes;

internal static class CardTransformUpgradeHelper
{
	public static bool CanTransformToRandomCard(CardModel card)
	{
		if (!card.IsTransformable || card.Pile?.Type != PileType.Hand)
		{
			return false;
		}

		try
		{
			return CardFactory.GetDefaultTransformationOptions(card, card.CombatState != null).Any();
		}
		catch (InvalidOperationException)
		{
			return false;
		}
	}

	public static CardTransformation CreateRandomOptionTransformation(
		CardModel original,
		IEnumerable<CardModel> replacementOptions,
		Rng rng)
	{
		CardModel replacement = CardFactory.CreateRandomCardForTransform(
			original,
			replacementOptions,
			original.CombatState != null,
			rng);
		PreserveUpgradeLevel(original, replacement);
		return new CardTransformation(original, replacement);
	}

	public static CardTransformation CreateStableOptionTransformation(
		CardModel original,
		IEnumerable<CardModel> replacementOptions,
		RunState runState,
		string source,
		int ordinal,
		params string?[] saltParts)
	{
		CardModel[] filteredOptions = GetStableTransformationOptions(original, replacementOptions, original.CombatState != null);
		CardModel canonicalReplacement = HextechStableRandom.Pick(
			filteredOptions,
			runState,
			HextechStableRandom.CardKey,
			BuildStableTransformSalt(original, source, ordinal, saltParts));
		CardModel replacement = original.CardScope!.CreateCard(canonicalReplacement, original.Owner!);
		PreserveUpgradeLevel(original, replacement);
		return new CardTransformation(original, replacement);
	}

	public static Task<CardPileAddResult?> TransformToStableRandom(
		CardModel original,
		RunState runState,
		string source,
		int ordinal,
		CardPreviewStyle style = CardPreviewStyle.HorizontalLayout,
		params string?[] saltParts)
	{
		CardTransformation transformation = CreateStableOptionTransformation(
			original,
			CardFactory.GetDefaultTransformationOptions(original, original.CombatState != null),
			runState,
			source,
			ordinal,
			saltParts);
		return CardCmd.Transform(transformation.Original, transformation.Replacement!, style);
	}

	public static CardTransformation CreateFixedReplacementTransformation(CardModel original, CardModel replacement)
	{
		PreserveUpgradeLevel(original, replacement);
		return new CardTransformation(original, replacement);
	}

	public static void PreserveUpgradeLevel(CardModel original, CardModel replacement)
	{
		RestoreUpgradeLevel(replacement, original.CurrentUpgradeLevel);
	}

	public static void RestoreUpgradeLevel(CardModel card, int capturedUpgradeLevel)
	{
		int upgradesToRestore = GetUpgradeRestorationSteps(
			card.CurrentUpgradeLevel,
			capturedUpgradeLevel,
			card.MaxUpgradeLevel);
		for (int i = 0; i < upgradesToRestore && card.IsUpgradable; i++)
		{
			CardCmd.Upgrade(card, CardPreviewStyle.None);
		}
	}

	internal static int GetUpgradeRestorationSteps(int currentUpgradeLevel, int capturedUpgradeLevel, int maxUpgradeLevel)
	{
		int targetUpgradeLevel = Math.Min(Math.Max(0, capturedUpgradeLevel), Math.Max(0, maxUpgradeLevel));
		return Math.Max(0, targetUpgradeLevel - Math.Max(0, currentUpgradeLevel));
	}

	private static string?[] BuildStableTransformSalt(CardModel original, string source, int ordinal, params string?[] saltParts)
	{
		string?[] result = new string?[saltParts.Length + 5];
		result[0] = source;
		result[1] = original.Owner == null ? "owner:none" : HextechStableRandom.PlayerKey(original.Owner);
		result[2] = HextechStableRandom.CardKey(original);
		result[3] = ordinal.ToString();
		result[4] = HextechStableRandom.CardActionKey(original);
		Array.Copy(saltParts, 0, result, 5, saltParts.Length);
		return result;
	}

	private static CardModel[] GetStableTransformationOptions(CardModel original, IEnumerable<CardModel> originalOptions, bool isInCombat)
	{
		IEnumerable<CardModel> source = originalOptions;
		if (original.Rarity is not CardRarity.Status and not CardRarity.Curse)
		{
			source = source.Where(static card => card.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare);
		}

		if (isInCombat)
		{
			source = source.Where(static card => card.CanBeGeneratedInCombat);
		}

		source = source.Where(card => card.Id != original.Id);
		CardModel[] options = FilterForPlayerCount(original.Owner!.RunState, source)
			.OrderBy(HextechStableRandom.CardKey, StringComparer.Ordinal)
			.ToArray();
		if (options.Length == 0)
		{
			throw new InvalidOperationException("All transformation options provided are invalid! Original options: " + string.Join(",", originalOptions));
		}

		return options;
	}

	private static IEnumerable<CardModel> FilterForPlayerCount(IRunState runState, IEnumerable<CardModel> options)
	{
		return runState.Players.Count > 1
			? options.Where(static card => card.MultiplayerConstraint != CardMultiplayerConstraint.SingleplayerOnly)
			: options;
	}
}
