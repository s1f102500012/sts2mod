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
