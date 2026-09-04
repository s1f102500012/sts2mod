using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.PeerInput;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic;
using MegaCrit.Sts2.Core.Runs;

namespace AITeammate.Scripts;

internal static class AiTeammatePeerInputPatches
{
    [HarmonyPatch(typeof(NRun), nameof(NRun._Process))]
    private static class NRunProcessPatch
    {
        private static void Postfix()
        {
            RunState? runState = RunManager.Instance?.DebugOnlyGetState();
            try
            {
                AiTeammateAutopilotUi.AttachToRun(runState);
            }
            catch (Exception ex)
            {
                Log.Warn($"[AITeammate] Failed to attach run autopilot UI: {ex.Message}");
            }

            AiTeammateSessionState? session = AiTeammateSessionRegistry.ActiveRunSession;
            if (AiTeammateSessionRegistry.IsRunAbandoning)
            {
                return;
            }

            if (session == null)
            {
                if (!AiTeammateSessionRegistry.AutopilotEnabled ||
                    !AiTeammateSessionRegistry.TryEnsureStandaloneHostController(runState, out AiTeammateDummyController standaloneHostController))
                {
                    return;
                }

                try
                {
                    AiTeammateMapAndTreasurePatches.TryAutoSelectMapNode();
                }
                catch (Exception ex)
                {
                    Log.Warn($"[AITeammate] Host autopilot map tick failed: {ex.Message}");
                }

                try
                {
                    AiTeammateMapAndTreasurePatches.TryAutoHandleTreasureRoom();
                }
                catch (Exception ex)
                {
                    Log.Warn($"[AITeammate] Treasure room automation tick failed: {ex.Message}");
                }

                try
                {
                    standaloneHostController.Tick();
                }
                catch (Exception ex)
                {
                    Log.Warn($"[AITeammate] Host autopilot tick failed for player={standaloneHostController.PlayerId}: {ex.Message}");
                }

                return;
            }

            AiTeammatePeerInputStateSync.EnsureAiPeerInputStates(session);
            try
            {
                AiTeammateMapAndTreasurePatches.TryAutoSelectMapNode();
            }
            catch (Exception ex)
            {
                Log.Warn($"[AITeammate] Host autopilot map tick failed: {ex.Message}");
            }

            try
            {
                AiTeammateMapAndTreasurePatches.TryAutoHandleTreasureRoom();
            }
            catch (Exception ex)
            {
                Log.Warn($"[AITeammate] Treasure room automation tick failed: {ex.Message}");
            }

            foreach (AiTeammateDummyController controller in session.AiControllers.Values)
            {
                try
                {
                    controller.Tick();
                }
                catch (Exception ex)
                {
                    Log.Warn($"[AITeammate] AI controller tick failed for player={controller.PlayerId}: {ex.Message}");
                }
            }

            if (AiTeammateSessionRegistry.AutopilotEnabled)
            {
                try
                {
                    session.HostController.Tick();
                }
                catch (Exception ex)
                {
                    Log.Warn($"[AITeammate] Host autopilot tick failed for player={session.HostController.PlayerId}: {ex.Message}");
                }
            }
        }
    }
}

internal static class AiTeammatePeerInputStateSync
{
    private static readonly HashSet<ulong> InitializedPeerInputPlayers = [];
    private static readonly MethodInfo? GetOrCreatePeerInputStateMethod =
        AccessTools.Method(typeof(PeerInputSynchronizer), "GetOrCreateStateForPlayer");
    private static readonly Type? PeerInputStateType =
        AccessTools.Inner(typeof(PeerInputSynchronizer), "PeerInputState");
    private static readonly FieldInfo? PeerInputStateNetMousePositionField =
        PeerInputStateType != null ? AccessTools.Field(PeerInputStateType, "netMousePosition") : null;
    private static readonly FieldInfo? PeerInputStateControllerFocusPositionField =
        PeerInputStateType != null ? AccessTools.Field(PeerInputStateType, "controllerFocusPosition") : null;
    private static readonly FieldInfo? PeerInputStateIsMouseDownField =
        PeerInputStateType != null ? AccessTools.Field(PeerInputStateType, "isMouseDown") : null;
    private static readonly FieldInfo? PeerInputStateIsUsingControllerField =
        PeerInputStateType != null ? AccessTools.Field(PeerInputStateType, "isUsingController") : null;
    private static readonly FieldInfo? PeerInputStateNetScreenTypeField =
        PeerInputStateType != null ? AccessTools.Field(PeerInputStateType, "netScreenType") : null;
    private static readonly FieldInfo? StateChangedEventField =
        AccessTools.Field(typeof(PeerInputSynchronizer), "StateChanged");
    private static readonly FieldInfo? ScreenChangedEventField =
        AccessTools.Field(typeof(PeerInputSynchronizer), "ScreenChanged");
    private static readonly FieldInfo? TreasureRoomRelicCollectionField =
        AccessTools.Field(typeof(NTreasureRoom), "_relicCollection");
    private static readonly FieldInfo? RelicCollectionHoldersInUseField =
        AccessTools.Field(typeof(NTreasureRoomRelicCollection), "_holdersInUse");

