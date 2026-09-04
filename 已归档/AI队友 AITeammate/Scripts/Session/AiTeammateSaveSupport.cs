using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Daily;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace AITeammate.Scripts;

internal sealed class AiTeammateSaveTagData
{
    [JsonPropertyName("host_player_id")]
    public ulong HostPlayerId { get; set; }

    [JsonPropertyName("use_test_map")]
    public bool UseTestMap { get; set; }

    [JsonPropertyName("ai_slots")]
    public List<AiTeammateSaveTagSlot> AiSlots { get; set; } = [];
}

internal sealed class AiTeammateSaveTagSlot
{
    [JsonPropertyName("slot_index")]
    public int SlotIndex { get; set; }

    [JsonPropertyName("player_id")]
    public ulong PlayerId { get; set; }
}

internal sealed class AiTeammateSavedRunContext
{
    public required SerializableRun SaveData { get; init; }

    public required AiTeammateSessionState SessionState { get; init; }
}

internal static class AiTeammateSaveSupport
{
    private const string SaveTagFileName = "ai_teammate_mp.tag";
    private static int _continueSavedRunInProgress;

    public static bool IsContinueSavedRunInProgress => Volatile.Read(ref _continueSavedRunInProgress) != 0;

    public static void MarkCurrentProfile(AiTeammateSessionState sessionState)
    {
        try
        {
            AiTeammateSaveTagData tagData = new()
            {
                HostPlayerId = sessionState.HostPlayerId,
                UseTestMap = sessionState.UseTestMap,
                AiSlots = sessionState.Participants
                    .Where((participant) => !participant.IsHost)
                    .OrderBy((participant) => participant.SlotIndex)
                    .Select((participant) => new AiTeammateSaveTagSlot
                    {
                        SlotIndex = participant.SlotIndex,
                        PlayerId = participant.PlayerId
                    })
                    .ToList()
            };

            string tagPath = GetSaveTagPath();
            string? tagDirectory = Path.GetDirectoryName(tagPath);
            if (!string.IsNullOrEmpty(tagDirectory))
            {
                Directory.CreateDirectory(tagDirectory);
            }

            File.WriteAllText(tagPath, JsonSerializer.Serialize(tagData));
            Log.Info($"[AITeammate] Saved AI teammate tag. host={tagData.HostPlayerId}, ai={tagData.AiSlots.Count}");
        }
        catch (Exception ex)
        {
            Log.Error($"[AITeammate] Failed to write AI teammate save tag: {ex}");
        }
    }

    public static void MarkCurrentRunIfNeeded()
    {
        AiTeammateSessionState? sessionState = AiTeammateSessionRegistry.Current;
        if (sessionState == null || sessionState.AiCount <= 0 || !RunManager.Instance.IsInProgress)
        {
            return;
        }

        if (RunManager.Instance.NetService?.Type != NetGameType.Host)
        {
            return;
        }

        if (RunManager.Instance.NetService is not AiTeammateLoopbackHostGameService)
        {
            return;
        }

        MarkCurrentProfile(sessionState);
    }

    public static void ClearCurrentProfile()
    {
        try
        {
            string tagPath = GetSaveTagPath();
            if (File.Exists(tagPath))
            {
                File.Delete(tagPath);
                Log.Info("[AITeammate] Cleared AI teammate save tag.");
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"[AITeammate] Failed to clear AI teammate save tag: {ex.Message}");
        }
    }

    public static bool HasContinueableSavedRun()
    {
        return TryLoadSavedRun(out _, clearInvalidTag: false);
    }

