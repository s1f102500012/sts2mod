using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Runs;

namespace AITeammate.Scripts;

internal readonly record struct AiTeammateSessionParticipant(
    int SlotIndex,
    ulong PlayerId,
    CharacterModel Character,
    bool IsHost,
    string DisplayName);

internal sealed class AiTeammateSessionState
{
    private const ulong AiNetIdOffset = 10_000UL;
    private const int HostSlotIndex = 0;
    private const int FirstAiSlotIndex = 1;
    private const int LastAiSlotIndex = 3;

    public AiTeammateSessionState(
        ulong hostPlayerId,
        AiTeammateDummyController hostController,
        IReadOnlyList<AiTeammateSessionParticipant> participants,
        IReadOnlyDictionary<ulong, AiTeammateDummyController> aiControllers,
        bool useTestMap)
    {
        HostPlayerId = hostPlayerId;
        HostController = hostController;
        Participants = participants;
        AiControllers = aiControllers;
        UseTestMap = useTestMap;
    }

    public ulong HostPlayerId { get; }

    public AiTeammateDummyController HostController { get; }

    public IReadOnlyList<AiTeammateSessionParticipant> Participants { get; }

    public IReadOnlyDictionary<ulong, AiTeammateDummyController> AiControllers { get; }

    public bool UseTestMap { get; }

    public bool HasHost => Participants.Any((participant) => participant.IsHost);

    public int AiCount => Participants.Count((participant) => !participant.IsHost);

    public static AiTeammateSessionState? CreateFromSelections(IReadOnlyDictionary<int, string?> selections, bool useTestMap)
    {
        TryResolveHostPlayerId(out ulong hostPlayerId);

        if (!selections.TryGetValue(HostSlotIndex, out string? hostCharacterId) ||
            string.IsNullOrWhiteSpace(hostCharacterId) ||
            !AiTeammatePlaceholderCharacters.TryGetById(hostCharacterId, out AiTeammatePlaceholderCharacter hostCharacterOption))
        {
            return null;
        }

        CharacterModel hostCharacter = hostCharacterOption.ResolveModel();
        List<AiTeammateSessionParticipant> participants =
        [
            new AiTeammateSessionParticipant(
                SlotIndex: HostSlotIndex,
                PlayerId: hostPlayerId,
                Character: hostCharacter,
                IsHost: true,
                DisplayName: AiTeammateParticipantNames.HostDisplayName(hostPlayerId))
        ];

        AiTeammateDummyController hostController = new(HostSlotIndex, hostPlayerId, hostCharacter);
        Dictionary<ulong, AiTeammateDummyController> aiControllers = new();
        HashSet<string> usedAiDisplayNames = new(StringComparer.OrdinalIgnoreCase);
        for (int slotIndex = FirstAiSlotIndex; slotIndex <= LastAiSlotIndex; slotIndex++)
        {
            if (!selections.TryGetValue(slotIndex, out string? aiCharacterId) ||
                string.IsNullOrWhiteSpace(aiCharacterId) ||
                !AiTeammatePlaceholderCharacters.TryGetById(aiCharacterId, out AiTeammatePlaceholderCharacter aiCharacterOption))
            {
                continue;
            }

            ulong playerId = hostPlayerId + AiNetIdOffset + (ulong)slotIndex;
            CharacterModel character = aiCharacterOption.ResolveModel();
            participants.Add(new AiTeammateSessionParticipant(
                SlotIndex: slotIndex,
                PlayerId: playerId,
                Character: character,
                IsHost: false,
                DisplayName: AiTeammateParticipantNames.AiDisplayName(
                    character,
                    playerId,
                    usedAiDisplayNames,
                    slotIndex)));
            aiControllers[playerId] = new AiTeammateDummyController(slotIndex, playerId, character);
        }

        return new AiTeammateSessionState(hostPlayerId, hostController, participants, aiControllers, useTestMap);
    }

    private static bool TryResolveHostPlayerId(out ulong hostPlayerId)
    {
        hostPlayerId = PlatformUtil.GetLocalPlayerId(PlatformUtil.PrimaryPlatform);
        if (hostPlayerId != 0UL)
        {
            return true;
        }

        hostPlayerId = 1UL;
        return false;
    }
}

internal static class AiTeammateSessionRegistry
{
    private static AiTeammateDummyController? _standaloneHostController;
    private static bool _isRunAbandoning;

    public static AiTeammateSessionState? Current { get; private set; }

    public static bool AutopilotEnabled { get; private set; }

    public static event Action<bool>? AutopilotChanged;

    public static bool IsRunAbandoning => _isRunAbandoning;

    public static AiTeammateSessionState? ActiveRunSession =>
        !_isRunAbandoning && Current != null && RunManager.Instance?.NetService is AiTeammateLoopbackHostGameService
            ? Current
            : null;

    public static void SetCurrent(AiTeammateSessionState? session)
    {
        bool shouldResetAutopilot = session == null || Current == null;
        Current = session;
        if (session != null)
        {
            _standaloneHostController = null;
            _isRunAbandoning = false;
        }

        if (shouldResetAutopilot)
        {
            SetAutopilotEnabled(false);
        }
    }

