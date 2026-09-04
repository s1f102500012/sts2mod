using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Saves;

namespace AITeammate.Scripts;

[HarmonyPatch(typeof(NRemoteLobbyPlayerContainer), nameof(NRemoteLobbyPlayerContainer.Initialize))]
internal static class AiTeammateRemoteLobbyPlayerContainerInitializePatch
{
    [HarmonyPostfix]
    private static void Postfix(NRemoteLobbyPlayerContainer __instance, StartRunLobby lobby)
    {
        AiTeammateOriginalMultiplayerUi.AttachAddAiButton(__instance, lobby);
    }
}

internal static class AiTeammateOriginalMultiplayerUi
{
    private const int DuplicateNodeFlags = 14;
    private const string AddAiContainerName = "AiTeammateAddAiButtonContainer";
    private const string AddAiButtonName = "AiTeammateAddAiButton";
    private const string AddAiHitButtonName = "AiTeammateAddAiHitButton";
    private const string EditPlayerHitButtonName = "AiTeammateEditPlayerHitButton";
    private const string EditPlayerHighlightName = "AiTeammateEditPlayerHighlight";
    private const ulong AiNetIdOffset = 10_000UL;
    private static readonly Dictionary<StartRunLobby, ulong> ActiveEditablePlayerIds = new();
    private static readonly FieldInfo RemoteLobbyNodesField =
        AccessTools.Field(typeof(NRemoteLobbyPlayerContainer), "_nodes")!;
    private static readonly FieldInfo? CharacterSelectRemotePlayerContainerField =
        AccessTools.Field(typeof(NCharacterSelectScreen), "_remotePlayerContainer");
    private static readonly FieldInfo? RemoteLobbyNameplateField =
        AccessTools.Field(typeof(NRemoteLobbyPlayer), "_nameplateLabel");

    public static void AttachAddAiButton(NRemoteLobbyPlayerContainer container, StartRunLobby lobby)
    {
        if (lobby.NetService is not AiTeammateLoopbackHostGameService)
        {
            RemoveAddAiButton(container);
            return;
        }

        try
        {
            SyncSessionFromLobby(lobby);
            Container? playerListContainer = container.GetNodeOrNull<Container>("Container");
            Control? inviteButton = container.GetNodeOrNull<Control>("%InviteButton");
            if (playerListContainer == null || inviteButton == null)
            {
                Log.Warn("[AITeammate] Could not find stock remote-player container nodes while adding AI button.");
                return;
            }

            Control addAiButtonContainer = GetOrCreateAddAiButtonContainer(playerListContainer);
            Control? addAiButton = addAiButtonContainer.GetNodeOrNull<Control>(AddAiButtonName);
            if (addAiButton == null)
            {
                addAiButton = CreateAddAiButton(inviteButton, lobby, container);
                addAiButtonContainer.AddChild(addAiButton);
            }

            addAiButtonContainer.Visible = true;
            RefreshAddAiButtonState(addAiButton, lobby);
            RebuildRemotePlayerList(container, playerListContainer, lobby);
            AttachEditablePlayerControls(container, lobby);
            RefreshRemotePlayerNames(container, lobby);
        }
        catch (Exception ex)
        {
            Log.Error($"[AITeammate] Failed to attach Add AI button to stock multiplayer UI: {ex}");
        }
    }

    public static bool TrySetSelectedAiCharacter(StartRunLobby lobby, CharacterModel character)
    {
        if (lobby.NetService is not AiTeammateLoopbackHostGameService)
        {
            return false;
        }

        ulong activePlayerId = GetActiveEditablePlayerId(lobby);
        if (activePlayerId == lobby.LocalPlayer.id)
        {
            return false;
        }

        int playerIndex = lobby.Players.FindIndex((player) => player.id == activePlayerId);
        if (playerIndex < 0)
        {
            ActiveEditablePlayerIds[lobby] = lobby.LocalPlayer.id;
            return false;
        }

        LobbyPlayer lobbyPlayer = lobby.Players[playerIndex];
        lobbyPlayer.character = character;
        lobbyPlayer.isReady = true;
        lobbyPlayer.unlockState = SaveManager.Instance.GenerateUnlockStateFromProgress().ToSerializable();
        lobbyPlayer.maxMultiplayerAscensionUnlocked = SaveManager.Instance.Progress.MaxMultiplayerAscension;
        lobby.Players[playerIndex] = lobbyPlayer;

        SyncSessionFromLobby(lobby);
        lobby.LobbyListener.PlayerChanged(lobbyPlayer, isRandomCharacterResolution: false);
        RefreshRemotePlayerNamesForLobby(lobby);
        Log.Info($"[AITeammate] Changed selected AI character. player={activePlayerId}, character={character.Id.Entry}");
        return true;
    }

