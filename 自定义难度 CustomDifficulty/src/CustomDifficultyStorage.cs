using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Saves;

namespace CustomDifficulty;

internal static class CustomDifficultyStorage
{
	// v2：新增 mode 与每房间增量字段；v1 旧文件缺省回退为固定倍率模式。
	private const int CurrentSchemaVersion = 2;
	private const string SettingsFileName = "settings.json";

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = true
	};

	private static bool _initialized;

	public static void Initialize()
	{
		if (_initialized)
		{
			return;
		}

		_initialized = true;
		LoadCurrentProfile();
	}

	public static void LoadCurrentProfile()
	{
		if (!TryGetSettingsPath(out string path))
		{
			return;
		}

		try
		{
			if (!File.Exists(path))
			{
				Log.Debug($"[{ModInfo.Id}] No persisted settings found at {path}; using defaults.");
				return;
			}

			string json = File.ReadAllText(path);
			PersistedSettings? settings = JsonSerializer.Deserialize<PersistedSettings>(json, JsonOptions);
			if (settings == null)
			{
				Log.Warn($"[{ModInfo.Id}] Persisted settings file was empty or invalid: {path}");
				return;
			}

			int hpTicks = CustomDifficultySettings.NormalizeTicks(settings.MonsterHpTicks);
			int attackTicks = CustomDifficultySettings.NormalizeTicks(settings.MonsterAttackTicks);
			CustomDifficultyMode mode = CustomDifficultySettings.NormalizeMode(settings.Mode);
			CustomDifficultySettings.SetPersisted(
				hpTicks,
				attackTicks,
				mode,
				settings.HpDeltaPercentPerRoom,
				settings.AttackDeltaPercentPerRoom);
			Log.Info(
				$"[{ModInfo.Id}] Loaded persisted settings: mode={mode} hp={CustomDifficultySettings.FormatMultiplier(hpTicks)} attack={CustomDifficultySettings.FormatMultiplier(attackTicks)} hpDelta={CustomDifficultySettings.FormatDeltaPercent(settings.HpDeltaPercentPerRoom)}/room attackDelta={CustomDifficultySettings.FormatDeltaPercent(settings.AttackDeltaPercentPerRoom)}/room.");
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}] Failed to load persisted settings: {ex}");
		}
	}

	public static void SaveCurrentProfile()
	{
		if (!TryGetSettingsPath(out string path))
		{
			return;
		}

		try
		{
			string? directory = Path.GetDirectoryName(path);
			if (!string.IsNullOrWhiteSpace(directory))
			{
				Directory.CreateDirectory(directory);
			}

			PersistedSettings settings = new()
			{
				SchemaVersion = CurrentSchemaVersion,
				MonsterHpTicks = CustomDifficultySettings.MonsterHpTicks,
				MonsterAttackTicks = CustomDifficultySettings.MonsterAttackTicks,
				Mode = (int)CustomDifficultySettings.Mode,
				HpDeltaPercentPerRoom = CustomDifficultySettings.HpDeltaPercentPerRoom,
				AttackDeltaPercentPerRoom = CustomDifficultySettings.AttackDeltaPercentPerRoom
			};

			File.WriteAllText(path, JsonSerializer.Serialize(settings, JsonOptions));
			Log.Debug($"[{ModInfo.Id}] Saved persisted settings to {path}.");
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}] Failed to save persisted settings: {ex}");
		}
	}

	private static bool TryGetSettingsPath(out string path)
	{
		path = string.Empty;
		try
		{
			SaveManager saveManager = SaveManager.Instance;
			if (!saveManager.IsProfileInitialized)
			{
				Log.Debug($"[{ModInfo.Id}] Save profile is not initialized yet; deferred settings persistence.");
				return false;
			}

			string profilePath = UserDataPathProvider.GetProfileScopedPath(
				saveManager.CurrentProfileId,
				$"{ModInfo.Id}/{SettingsFileName}");
			path = ProjectSettings.GlobalizePath(profilePath);
			return !string.IsNullOrWhiteSpace(path);
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}] Failed to resolve persisted settings path: {ex.Message}");
			return false;
		}
	}

	private sealed class PersistedSettings
	{
		[JsonPropertyName("schema_version")]
		public int SchemaVersion { get; set; }

		[JsonPropertyName("monster_hp_ticks")]
		public int MonsterHpTicks { get; set; } = CustomDifficultySettings.DefaultTicks;

		[JsonPropertyName("monster_attack_ticks")]
		public int MonsterAttackTicks { get; set; } = CustomDifficultySettings.DefaultTicks;

		[JsonPropertyName("mode")]
		public int Mode { get; set; } = (int)CustomDifficultyMode.Fixed;

		[JsonPropertyName("hp_delta_percent_per_room")]
		public int HpDeltaPercentPerRoom { get; set; } = CustomDifficultySettings.DefaultDeltaPercent;

		[JsonPropertyName("attack_delta_percent_per_room")]
		public int AttackDeltaPercentPerRoom { get; set; } = CustomDifficultySettings.DefaultDeltaPercent;
	}
}