    public static void SetAutopilotEnabled(bool enabled)
    {
        if (AutopilotEnabled == enabled)
        {
            return;
        }

        AutopilotEnabled = enabled;
        if (!enabled)
        {
            _standaloneHostController = null;
        }

        Log.Info($"[AITeammate] Host autopilot {(enabled ? "enabled" : "disabled")}.");
        AutopilotChanged?.Invoke(enabled);
    }

    public static void MarkRunAbandoning(string reason)
    {
        if (!_isRunAbandoning)
        {
            Log.Info($"[AITeammate] Marking AI teammate run abandoning. reason={reason}");
        }

        _isRunAbandoning = true;
        _standaloneHostController = null;
        SetAutopilotEnabled(false);
    }

    public static bool ShouldAutomateAiPlayer(Player? player)
    {
        if (player == null)
        {
            return false;
        }

        AiTeammateSessionState? activeSession = ActiveRunSession;
        if (activeSession?.AiControllers.ContainsKey(player.NetId) == true)
        {
            return true;
        }

        if (!AutopilotEnabled)
        {
            return false;
        }

        if (activeSession is { } session && player.NetId == session.HostPlayerId)
        {
            return true;
        }

        if (player.RunState.Players.Count <= 1)
        {
            return true;
        }

        ulong localPlayerId = PlatformUtil.GetLocalPlayerId(PlatformUtil.PrimaryPlatform);
        if (localPlayerId != 0UL)
        {
            return player.NetId == localPlayerId;
        }

        return _standaloneHostController?.PlayerId == player.NetId;
    }

    public static bool CanUseDirectSelectionAutomation(Player? player)
    {
        if (player == null)
        {
            return false;
        }

        AiTeammateSessionState? activeSession = ActiveRunSession;
        if (activeSession?.AiControllers.ContainsKey(player.NetId) == true)
        {
            return true;
        }

        if (!AutopilotEnabled)
        {
            return false;
        }

        if (activeSession is { } session && player.NetId == session.HostPlayerId)
        {
            return true;
        }

        return player.RunState.Players.Count <= 1 &&
               ShouldAutomateAiPlayer(player);
    }

    public static bool CanShowAutopilotToggle(RunState? runState)
    {
        return TryGetLocalRunPlayer(runState, out _);
    }

    public static bool TryGetAutopilotHostPlayer(RunState? runState, out Player player)
    {
        player = null!;
        if (runState == null)
        {
            return false;
        }

        if (ActiveRunSession is { } session &&
            runState.GetPlayer(session.HostPlayerId) is { } sessionHost)
        {
            player = sessionHost;
            return true;
        }

        return TryGetLocalRunPlayer(runState, out player);
    }

    public static bool TryEnsureStandaloneHostController(RunState? runState, out AiTeammateDummyController controller)
    {
        controller = null!;
        if (ActiveRunSession is { } session)
        {
            controller = session.HostController;
            return true;
        }

        if (!AutopilotEnabled || !TryGetLocalRunPlayer(runState, out Player player))
        {
            return false;
        }

        if (_standaloneHostController?.PlayerId != player.NetId)
        {
            _standaloneHostController = new AiTeammateDummyController(0, player.NetId, player.Character);
            Log.Info($"[AITeammate] Created standalone host autopilot controller for player={player.NetId}, character={player.Character.Id.Entry}");
        }

        controller = _standaloneHostController;
        return true;
    }

    public static bool TryGetAutopilotHostController(out AiTeammateDummyController controller)
    {
        controller = null!;
        if (ActiveRunSession is { } session && AutopilotEnabled)
        {
            controller = session.HostController;
            return true;
        }

        if (_standaloneHostController != null && AutopilotEnabled)
        {
            controller = _standaloneHostController;
            return true;
        }

        return false;
    }

    private static bool TryGetLocalRunPlayer(RunState? runState, out Player player)
    {
        player = null!;
        if (runState?.Players == null || runState.Players.Count == 0)
        {
            return false;
        }

        ulong localPlayerId = PlatformUtil.GetLocalPlayerId(PlatformUtil.PrimaryPlatform);
        if (localPlayerId != 0UL)
        {
            Player? localPlayer = runState.GetPlayer(localPlayerId);
            if (localPlayer != null)
            {
                player = localPlayer;
                return true;
            }

            if (runState.Players.Count != 1)
            {
                return false;
            }
        }

        Player? firstPlayer = runState.Players.FirstOrDefault();
        if (firstPlayer == null)
        {
            return false;
        }

        player = firstPlayer;
        return true;
    }

    public static bool TryGetDisplayName(ulong playerId, out string displayName)
    {
        foreach (AiTeammateSessionParticipant participant in Current?.Participants ?? Array.Empty<AiTeammateSessionParticipant>())
        {
            if (participant.PlayerId == playerId)
            {
                displayName = participant.DisplayName;
                return true;
            }
        }

        displayName = string.Empty;
        return false;
    }

    public static bool TryGetParticipant(ulong playerId, out AiTeammateSessionParticipant participant)
    {
        foreach (AiTeammateSessionParticipant currentParticipant in Current?.Participants ?? Array.Empty<AiTeammateSessionParticipant>())
        {
            if (currentParticipant.PlayerId == playerId)
            {
                participant = currentParticipant;
                return true;
            }
        }

        participant = default;
        return false;
    }

    public static bool ShouldUseTestMap(RunState? runState)
    {
        return ActiveRunSession?.UseTestMap == true &&
               runState != null;
    }
}
