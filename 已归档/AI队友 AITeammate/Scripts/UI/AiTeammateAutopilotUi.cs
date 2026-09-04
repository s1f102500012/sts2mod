using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Runs;

namespace AITeammate.Scripts;

[HarmonyPatch(typeof(NMultiplayerPlayerStateContainer), nameof(NMultiplayerPlayerStateContainer.Initialize))]
internal static class AiTeammateMultiplayerPlayerStateContainerInitializePatch
{
    private static void Postfix(NMultiplayerPlayerStateContainer __instance, RunState runState)
    {
        try
        {
            AiTeammateAutopilotUi.Attach(__instance, runState);
        }
        catch (Exception ex)
        {
            Log.Warn($"[AITeammate] Failed to attach autopilot UI: {ex.Message}");
        }
    }
}

[HarmonyPatch(typeof(NMultiplayerPlayerStateContainer), "UpdatePosition")]
internal static class AiTeammateMultiplayerPlayerStateContainerUpdatePositionPatch
{
    private static void Postfix(NMultiplayerPlayerStateContainer __instance)
    {
        try
        {
            AiTeammateAutopilotUi.RefreshPosition(__instance);
        }
        catch (Exception ex)
        {
            Log.Warn($"[AITeammate] Failed to refresh autopilot UI position: {ex.Message}");
        }
    }
}

[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.InitializeSingleplayer))]
internal static class AiTeammateSingleplayerCharacterSelectInitializePatch
{
    private static void Postfix(NCharacterSelectScreen __instance)
    {
        try
        {
            AiTeammateAutopilotUi.AttachToSingleplayerCharacterSelect(__instance);
        }
        catch (Exception ex)
        {
            Log.Warn($"[AITeammate] Failed to attach singleplayer character-select autopilot UI: {ex.Message}");
        }
    }
}

[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen._Ready))]
internal static class AiTeammateSingleplayerCharacterSelectReadyPatch
{
    private static void Postfix(NCharacterSelectScreen __instance)
    {
        try
        {
            AiTeammateAutopilotUi.AttachToSingleplayerCharacterSelectIfReady(__instance);
        }
        catch (Exception ex)
        {
            Log.Warn($"[AITeammate] Failed to attach ready singleplayer character-select autopilot UI: {ex.Message}");
        }
    }
}

[HarmonyPatch(typeof(NSingleplayerSubmenu), nameof(NSingleplayerSubmenu._Ready))]
internal static class AiTeammateSingleplayerSubmenuReadyPatch
{
    private static void Postfix(NSingleplayerSubmenu __instance)
    {
        try
        {
            AiTeammateAutopilotUi.AttachToSingleplayerSubmenu(__instance);
        }
        catch (Exception ex)
        {
            Log.Warn($"[AITeammate] Failed to attach singleplayer submenu autopilot UI: {ex.Message}");
        }
    }
}

[HarmonyPatch(typeof(NSingleplayerSubmenu), nameof(NSingleplayerSubmenu.OnSubmenuOpened))]
internal static class AiTeammateSingleplayerSubmenuOpenedPatch
{
    private static void Postfix(NSingleplayerSubmenu __instance)
    {
        try
        {
            AiTeammateAutopilotUi.AttachToSingleplayerSubmenu(__instance);
        }
        catch (Exception ex)
        {
            Log.Warn($"[AITeammate] Failed to refresh singleplayer submenu autopilot UI: {ex.Message}");
        }
    }
}

[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu.OpenSingleplayerSubmenu))]
internal static class AiTeammateMainMenuOpenSingleplayerSubmenuPatch
{
    private static void Postfix(NSingleplayerSubmenu __result)
    {
        try
        {
            AiTeammateAutopilotUi.AttachToSingleplayerSubmenu(__result);
        }
        catch (Exception ex)
        {
            Log.Warn($"[AITeammate] Failed to attach opened singleplayer submenu autopilot UI: {ex.Message}");
        }
    }
}

[HarmonyPatch(typeof(NSubmenuStack), nameof(NSubmenuStack.Push))]
internal static class AiTeammateSubmenuStackPushPatch
{
    private static void Postfix(NSubmenu screen)
    {
        try
        {
            AiTeammateAutopilotUi.AttachToSingleplayerMenuNode(screen);
        }
        catch (Exception ex)
        {
            Log.Warn($"[AITeammate] Failed to attach pushed singleplayer autopilot UI: {ex.Message}");
        }
    }
}

internal static class AiTeammateAutopilotUi
{
    private const string HostName = "AiTeammateAutopilotHost";
    private const string SingleplayerSubmenuHostName = "AiTeammateSingleplayerSubmenuAutopilotHost";
    private const string SingleplayerCharacterSelectHostName = "AiTeammateSingleplayerAutopilotHost";
    private const string TickboxName = "AiTeammateAutopilotTickbox";
    private const string LabelName = "AiTeammateAutopilotLabel";
    private const string LegacyToggleName = "AiTeammateAutopilotToggle";
    private const string TickboxScenePath = "res://scenes/ui/tickbox.tscn";
    private const float PlayerRowHeight = 66f;
    private const float ToggleX = 34f;
    private const float ToggleYOffset = 8f;
    private const float LabelX = 56f;
    private const float LabelY = 6f;
    private const float LabelWidth = 108f;
    private const float LabelHeight = 36f;
    private static readonly Vector2 SingleplayerTogglePosition = new(34f, 92f);
    private static readonly Vector2 SingleplayerSubmenuTogglePosition = new(44f, 178f);
    private static readonly Vector2 SingleplayerCharacterSelectTogglePosition = new(34f, 168f);