    public static void SyncSessionFromLobby(StartRunLobby lobby)
    {
        if (lobby.NetService is not AiTeammateLoopbackHostGameService)
        {
            return;
        }

        try
        {
            LobbyPlayer host = lobby.LocalPlayer;
            CharacterModel hostCharacter = host.character;
            List<AiTeammateSessionParticipant> participants =
            [
                new AiTeammateSessionParticipant(
                    SlotIndex: 0,
                    PlayerId: host.id,
                    Character: hostCharacter,
                    IsHost: true,
                    DisplayName: AiTeammateParticipantNames.HostDisplayName(host.id))
            ];

            AiTeammateDummyController hostController = new(0, host.id, hostCharacter);
            Dictionary<ulong, AiTeammateDummyController> aiControllers = new();
            HashSet<string> usedAiDisplayNames = new(StringComparer.OrdinalIgnoreCase);
            int aiSlotIndex = 1;
            foreach (LobbyPlayer player in lobby.Players
                         .Where((candidate) => candidate.id != host.id)
                         .OrderBy((candidate) => candidate.slotId))
            {
                int slotIndex = Math.Clamp(player.slotId, 1, 3);
                if (participants.Any((participant) => participant.SlotIndex == slotIndex))
                {
                    slotIndex = aiSlotIndex;
                }

                participants.Add(new AiTeammateSessionParticipant(
                    SlotIndex: slotIndex,
                    PlayerId: player.id,
                    Character: player.character,
                    IsHost: false,
                    DisplayName: AiTeammateParticipantNames.AiDisplayName(
                        player.character,
                        player.id,
                        usedAiDisplayNames,
                        aiSlotIndex)));
                aiControllers[player.id] = new AiTeammateDummyController(aiSlotIndex, player.id, player.character);
                aiSlotIndex++;
            }

            AiTeammateSessionRegistry.SetCurrent(new AiTeammateSessionState(
                host.id,
                hostController,
                participants,
                aiControllers,
                useTestMap: AiTeammateQuickStart.UseTestMap));
        }
        catch (Exception ex)
        {
            Log.Warn($"[AITeammate] Failed to sync AI teammate session from stock lobby: {ex.Message}");
        }
    }

    public static int FillWithRandomAiPlayers(StartRunLobby lobby, int desiredTotalPlayers, Random rng)
    {
        if (lobby.NetService is not AiTeammateLoopbackHostGameService)
        {
            return 0;
        }

        int addedCount = 0;
        int cappedDesiredPlayers = Math.Clamp(desiredTotalPlayers, 1, lobby.MaxPlayers);
        while (lobby.Players.Count < cappedDesiredPlayers && CanAddAi(lobby))
        {
            CharacterModel aiCharacter = ChooseRandomUnusedCharacter(lobby, rng);
            if (!TryAddAiPlayerToLobby(lobby, aiCharacter, out LobbyPlayer addedPlayer))
            {
                break;
            }

            ActiveEditablePlayerIds[lobby] = addedPlayer.id;
            addedCount++;
        }

        if (addedCount > 0)
        {
            AccessTools.Method(typeof(StartRunLobby), "UpdateMaxMultiplayerAscension")?.Invoke(lobby, Array.Empty<object>());
            SyncSessionFromLobby(lobby);
            Log.Info($"[AITeammate] Quick-start filled AI players. added={addedCount}, count={lobby.Players.Count}");
        }

        return addedCount;
    }

    public static CharacterModel ChooseRandomUnusedCharacter(StartRunLobby lobby, Random rng)
    {
        HashSet<string> usedCharacterIds = new(StringComparer.OrdinalIgnoreCase);
        foreach (LobbyPlayer player in lobby.Players)
        {
            if (AiTeammatePlaceholderCharacters.TryGetByModelId(player.character.Id.Entry, out AiTeammatePlaceholderCharacter usedCharacter))
            {
                usedCharacterIds.Add(usedCharacter.Id);
            }
        }

        AiTeammatePlaceholderCharacter[] pool = AiTeammatePlaceholderCharacters.All
            .Where((candidate) => !usedCharacterIds.Contains(candidate.Id))
            .ToArray();
        if (pool.Length == 0)
        {
            pool = AiTeammatePlaceholderCharacters.All;
        }

        return pool[Math.Clamp(rng.Next(pool.Length), 0, pool.Length - 1)].ResolveModel();
    }

