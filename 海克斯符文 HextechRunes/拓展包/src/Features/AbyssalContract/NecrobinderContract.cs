using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HextechRunesSponsorPack;

// 缚魂者:赠送一张刻印的血肉戏法,代价是己方回合开始给场上所有生物(含自己)随机上负面。
internal sealed class NecrobinderContract : AbyssalContractBase
{
	public override IEnumerable<IHoverTip> ExtraHoverTips =>
		HoverTipFactory.FromRelic<NecrobinderContractChoiceRelic>();

	public override Task ApplyInitialEffect(AbyssalContractRune rune)
	{
		return rune.AddContractCards<SleightOfFlesh>(1, ApplyImbuedEnchantment);
	}

	public override async Task BeforeSideTurnStart(
		AbyssalContractRune rune,
		PlayerChoiceContext choiceContext,
		CombatSide side,
		HextechCombatState combatState)
	{
		if (side != rune.Owner.Creature.Side)
		{
			return;
		}

		IReadOnlyList<Creature> targets = combatState.Creatures
			.Where(static creature => creature.IsAlive && creature.CanReceivePowers)
			.ToArray();
		if (targets.Count == 0)
		{
			return;
		}

		rune.Flash(targets);
		foreach (Creature target in targets)
		{
			for (int i = 0; i < AbyssalContractRune.NecrobinderDebuffApplications; i++)
			{
				await ApplyRandomDebuff(rune, choiceContext, target);
			}
		}
	}

	// 消耗的是 RunState.Rng.Niche(两端同一条命令路径、同一序列),不引入本地随机。
	private static async Task ApplyRandomDebuff(
		AbyssalContractRune rune,
		PlayerChoiceContext choiceContext,
		Creature target)
	{
		if (rune.Owner == null)
		{
			return;
		}

		switch (rune.Owner.RunState.Rng.Niche.NextInt(5))
		{
			case 0:
				await PowerCmd.Apply<WeakPower>(choiceContext, target, 1m, rune.Owner.Creature, null);
				break;
			case 1:
				await PowerCmd.Apply<VulnerablePower>(choiceContext, target, 1m, rune.Owner.Creature, null);
				break;
			case 2:
				await PowerCmd.Apply<FrailPower>(choiceContext, target, 1m, rune.Owner.Creature, null);
				break;
			case 3:
				await PowerCmd.Apply<DoomPower>(choiceContext, target, 1m, rune.Owner.Creature, null);
				break;
			default:
				await PowerCmd.Apply<PoisonPower>(choiceContext, target, 1m, rune.Owner.Creature, null);
				break;
		}
	}
}
