using HextechRunes;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rooms;

namespace HextechRunesSponsorPack;

public sealed class StarlightSparkleRune : HextechRelicBase
{
	private static readonly Type[] RelicTypes =
	[
		typeof(WhiteStar),
		typeof(BlackStar),
		typeof(GoldStarRelic)
	];

	public override bool HasUponPickupEffect => true;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<StrengthPower>(1m),
		new PowerVar<DexterityPower>(1m),
		new PowerVar<PlatingPower>(3m),
		new PowerVar<RegenPower>(3m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		.. HoverTipFactory.FromRelic<WhiteStar>(),
		.. HoverTipFactory.FromRelic<BlackStar>(),
		.. HoverTipFactory.FromRelic<GoldStarRelic>(),
		HoverTipFactory.FromPower<StrengthPower>(),
		HoverTipFactory.FromPower<DexterityPower>(),
		HoverTipFactory.FromPower<PlatingPower>(),
		HoverTipFactory.FromPower<RegenPower>()
	];

	public override async Task AfterObtained()
	{
		if (Owner == null)
		{
			return;
		}

		Flash();
		await RelicBundleGrantHelper.GrantRelics(Owner, RelicTypes);
	}

	public override async Task BeforeCombatStart()
	{
		if (Owner == null
			|| Owner.Creature.IsDead
			|| Owner.RunState.CurrentRoom is not CombatRoom { RoomType: RoomType.Elite })
		{
			return;
		}

		Flash();
		await PowerCmd.Apply<StrengthPower>(Owner.Creature, DynamicVars.Strength.BaseValue, Owner.Creature, null);
		await PowerCmd.Apply<DexterityPower>(Owner.Creature, DynamicVars.Dexterity.BaseValue, Owner.Creature, null);
		await PowerCmd.Apply<PlatingPower>(Owner.Creature, DynamicVars["PlatingPower"].BaseValue, Owner.Creature, null);
		await PowerCmd.Apply<RegenPower>(Owner.Creature, DynamicVars["RegenPower"].BaseValue, Owner.Creature, null);
	}

	public override bool IsAvailableForPlayer(Player player)
	{
		return true;
	}
}
