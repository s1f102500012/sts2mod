using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;
using IntegratedStrategyEvents.Encounters;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace IntegratedStrategyEvents.Powers;

public sealed class SaintguardShieldPower : PowerModel, IModPowerAssetOverrides
{
	public PowerAssetProfile AssetProfile => PowerAssetProfile.Empty;

	private const string RampartPowerPackedIconPath =
		"res://images/atlases/power_atlas.sprites/rampart_power.tres";
	private const string RampartPowerBigIconPath = "res://images/powers/rampart_power.png";

	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Counter;

	public string? CustomIconPath => RampartPowerPackedIconPath;

	public string? CustomBigIconPath => RampartPowerBigIconPath;

#if STS2_109_OR_NEWER
	public override decimal ModifyDamageAdditive(
		Creature? target,
		decimal amount,
		ValueProp props,
		Creature? dealer,
		CardModel? cardSource,
		CardPlay? cardPlay)
#else
	public override decimal ModifyDamageAdditive(
		Creature? target,
		decimal amount,
		ValueProp props,
		Creature? dealer,
		CardModel? cardSource)
#endif
	{
		_ = dealer;
		_ = cardSource;
#if STS2_109_OR_NEWER
		_ = cardPlay;
#endif
		if (target != Owner || amount <= 0m || Amount <= 0 || props.HasFlag(ValueProp.Unpowered))
		{
			return 0m;
		}

		return -Math.Min(amount, Amount);
	}

	public override bool ShouldPowerBeRemovedAfterOwnerDeath()
	{
		return Owner?.Monster is not BozhokastiSaintguardGunner boss || boss.HasRevived;
	}
}