    private static Control GetOrCreateAddAiButtonContainer(Container playerListContainer)
    {
        if (playerListContainer.GetNodeOrNull<Control>(AddAiContainerName) is { } existingContainer)
        {
            return existingContainer;
        }

        Control addAiButtonContainer = new()
        {
            Name = AddAiContainerName,
            CustomMinimumSize = new Vector2(200f, 50f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        playerListContainer.AddChild(addAiButtonContainer);
        return addAiButtonContainer;
    }

    private static Control CreateAddAiButton(Control inviteButton, StartRunLobby lobby, NRemoteLobbyPlayerContainer container)
    {
        Control addAiButton = new()
        {
            Name = AddAiButtonName,
            CustomMinimumSize = new Vector2(200f, 50f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        addAiButton.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        addAiButton.OffsetLeft = 0f;
        addAiButton.OffsetTop = 0f;
        addAiButton.OffsetRight = 200f;
        addAiButton.OffsetBottom = 50f;

        DuplicateInviteChild(inviteButton, "Background", addAiButton);
        Control label = DuplicateInviteChild(inviteButton, "Label", addAiButton) ?? CreateFallbackLabel(addAiButton);
        SetButtonLabel(label, AiTeammateLocalization.Tr("button.add_ai"));

        Button hitButton = CreateTransparentHitButton(AddAiHitButtonName, Vector2.Zero);
        hitButton.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        hitButton.Pressed += () => AddAiPlayer(lobby, container, addAiButton);
        addAiButton.AddChild(hitButton);

        return addAiButton;
    }

    private static Control? DuplicateInviteChild(Control inviteButton, string childName, Control target)
    {
        Node? source = inviteButton.GetNodeOrNull<Node>(childName);
        if (source == null)
        {
            return null;
        }

        Node duplicate = source.Duplicate(DuplicateNodeFlags);
        if (duplicate is not Control duplicateControl)
        {
            return null;
        }

        duplicateControl.Name = childName;
        duplicateControl.MouseFilter = Control.MouseFilterEnum.Ignore;
        duplicateControl.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        duplicateControl.OffsetLeft = source is Control sourceControl ? sourceControl.OffsetLeft : 0f;
        duplicateControl.OffsetTop = source is Control sourceControl2 ? sourceControl2.OffsetTop : 0f;
        duplicateControl.OffsetRight = source is Control sourceControl3 ? sourceControl3.OffsetRight : 0f;
        duplicateControl.OffsetBottom = source is Control sourceControl4 ? sourceControl4.OffsetBottom : 0f;
        target.AddChild(duplicateControl);
        return duplicateControl;
    }

    private static Control CreateFallbackLabel(Control parent)
    {
        Label label = new()
        {
            Name = "Label",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        label.AddThemeColorOverride("font_color", new Color(1f, 0.964706f, 0.886275f, 1f));
        label.AddThemeFontSizeOverride("font_size", 22);
        parent.AddChild(label);
        return label;
    }

    private static void SetButtonLabel(Control labelControl, string text)
    {
        switch (labelControl)
        {
            case RichTextLabel richTextLabel:
                richTextLabel.Text = text;
                break;
            case Label label:
                label.Text = text;
                break;
        }
    }

    private static Button CreateTransparentHitButton(string nodeName, Vector2 minimumSize)
    {
        Button button = new()
        {
            Name = nodeName,
            CustomMinimumSize = minimumSize,
            FocusMode = Control.FocusModeEnum.All,
            MouseFilter = Control.MouseFilterEnum.Stop,
            Flat = true
        };
        StyleBoxEmpty empty = new();
        button.AddThemeStyleboxOverride("normal", empty);
        button.AddThemeStyleboxOverride("hover", empty);
        button.AddThemeStyleboxOverride("pressed", empty);
        button.AddThemeStyleboxOverride("focus", empty);
        button.AddThemeStyleboxOverride("disabled", empty);
        return button;
    }

    private static void RemoveAddAiButton(NRemoteLobbyPlayerContainer container)
    {
        Control? addAiButtonContainer = container.GetNodeOrNull<Control>($"Container/{AddAiContainerName}");
        if (addAiButtonContainer != null)
        {
            addAiButtonContainer.QueueFree();
        }
    }

    private static void AddAiPlayer(StartRunLobby lobby, NRemoteLobbyPlayerContainer container, Control addAiButton)
    {
        if (lobby.NetService is not AiTeammateLoopbackHostGameService || !CanAddAi(lobby))
        {
            RefreshAddAiButtonState(addAiButton, lobby);
            return;
        }

        try
        {
            CharacterModel aiCharacter = ChooseNextAiCharacter(lobby);
            if (!TryAddAiPlayerToLobby(lobby, aiCharacter, out LobbyPlayer lobbyPlayer))
            {
                RefreshAddAiButtonState(addAiButton, lobby);
                return;
            }

            if (container.GetNodeOrNull<Container>("Container") is { } playerListContainer)
            {
                RebuildRemotePlayerList(container, playerListContainer, lobby);
                AttachEditablePlayerControls(container, lobby);
            }
            AccessTools.Method(typeof(StartRunLobby), "UpdateMaxMultiplayerAscension")?.Invoke(lobby, Array.Empty<object>());
            SyncSessionFromLobby(lobby);
            SetActiveEditablePlayer(lobby, container, lobbyPlayer.id);
            RefreshRemotePlayerNames(container, lobby);
            RefreshAddAiButtonState(addAiButton, lobby);
            Log.Info($"[AITeammate] Added AI player through stock multiplayer UI. player={lobbyPlayer.id}, character={lobbyPlayer.character.Id.Entry}, count={lobby.Players.Count}");
        }
        catch (Exception ex)
        {
            Log.Error($"[AITeammate] Failed to add AI player through stock multiplayer UI: {ex}");
        }
        finally
        {
            RefreshAddAiButtonState(addAiButton, lobby);
        }
    }

    private static bool TryAddAiPlayerToLobby(StartRunLobby lobby, CharacterModel aiCharacter, out LobbyPlayer lobbyPlayer)
    {
        lobbyPlayer = default;
        if (lobby.NetService is not AiTeammateLoopbackHostGameService loopbackService || !CanAddAi(lobby))
        {
            return false;
        }

        LobbyPlayer host = lobby.LocalPlayer;
        ulong aiPlayerId = GenerateAiPlayerId(host.id, lobby);
        ulong restoreSenderId = host.id;

        LobbyPlayer? addedPlayer;
        try
        {
            loopbackService.SetCurrentSenderId(aiPlayerId);
            addedPlayer = lobby.AddLocalHostPlayerInternal(
                SaveManager.Instance.GenerateUnlockStateFromProgress().ToSerializable(),
                SaveManager.Instance.Progress.MaxMultiplayerAscension);
        }
        finally
        {
            loopbackService.SetCurrentSenderId(restoreSenderId);
        }

        if (!addedPlayer.HasValue)
        {
            return false;
        }

        lobbyPlayer = addedPlayer.Value;
        lobbyPlayer.character = aiCharacter;
        lobbyPlayer.isReady = true;
        lobbyPlayer.unlockState = SaveManager.Instance.GenerateUnlockStateFromProgress().ToSerializable();
        lobbyPlayer.maxMultiplayerAscensionUnlocked = SaveManager.Instance.Progress.MaxMultiplayerAscension;

        ulong addedPlayerId = lobbyPlayer.id;
        int playerIndex = lobby.Players.FindIndex((player) => player.id == addedPlayerId);
        if (playerIndex >= 0)
        {
            lobby.Players[playerIndex] = lobbyPlayer;
        }

        SyncSessionFromLobby(lobby);
        lobby.LobbyListener.PlayerChanged(lobbyPlayer, isRandomCharacterResolution: false);
        RefreshRemotePlayerNamesForLobby(lobby);
        return true;
    }

    private static CharacterModel ChooseNextAiCharacter(StartRunLobby lobby)
    {
        HashSet<string> usedCharacterIds = new(StringComparer.OrdinalIgnoreCase);
        foreach (LobbyPlayer player in lobby.Players)
        {
            if (AiTeammatePlaceholderCharacters.TryGetByModelId(player.character.Id.Entry, out AiTeammatePlaceholderCharacter usedCharacter))
            {
                usedCharacterIds.Add(usedCharacter.Id);
            }
        }

        AiTeammatePlaceholderCharacter selected = AiTeammatePlaceholderCharacters.All
            .FirstOrDefault((candidate) => !usedCharacterIds.Contains(candidate.Id));
        if (string.IsNullOrEmpty(selected.Id))
        {
            int index = Math.Clamp(lobby.Players.Count - 1, 0, AiTeammatePlaceholderCharacters.All.Length - 1);
            selected = AiTeammatePlaceholderCharacters.All[index];
        }

        return selected.ResolveModel();
    }

    private static ulong GenerateAiPlayerId(ulong hostPlayerId, StartRunLobby lobby)
    {
        for (int slotIndex = 1; slotIndex <= 3; slotIndex++)
        {
            ulong candidate = hostPlayerId + AiNetIdOffset + (ulong)slotIndex;
            if (lobby.Players.All((player) => player.id != candidate))
            {
                return candidate;
            }
        }

        ulong fallback = hostPlayerId + AiNetIdOffset + 100UL;
        while (lobby.Players.Any((player) => player.id == fallback))
        {
            fallback++;
        }

        return fallback;
    }

    private static bool CanAddAi(StartRunLobby lobby)
    {
        if (lobby.NetService is not AiTeammateLoopbackHostGameService || lobby.IsAboutToBeginGame())
        {
            return false;
        }

        return lobby.Players.Count < lobby.MaxPlayers &&
               lobby.Players.Count((player) => player.id != lobby.LocalPlayer.id) < 3;
    }

    private static void RefreshAddAiButtonState(Control addAiButton, StartRunLobby lobby)
    {
        bool canAddAi = CanAddAi(lobby);
        addAiButton.Modulate = canAddAi ? Colors.White : new Color(0.55f, 0.55f, 0.55f, 0.65f);
        if (addAiButton.GetNodeOrNull<Button>(AddAiHitButtonName) is { } hitButton)
        {
            hitButton.Disabled = !canAddAi;
            hitButton.MouseFilter = canAddAi ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore;
        }
    }

    private static void EnsureRemotePlayerNode(NRemoteLobbyPlayerContainer container, LobbyPlayer lobbyPlayer)
    {
        List<NRemoteLobbyPlayer>? nodes = RemoteLobbyNodesField.GetValue(container) as List<NRemoteLobbyPlayer>;
        if (nodes == null)
        {
            container.OnPlayerChanged(lobbyPlayer);
            return;
        }

        if (nodes.Any((node) => node.PlayerId == lobbyPlayer.id))
        {
            container.OnPlayerChanged(lobbyPlayer);
            return;
        }

        container.OnPlayerConnected(lobbyPlayer);
        container.OnPlayerChanged(lobbyPlayer);
    }

    private static void RebuildRemotePlayerList(NRemoteLobbyPlayerContainer container, Container playerListContainer, StartRunLobby lobby)
    {
        List<NRemoteLobbyPlayer>? nodes = RemoteLobbyNodesField.GetValue(container) as List<NRemoteLobbyPlayer>;
        if (nodes == null)
        {
            return;
        }

        foreach (NRemoteLobbyPlayer node in nodes)
        {
            if (GodotObject.IsInstanceValid(node))
            {
                node.QueueFree();
            }
        }
        nodes.Clear();

        int insertIndex = 0;
        if (playerListContainer.GetNodeOrNull<Control>("SoloLabel") is { } soloLabel)
        {
            soloLabel.Visible = lobby.Players.Count == 1;
            insertIndex = soloLabel.GetIndex() + 1;
        }

        foreach (LobbyPlayer player in lobby.Players.OrderBy((candidate) => candidate.slotId))
        {
            NRemoteLobbyPlayer remotePlayerNode = NRemoteLobbyPlayer.Create(
                player,
                lobby.NetService.Platform,
                isSingleplayer: false);
            playerListContainer.AddChild(remotePlayerNode);
            playerListContainer.MoveChild(remotePlayerNode, insertIndex++);
            nodes.Add(remotePlayerNode);
        }

        if (playerListContainer.GetNodeOrNull<Control>(AddAiContainerName) is { } addAiContainer)
        {
            playerListContainer.MoveChild(addAiContainer, insertIndex++);
        }

        if (playerListContainer.GetNodeOrNull<Control>("InviteButtonContainer") is { } inviteContainer)
        {
            playerListContainer.MoveChild(inviteContainer, playerListContainer.GetChildCount() - 1);
        }

        RefreshRemotePlayerNames(container, lobby);
    }

    private static void AttachEditablePlayerControls(NRemoteLobbyPlayerContainer container, StartRunLobby lobby)
    {
        List<NRemoteLobbyPlayer>? nodes = RemoteLobbyNodesField.GetValue(container) as List<NRemoteLobbyPlayer>;
        if (nodes == null)
        {
            return;
        }

        _ = GetActiveEditablePlayerId(lobby);
        foreach (NRemoteLobbyPlayer node in nodes)
        {
            ulong playerId = node.PlayerId;
            if (((Node)node).GetNodeOrNull<Button>(EditPlayerHitButtonName) == null)
            {
                Button hitButton = CreateTransparentHitButton(EditPlayerHitButtonName, Vector2.Zero);
                hitButton.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                hitButton.Pressed += () => SetActiveEditablePlayer(lobby, container, playerId);
                node.AddChild(hitButton);
            }

            if (((Node)node).GetNodeOrNull<Panel>(EditPlayerHighlightName) == null)
            {
                Panel highlight = CreateEditablePlayerHighlight();
                node.AddChild(highlight);
                node.MoveChild(highlight, node.GetChildCount() - 1);
            }
        }

        RefreshEditablePlayerHighlights(container, lobby);
    }

    private static Panel CreateEditablePlayerHighlight()
    {
        StyleBoxFlat style = new()
        {
            BgColor = new Color(0f, 0f, 0f, 0f),
            BorderColor = new Color(1f, 0.76f, 0.18f, 0.95f),
            BorderWidthLeft = 3,
            BorderWidthTop = 3,
            BorderWidthRight = 3,
            BorderWidthBottom = 3,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3
        };

        Panel highlight = new()
        {
            Name = EditPlayerHighlightName,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        highlight.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        highlight.AddThemeStyleboxOverride("panel", style);
        return highlight;
    }

    private static void SetActiveEditablePlayer(StartRunLobby lobby, NRemoteLobbyPlayerContainer container, ulong playerId)
    {
        if (lobby.Players.All((player) => player.id != playerId))
        {
            playerId = lobby.LocalPlayer.id;
        }

        ActiveEditablePlayerIds[lobby] = playerId;
        RefreshEditablePlayerHighlights(container, lobby);
        SyncCharacterSelectPreviewToPlayer(lobby, container, playerId);
        Log.Info($"[AITeammate] Active editable lobby player changed. player={playerId}, host={lobby.LocalPlayer.id}");
    }

    private static void SyncCharacterSelectPreviewToPlayer(
        StartRunLobby lobby,
        NRemoteLobbyPlayerContainer container,
        ulong playerId)
    {
        try
        {
            int playerIndex = lobby.Players.FindIndex((player) => player.id == playerId);
            if (playerIndex < 0)
            {
                return;
            }

            LobbyPlayer player = lobby.Players[playerIndex];
            NCharacterSelectScreen? screen =
                lobby.LobbyListener as NCharacterSelectScreen ?? FindCharacterSelectScreen(container);
            if (screen == null)
            {
                Log.Warn("[AITeammate] Could not sync character preview because the character select screen was not found.");
                return;
            }

            NCharacterSelectButton? button = FindCharacterSelectButton(screen, player.character);
            if (button == null)
            {
                Log.Warn($"[AITeammate] Could not sync character preview because no button matched character={player.character.Id.Entry}.");
                return;
            }

            screen.SelectCharacter(button, player.character);
        }
        catch (Exception ex)
        {
            Log.Warn($"[AITeammate] Failed to sync character preview for selected lobby player: {ex.Message}");
        }
    }

    private static NCharacterSelectScreen? FindCharacterSelectScreen(Node startNode)
    {
        Node? node = startNode;
        while (node != null)
        {
            if (node is NCharacterSelectScreen screen)
            {
                return screen;
            }

            node = node.GetParent();
        }

        return null;
    }

    private static void RefreshRemotePlayerNamesForLobby(StartRunLobby lobby)
    {
        if (lobby.NetService is not AiTeammateLoopbackHostGameService)
        {
            return;
        }

        NRemoteLobbyPlayerContainer? container = null;
        if (lobby.LobbyListener is NCharacterSelectScreen screen)
        {
            container = CharacterSelectRemotePlayerContainerField?.GetValue(screen) as NRemoteLobbyPlayerContainer
                        ?? EnumerateDescendants(screen).OfType<NRemoteLobbyPlayerContainer>().FirstOrDefault();
        }

        if (container != null)
        {
            RefreshRemotePlayerNames(container, lobby);
        }
    }

    private static void RefreshRemotePlayerNames(NRemoteLobbyPlayerContainer container, StartRunLobby lobby)
    {
        if (lobby.NetService is not AiTeammateLoopbackHostGameService)
        {
            return;
        }

        List<NRemoteLobbyPlayer>? nodes = RemoteLobbyNodesField.GetValue(container) as List<NRemoteLobbyPlayer>;
        if (nodes == null)
        {
            return;
        }

        foreach (NRemoteLobbyPlayer node in nodes)
        {
            RefreshRemoteLobbyPlayerName(node);
        }
    }

    public static void RefreshRemoteLobbyPlayerName(NRemoteLobbyPlayer node)
    {
        try
        {
            if (!GodotObject.IsInstanceValid(node) ||
                !AiTeammateSessionRegistry.TryGetDisplayName(node.PlayerId, out string displayName) ||
                string.IsNullOrWhiteSpace(displayName))
            {
                return;
            }

            object? nameplate = RemoteLobbyNameplateField?.GetValue(node);
            if (nameplate == null)
            {
                return;
            }

            MethodInfo? setTextAutoSizeMethod = AccessTools.Method(nameplate.GetType(), "SetTextAutoSize", [typeof(string)]);
            if (setTextAutoSizeMethod != null)
            {
                setTextAutoSizeMethod.Invoke(nameplate, [displayName]);
                return;
            }

            switch (nameplate)
            {
                case Label label:
                    label.Text = displayName;
                    break;
                case RichTextLabel richTextLabel:
                    richTextLabel.Text = displayName;
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"[AITeammate] Failed to refresh remote lobby player nameplate: {ex.Message}");
        }
    }

    private static NCharacterSelectButton? FindCharacterSelectButton(NCharacterSelectScreen screen, CharacterModel character)
    {
        string targetCharacterId = character.Id.Entry;
        foreach (Node node in EnumerateDescendants(screen))
        {
            if (node is not NCharacterSelectButton button || button.IsRandom)
            {
                continue;
            }

            CharacterModel buttonCharacter = button.Character;
            if (string.Equals(buttonCharacter.Id.Entry, targetCharacterId, StringComparison.OrdinalIgnoreCase))
            {
                return button;
            }
        }

        return null;
    }

    private static IEnumerable<Node> EnumerateDescendants(Node node)
    {
        for (int index = 0; index < node.GetChildCount(); index++)
        {
            Node child = node.GetChild(index);
            yield return child;

            foreach (Node descendant in EnumerateDescendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static void RefreshEditablePlayerHighlights(NRemoteLobbyPlayerContainer container, StartRunLobby lobby)
    {
        List<NRemoteLobbyPlayer>? nodes = RemoteLobbyNodesField.GetValue(container) as List<NRemoteLobbyPlayer>;
        if (nodes == null)
        {
            return;
        }

        ulong activePlayerId = GetActiveEditablePlayerId(lobby);
        foreach (NRemoteLobbyPlayer node in nodes)
        {
            if (((Node)node).GetNodeOrNull<Panel>(EditPlayerHighlightName) is { } highlight)
            {
                highlight.Visible = node.PlayerId == activePlayerId;
            }
        }
    }

    private static ulong GetActiveEditablePlayerId(StartRunLobby lobby)
    {
        if (!ActiveEditablePlayerIds.TryGetValue(lobby, out ulong playerId) ||
            lobby.Players.All((player) => player.id != playerId))
        {
            playerId = lobby.LocalPlayer.id;
            ActiveEditablePlayerIds[lobby] = playerId;
        }

        return playerId;
    }

}

[HarmonyPatch(typeof(StartRunLobby), nameof(StartRunLobby.SetLocalCharacter))]
internal static class AiTeammateStartRunLobbySetLocalCharacterPatch
{
    [HarmonyPrefix]
    private static bool Prefix(StartRunLobby __instance, CharacterModel character)
    {
        return !AiTeammateOriginalMultiplayerUi.TrySetSelectedAiCharacter(__instance, character);
    }
}
