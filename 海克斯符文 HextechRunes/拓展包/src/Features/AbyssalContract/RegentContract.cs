using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Relics;

namespace HextechRunesSponsorPack;

// 摄政:起始遗物换成击剑手册,赠送两张刻印牌;代价是只有击剑手册能锻造(门控补丁见 AbyssalContractPatches)。
internal sealed class RegentContract : AbyssalContractBase
{
	public override IEnumerable<IHoverTip> ExtraHoverTips =>
		HoverTipFactory.FromRelic<RegentContractChoiceRelic>();

	public override async Task ApplyInitialEffect(AbyssalContractRune rune)
	{
		await ReplaceCurrentStartingRelicWithFencingManual(rune);
		await rune.AddContractCards<SwordSage>(1, ApplyImbuedEnchantment);
		await rune.AddContractCards<Parry>(1, ApplyImbuedEnchantment);
	}

	private static async Task ReplaceCurrentStartingRelicWithFencingManual(AbyssalContractRune rune)
	{
		Player? owner = rune.Owner;
		if (owner == null || owner.GetRelic<FencingManual>() != null)
		{
			return;
		}

		RelicModel? starter = owner.Character switch
		{
			Ironclad => (RelicModel?)owner.GetRelic<BurningBlood>() ?? owner.GetRelic<BlackBlood>(),
			Silent => (RelicModel?)owner.GetRelic<RingOfTheSnake>() ?? owner.GetRelic<RingOfTheDrake>(),
			Regent => (RelicModel?)owner.GetRelic<DivineRight>() ?? owner.GetRelic<DivineDestiny>(),
			Necrobinder => (RelicModel?)owner.GetRelic<BoundPhylactery>() ?? owner.GetRelic<PhylacteryUnbound>(),
			Defect => (RelicModel?)owner.GetRelic<CrackedCore>() ?? owner.GetRelic<InfusedCore>(),
			_ => null
		};
		FencingManual replacement = (FencingManual)ModelDb.Relic<FencingManual>().ToMutable();
		if (starter != null)
		{
			await RelicCmd.Replace(starter, replacement);
		}
		else
		{
			await RelicCmd.Obtain(replacement, owner);
		}
	}
}
