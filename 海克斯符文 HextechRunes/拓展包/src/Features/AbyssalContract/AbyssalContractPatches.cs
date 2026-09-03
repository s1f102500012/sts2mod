using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Relics;

namespace HextechRunesSponsorPack;

// 两个补丁都只在深渊契约实际持有对应契约时才改变行为:
// - ForgeCmd.Forge:摄政契约「只有击剑手册能锻造」的门控。原版这条路径上只有事后的 Hook.AfterForge
//   (0.111 ForgeCmd 第 56 行),没有事前拦截口子,所以是跳过型前缀;它不复制任何原版逻辑。
// - OrbCmd.Channel:自动机契约把充能球一律换成闪电。原版没有 ModifyOrbBeingChanneled 之类的钩子,
//   这是非跳过的参数改写前缀。
internal static class AbyssalContractPatches
{
	[SponsorPatch("abyssal.regent-forge", "深渊契约·摄政")]
	[HarmonyPatch(typeof(ForgeCmd), nameof(ForgeCmd.Forge), [typeof(decimal), typeof(Player), typeof(AbstractModel)])]
	internal static class RegentForgePatch
	{
		[HarmonyPrefix]
		private static bool ForgePrefix(
			Player player,
			AbstractModel? source,
			ref Task<IEnumerable<SovereignBlade>> __result)
		{
			AbyssalContractRune? rune = player.GetRelic<AbyssalContractRune>();
			if (rune == null
				|| !rune.HasContract(AbyssalContractKind.Regent)
				|| source is FencingManual)
			{
				return true;
			}

			rune.Flash();
			__result = Task.FromResult<IEnumerable<SovereignBlade>>(Array.Empty<SovereignBlade>());
			return false;
		}
	}

	[SponsorPatch("abyssal.automaton-channel", "深渊契约·自动机")]
	[HarmonyPatch(typeof(OrbCmd), nameof(OrbCmd.Channel), [typeof(PlayerChoiceContext), typeof(OrbModel), typeof(Player)])]
	internal static class AutomatonChannelPatch
	{
		[HarmonyPrefix]
		private static void ChannelOrbPrefix(ref OrbModel orb, Player player)
		{
			AbyssalContractRune? rune = player.GetRelic<AbyssalContractRune>();
			if (rune == null
				|| !rune.HasContract(AbyssalContractKind.Automaton)
				|| orb is LightningOrb)
			{
				return;
			}

			orb = ModelDb.Orb<LightningOrb>().ToMutable();
		}
	}
}
