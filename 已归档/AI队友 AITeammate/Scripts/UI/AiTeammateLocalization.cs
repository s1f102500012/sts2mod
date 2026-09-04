using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Localization;
using GodotFileAccess = Godot.FileAccess;

namespace AITeammate.Scripts;

internal static class AiTeammateLocalization
{
    private const string LocalizationFileName = "ai_teammate_ui.loc";
    private static string? loadedLanguageCode;
    private static Dictionary<string, string>? loadedLanguageTable;

    private static readonly Dictionary<string, string> English = new(StringComparer.Ordinal)
    {
        ["main_menu.play_with_ai"] = "Play with AI Teammate",
        ["setup.title"] = "AI Teammate Setup",
        ["setup.subtitle"] = "Choose a real host character and any AI teammates, then reuse the multiplayer lobby start flow.",
        ["setup.slot.human"] = "Human Player",
        ["setup.slot.ai"] = "AI Player {0}",
        ["setup.slot.required"] = "Required",
        ["setup.slot.optional"] = "Optional",
        ["setup.slot.host"] = "Host",
        ["setup.slot.ai_teammate"] = "AI teammate",
        ["setup.remove"] = "X",
        ["session.host_player"] = "Host Player",
        ["session.ai_player"] = "AI Player {0}",
        ["setup.session.title"] = "Session panel",
        ["setup.session.select_host"] = "Select the host character first to build a local teammate session.",
        ["setup.session.participants"] = "Session participants: {0}",
        ["setup.session.ready_with_ai"] = "Host plus {0} local fake remote teammate(s) now share a real StartRunLobby model.{1}",
        ["setup.session.test_map_enabled"] = " Test map enabled.",
        ["setup.session.add_ai"] = "Host is ready in the session model. Add at least one AI teammate to enable Proceed.",
        ["setup.test_map"] = "Use Test Map",
        ["button.proceed"] = "Proceed",
        ["button.add_ai"] = "Add AI",
        ["button.add_ai_teammate"] = "Add AI Teammate",
        ["run.autopilot"] = "AI Autopilot",
        ["button.cancel"] = "Cancel",
        ["button.clear_selection"] = "Clear Selection",
        ["button.select"] = "Select",
        ["picker.title"] = "Choose AI Character",
        ["picker.subtitle.default"] = "Choose a placeholder character for this AI slot.",
        ["picker.subtitle.host"] = "Choose a placeholder character for the Human Player slot.",
        ["picker.subtitle.ai"] = "Choose a placeholder character for AI Player {0}.",
        ["continue.title"] = "AI Teammate Run Found",
        ["continue.default_description"] = "Continue your saved AI teammate run, or abandon it and return to setup.",
        ["continue.missing_save_description"] = "No valid AI teammate save was found. Press Back to return, or reopen the mode to start a new setup flow.",
        ["continue.continue_run"] = "Continue Run",
        ["continue.abandon_run"] = "Abandon Run",
        ["character.ironclad"] = "Ironclad",
        ["character.silent"] = "Silent",
        ["character.defect"] = "Defect",
        ["character.regent"] = "Regent",
        ["character.necrobinder"] = "Necrobinder"
    };

    private static readonly Dictionary<string, string> SimplifiedChinese = new(StringComparer.Ordinal)
    {
        ["main_menu.play_with_ai"] = "AI多人模式",
        ["setup.title"] = "AI队友设置",
        ["setup.subtitle"] = "选择主机角色和AI队友，然后沿用原版多人开始流程。",
        ["setup.slot.human"] = "玩家角色",
        ["setup.slot.ai"] = "AI队友{0}",
        ["setup.slot.required"] = "必选",
        ["setup.slot.optional"] = "可选",
        ["setup.slot.host"] = "主机",
        ["setup.slot.ai_teammate"] = "AI队友",
        ["setup.remove"] = "X",
        ["session.host_player"] = "主机玩家",
        ["session.ai_player"] = "AI队友{0}",
        ["setup.session.title"] = "队伍面板",
        ["setup.session.select_host"] = "先选择玩家角色，才能创建本地AI队友队伍。",
        ["setup.session.participants"] = "队伍人数：{0}",
        ["setup.session.ready_with_ai"] = "主机和{0}名本地AI队友已加入原版StartRunLobby模型。{1}",
        ["setup.session.test_map_enabled"] = "已启用测试地图。",
        ["setup.session.add_ai"] = "主机已进入队伍模型。至少添加一名AI队友后才能继续。",
        ["setup.test_map"] = "使用测试地图",
        ["button.proceed"] = "继续",
        ["button.add_ai"] = "添加AI",
        ["button.add_ai_teammate"] = "添加AI队友",
        ["run.autopilot"] = "AI托管",
        ["button.cancel"] = "取消",
        ["button.clear_selection"] = "清除选择",
        ["button.select"] = "选择",
        ["picker.title"] = "选择AI角色",
        ["picker.subtitle.default"] = "为这个AI槽位选择一个角色。",
        ["picker.subtitle.host"] = "为玩家槽位选择角色。",
        ["picker.subtitle.ai"] = "为AI队友{0}选择角色。",
        ["continue.title"] = "发现AI队友存档",
        ["continue.default_description"] = "继续已保存的AI队友流程，或放弃并回到设置界面。",
        ["continue.missing_save_description"] = "没有找到可继续的AI队友存档。按返回键退出，或重新打开该模式创建新队伍。",
        ["continue.continue_run"] = "继续流程",
        ["continue.abandon_run"] = "放弃流程",
        ["character.ironclad"] = "铁甲战士",
        ["character.silent"] = "静默猎手",
        ["character.defect"] = "故障机器人",
        ["character.regent"] = "储君",
        ["character.necrobinder"] = "亡灵缚手"
    };

