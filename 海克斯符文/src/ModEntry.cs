using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Rooms;
using MonoMod.RuntimeDetour;

namespace HextechRunes;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
	private static Hook? _finalizeStartingRelicsHook;

	private static Hook? _startRunHook;

	private static Hook? _eventRoomProceedHook;

	private static bool _subscribedRoomEntered;

	private static bool _subscribedRoomExited;

	private delegate Task OrigFinalizeStartingRelics(RunManager self);

	private delegate Task OrigStartRun(NGame self, RunState runState);

	private delegate Task OrigEventRoomProceed();

	public static void Initialize()
	{
		HextechModelBootstrap.Install();
		InstallHooks();
		HextechCombatHooks.Install();
		HextechInspectHooks.Install();
		AssetHooks.Install();
		CollectionHooks.Install();
		Log.Info($"[{ModInfo.Id}] Loaded for Slay the Spire 2 {ModInfo.TargetGameVersion}.");
	}

	private static void InstallHooks()
	{
		_finalizeStartingRelicsHook = new Hook(
			RequireMethod(typeof(RunManager), nameof(RunManager.FinalizeStartingRelics), BindingFlags.Instance | BindingFlags.Public),
			FinalizeStartingRelicsDetour);
		_startRunHook = new Hook(
			RequireMethod(typeof(NGame), "StartRun", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, typeof(RunState)),
			StartRunDetour);
		_eventRoomProceedHook = new Hook(
			RequireMethod(typeof(NEventRoom), nameof(NEventRoom.Proceed), BindingFlags.Public | BindingFlags.Static),
			EventRoomProceedDetour);
	}

	private static async Task FinalizeStartingRelicsDetour(OrigFinalizeStartingRelics orig, RunManager self)
	{
		await orig(self);

		RunState? runState = self.DebugOnlyGetState();
		if (runState == null)
		{
			return;
		}

		foreach (Player player in runState.Players)
		{
			HextechRuneSelectionCoordinator.RemoveRunesFromGrabBags(player);
		}
	}

	private static async Task StartRunDetour(OrigStartRun orig, NGame self, RunState runState)
	{
		HextechRuneSelectionCoordinator.ResetActSelectionState();
		HextechEnemyUi.Clear();
		HextechEnemyUi.HideMayhemModifierBadge();
		SubscribeRoomEnteredIfNeeded();
		SubscribeRoomExitedIfNeeded();
		Log.Info($"[{ModInfo.Id}][Mayhem] StartRunDetour begin: seed={runState.Rng.StringSeed} actIndex={runState.CurrentActIndex} startedWithNeow={runState.ExtraFields.StartedWithNeow}");
		await orig(self, runState);

		HextechMayhemModifier modifier = EnsureMayhemModifier(runState);
		Log.Info($"[{ModInfo.Id}][Mayhem] StartRunDetour end: currentRoom={runState.CurrentRoom?.GetType().Name ?? "null"} actIndex={runState.CurrentActIndex} {DescribeCurrentEventState(runState)}");
		HextechEnemyUi.HideMayhemModifierBadge();
		HextechEnemyUi.Refresh(modifier);
		if (!modifier.IsActResolved(runState.CurrentActIndex)
			&& IsCurrentRun(runState))
		{
			if (ShouldDeferAct0SelectionUntilAfterNeow(runState))
			{
				Log.Info($"[{ModInfo.Id}][Mayhem] StartRunDetour: deferring act0 selection until post-Neow map entry {DescribeCurrentEventState(runState)}");
			}
			else
			{
				Log.Info($"[{ModInfo.Id}][Mayhem] StartRunDetour: selecting act{runState.CurrentActIndex} hex immediately after StartRun");
				await HextechRuneSelectionCoordinator.HandleActSelection(runState, modifier);
			}
		}
	}

	private static async Task EventRoomProceedDetour(OrigEventRoomProceed orig)
	{
		bool shouldSelectAfterProceed = TryGetPendingAncientProceedSelection(out RunState runState, out string eventId);
		if (shouldSelectAfterProceed)
		{
			Log.Info($"[{ModInfo.Id}][Mayhem] EventRoomProceed begin: event={eventId} {DescribeCurrentEventState(runState)}");
		}

		await orig();

		if (!shouldSelectAfterProceed)
		{
			return;
		}

		if (!IsCurrentRun(runState))
		{
			Log.Info($"[{ModInfo.Id}][Mayhem] EventRoomProceed skip: run changed after proceed event={eventId}");
			return;
		}

		HextechMayhemModifier? modifier = GetMayhemModifier(runState);
		if (modifier == null)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] EventRoomProceed skip: no modifier after proceed event={eventId}");
			return;
		}

		if (modifier.IsActResolved(0))
		{
			Log.Info($"[{ModInfo.Id}][Mayhem] EventRoomProceed skip: act0 already resolved event={eventId}");
			return;
		}

		if (NGame.Instance != null)
		{
			await NGame.Instance.ToSignal(NGame.Instance.GetTree(), SceneTree.SignalName.ProcessFrame);
		}

		if (!IsCurrentRun(runState))
		{
			Log.Info($"[{ModInfo.Id}][Mayhem] EventRoomProceed skip: stale after frame event={eventId}");
			return;
		}

		Log.Info($"[{ModInfo.Id}][Mayhem] EventRoomProceed: triggering act0 selection after ancient proceed event={eventId} mapOpen={NMapScreen.Instance?.IsOpen == true}");
		await HextechRuneSelectionCoordinator.HandleActSelection(runState, modifier);
	}

	private static HextechMayhemModifier? GetMayhemModifier(RunState runState)
	{
		return runState.Modifiers.OfType<HextechMayhemModifier>().LastOrDefault();
	}

	private static void SubscribeRoomEnteredIfNeeded()
	{
		if (_subscribedRoomEntered)
		{
			return;
		}

		RunManager.Instance.RoomEntered += OnRoomEntered;
		_subscribedRoomEntered = true;
	}

	private static void SubscribeRoomExitedIfNeeded()
	{
		if (_subscribedRoomExited)
		{
			return;
		}

		RunManager.Instance.RoomExited += OnRoomExited;
		_subscribedRoomExited = true;
	}

	private static void OnRoomEntered()
	{
		if (RunManager.Instance.DebugOnlyGetState() is not RunState runState)
		{
			Log.Info($"[{ModInfo.Id}][Mayhem] OnRoomEntered: no run state");
			return;
		}

		HextechMayhemModifier? modifier = GetMayhemModifier(runState);
		Log.Info($"[{ModInfo.Id}][Mayhem] OnRoomEntered: room={runState.CurrentRoom?.GetType().Name ?? "null"} actIndex={runState.CurrentActIndex} actResolved={modifier?.IsActResolved(runState.CurrentActIndex)} startedWithNeow={runState.ExtraFields.StartedWithNeow} {DescribeCurrentEventState(runState)}");
		if (runState.CurrentRoom is EventRoom { CanonicalEvent: AncientEventModel ancientEvent }
			&& modifier != null
			&& runState.CurrentActIndex == 0
			&& runState.ExtraFields.StartedWithNeow
			&& !modifier.IsActResolved(0))
		{
			Log.Info($"[{ModInfo.Id}][Mayhem] OnRoomEntered: ancient start event detected. event={ancientEvent.Id.Entry} actResolved={modifier.IsActResolved(0)} {DescribeCurrentEventState(runState)}");
		}
		if (runState.CurrentRoom is MapRoom
			&& modifier != null
			&& !modifier.IsActResolved(runState.CurrentActIndex))
		{
			Log.Info($"[{ModInfo.Id}][Mayhem] OnRoomEntered: scheduling selection for MapRoom");
			TaskHelper.RunSafely(HextechRuneSelectionCoordinator.HandleActSelection(runState, modifier));
		}

		HextechEnemyUi.HideMayhemModifierBadge();
		if (modifier != null)
		{
			HextechEnemyUi.Refresh(modifier);
		}
	}

	private static void OnRoomExited()
	{
		try
		{
			if (RunManager.Instance.DebugOnlyGetState() is not RunState runState)
			{
				Log.Info($"[{ModInfo.Id}][Mayhem] OnRoomExited: no run state");
				return;
			}

			MapPointHistoryEntry? currentHistory = runState.CurrentMapPointHistoryEntry;
			IReadOnlyList<MapPointRoomHistoryEntry>? rooms = currentHistory?.Rooms;
			MapPointRoomHistoryEntry? roomHistory = rooms != null && rooms.Count > 0 ? rooms[^1] : null;
			string modelEntry = roomHistory?.ModelId?.Entry ?? "null";
			Log.Info($"[{ModInfo.Id}][Mayhem] OnRoomExited: currentRoom={(runState.CurrentRoom?.GetType().Name ?? "null")} lastHistoryRoom={roomHistory?.RoomType} model={modelEntry}");
		}
		catch (Exception ex)
		{
			Log.Error($"[{ModInfo.Id}][Mayhem] OnRoomExited failed: {ex}");
		}
	}

	internal static HextechMayhemModifier EnsureMayhemModifier(RunState runState)
	{
		if (GetMayhemModifier(runState) is HextechMayhemModifier existing)
		{
			existing.ResetForNewRun();
			Log.Info($"[{ModInfo.Id}][Mayhem] EnsureMayhemModifier: reset existing state");
			return existing;
		}

		HextechMayhemModifier modifier = (HextechMayhemModifier)ModelDb.Modifier<HextechMayhemModifier>().ToMutable();
		modifier.ResetForNewRun();
		modifier.OnRunLoaded(runState);
		runState.AddModifierDebug(modifier);
		Log.Info($"[{ModInfo.Id}][Mayhem] EnsureMayhemModifier: added");
		return modifier;
	}

	internal static Task HandleHextechActStarted(HextechMayhemModifier modifier)
	{
		return HextechRuneSelectionCoordinator.HandleActStarted(modifier);
	}

	private static bool IsCurrentRun(RunState runState)
	{
		return ReferenceEquals(RunManager.Instance.DebugOnlyGetState(), runState);
	}

	private static bool ShouldDeferAct0SelectionUntilAfterNeow(RunState runState)
	{
		return runState.CurrentActIndex == 0
			&& runState.ExtraFields.StartedWithNeow
			&& runState.CurrentRoom is EventRoom { CanonicalEvent: AncientEventModel };
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

	private static bool TryGetPendingAncientProceedSelection(out RunState runState, out string eventId)
	{
		runState = null!;
		eventId = "null";

		if (RunManager.Instance.DebugOnlyGetState() is not RunState currentRunState
			|| currentRunState.CurrentActIndex != 0
			|| !currentRunState.ExtraFields.StartedWithNeow
			|| currentRunState.CurrentRoom is not EventRoom { CanonicalEvent: AncientEventModel ancientEvent })
		{
			return false;
		}

		if (GetMayhemModifier(currentRunState)?.IsActResolved(0) == true)
		{
			return false;
		}

		runState = currentRunState;
		eventId = ancientEvent.Id.Entry;
		return true;
	}

	private static MethodInfo RequireMethod(Type type, string name, BindingFlags flags, params Type[] parameters)
	{
		return type.GetMethod(name, flags, binder: null, parameters, modifiers: null)
			?? throw new InvalidOperationException($"Could not find required method {type.FullName}.{name}.");
	}

	private static FieldInfo RequireField(Type type, string name)
	{
		return type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new InvalidOperationException($"Could not find required field {type.FullName}.{name}.");
	}
}
