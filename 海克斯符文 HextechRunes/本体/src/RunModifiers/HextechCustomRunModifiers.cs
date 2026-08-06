using MegaCrit.Sts2.Core.Localization;

namespace HextechRunes;

public sealed class HextechSilverRunModifier : ModifierModel
{
	public override LocString Title => new("modifiers", "HEXTECH_SILVER_RUN.title");

	public override LocString Description => new("modifiers", "HEXTECH_SILVER_RUN.description");

	protected override string IconPath => $"res://{ModInfo.Id}/images/relics/silverForge.png";
}

public sealed class HextechGoldRunModifier : ModifierModel
{
	public override LocString Title => new("modifiers", "HEXTECH_GOLD_RUN.title");

	public override LocString Description => new("modifiers", "HEXTECH_GOLD_RUN.description");

	protected override string IconPath => $"res://{ModInfo.Id}/images/relics/goldForge.png";
}

public sealed class HextechPrismaticRunModifier : ModifierModel
{
	public override LocString Title => new("modifiers", "HEXTECH_PRISMATIC_RUN.title");

	public override LocString Description => new("modifiers", "HEXTECH_PRISMATIC_RUN.description");

	protected override string IconPath => $"res://{ModInfo.Id}/images/relics/prismaticForge.png";
}

internal static class HextechCustomRunModifierCompatibility
{
	public static HextechRarityTier? GetForcedRarity(RunState runState)
	{
		foreach (ModifierModel modifier in runState.Modifiers)
		{
			if (TryGetForcedRarity(modifier, out HextechRarityTier rarity))
			{
				return rarity;
			}
		}

		return null;
	}

	private static bool TryGetForcedRarity(ModifierModel modifier, out HextechRarityTier rarity)
	{
		switch (modifier)
		{
			case HextechSilverRunModifier:
				rarity = HextechRarityTier.Silver;
				return true;
			case HextechGoldRunModifier:
				rarity = HextechRarityTier.Gold;
				return true;
			case HextechPrismaticRunModifier:
				rarity = HextechRarityTier.Prismatic;
				return true;
			default:
				rarity = default;
				return false;
		}
	}
}