    public static string Tr(string key, params object[] args)
    {
        string languageCode = CurrentLanguageCode();
        Dictionary<string, string> table = LoadExternalTable(languageCode) ??
                                           (IsChineseLanguage(languageCode) ? SimplifiedChinese : English);
        if (!table.TryGetValue(key, out string? value) && !English.TryGetValue(key, out value))
        {
            return key;
        }

        return args.Length == 0 ? value : string.Format(CultureInfo.InvariantCulture, value, args);
    }

    public static string CharacterName(AiTeammatePlaceholderCharacter character)
    {
        try
        {
            return character.ResolveModel().Title.GetFormattedText();
        }
        catch (Exception ex)
        {
            Log.Warn($"[AITeammate] Failed to resolve localized character name for {character.Id}: {ex.Message}");
            return Tr("character." + character.Id);
        }
    }

    public static bool IsCurrentChineseLanguage()
    {
        return IsChineseLanguage(CurrentLanguageCode());
    }

    private static Dictionary<string, string>? LoadExternalTable(string languageCode)
    {
        if (string.Equals(loadedLanguageCode, languageCode, StringComparison.OrdinalIgnoreCase))
        {
            return loadedLanguageTable;
        }

        loadedLanguageCode = languageCode;
        loadedLanguageTable = TryLoadExternalTable(languageCode);
        if (loadedLanguageTable == null && IsChineseLanguage(languageCode))
        {
            loadedLanguageTable = TryLoadExternalTable("zhs");
        }

        if (loadedLanguageTable == null && !string.Equals(languageCode, "eng", StringComparison.OrdinalIgnoreCase))
        {
            loadedLanguageTable = TryLoadExternalTable("eng");
        }

        return loadedLanguageTable;
    }

    private static Dictionary<string, string>? TryLoadExternalTable(string languageCode)
    {
        try
        {
            string? assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrWhiteSpace(assemblyDirectory))
            {
                return null;
            }

            string path = Path.Combine(assemblyDirectory, "localization", languageCode, LocalizationFileName);
            if (!File.Exists(path))
            {
                return TryLoadResourceTable(languageCode);
            }

            string json = File.ReadAllText(path);
            Dictionary<string, string>? table = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            Log.Info($"[AITeammate] Loaded UI localization language={languageCode} entries={table?.Count ?? 0}.");
            return table;
        }
        catch (Exception ex)
        {
            Log.Warn($"[AITeammate] Failed to load UI localization language={languageCode}: {ex.Message}");
            return null;
        }
    }

    private static Dictionary<string, string>? TryLoadResourceTable(string languageCode)
    {
        try
        {
            string path = $"res://sts2AITeammate/localization/{languageCode}/{LocalizationFileName}";
            if (!GodotFileAccess.FileExists(path))
            {
                return null;
            }

            using GodotFileAccess file = GodotFileAccess.Open(path, GodotFileAccess.ModeFlags.Read);
            string json = file.GetAsText();
            Dictionary<string, string>? table = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            Log.Info($"[AITeammate] Loaded UI localization from PCK language={languageCode} entries={table?.Count ?? 0}.");
            return table;
        }
        catch (Exception ex)
        {
            Log.Warn($"[AITeammate] Failed to load PCK UI localization language={languageCode}: {ex.Message}");
            return null;
        }
    }

    private static string CurrentLanguageCode()
    {
        string? language = LocManager.Instance?.Language;
        return string.IsNullOrWhiteSpace(language) ? "eng" : language;
    }

    private static bool IsChineseLanguage(string language)
    {
        return string.Equals(language, "zhs", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(language, "zht", StringComparison.OrdinalIgnoreCase);
    }
}
