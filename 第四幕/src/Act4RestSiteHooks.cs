using System.Reflection;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Runs;
using MonoMod.RuntimeDetour;

namespace StS1Act4;

internal static class Act4RestSiteHooks
{
	private static readonly Logger Logger = new("StS1Act4.RestSite", LogType.Generic);

	private static Hook? _restSiteBgPathHook;

	private static Hook? _restSiteCharacterReadyHook;

	private static readonly FieldInfo RunStateCurrentActIndexField = typeof(RunState).GetField("_currentActIndex", BindingFlags.Instance | BindingFlags.NonPublic)
		?? throw new InvalidOperationException("Could not access RunState._currentActIndex.");

	private delegate string OrigGetRestSiteBackgroundPath(ActModel self);

	private delegate void OrigRestSiteCharacterReady(NRestSiteCharacter self);

	public static void Install()
	{
		MethodInfo restSiteBgGetter = typeof(ActModel).GetProperty(nameof(ActModel.RestSiteBackgroundPath), BindingFlags.Instance | BindingFlags.Public)?.GetMethod
			?? throw new InvalidOperationException("Could not find ActModel.RestSiteBackgroundPath getter.");
		MethodInfo readyMethod = typeof(NRestSiteCharacter).GetMethod(nameof(NRestSiteCharacter._Ready), BindingFlags.Instance | BindingFlags.Public)
			?? throw new InvalidOperationException("Could not find NRestSiteCharacter._Ready.");

		_restSiteBgPathHook = new Hook(restSiteBgGetter, RestSiteBackgroundPathDetour);
		_restSiteCharacterReadyHook = new Hook(readyMethod, RestSiteCharacterReadyDetour);
	}

	private static string RestSiteBackgroundPathDetour(OrigGetRestSiteBackgroundPath orig, ActModel self)
	{
		if (self.Id == ModelDb.GetId<Sts1Act4>())
		{
			Logger.Info("Using overgrowth rest site background for act4.");
			return "res://scenes/rest_site/overgrowth_rest_site.tscn";
		}

		return orig(self);
	}

	private static void RestSiteCharacterReadyDetour(OrigRestSiteCharacterReady orig, NRestSiteCharacter self)
	{
		if (self.Player?.RunState is not RunState runState || runState.CurrentActIndex < 3)
		{
			orig(self);
			return;
		}

		int original = runState.CurrentActIndex;
		try
		{
			Logger.Info($"NRestSiteCharacter._Ready detour hit. act={original} -> temp=2");
			RunStateCurrentActIndexField.SetValue(runState, 2);
			orig(self);
		}
		finally
		{
			RunStateCurrentActIndexField.SetValue(runState, original);
		}
	}
}