    public static void Attach(NMultiplayerPlayerStateContainer container, RunState runState)
    {
        RemoveLegacyDirectChild(container);

        if (!ShouldShow(runState))
        {
            RemoveHost();
            return;
        }

        Control? host = GetOrCreateHost();
        if (host == null)
        {
            return;
        }

        RefreshContent(host);
        UpdatePosition(host, container, runState);
    }

    public static void AttachToRun(RunState? runState)
    {
        if (runState == null)
        {
            RemoveHost();
            return;
        }

        if (runState.Players.Count > 1)
        {
            return;
        }

        if (!ShouldShow(runState))
        {
            RemoveHost();
            return;
        }

        Control? host = GetOrCreateHost();
        if (host == null)
        {
            return;
        }

        RefreshContent(host);
        host.GlobalPosition = SingleplayerTogglePosition;
    }

    public static void AttachToSingleplayerCharacterSelect(NCharacterSelectScreen screen)
    {
        if (!IsSingleplayerCharacterSelect(screen))
        {
            return;
        }

        if (screen.GetNodeOrNull<Control>(SingleplayerCharacterSelectHostName) is { } existingHost)
        {
            RefreshContent(existingHost);
            return;
        }

        Control host = CreateHost(SingleplayerCharacterSelectHostName);
        host.Position = SingleplayerCharacterSelectTogglePosition;
        screen.AddChild(host);
        RefreshContent(host);
        Log.Info("[AITeammate] Attached singleplayer character-select autopilot toggle.");
    }

    public static void AttachToSingleplayerCharacterSelectIfReady(NCharacterSelectScreen screen)
    {
        if (IsSingleplayerCharacterSelect(screen))
        {
            AttachToSingleplayerCharacterSelect(screen);
        }
    }

    public static void AttachToSingleplayerSubmenu(NSingleplayerSubmenu submenu)
    {
        if (submenu.GetNodeOrNull<Control>(SingleplayerSubmenuHostName) is { } existingHost)
        {
            RefreshContent(existingHost);
            existingHost.Visible = true;
            return;
        }

        Control host = CreateHost(SingleplayerSubmenuHostName);
        host.Position = SingleplayerSubmenuTogglePosition;
        submenu.AddChild(host);
        RefreshContent(host);
        Log.Info("[AITeammate] Attached singleplayer submenu autopilot toggle.");
    }

    public static void AttachToSingleplayerMenuNode(Node node)
    {
        switch (node)
        {
            case NSingleplayerSubmenu submenu:
                AttachToSingleplayerSubmenu(submenu);
                break;
            case NCharacterSelectScreen characterSelect:
                AttachToSingleplayerCharacterSelectIfReady(characterSelect);
                break;
        }
    }

    public static void RefreshPosition(NMultiplayerPlayerStateContainer container)
    {
        Control? host = GetGlobalUi()?.GetNodeOrNull<Control>(HostName);
        RunState? runState = RunManager.Instance?.DebugOnlyGetState();
        if (host == null || runState == null)
        {
            return;
        }

        if (!ShouldShow(runState))
        {
            RemoveHost();
            return;
        }

        RefreshContent(host);
        UpdatePosition(host, container, runState);
    }

    private static bool ShouldShow(RunState? runState)
    {
        return AiTeammateSessionRegistry.CanShowAutopilotToggle(runState);
    }

    private static bool IsSingleplayerCharacterSelect(NCharacterSelectScreen screen)
    {
        try
        {
            return screen.Lobby.NetService.Type == NetGameType.Singleplayer;
        }
        catch
        {
            return false;
        }
    }

    private static Control? GetOrCreateHost()
    {
        Control? globalUi = GetGlobalUi();
        if (globalUi == null)
        {
            return null;
        }

        if (globalUi.GetNodeOrNull<Control>(HostName) is { } existingHost)
        {
            return existingHost;
        }

        Control host = CreateHost(HostName);
        host.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        globalUi.AddChild(host);
        return host;
    }

    private static Control CreateHost(string hostName)
    {
        Control host = new()
        {
            Name = hostName,
            FocusMode = Control.FocusModeEnum.All,
            MouseFilter = Control.MouseFilterEnum.Pass,
            ZIndex = 4096,
            CustomMinimumSize = new Vector2(210f, 44f),
            Size = new Vector2(210f, 44f)
        };

        Control tickbox = CreateTickbox();
        host.AddChild(tickbox);
        Label label = CreateLabel();
        host.AddChild(label);
        SetTickboxVisualState(tickbox, AiTeammateSessionRegistry.AutopilotEnabled);
        return host;
    }