    public static bool TryLoadSavedRun(out AiTeammateSavedRunContext? context, bool clearInvalidTag = false)
    {
        context = null;

        if (!TryReadCurrentProfile(out AiTeammateSaveTagData? tagData) || tagData == null)
        {
            return false;
        }

        if (tagData.AiSlots.Count == 0)
        {
            return InvalidateSavedRun("[AITeammate] AI teammate save tag had no AI slots.", clearInvalidTag);
        }

        if (!SaveManager.Instance.HasMultiplayerRunSave)
        {
            return InvalidateSavedRun("[AITeammate] No multiplayer run save exists for the current profile.", clearInvalidTag, warning: false);
        }

        ReadSaveResult<SerializableRun> readSaveResult = SaveManager.Instance.LoadAndCanonicalizeMultiplayerRunSave(tagData.HostPlayerId);
        if (!readSaveResult.Success || readSaveResult.SaveData == null)
        {
            return InvalidateSavedRun(
                $"[AITeammate] Failed to load multiplayer run save for AI teammate mode. status={readSaveResult.Status}",
                clearInvalidTag);
        }

        if (!TryCreateSessionState(tagData, readSaveResult.SaveData, out AiTeammateSessionState? sessionState) || sessionState == null)
        {
            return InvalidateSavedRun(
                "[AITeammate] Multiplayer run save loaded, but AI teammate session reconstruction failed.",
                clearInvalidTag);
        }

        context = new AiTeammateSavedRunContext
        {
            SaveData = readSaveResult.SaveData,
            SessionState = sessionState
        };
        return true;
    }

    public static async Task<bool> ContinueSavedRunAsync()
    {
        if (Interlocked.CompareExchange(ref _continueSavedRunInProgress, 1, 0) != 0)
        {
            Log.Info("[AITeammate] Saved AI teammate continue is already in progress; ignoring duplicate request.");
            return false;
        }

        try
        {
            return await ContinueSavedRunCoreAsync();
        }
        finally
        {
            Volatile.Write(ref _continueSavedRunInProgress, 0);
        }
    }

    private static async Task<bool> ContinueSavedRunCoreAsync()
    {
        if (!TryLoadSavedRun(out AiTeammateSavedRunContext? savedRun, clearInvalidTag: true) || savedRun == null)
        {
            ShowInvalidSavePopup();
            return false;
        }

        LoadRunLobby? lobby = null;
        bool runManagerOwnsLobby = false;
        bool transitionFadedOut = false;

        try
        {
            AiTeammateSessionRegistry.SetCurrent(savedRun.SessionState);

            AiTeammateLoopbackHostGameService netService = new(savedRun.SessionState.HostPlayerId);
            lobby = new LoadRunLobby(netService, NoopLoadRunLobbyListener.Instance, savedRun.SaveData);
            lobby.AddLocalHostPlayer();
            foreach (AiTeammateSessionParticipant participant in savedRun.SessionState.Participants)
            {
                lobby.ConnectedPlayerIds.Add(participant.PlayerId);
            }

            NGame game = NGame.Instance ?? throw new InvalidOperationException("NGame.Instance was null while trying to continue the AI teammate run.");
            game.RemoteCursorContainer.Initialize(lobby.InputSynchronizer, lobby.ConnectedPlayerIds);
            game.ReactionContainer.InitializeNetworking(netService);
            NAudioManager.Instance?.StopMusic();

            SerializablePlayer localPlayer = savedRun.SaveData.Players.First((player) => player.NetId == savedRun.SessionState.HostPlayerId);
            if (localPlayer.CharacterId != null)
            {
                CharacterModel transitionCharacter = ModelDb.GetById<CharacterModel>(localPlayer.CharacterId);
                SfxCmd.Play(transitionCharacter.CharacterTransitionSfx);
                await game.Transition.FadeOut(0.8f, transitionCharacter.CharacterSelectTransitionPath);
                transitionFadedOut = true;
            }
            else
            {
                await game.Transition.FadeOut();
                transitionFadedOut = true;
            }

            RunState runState = RunState.FromSerializable(savedRun.SaveData);
            Log.Info("[AITeammate] Setting up saved AI teammate run.");
            RunManager.Instance.SetUpSavedMultiPlayer(runState, lobby);
            runManagerOwnsLobby = true;
            Log.Info("[AITeammate] Saved AI teammate run manager initialized. Loading run scene.");
            await game.LoadRun(runState, savedRun.SaveData.PreFinishedRoom);
            lobby.CleanUp(disconnectSession: false);
            await game.Transition.FadeIn();
            Log.Info($"[AITeammate] Continued saved AI teammate run. players={savedRun.SessionState.Participants.Count}");
            return true;
        }
        catch (Exception ex)
        {
            if (!runManagerOwnsLobby)
            {
                lobby?.CleanUp(disconnectSession: true);
            }

            ClearSessionAfterFailedContinueIfSafe();
            Log.Error($"[AITeammate] Failed to continue saved AI teammate run: {ex}");
            if (transitionFadedOut)
            {
                await TryRestoreFadeInAfterFailedContinue();
            }

            ShowInvalidSavePopup();
            return false;
        }
    }

