namespace HextechRunes;

internal static class HextechAssets
{
	public const string HextechSubcategoryKey = "HEXTECH_RUNES_SUBCATEGORY";

	public const string ForgeSubcategoryKey = "HEXTECH_FORGES_SUBCATEGORY";

	public const string TrickMagicCardPortraitPath = "res://HextechRunes/images/cards/trickMagicCard.png";

	public const string BladeWaltzCardPortraitPath = "res://HextechRunes/images/cards/bladeWaltzCard.png";

	public const string CatalystCardPortraitPath = "res://HextechRunes/images/cards/catalystCard.png";

	public const string WhiteHoleCardPortraitPath = "res://HextechRunes/images/cards/whiteHoleCard.png";

	public const string SearingAttackCardPortraitPath = "res://HextechRunes/images/cards/searingAttackCard.png";

	public const string FeelTheBurnCardPortraitPath = "res://HextechRunes/images/cards/feelTheBurnCard.png";

	public const string OkBoomerangCardPortraitPath = "res://HextechRunes/images/cards/okBoomerangCard.png";

	public const string ReprogramCardPortraitPath = "res://HextechRunes/images/cards/reprogramCard.png";

	public const string MikaelsBlessingCardPortraitPath = "res://HextechRunes/images/cards/mikaelsBlessingCard.png";

	public const string OstyWishCardPortraitPath = "res://HextechRunes/images/cards/ostyWishCard.png";

	public const string OceanDragonSoulCardPortraitPath = "res://HextechRunes/images/cards/oceanDragonSoulCard.png";

	public const string InfernalDragonSoulCardPortraitPath = "res://HextechRunes/images/cards/infernalDragonSoulCard.png";

	public const string HextechDragonSoulCardPortraitPath = "res://HextechRunes/images/cards/hextechDragonSoulCard.png";

	public const string MountainDragonSoulCardPortraitPath = "res://HextechRunes/images/cards/mountainDragonSoulCard.png";

	public const string ChemtechDragonSoulCardPortraitPath = "res://HextechRunes/images/cards/chemtechDragonSoulCard.png";

	public const string CloudDragonSoulCardPortraitPath = "res://HextechRunes/images/cards/cloudDragonSoulCard.png";

	public const string OceanDragonSoulPowerIconPath = "res://HextechRunes/images/powers/hextechOceanDragonSoulPower.png";

	public const string InfernalDragonSoulPowerIconPath = "res://HextechRunes/images/powers/hextechInfernalDragonSoulPower.png";

	public const string HextechDragonSoulPowerIconPath = "res://HextechRunes/images/powers/hextechDragonSoulPower.png";

	public const string MountainDragonSoulPowerIconPath = "res://HextechRunes/images/powers/hextechMountainDragonSoulPower.png";

	public const string ChemtechDragonSoulPowerIconPath = "res://HextechRunes/images/powers/hextechChemtechDragonSoulPower.png";

	public const string CloudDragonSoulPowerIconPath = "res://HextechRunes/images/powers/hextechCloudDragonSoulPower.png";

	public const string HandOfBaronAuraRunePath = "res://HextechRunes/images/effects/jungle_buff_baron.png";

	public const string HandOfBaronAuraRingPath = "res://HextechRunes/images/effects/ring_soft_02.png";

	public const string HandOfBaronAuraDiscPath = "res://HextechRunes/images/effects/disc32.ha_crepe.png";

	public const string HandOfBaronAuraSmokePath = "res://HextechRunes/images/effects/srx_infernal_smoke_trail.png";

	public const string MikaelsBlessingAoeRunePath = "res://HextechRunes/images/effects/milio_base_r_aoe_rune.png";

	public const string SlowCookHeatGlowPath = "res://HextechRunes/images/effects/slow_cook/heat_glow.png";

	public const string SlowCookAoeGradientPath = "res://HextechRunes/images/effects/slow_cook/aoe_gradient.png";

	public const string SlowCookAoeGradientSubtlePath = "res://HextechRunes/images/effects/slow_cook/aoe_gradient_subtle.png";

	public const string SlowCookAoeEdgePath = "res://HextechRunes/images/effects/slow_cook/aoe_edge.png";

	public const string SlowCookAoePolarPath = "res://HextechRunes/images/effects/slow_cook/aoe_polar.png";

	public const string SlowCookEdgeAccentPath = "res://HextechRunes/images/effects/slow_cook/edge_accent.png";

	public const string SlowCookGroundRingPath = "res://HextechRunes/images/effects/slow_cook/ground_ring.png";

	public const string SlowCookFlameNoisePath = "res://HextechRunes/images/effects/slow_cook/flame_noise.png";

	public const string SlowCookInnerFirePath = "res://HextechRunes/images/effects/slow_cook/inner_fire.png";

	public const string SlowCookInnerFireBPath = "res://HextechRunes/images/effects/slow_cook/inner_fire_b.png";

	public const string SlowCookFlarePath = "res://HextechRunes/images/effects/slow_cook/flare.png";

	public static string? TryGetCustomRelicIconPath(RelicModel relic)
	{
		if (relic is HundredRefinementsHex)
		{
			return $"res://{ModInfo.Id}/images/relics/hundredRefinementsRune.png";
		}

		if (relic is HungryHex or InspectHex or GripHex)
		{
			return $"res://{ModInfo.Id}/images/relics/eightPennyGateRune.png";
		}

		if (relic is SomethingForNothingRune)
		{
			return $"res://{ModInfo.Id}/images/relics/acceleratingSorceryRune.png";
		}

		if (HextechCatalog.IsHextechEnemyHexIconRelic(relic))
		{
			ModelId id = relic.CanonicalInstance?.Id ?? relic.Id;
			return $"res://{ModInfo.Id}/images/relics/{ToImageFileStem(id.Entry)}.png";
		}

		if (HextechCatalog.IsHextechRelic(relic))
		{
			ModelId id = relic.CanonicalInstance?.Id ?? relic.Id;
			string assetModId = HextechExternalContentRegistry.GetAssetModId(id) ?? ModInfo.Id;
			return $"res://{assetModId}/images/relics/{ToImageFileStem(id.Entry)}.png";
		}

		if (HextechCatalog.TryGetForgeRarity(relic, out HextechRarityTier forgeRarity))
		{
			return GetForgeIconPath(forgeRarity);
		}

		if (HextechCatalog.IsHextechShopRelic(relic))
		{
			return $"res://{ModInfo.Id}/images/relics/silverForge.png";
		}

		return null;
	}

	public static string GetForgeIconPath(HextechRarityTier rarity)
	{
		string iconStem = rarity switch
		{
			HextechRarityTier.Silver => "silverForge",
			HextechRarityTier.Gold => "goldForge",
			HextechRarityTier.Prismatic => "prismaticForge",
			_ => "silverForge"
		};
		return $"res://{ModInfo.Id}/images/relics/{iconStem}.png";
	}

	internal static string ToImageFileStem(string entry)
	{
		string[] parts = entry.ToLowerInvariant().Split('_', StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length == 0)
		{
			return entry;
		}

		return parts[0] + string.Concat(parts.Skip(1).Select(static part => char.ToUpperInvariant(part[0]) + part[1..]));
	}
}
