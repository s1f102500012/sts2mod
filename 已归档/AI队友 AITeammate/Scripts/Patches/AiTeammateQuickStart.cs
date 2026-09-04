using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace AITeammate.Scripts;

internal static class AiTeammateQuickStart
{
    private const string QuickStartEnv = "STS2_AI_QUICKSTART";
    private const string PlayersEnv = "STS2_AI_QUICKSTART_PLAYERS";
    private const string SeedEnv = "STS2_AI_QUICKSTART_SEED";
    private const string AutoBeginEnv = "STS2_AI_QUICKSTART_BEGIN";
    private const string AutopilotEnv = "STS2_AI_AUTOPILOT";
    private const string TestMapEnv = "STS2_AI_TEST_MAP";
    private const string QuickStartTestMapEnv = "STS2_AI_QUICKSTART_TEST_MAP";
    private static bool _hasQueuedQuickStart;

    public static bool UseTestMap =>
        ReadBool(TestMapEnv, defaultValue: false) ||
        ReadBool(QuickStartTestMapEnv, defaultValue: false);

    public static void TryStartFromMainMenu(NMainMenu mainMenu)
    {
        if (_hasQueuedQuickStart || !ReadBool(QuickStartEnv, defaultValue: false))
        {
            return;
        }

        _hasQueuedQuickStart = true;
        TaskHelper.RunSafely(StartFromMainMenuAsync(mainMenu));
    }

    private static async Task StartFromMainMenuAsync(NMainMenu mainMenu)
    {
        await ((Node)mainMenu).ToSignal(((Node)mainMenu).GetTree(), SceneTree.SignalName.ProcessFrame);

        try
        {
            int desiredPlayers = Math.Clamp(ReadInt(PlayersEnv, 4), 1, 4);
            string seed = ReadString(SeedEnv);
            if (string.IsNullOrWhiteSpace(seed))
            {
                seed = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
            }

            int rngSeed = StringHelper.GetDeterministicHashCode(seed);
            if (rngSeed == int.MinValue)
            {
                rngSeed = 0;
            }

            Random rng = new(Math.Abs(rngSeed));
            NCharacterSelectScreen screen = MainMenuAiTeammatePatch.CreateStockAiLobbyScreen(mainMenu.SubmenuStack);
            mainMenu.SubmenuStack.Push(screen);

            StartRunLobby lobby = screen.Lobby;
            CharacterModel hostCharacter = AiTeammateOriginalMultiplayerUi.ChooseRandomUnusedCharacter(lobby, rng);
            lobby.SetLocalCharacter(hostCharacter);
            int addedAi = AiTeammateOriginalMultiplayerUi.FillWithRandomAiPlayers(lobby, desiredPlayers, rng);
            AiTeammateOriginalMultiplayerUi.SyncSessionFromLobby(lobby);

            if (ReadBool(AutopilotEnv, defaultValue: true))
            {
                AiTeammateSessionRegistry.SetAutopilotEnabled(true);
            }

            Log.Info($"[AITeammate] Quick-start prepared stock lobby. players={lobby.Players.Count}, addedAi={addedAi}, host={hostCharacter.Id.Entry}, seed={seed}, autopilot={AiTeammateSessionRegistry.AutopilotEnabled}, testMap={UseTestMap}");

            if (ReadBool(AutoBeginEnv, defaultValue: true))
            {
                BeginRun(lobby, seed);
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[AITeammate] Quick-start failed: {ex}");
        }
    }

    private static void BeginRun(StartRunLobby lobby, string seed)
    {
        MethodInfo? beginRunMethod = AccessTools.Method(typeof(StartRunLobby), "BeginRunForAllPlayers");
        if (beginRunMethod == null)
        {
            Log.Error("[AITeammate] Quick-start could not find StartRunLobby.BeginRunForAllPlayers.");
            return;
        }

        Log.Info($"[AITeammate] Quick-start beginning run. seed={seed}, players={lobby.Players.Count}");
        beginRunMethod.Invoke(lobby, new object[] { seed, new List<ModifierModel>() });
    }

    private static string ReadString(string name)
    {
        return System.Environment.GetEnvironmentVariable(name) ?? string.Empty;
    }

    private static int ReadInt(string name, int defaultValue)
    {
        return int.TryParse(System.Environment.GetEnvironmentVariable(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : defaultValue;
    }

    private static bool ReadBool(string name, bool defaultValue)
    {
        string? value = System.Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("on", StringComparison.OrdinalIgnoreCase);
    }
}