    public static void EnsureAiPeerInputStates(AiTeammateSessionState session)
    {
        PeerInputSynchronizer synchronizer = RunManager.Instance.InputSynchronizer;
        bool isInSharedRelicPicking = IsSharedRelicPickingUiActive();
        NetScreenType desiredScreenType = isInSharedRelicPicking
            ? NetScreenType.SharedRelicPicking
            : NetScreenType.Room;

        int aiIndex = 0;
        foreach (AiTeammateSessionParticipant participant in session.Participants.Where(static participant => !participant.IsHost))
        {
            object? state = GetOrCreatePeerInputStateMethod?.Invoke(synchronizer, new object[] { participant.PlayerId });
            if (state == null)
            {
                continue;
            }

            if (InitializedPeerInputPlayers.Add(participant.PlayerId))
            {
                Log.Info($"[AITeammate] Created peer input state for AI player={participant.PlayerId}");
            }

            SyncAiPeerInputState(synchronizer, state, participant.PlayerId, aiIndex, desiredScreenType);
            aiIndex++;
        }
    }

    private static void SyncAiPeerInputState(
        PeerInputSynchronizer synchronizer,
        object state,
        ulong playerId,
        int aiIndex,
        NetScreenType desiredScreenType)
    {
        bool changed = false;
        changed |= SetFieldValue(PeerInputStateNetScreenTypeField, state, desiredScreenType);
        changed |= SetFieldValue(PeerInputStateIsMouseDownField, state, false);
        changed |= SetFieldValue(PeerInputStateIsUsingControllerField, state, false);

        Vector2 desiredPointingPosition = GetDesiredPointerPosition(playerId, aiIndex, desiredScreenType);
        changed |= SetFieldValue(PeerInputStateNetMousePositionField, state, desiredPointingPosition);
        changed |= SetFieldValue(PeerInputStateControllerFocusPositionField, state, desiredPointingPosition);

        if (!changed)
        {
            return;
        }

        RaiseStateChanged(synchronizer, playerId);
        if (desiredScreenType == NetScreenType.SharedRelicPicking)
        {
            Log.Info($"[AITeammate] Synced AI peer input for shared relic picking. player={playerId} slot={aiIndex}");
        }
    }

    private static bool SetFieldValue<T>(FieldInfo? field, object target, T value)
    {
        if (field == null)
        {
            return false;
        }

        object? currentValue = field.GetValue(target);
        if (Equals(currentValue, value))
        {
            return false;
        }

        field.SetValue(target, value);
        return true;
    }

    private static void RaiseStateChanged(PeerInputSynchronizer synchronizer, ulong playerId)
    {
        (StateChangedEventField?.GetValue(synchronizer) as Action<ulong>)?.Invoke(playerId);
        (ScreenChangedEventField?.GetValue(synchronizer) as Action<ulong, NetScreenType>)?.Invoke(playerId, synchronizer.GetScreenType(playerId));
    }

    private static bool IsSharedRelicPickingUiActive()
    {
        return NRun.Instance?.TreasureRoom is { DefaultFocusedControl: not null };
    }

    private static Vector2 GetDesiredPointerPosition(ulong playerId, int aiIndex, NetScreenType desiredScreenType)
    {
        if (desiredScreenType == NetScreenType.SharedRelicPicking &&
            TryGetNormalizedRelicHolderPosition(playerId, out Vector2 holderPosition))
        {
            return holderPosition;
        }

        return new Vector2(0.3f + aiIndex * 0.2f, 0.72f);
    }

    private static bool TryGetNormalizedRelicHolderPosition(ulong playerId, out Vector2 normalizedPosition)
    {
        normalizedPosition = default;

        NTreasureRoom? treasureRoom = NRun.Instance?.TreasureRoom;
        if (treasureRoom == null)
        {
            return false;
        }

        MegaCrit.Sts2.Core.Entities.Players.Player? player = RunManager.Instance.DebugOnlyGetState()?.GetPlayer(playerId);
        int? voteIndex = null;
        if (player != null)
        {
            TreasureRoomRelicSynchronizer.PlayerVote playerVote = RunManager.Instance.TreasureRoomRelicSynchronizer.GetPlayerVote(player);
            if (playerVote.voteReceived)
            {
                voteIndex = playerVote.index;
            }
        }

        if (!voteIndex.HasValue)
        {
            return false;
        }

        if (TreasureRoomRelicCollectionField?.GetValue(treasureRoom) is not NTreasureRoomRelicCollection relicCollection ||
            RelicCollectionHoldersInUseField?.GetValue(relicCollection) is not List<NTreasureRoomRelicHolder> holders)
        {
            return false;
        }

        NTreasureRoomRelicHolder? holder = holders.FirstOrDefault(candidate => candidate.Index == voteIndex.Value);
        if (holder == null || !holder.IsInsideTree())
        {
            return false;
        }

        Vector2 viewportSize = holder.GetViewportRect().Size;
        if (viewportSize.X <= 0f || viewportSize.Y <= 0f)
        {
            return false;
        }

        Vector2 center = holder.GlobalPosition + holder.Size * 0.5f;
        normalizedPosition = new Vector2(
            Mathf.Clamp(center.X / viewportSize.X, 0.05f, 0.95f),
            Mathf.Clamp(center.Y / viewportSize.Y, 0.05f, 0.95f));
        return true;
    }
}
