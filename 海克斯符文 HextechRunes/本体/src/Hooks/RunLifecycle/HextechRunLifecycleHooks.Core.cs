using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

internal static partial class HextechRunLifecycleHooks
{
	private static bool _subscribedRoomEntered;
	private static bool _subscribedRoomExited;
	private static RunManager? _subscribedRoomEnteredManager;
	private static RunManager? _subscribedRoomExitedManager;
	private static HashSet<RunState>? _runsInsideStartRunOrig;

	private static HashSet<RunState> RunsInsideStartRunOrig => _runsInsideStartRunOrig ??= new HashSet<RunState>();

	private readonly record struct EventRoomProceedState(bool ShouldSelectAfterProceed, RunState RunState, int ActIndex, string EventId);

	public static void Install(Harmony harmony)
	{
		harmony.Patch(
			RequireMethod(typeof(RunManager), nameof(RunManager.FinalizeStartingRelics), BindingFlags.Instance | BindingFlags.Public),
			postfix: new HarmonyMethod(typeof(HextechRunLifecycleHooks), nameof(FinalizeStartingRelicsPostfix)));
		harmony.Patch(
			RequireMethod(typeof(NGame), "StartRun", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, typeof(RunState)),
			prefix: new HarmonyMethod(typeof(HextechRunLifecycleHooks), nameof(StartRunPrefix)),
			postfix: new HarmonyMethod(typeof(HextechRunLifecycleHooks), nameof(StartRunPostfix)));
		harmony.Patch(
			RequireMethod(typeof(NGame), "LoadRun", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, typeof(RunState), typeof(SerializableRoom)),
			postfix: new HarmonyMethod(typeof(HextechRunLifecycleHooks), nameof(LoadRunPostfix)));
		harmony.Patch(
			RequireMethod(typeof(NTopBar), nameof(NTopBar.Initialize), BindingFlags.Instance | BindingFlags.Public, typeof(IRunState)),
			postfix: new HarmonyMethod(typeof(HextechRunLifecycleHooks), nameof(TopBarInitializePostfix)));
		harmony.Patch(
			RequireMethod(typeof(NEventRoom), nameof(NEventRoom.Proceed), BindingFlags.Public | BindingFlags.Static),
			prefix: new HarmonyMethod(typeof(HextechRunLifecycleHooks), nameof(EventRoomProceedPrefix)),
			postfix: new HarmonyMethod(typeof(HextechRunLifecycleHooks), nameof(EventRoomProceedPostfix)));
		harmony.Patch(
			RequireMethod(typeof(RunManager), nameof(RunManager.OnEnded), BindingFlags.Instance | BindingFlags.Public, typeof(bool)),
			prefix: new HarmonyMethod(typeof(HextechRunLifecycleHooks), nameof(RunEndedPrefix)),
			postfix: new HarmonyMethod(typeof(HextechRunLifecycleHooks), nameof(RunEndedPostfix)));
	}

	internal static HextechMayhemModifier EnsureMayhemModifier(RunState runState)
	{
		if (HextechMayhemModifier.FindIn(runState) is HextechMayhemModifier existing)
		{
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] EnsureMayhemModifier: existing state preserved {existing.DescribeActState()}");
			return existing;
		}

		HextechMayhemModifier modifier = (HextechMayhemModifier)ModelDb.Modifier<HextechMayhemModifier>().ToMutable();
		modifier.ResetForNewRun();
		modifier.OnRunLoaded(runState);
		runState.AddModifierDebug(modifier);
		HextechLog.Info($"[{ModInfo.Id}][Mayhem] EnsureMayhemModifier: added");
		return modifier;
	}

	internal static Task HandleHextechActStarted(HextechMayhemModifier modifier)
	{
		return HextechRuneSelectionCoordinator.HandleActStarted(modifier);
	}

	private static HextechMayhemModifier GetOrRecoverMayhemModifier(RunState runState, string reason)
	{
		if (HextechMayhemModifier.FindIn(runState) is HextechMayhemModifier existing)
		{
			return existing;
		}

		Log.Warn($"[{ModInfo.Id}][Mayhem] {reason}; reattaching");
		return EnsureMayhemModifier(runState);
	}

	private static bool IsCurrentRun(RunState runState)
	{
		return ReferenceEquals(RunManager.Instance.DebugOnlyGetState(), runState);
	}

	private static bool ShouldDeferActSelectionUntilAfterCurrentEvent(RunState runState)
	{
		return runState.CurrentActIndex >= 0
			&& runState.CurrentRoom is EventRoom { CanonicalEvent: AncientEventModel };
	}

	private static int ResolveCurrentStageIndex(
		RunState runState,
		HextechMayhemModifier modifier,
		out string? extraStageId)
	{
		extraStageId = HextechRunesInterop.GetCurrentExtraActId(runState);
		if (!string.IsNullOrWhiteSpace(extraStageId))
		{
			return modifier.ActivateExtraStage(extraStageId);
		}

		modifier.ClearActiveExtraStage();
		return modifier.GetCurrentActSelectionIndex();
	}

	private static string DescribeCurrentEventState(RunState runState)
	{
		if (runState.CurrentRoom is not EventRoom eventRoom)
		{
			return "eventState=none";
		}

		try
		{
			EventModel localEvent = eventRoom.LocalMutableEvent;
			return $"eventState={localEvent.Id.Entry} finished={localEvent.IsFinished} options={localEvent.CurrentOptions.Count}";
		}
		catch (Exception ex)
		{
			return $"eventState={eventRoom.CanonicalEvent.Id.Entry} localUnavailable={ex.GetType().Name}";
		}
	}

	private static async Task WaitOneFrame()
	{
		if (NGame.Instance != null)
		{
			await NGame.Instance.ToSignal(NGame.Instance.GetTree(), SceneTree.SignalName.ProcessFrame);
			return;
		}

		await Task.Yield();
	}
}
