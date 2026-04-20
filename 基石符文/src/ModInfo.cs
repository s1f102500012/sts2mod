using MegaCrit.Sts2.Core.Models;

namespace KeystoneRunes;

internal static class ModInfo
{
	public readonly record struct RuneSeriesGroup(string LocalizationKey, IReadOnlyList<RelicModel> Relics);

	public const string Id = "KeystoneRunes";

	public const string DisplayName = "基石符文";

	public const string TargetGameVersion = "0.103.2";

	public const string KeystoneSubcategoryKey = "KEYSTONE_RUNES_SUBCATEGORY";

	public const string ElectrocuteIconPath = "res://KeystoneRunes/images/relics/electrocute.png";

	public const string FirstStrikeIconPath = "res://KeystoneRunes/images/relics/first_strike.png";

	public const string GraspIconPath = "res://KeystoneRunes/images/relics/grasp.png";

	public const string ConquerorIconPath = "res://KeystoneRunes/images/relics/conqueror.png";

	public const string AeryIconPath = "res://KeystoneRunes/images/relics/aery.png";

	public const string PressAttackIconPath = "res://KeystoneRunes/images/relics/press_the_attack.png";

	public const string PhaseRushIconPath = "res://KeystoneRunes/images/relics/phase_rush.png";

	public const string UnsealedSpellbookIconPath = "res://KeystoneRunes/images/relics/unsealed_spellbook.png";

	public const string HailOfBladesIconPath = "res://KeystoneRunes/images/relics/hail_of_blades.png";

	public const string FleetFootworkIconPath = "res://KeystoneRunes/images/relics/fleet_footwork.png";

	public const string ArcaneCometIconPath = "res://KeystoneRunes/images/relics/arcane_comet.png";

	public const string DarkHarvestIconPath = "res://KeystoneRunes/images/relics/dark_harvest.png";

	public const string GlacialAugmentIconPath = "res://KeystoneRunes/images/relics/glacial_augment.png";

	public const string AftershockIconPath = "res://KeystoneRunes/images/relics/aftershock.png";

	public static IReadOnlyList<RelicModel> GetCanonicalRunes()
	{
		return
		[
			ModelDb.Relic<ElectrocuteRune>(),
			ModelDb.Relic<FirstStrikeRune>(),
			ModelDb.Relic<UndyingGraspRune>(),
			ModelDb.Relic<ConquerorRune>(),
			ModelDb.Relic<SummonAeryRune>(),
			ModelDb.Relic<PressTheAttackRune>(),
			ModelDb.Relic<PhaseRushRune>(),
			ModelDb.Relic<UnsealedSpellbookRune>(),
			ModelDb.Relic<HailOfBladesRune>(),
			ModelDb.Relic<FleetFootworkRune>(),
			ModelDb.Relic<ArcaneCometRune>(),
			ModelDb.Relic<DarkHarvestRune>(),
			ModelDb.Relic<GlacialAugmentRune>(),
			ModelDb.Relic<AftershockRune>()
		];
	}

	public static bool IsKeystoneRelic(RelicModel? relic)
	{
		if (relic == null)
		{
			return false;
		}

		ModelId id = relic.CanonicalInstance?.Id ?? relic.Id;
		return id == ModelDb.GetId<ElectrocuteRune>()
			|| id == ModelDb.GetId<FirstStrikeRune>()
			|| id == ModelDb.GetId<UndyingGraspRune>()
			|| id == ModelDb.GetId<ConquerorRune>()
			|| id == ModelDb.GetId<SummonAeryRune>()
			|| id == ModelDb.GetId<PressTheAttackRune>()
			|| id == ModelDb.GetId<PhaseRushRune>()
			|| id == ModelDb.GetId<UnsealedSpellbookRune>()
			|| id == ModelDb.GetId<HailOfBladesRune>()
			|| id == ModelDb.GetId<FleetFootworkRune>()
			|| id == ModelDb.GetId<ArcaneCometRune>()
			|| id == ModelDb.GetId<DarkHarvestRune>()
			|| id == ModelDb.GetId<GlacialAugmentRune>()
			|| id == ModelDb.GetId<AftershockRune>();
	}

	public static string? TryGetRelicIconPath(RelicModel relic)
	{
		ModelId id = relic.CanonicalInstance?.Id ?? relic.Id;
		if (id == ModelDb.GetId<ElectrocuteRune>())
		{
			return ElectrocuteIconPath;
		}

		if (id == ModelDb.GetId<FirstStrikeRune>())
		{
			return FirstStrikeIconPath;
		}

		if (id == ModelDb.GetId<UndyingGraspRune>())
		{
			return GraspIconPath;
		}

		if (id == ModelDb.GetId<ConquerorRune>())
		{
			return ConquerorIconPath;
		}

		if (id == ModelDb.GetId<SummonAeryRune>())
		{
			return AeryIconPath;
		}

		if (id == ModelDb.GetId<PressTheAttackRune>())
		{
			return PressAttackIconPath;
		}

		if (id == ModelDb.GetId<PhaseRushRune>())
		{
			return PhaseRushIconPath;
		}

		if (id == ModelDb.GetId<UnsealedSpellbookRune>())
		{
			return UnsealedSpellbookIconPath;
		}

		if (id == ModelDb.GetId<HailOfBladesRune>())
		{
			return HailOfBladesIconPath;
		}

		if (id == ModelDb.GetId<FleetFootworkRune>())
		{
			return FleetFootworkIconPath;
		}

		if (id == ModelDb.GetId<ArcaneCometRune>())
		{
			return ArcaneCometIconPath;
		}

		if (id == ModelDb.GetId<DarkHarvestRune>())
		{
			return DarkHarvestIconPath;
		}

		if (id == ModelDb.GetId<GlacialAugmentRune>())
		{
			return GlacialAugmentIconPath;
		}

		if (id == ModelDb.GetId<AftershockRune>())
		{
			return AftershockIconPath;
		}

		return null;
	}

	public static IReadOnlyList<RuneSeriesGroup> GetRuneSeriesGroups(IReadOnlyList<RelicModel> relics)
	{
		Dictionary<ModelId, RelicModel> byId = relics.ToDictionary(static relic => relic.CanonicalInstance?.Id ?? relic.Id);

		IReadOnlyList<RelicModel> BuildGroup(params Type[] runeTypes)
		{
			List<RelicModel> group = new();
			foreach (Type runeType in runeTypes)
			{
				ModelId id = ModelDb.GetId(runeType);
				if (byId.TryGetValue(id, out RelicModel? relic))
				{
					group.Add(relic);
				}
			}

			return group;
		}

		return
		[
			new RuneSeriesGroup("PRECISION", BuildGroup(typeof(ConquerorRune), typeof(PressTheAttackRune), typeof(FleetFootworkRune))),
			new RuneSeriesGroup("DOMINATION", BuildGroup(typeof(ElectrocuteRune), typeof(HailOfBladesRune), typeof(DarkHarvestRune))),
			new RuneSeriesGroup("SORCERY", BuildGroup(typeof(SummonAeryRune), typeof(ArcaneCometRune), typeof(PhaseRushRune))),
			new RuneSeriesGroup("RESOLVE", BuildGroup(typeof(UndyingGraspRune), typeof(AftershockRune))),
			new RuneSeriesGroup("INSPIRATION", BuildGroup(typeof(FirstStrikeRune), typeof(UnsealedSpellbookRune), typeof(GlacialAugmentRune)))
		];
	}
}