    private static void RefreshContent(Control host)
    {
        if (host.GetNodeOrNull<Control>(TickboxName) is { } tickbox)
        {
            SetTickboxVisualState(tickbox, AiTeammateSessionRegistry.AutopilotEnabled);
        }

        if (host.GetNodeOrNull<Label>(LabelName) is { } label)
        {
            label.Text = AiTeammateLocalization.Tr("run.autopilot");
        }
    }

    private static Control CreateTickbox()
    {
        PackedScene scene = PreloadManager.Cache.GetScene(TickboxScenePath);
        Control tickbox = scene.Instantiate<Control>(PackedScene.GenEditState.Disabled);
        tickbox.Name = TickboxName;
        tickbox.Position = Vector2.Zero;
        tickbox.FocusMode = Control.FocusModeEnum.All;
        tickbox.MouseFilter = Control.MouseFilterEnum.Stop;
        tickbox.CustomMinimumSize = new Vector2(44f, 44f);
        tickbox.Size = new Vector2(44f, 44f);

        if (tickbox is NTickbox nativeTickbox)
        {
            nativeTickbox.Connect(NTickbox.SignalName.Toggled, Callable.From<NTickbox>(OnNativeTickboxToggled));
        }
        else
        {
            tickbox.Connect(Control.SignalName.GuiInput, Callable.From<InputEvent>(inputEvent => OnTickboxGuiInput(tickbox, inputEvent)));
        }

        return tickbox;
    }

    private static Label CreateLabel()
    {
        Label label = new()
        {
            Name = LabelName,
            Text = AiTeammateLocalization.Tr("run.autopilot"),
            Position = new Vector2(LabelX, LabelY),
            CustomMinimumSize = new Vector2(LabelWidth, LabelHeight),
            Size = new Vector2(LabelWidth, LabelHeight),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.AddThemeColorOverride("font_color", new Color(0.97f, 0.82f, 0.34f));
        label.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.85f));
        label.AddThemeConstantOverride("shadow_offset_x", 2);
        label.AddThemeConstantOverride("shadow_offset_y", 2);
        label.AddThemeFontSizeOverride("font_size", 22);
        return label;
    }

    private static void UpdatePosition(Control host, NMultiplayerPlayerStateContainer container, RunState runState)
    {
        int playerRows = Math.Max(1, runState.Players.Count);
        host.GlobalPosition = container.GlobalPosition + new Vector2(ToggleX, playerRows * PlayerRowHeight + ToggleYOffset);
    }

    private static void RemoveHost()
    {
        if (GetGlobalUi()?.GetNodeOrNull<Control>(HostName) is not { } host)
        {
            return;
        }

        host.GetParent()?.RemoveChild(host);
        host.QueueFree();
    }

    private static void RemoveLegacyDirectChild(NMultiplayerPlayerStateContainer container)
    {
        if (container.GetNodeOrNull<Control>(LegacyToggleName) is not { } legacy)
        {
            return;
        }

        legacy.GetParent()?.RemoveChild(legacy);
        legacy.QueueFree();
    }

    private static Control? GetGlobalUi()
    {
        return NRun.Instance?.GlobalUi;
    }

    private static void OnNativeTickboxToggled(NTickbox tickbox)
    {
        AiTeammateSessionRegistry.SetAutopilotEnabled(tickbox.IsTicked);
        RefreshAllMountedTickboxes(tickbox.IsTicked);
    }

    private static void OnTickboxGuiInput(Control tickbox, InputEvent inputEvent)
    {
        if (inputEvent is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
        {
            return;
        }

        bool enabled = !AiTeammateSessionRegistry.AutopilotEnabled;
        AiTeammateSessionRegistry.SetAutopilotEnabled(enabled);
        SetTickboxVisualState(tickbox, enabled);
        RefreshAllMountedTickboxes(enabled);

        SfxCmd.Play(enabled ? "event:/sfx/ui/clicks/ui_checkbox_on" : "event:/sfx/ui/clicks/ui_checkbox_off");
    }

    private static void RefreshAllMountedTickboxes(bool enabled)
    {
        if (GetGlobalUi()?.GetNodeOrNull<Control>(HostName)?.GetNodeOrNull<Control>(TickboxName) is { } runTickbox)
        {
            SetTickboxVisualState(runTickbox, enabled);
        }
    }

    private static void SetTickboxVisualState(Control tickbox, bool enabled)
    {
        if (tickbox is NTickbox nativeTickbox)
        {
            nativeTickbox.IsTicked = enabled;
            return;
        }

        if (FindControl(tickbox, "Ticked") is { } ticked)
        {
            ticked.Visible = enabled;
        }

        if (FindControl(tickbox, "NotTicked") is { } notTicked)
        {
            notTicked.Visible = !enabled;
        }
    }

    private static Control? FindControl(Node root, string name)
    {
        return root.FindChild(name, recursive: true, owned: false) as Control;
    }
}
