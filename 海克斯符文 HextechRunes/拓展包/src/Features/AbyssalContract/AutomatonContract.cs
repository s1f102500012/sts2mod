using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.ValueProps;

namespace HextechRunesSponsorPack;

// 自动机:珠槽近乎无限、所有充能球替换为闪电(参数改写补丁见 AbyssalContractPatches),
// 代价是回合结束按场上珠数自伤。
internal sealed class AutomatonContract : AbyssalContractBase
{
	public override IEnumerable<IHoverTip> ExtraHoverTips =>
		HoverTipFactory.FromRelic<AutomatonContractChoiceRelic>();

	public override async Task ApplyInitialEffect(AbyssalContractRune rune)
	{
		ApplyOrbSlots(rune);
		await UpgradeCurrentStartingRelic(rune);
	}

	public override Task AfterRemoved(AbyssalContractRune rune)
	{
		Player owner = rune.Owner;
		owner.BaseOrbSlotCount = Math.Max(0, owner.BaseOrbSlotCount - AbyssalContractRune.AutomatonOrbSlotBonus);
		if (owner.PlayerCombatState != null && owner.Creature.CombatState != null)
		{
			owner.PlayerCombatState.OrbQueue.RemoveCapacity(AbyssalContractRune.AutomatonOrbSlotBonus);
		}

		return Task.CompletedTask;
	}

	public override async Task BeforeTurnEnd(
		AbyssalContractRune rune,
		PlayerChoiceContext choiceContext,
		CombatSide side)
	{
		if (side != rune.Owner.Creature.Side || rune.Owner.PlayerCombatState == null)
		{
			return;
		}

		int orbCount = rune.Owner.PlayerCombatState.OrbQueue.Orbs.Count;
		if (orbCount <= 0)
		{
			return;
		}

		rune.Flash([rune.Owner.Creature]);
		await CreatureCmd.Damage(
			choiceContext,
			rune.Owner.Creature,
			orbCount * AbyssalContractRune.AutomatonDamagePerOrb,
			ValueProp.Unpowered,
			rune.Owner.Creature);
	}

	// SerializablePlayer.BaseOrbSlotCount 是 16 位序列化,+99 仍在范围内(联机安全)。
	private static void ApplyOrbSlots(AbyssalContractRune rune)
	{
		Player? owner = rune.Owner;
		if (owner == null)
		{
			return;
		}

		owner.BaseOrbSlotCount += AbyssalContractRune.AutomatonOrbSlotBonus;
		if (owner.PlayerCombatState != null && owner.Creature.CombatState != null)
		{
			owner.PlayerCombatState.OrbQueue.AddCapacity(AbyssalContractRune.AutomatonOrbSlotBonus);
		}
	}
}
