namespace HextechRunes;

internal sealed partial class HextechMayhemModifier
{
	public override async Task AfterActEntered()
	{
		ClearActiveExtraStage();
		int actIndex = GetCurrentActSelectionIndex();
		if (!IsStageResolved(actIndex) && TryRecoverResolvedActsFromPlayerRelics(nameof(AfterActEntered), actIndex))
		{
			HextechEnemyUi.Refresh(this);
		}

		if (RunState.CurrentActIndex <= 0 || IsStageResolved(actIndex))
		{
			return;
		}

		if (ShouldDeferImmediateActSelection(RunState.CurrentRoom))
		{
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] AfterActEntered: deferring act selection until room/event flow is stable actIndex={actIndex} currentRoom={RunState.CurrentRoom?.GetType().Name ?? "null"}");
			return;
		}

		HextechLog.Info($"[{ModInfo.Id}][Mayhem] AfterActEntered: resolving act selection before first room actIndex={actIndex}");
		await HextechRuneSelectionCoordinator.HandleStageSelection(RunState, this, actIndex);
	}

	public override async Task BeforeRoomEntered(AbstractRoom room)
	{
		string? extraStageId = HextechRunesInterop.GetCurrentExtraActId(RunState);
		int actIndex = string.IsNullOrWhiteSpace(extraStageId)
			? GetCurrentActSelectionIndex()
			: ActivateExtraStage(extraStageId);
		if (!IsStageResolved(actIndex) && TryRecoverResolvedActsFromPlayerRelics(nameof(BeforeRoomEntered), actIndex))
		{
			HextechEnemyUi.Refresh(this);
		}

		if (actIndex < 0 || IsStageResolved(actIndex) || room is EventRoom or MapRoom)
		{
			return;
		}

		if (RunState.CurrentActIndex == 0 && string.IsNullOrWhiteSpace(extraStageId))
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] BeforeRoomEntered: skipping unsafe act0 selection before room={room.GetType().Name}; waiting for post-Neow or map path");
			return;
		}

		HextechLog.Info($"[{ModInfo.Id}][Mayhem] BeforeRoomEntered: resolving pending act selection before room={room.GetType().Name} actIndex={actIndex}");
		await HextechRuneSelectionCoordinator.HandleStageSelection(RunState, this, actIndex);
	}

	private static bool ShouldDeferImmediateActSelection(AbstractRoom? currentRoom)
	{
		return currentRoom == null
			|| currentRoom is EventRoom { CanonicalEvent: AncientEventModel };
	}
}
