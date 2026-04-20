using System.Reflection;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Singleton;
using MonoMod.RuntimeDetour;

namespace StS1Act4;

internal static class Act4MultiplayerScalingHooks
{
	private static Hook? _getMultiplayerScalingHook;

	private static bool _loggedFallback;

	private delegate decimal OrigGetMultiplayerScaling(EncounterModel? encounter, int actIndex);

	public static void Install()
	{
		MethodInfo getMultiplayerScaling = typeof(MultiplayerScalingModel).GetMethod(
			nameof(MultiplayerScalingModel.GetMultiplayerScaling),
			BindingFlags.Public | BindingFlags.Static,
			binder: null,
			new[] { typeof(EncounterModel), typeof(int) },
			modifiers: null)
			?? throw new InvalidOperationException("Could not find MultiplayerScalingModel.GetMultiplayerScaling(EncounterModel?, int).");

		_getMultiplayerScalingHook = new Hook(getMultiplayerScaling, GetMultiplayerScalingDetour);
	}

	private static decimal GetMultiplayerScalingDetour(OrigGetMultiplayerScaling orig, EncounterModel? encounter, int actIndex)
	{
		if (actIndex <= 2)
		{
			return orig(encounter, actIndex);
		}

		if (!_loggedFallback)
		{
			string encounterName = encounter?.Id.Entry ?? "<null>";
			Log.Warn($"[{ModInfo.Id}] Reusing final native multiplayer scaling tier for actIndex {actIndex} encounter {encounterName}.");
			_loggedFallback = true;
		}

		return orig(encounter, 2);
	}
}