    private static void ClearSessionAfterFailedContinueIfSafe()
    {
        if (RunManager.Instance.DebugOnlyGetState() != null &&
            RunManager.Instance.NetService is AiTeammateLoopbackHostGameService)
        {
            Log.Warn("[AITeammate] Continue failed after the loopback run manager was initialized; keeping reconstructed AI session state.");
            return;
        }

        AiTeammateSessionRegistry.SetCurrent(null);
    }

    private static async Task TryRestoreFadeInAfterFailedContinue()
    {
        try
        {
            NGame? game = NGame.Instance;
            if (game != null)
            {
                await game.Transition.FadeIn();
            }
        }
        catch (Exception fadeEx)
        {
            Log.Warn($"[AITeammate] Failed to restore transition after failed saved-run continue: {fadeEx.Message}");
        }
    }

    public static void AbandonSavedRun()
    {
        if (TryLoadSavedRun(out AiTeammateSavedRunContext? savedRun) && savedRun != null)
        {
            try
            {
                SaveManager.Instance.UpdateProgressWithRunData(savedRun.SaveData, victory: false);
                RunHistoryUtilities.CreateRunHistoryEntry(savedRun.SaveData, victory: false, isAbandoned: true, savedRun.SaveData.PlatformType);
                if (savedRun.SaveData.DailyTime.HasValue)
                {
                    int score = ScoreUtility.CalculateScore(savedRun.SaveData, won: false);
                    TaskHelper.RunSafely(DailyRunUtility.UploadScore(savedRun.SaveData.DailyTime.Value, score, savedRun.SaveData.Players));
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[AITeammate] Failed to record abandoned AI teammate run: {ex}");
            }
        }

        SaveManager.Instance.DeleteCurrentMultiplayerRun();
        ClearCurrentProfile();
        AiTeammateSessionRegistry.SetCurrent(null);
    }

    public static bool IsActiveInProgressRun()
    {
        return AiTeammateSessionRegistry.Current != null &&
               RunManager.Instance.IsInProgress &&
               RunManager.Instance.NetService is AiTeammateLoopbackHostGameService;
    }

    public static void PrepareForInProgressAbandon()
    {
        if (!IsActiveInProgressRun())
        {
            return;
        }

        Log.Info("[AITeammate] Preparing AI teammate run for in-progress abandon.");
        ClearCurrentProfile();
        AiTeammateSessionRegistry.MarkRunAbandoning("run_manager_abandon");
    }

    public static void ClearInMemorySessionIfNeeded()
    {
        if (AiTeammateSessionRegistry.Current == null)
        {
            return;
        }

        Log.Info("[AITeammate] Clearing in-memory AI teammate session state.");
        AiTeammateSessionRegistry.SetCurrent(null);
    }

    private static bool TryReadCurrentProfile(out AiTeammateSaveTagData? tagData)
    {
        tagData = null;

        try
        {
            string tagPath = GetSaveTagPath();
            if (!File.Exists(tagPath))
            {
                return false;
            }

            string? content = File.ReadAllText(tagPath);
            if (string.IsNullOrWhiteSpace(content))
            {
                Log.Warn($"[AITeammate] AI teammate save tag was empty at {tagPath}");
                return false;
            }

            tagData = JsonSerializer.Deserialize<AiTeammateSaveTagData>(content);
            if (tagData == null || tagData.HostPlayerId == 0UL)
            {
                Log.Warn($"[AITeammate] AI teammate save tag was invalid at {tagPath}");
                return false;
            }

            tagData.AiSlots = tagData.AiSlots
                .Where((slot) => slot.PlayerId != 0UL)
                .GroupBy((slot) => slot.SlotIndex)
                .Select((group) => group.First())
                .OrderBy((slot) => slot.SlotIndex)
                .ToList();
            return true;
        }
        catch (Exception ex)
        {
            Log.Warn($"[AITeammate] Failed to read AI teammate save tag: {ex.Message}");
            return false;
        }
    }

    private static bool InvalidateSavedRun(string message, bool clearInvalidTag, bool warning = true)
    {
        if (warning)
        {
            Log.Warn(message);
        }
        else
        {
            Log.Info(message);
        }

        if (clearInvalidTag)
        {
            ClearCurrentProfile();
        }

        return false;
    }

    private static bool TryCreateSessionState(AiTeammateSaveTagData tagData, SerializableRun saveData, out AiTeammateSessionState? sessionState)
    {
        sessionState = null;

        SerializablePlayer? hostSave = saveData.Players.FirstOrDefault((player) => player.NetId == tagData.HostPlayerId);
        if (hostSave?.CharacterId == null)
        {
            return false;
        }

        CharacterModel hostCharacter = ModelDb.GetById<CharacterModel>(hostSave.CharacterId);
        List<AiTeammateSessionParticipant> participants =
        [
            new AiTeammateSessionParticipant(
                SlotIndex: 0,
                PlayerId: tagData.HostPlayerId,
                Character: hostCharacter,
                IsHost: true,
                DisplayName: AiTeammateParticipantNames.HostDisplayName(tagData.HostPlayerId))
        ];

        AiTeammateDummyController hostController = new(0, tagData.HostPlayerId, hostCharacter);
        Dictionary<ulong, AiTeammateDummyController> aiControllers = [];
        HashSet<string> usedAiDisplayNames = new(StringComparer.OrdinalIgnoreCase);
        foreach (AiTeammateSaveTagSlot slot in tagData.AiSlots.OrderBy(static slot => slot.SlotIndex))
        {
            SerializablePlayer? aiSave = saveData.Players.FirstOrDefault((player) => player.NetId == slot.PlayerId);
            if (aiSave?.CharacterId == null)
            {
                return false;
            }

            CharacterModel character = ModelDb.GetById<CharacterModel>(aiSave.CharacterId);
            participants.Add(new AiTeammateSessionParticipant(
                SlotIndex: slot.SlotIndex,
                PlayerId: slot.PlayerId,
                Character: character,
                IsHost: false,
                DisplayName: AiTeammateParticipantNames.AiDisplayName(
                    character,
                    slot.PlayerId,
                    usedAiDisplayNames,
                    slot.SlotIndex)));
            aiControllers[slot.PlayerId] = new AiTeammateDummyController(slot.SlotIndex, slot.PlayerId, character);
        }

        sessionState = new AiTeammateSessionState(tagData.HostPlayerId, hostController, participants, aiControllers, tagData.UseTestMap);
        return true;
    }

    private static string GetSaveTagPath()
    {
        string path = SaveManager.Instance.GetProfileScopedPath(Path.Combine(UserDataPathProvider.SavesDir, SaveTagFileName));
        if (path.StartsWith("user://", StringComparison.OrdinalIgnoreCase))
        {
            path = ProjectSettings.GlobalizePath(path);
        }

        return path;
    }

    private static void ShowInvalidSavePopup()
    {
        NErrorPopup? modalToCreate = NErrorPopup.Create(
            new LocString("main_menu_ui", "INVALID_SAVE_POPUP.title"),
            new LocString("main_menu_ui", "INVALID_SAVE_POPUP.description_run"),
            new LocString("main_menu_ui", "INVALID_SAVE_POPUP.dismiss"),
            showReportBugButton: true);
        if (modalToCreate == null || NModalContainer.Instance == null)
        {
            return;
        }

        NModalContainer.Instance.Add(modalToCreate);
        NModalContainer.Instance.ShowBackstop();
    }

    private sealed class NoopLoadRunLobbyListener : ILoadRunLobbyListener
    {
        internal static readonly NoopLoadRunLobbyListener Instance = new();

        public void PlayerConnected(ulong playerId)
        {
        }

        public void RemotePlayerDisconnected(ulong playerId)
        {
        }

        public Task<bool> ShouldAllowRunToBegin()
        {
            return Task.FromResult(true);
        }

        public void BeginRun()
        {
        }

        public void PlayerReadyChanged(ulong playerId)
        {
        }

        public void LocalPlayerDisconnected(NetErrorInfo info)
        {
        }
    }
}
