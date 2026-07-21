using System.Reflection;
using HarmonyLib;
using HextechRunes;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Relics;

namespace HextechRunesSponsorPack;

internal static class AbyssalContractHooks
{
	private const string HarmonyId = "Natsuki.HextechRunesSponsorPack.AbyssalContract";
	private static readonly object InstallLock = new();
	private static Harmony? _harmony;
	private static bool _installed;

	internal static void Install()
	{
		lock (InstallLock)
		{
			if (_installed)
			{
				return;
			}

			MethodInfo forge = AccessTools.Method(
				typeof(ForgeCmd),
				nameof(ForgeCmd.Forge),
				[typeof(decimal), typeof(Player), typeof(AbstractModel)])
				?? throw new MissingMethodException(typeof(ForgeCmd).FullName, nameof(ForgeCmd.Forge));
			MethodInfo channelOrb = AccessTools.Method(
				typeof(OrbCmd),
				nameof(OrbCmd.Channel),
				[typeof(PlayerChoiceContext), typeof(OrbModel), typeof(Player)])
				?? throw new MissingMethodException(typeof(OrbCmd).FullName, nameof(OrbCmd.Channel));

			Harmony harmony = _harmony ??= new Harmony(HarmonyId);
			try
			{
				harmony.Patch(
					forge,
					prefix: new HarmonyMethod(typeof(AbyssalContractHooks), nameof(ForgePrefix)));
				harmony.Patch(
					channelOrb,
					prefix: new HarmonyMethod(typeof(AbyssalContractHooks), nameof(ChannelOrbPrefix)));
			}
			catch
			{
				harmony.UnpatchAll(HarmonyId);
				_harmony = null;
				throw;
			}

			_installed = true;
			Log.Info($"[{ModInfo.Id}] Abyssal Contract hooks installed.");
		}
	}

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
