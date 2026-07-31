using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace HextechRunes;

internal static partial class HextechRelicVisibilityHooks
{
	private const bool DefaultShowHiddenRelicsToggle = false;
	private const bool DefaultShowUpdateNotice = true;
	private const bool DefaultCollapseEnemyHexes = false;

	// 折叠敌方海克斯(纯 UI 偏好,默认关):开=顶栏地图按钮左侧一个折叠按钮,点开在下方弹出敌方海克斯窗口;
	// 关=旧版行为(敌方海克斯直接平铺在顶栏 modifiers 里)。读取见 HextechEnemyUi。
	internal static bool GetCollapseEnemyHexes()
	{
		return _config.CollapseEnemyHexes;
	}

	internal static bool GetDefaultCollapseEnemyHexes()
	{
		return DefaultCollapseEnemyHexes;
	}

	internal static void SetCollapseEnemyHexes(bool collapse)
	{
		_config.CollapseEnemyHexes = collapse;
		SaveConfig(_config);
		HextechLog.Info($"[{ModInfo.Id}][Mayhem] collapse_enemy_hexes={collapse}.");
	}

	internal static bool GetShowHiddenRelicsToggle()
	{
		return _config.ShowHiddenRelicsToggle;
	}

	internal static bool GetDefaultShowHiddenRelicsToggle()
	{
		return DefaultShowHiddenRelicsToggle;
	}

	internal static bool GetShowUpdateNotice()
	{
		return _config.ShowUpdateNotice;
	}

	internal static bool GetDefaultShowUpdateNotice()
	{
		return DefaultShowUpdateNotice;
	}

	internal static void SetShowUpdateNotice(bool showNotice)
	{
		_config.ShowUpdateNotice = showNotice;
		SaveConfig(_config);
		HextechLog.Info($"[{ModInfo.Id}][Mayhem] show_update_notice={showNotice}.");
	}

	internal static void SetShowHiddenRelicsToggle(bool showToggle)
	{
		_config.ShowHiddenRelicsToggle = showToggle;
		SaveConfig(_config);

		NGlobalUi? globalUi = NRun.Instance?.GlobalUi;
		if (globalUi != null && GodotObject.IsInstanceValid(globalUi))
		{
			InstallToggle(globalUi);
			ApplyHiddenState(globalUi);
		}

		HextechLog.Info($"[{ModInfo.Id}][Mayhem] show_hidden_ui_toggle={showToggle}.");
	}

	private static ModUiConfig LoadOrCreateConfig()
	{
		string? configPath = null;
		try
		{
			configPath = GetConfigPath();
			if (!File.Exists(configPath))
			{
				ModUiConfig defaultConfig = CreateCurrentUiConfig();
				SaveConfig(defaultConfig);
				return defaultConfig;
			}

			ModUiConfig? parsed = JsonSerializer.Deserialize<ModUiConfig>(File.ReadAllText(configPath), JsonOptions);
			ModUiConfig config = parsed ?? new ModUiConfig();
			// 0.8.4 一次性强制回默认(与 rune_config 的 v15 重置同批):旧 UI 偏好整体丢弃。
			if (config.ConfigVersion < CurrentUiConfigVersion)
			{
				HextechLog.Info($"[{ModInfo.Id}][Mayhem] UI config version {config.ConfigVersion} < {CurrentUiConfigVersion}; forcing reset to defaults (0.8.4).");
				config = CreateCurrentUiConfig();
			}

			SaveConfig(config);
			return config;
		}
		catch (JsonException ex)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Relic visibility config JSON is invalid; using defaults: {ex.Message}", 2);
			ModUiConfig config = CreateCurrentUiConfig();
			if (configPath != null && TryBackupCorruptConfig(configPath))
			{
				SaveConfig(config);
			}

			return config;
		}
		catch (UnauthorizedAccessException ex)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Relic visibility config read was denied; using in-memory defaults without overwriting the file: {ex.Message}", 2);
			return CreateCurrentUiConfig();
		}
		catch (IOException ex)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Relic visibility config read failed due to I/O; using in-memory defaults without overwriting the file: {ex.Message}", 2);
			return CreateCurrentUiConfig();
		}
		catch (Exception ex)
		{
			Log.Error($"[{ModInfo.Id}][Mayhem] Unexpected relic visibility config read failure; using in-memory defaults without overwriting the file: {ex}");
			return CreateCurrentUiConfig();
		}
	}

	private static ModUiConfig CreateCurrentUiConfig()
	{
		return new ModUiConfig { ConfigVersion = CurrentUiConfigVersion };
	}

	private static bool TryBackupCorruptConfig(string configPath)
	{
		try
		{
			File.Copy(configPath, configPath + ".corrupt.bak", overwrite: true);
			return true;
		}
		catch (UnauthorizedAccessException ex)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Could not back up corrupt relic visibility config; original file will not be overwritten: {ex.Message}", 2);
			return false;
		}
		catch (IOException ex)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Could not back up corrupt relic visibility config; original file will not be overwritten: {ex.Message}", 2);
			return false;
		}
		catch (Exception ex)
		{
			Log.Error($"[{ModInfo.Id}][Mayhem] Unexpected relic visibility config backup failure; original file will not be overwritten: {ex}");
			return false;
		}
	}

	private static void SaveConfig(ModUiConfig config)
	{
		try
		{
			string configPath = GetConfigPath();
			Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
			string serialized = JsonSerializer.Serialize(config, JsonOptions);
			File.WriteAllText(configPath, serialized);
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Relic visibility config write failed: {ex.Message}", 2);
		}
	}

	private static string GetConfigPath()
	{
		return HextechDataPaths.GetFilePath(ConfigFileName);
	}

	private const int CurrentUiConfigVersion = 1;

	private sealed class ModUiConfig
	{
		// 默认必须是 0:旧文件无此字段时反序列化保留属性初始值,0 才能触发一次性重置;
		// 新建/重置的实例由 CreateCurrentUiConfig 显式设为 CurrentUiConfigVersion。
		[JsonPropertyName("config_version")]
		public int ConfigVersion { get; set; }

		[JsonPropertyName("show_hidden_relics_toggle")]
		public bool ShowHiddenRelicsToggle { get; set; } = DefaultShowHiddenRelicsToggle;

		[JsonPropertyName("show_update_notice")]
		public bool ShowUpdateNotice { get; set; } = DefaultShowUpdateNotice;

		[JsonPropertyName("collapse_enemy_hexes")]
		public bool CollapseEnemyHexes { get; set; } = DefaultCollapseEnemyHexes;

		[JsonPropertyName("hide_relics")]
		public bool HideRelics { get; set; }
	}
}
