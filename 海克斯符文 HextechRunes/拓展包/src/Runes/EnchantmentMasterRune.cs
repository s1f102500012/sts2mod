using HextechRunesSponsorPack;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using SponsorArcaneForge = HextechRunesSponsorPack.ArcaneForge;
using SponsorEnchantmentForge = HextechRunesSponsorPack.EnchantmentForge;
using SponsorEntropyForge = HextechRunesSponsorPack.EntropyForge;
using SponsorEvolutionForge = HextechRunesSponsorPack.EvolutionForge;
using SponsorMysticForge = HextechRunesSponsorPack.MysticForge;

namespace HextechRunes;

public sealed class EnchantmentMasterRune : HextechRelicBase
{
	private static readonly HashSet<Type> GoldEnchantmentForgeTypes =
	[
		typeof(GlamForge),
		typeof(SponsorEnchantmentForge),
		typeof(SponsorEntropyForge)
	];

	private static readonly HashSet<Type> PrismaticEnchantmentForgeTypes =
	[
		typeof(SpiralForge),
		typeof(SponsorArcaneForge),
		typeof(SponsorEvolutionForge),
		typeof(SponsorMysticForge)
	];

	public override bool HasUponPickupEffect => true;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("PrismaticForgeCount", 1m),
		new DynamicVar("GoldForgeCount", 2m)
	];

	public override async Task AfterObtained()
	{
		if (Owner == null)
		{
			return;
		}

		Flash();
		await HextechRunesApi.ObtainRandomForges(
			Owner,
			HextechRarityTier.Prismatic,
			DynamicVars["PrismaticForgeCount"].IntValue,
			IsPrismaticEnchantmentForge,
			"enchantment-master-prismatic");
		await HextechRunesApi.ObtainRandomForges(
			Owner,
			HextechRarityTier.Gold,
			DynamicVars["GoldForgeCount"].IntValue,
			IsGoldEnchantmentForge,
			"enchantment-master-gold");
	}

	public override bool IsAvailableForPlayer(Player player)
	{
		return true;
	}

	private static bool IsGoldEnchantmentForge(Type forgeType)
	{
		return GoldEnchantmentForgeTypes.Contains(forgeType);
	}

	private static bool IsPrismaticEnchantmentForge(Type forgeType)
	{
		return PrismaticEnchantmentForgeTypes.Contains(forgeType);
	}
}
