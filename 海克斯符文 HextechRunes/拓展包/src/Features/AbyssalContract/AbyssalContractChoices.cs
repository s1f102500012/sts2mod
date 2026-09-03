using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;

namespace HextechRunesSponsorPack;

public abstract class AbyssalContractChoiceRelic : RelicModel
{
	public sealed override RelicRarity Rarity => RelicRarity.Event;
}

public sealed class WarriorContractChoiceRelic : AbyssalContractChoiceRelic
{
	protected override string IconBaseName => "burning_blood";

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<StrengthPower>("InitialStrength", AbyssalContractRune.WarriorInitialStrength)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<StrengthPower>()
	];
}

public sealed class HunterContractChoiceRelic : AbyssalContractChoiceRelic
{
	protected override string IconBaseName => "ring_of_the_snake";

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new CardsVar("Snakebites", AbyssalContractRune.HunterSnakebiteCount),
		new DynamicVar("SkillInterval", AbyssalContractRune.HunterSkillInterval),
		new DynamicVar("CombatInterval", AbyssalContractRune.HunterCombatInterval),
		new EnergyVar("CostReduction", AbyssalContractRune.HunterSnakebiteCostReduction)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromCard<Snakebite>()
	];
}

public sealed class RegentContractChoiceRelic : AbyssalContractChoiceRelic
{
	protected override string IconBaseName => "divine_right";

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		.. HoverTipFactory.FromRelic<FencingManual>(),
		HoverTipFactory.FromCard<SwordSage>(),
		HoverTipFactory.FromCard<Parry>(),
		.. HoverTipFactory.FromEnchantment<Imbued>()
	];
}

public sealed class NecrobinderContractChoiceRelic : AbyssalContractChoiceRelic
{
	protected override string IconBaseName => "bound_phylactery";

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("DebuffApplications", AbyssalContractRune.NecrobinderDebuffApplications)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromCard<SleightOfFlesh>(),
		.. HoverTipFactory.FromEnchantment<Imbued>(),
		HoverTipFactory.FromPower<WeakPower>(),
		HoverTipFactory.FromPower<VulnerablePower>(),
		HoverTipFactory.FromPower<FrailPower>(),
		HoverTipFactory.FromPower<DoomPower>(),
		HoverTipFactory.FromPower<PoisonPower>()
	];
}

public sealed class AutomatonContractChoiceRelic : AbyssalContractChoiceRelic
{
	protected override string IconBaseName => "cracked_core";

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("OrbSlots", AbyssalContractRune.AutomatonOrbSlotBonus),
		new DynamicVar("DamagePerOrb", AbyssalContractRune.AutomatonDamagePerOrb)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromOrb<LightningOrb>()
	];
}
