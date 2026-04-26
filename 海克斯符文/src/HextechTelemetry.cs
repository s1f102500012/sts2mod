using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace HextechRunes;

internal static class HextechTelemetry
{
	public sealed record RuneChoiceRecord(
		int ActIndex,
		int PlayerSlot,
		string Rarity,
		IReadOnlyList<string> Options,
		string? Selected,
		int RerollCount);

	private sealed record TelemetryConfig(bool Enabled, string Endpoint);

	private sealed record RunEndedPayload(
		int SchemaVersion,
		string ModId,
		string ModVersion,
		string GameVersion,
		string UploadedAtUtc,
		RunTelemetry Run,
		IReadOnlyList<PlayerTelemetry> Players,
		IReadOnlyList<RuneChoiceRecord> RuneChoices,
		IReadOnlyList<MonsterHexTelemetry> MonsterHexes);

	private sealed record RunTelemetry(
		string RunId,
		string SeedHash,
		bool IsVictory,
		string NetMode,
		int PlayerCount,
		int Ascension,
		int CurrentActIndex,
		int TotalFloor,
		long RunTime);

	private sealed record PlayerTelemetry(
		int Slot,
		string Character,
		IReadOnlyList<string> HextechRunes);

	private sealed record MonsterHexTelemetry(
		int ActIndex,
		string Rarity,
		string Hex);

	private const string DefaultEndpoint = "http://39.96.216.77/api/hextech-runes/run-result";
	private const string ConfigFileName = "telemetry_config.json";
	private const string PendingFileName = "telemetry_pending.jsonl";
	private const int MaxPendingLines = 64;

	internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private static readonly HttpClient HttpClient = new()
	{
		Timeout = TimeSpan.FromSeconds(5)
	};

	private static readonly object QueueLock = new();
	private static readonly HashSet<string> SubmittedRunIds = new(StringComparer.Ordinal);

	public static void Initialize()
	{
		try
		{
			EnsureConfigFile();
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Telemetry config init failed: {ex.Message}");
		}
	}

	public static void RecordRuneChoice(
		RunState runState,
		int actIndex,
		HextechRarityTier rarity,
		Player player,
		IReadOnlyList<RelicModel> options,
		RelicModel selected,
		int rerollCount)
	{
		try
		{
			HextechMayhemModifier? modifier = GetMayhemModifier(runState);
			if (modifier == null)
			{
				return;
			}

			int playerSlot = GetPlayerSlot(runState, player);
			RuneChoiceRecord record = new(
				actIndex,
				playerSlot,
				rarity.ToString(),
				options.Select(GetRelicId).ToArray(),
				GetRelicId(selected),
				Math.Max(0, rerollCount));
			modifier.RecordTelemetryChoice(record);
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Telemetry choice record failed: {ex.Message}");
		}
	}

	public static void OnRunEnded(RunState? runState, SerializableRun serializableRun, bool isVictory)
	{
		try
		{
			TelemetryConfig config = LoadConfig();
			if (!config.Enabled)
			{
				return;
			}

			NetGameType gameType = RunManager.Instance.NetService.Type;
			if (gameType is NetGameType.Client or NetGameType.Replay)
			{
				Log.Info($"[{ModInfo.Id}][Mayhem] Telemetry upload skipped for netMode={gameType}");
				return;
			}

			RunEndedPayload? payload = BuildPayload(runState, serializableRun, isVictory, gameType);
			if (payload == null)
			{
				return;
			}

			if (!SubmittedRunIds.Add(payload.Run.RunId))
			{
				return;
			}

			string json = JsonSerializer.Serialize(payload, JsonOptions);
			_ = Task.Run(() => UploadPendingThenCurrentAsync(config.Endpoint, json, payload.Run.RunId));
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Telemetry upload scheduling failed: {ex.Message}");
		}
	}

	private static RunEndedPayload? BuildPayload(RunState? runState, SerializableRun serializableRun, bool isVictory, NetGameType gameType)
	{
		if (runState == null)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Telemetry payload skipped: runState unavailable");
			return null;
		}

		string seed = runState.Rng.StringSeed ?? "";
		IReadOnlyList<Player> players = runState.Players;
		string seedHash = Sha256Hex("seed|" + seed);
		string runId = Sha256Hex(string.Join("|",
		[
			"hextech-runes-run-v1",
			seed,
			runState.AscensionLevel.ToString(),
			players.Count.ToString(),
			string.Join(",", players.Select(static player => player.Character.Id.Entry))
		]));

		HextechMayhemModifier? modifier = GetMayhemModifier(runState);
		IReadOnlyList<RuneChoiceRecord> runeChoices = modifier?.GetTelemetryChoiceRecords() ?? [];
		List<MonsterHexTelemetry> monsterHexes = [];
		if (modifier != null)
		{
			for (int actIndex = 0; actIndex < 3; actIndex++)
			{
				MonsterHexKind? hex = modifier.GetMonsterHexForAct(actIndex);
				HextechRarityTier? rarity = modifier.GetRarityForAct(actIndex);
				if (hex.HasValue && rarity.HasValue)
				{
					monsterHexes.Add(new MonsterHexTelemetry(actIndex, rarity.Value.ToString(), hex.Value.ToString()));
				}
			}
		}

		List<PlayerTelemetry> playerPayloads = [];
		for (int i = 0; i < players.Count; i++)
		{
			Player player = players[i];
			playerPayloads.Add(new PlayerTelemetry(
				i,
				player.Character.Id.Entry,
				player.Relics
					.Where(ModInfo.IsHextechRelic)
					.Select(GetRelicId)
					.Distinct(StringComparer.Ordinal)
					.OrderBy(static id => id, StringComparer.Ordinal)
					.ToArray()));
		}

		return new RunEndedPayload(
			1,
			ModInfo.Id,
			ModInfo.Version,
			ModInfo.TargetGameVersion,
			DateTimeOffset.UtcNow.ToString("O"),
			new RunTelemetry(
				runId,
				seedHash,
				isVictory,
				gameType.ToString(),
				players.Count,
				runState.AscensionLevel,
				runState.CurrentActIndex,
				runState.TotalFloor,
				serializableRun.RunTime),
			playerPayloads,
			runeChoices,
			monsterHexes);
	}

	private static async Task UploadPendingThenCurrentAsync(string endpoint, string currentJson, string runId)
	{
		List<string> pending = ReadPendingPayloads();
		pending.Add(currentJson);

		List<string> unsent = [];
		foreach (string payload in pending.TakeLast(MaxPendingLines))
		{
			try
			{
				using StringContent content = new(payload, Encoding.UTF8, "application/json");
				using HttpResponseMessage response = await HttpClient.PostAsync(endpoint, content);
				if (!response.IsSuccessStatusCode)
				{
					unsent.Add(payload);
				}
			}
			catch
			{
				unsent.Add(payload);
			}
		}

		WritePendingPayloads(unsent.TakeLast(MaxPendingLines).ToList());
		if (unsent.Count == 0)
		{
			Log.Info($"[{ModInfo.Id}][Mayhem] Telemetry uploaded run={runId}");
		}
		else
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Telemetry upload deferred unsent={unsent.Count}");
		}
	}

	private static TelemetryConfig LoadConfig()
	{
		EnsureConfigFile();
		try
		{
			string json = File.ReadAllText(GetConfigPath());
			TelemetryConfig? config = JsonSerializer.Deserialize<TelemetryConfig>(json, JsonOptions);
			if (config != null && !string.IsNullOrWhiteSpace(config.Endpoint))
			{
				return config;
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Telemetry config read failed: {ex.Message}");
		}

		return new TelemetryConfig(true, DefaultEndpoint);
	}

	private static void EnsureConfigFile()
	{
		string configPath = GetConfigPath();
		if (File.Exists(configPath))
		{
			return;
		}

		Directory.CreateDirectory(GetDataDirectory());
		TelemetryConfig config = new(true, DefaultEndpoint);
		File.WriteAllText(configPath, JsonSerializer.Serialize(config, new JsonSerializerOptions(JsonOptions) { WriteIndented = true }));
	}

	private static List<string> ReadPendingPayloads()
	{
		lock (QueueLock)
		{
			string path = GetPendingPath();
			if (!File.Exists(path))
			{
				return [];
			}

			return File.ReadLines(path)
				.Where(static line => !string.IsNullOrWhiteSpace(line))
				.TakeLast(MaxPendingLines)
				.ToList();
		}
	}

	private static void WritePendingPayloads(IReadOnlyList<string> payloads)
	{
		lock (QueueLock)
		{
			string path = GetPendingPath();
			Directory.CreateDirectory(GetDataDirectory());
			if (payloads.Count == 0)
			{
				if (File.Exists(path))
				{
					File.Delete(path);
				}

				return;
			}

			File.WriteAllLines(path, payloads);
		}
	}

	private static string GetDataDirectory()
	{
		try
		{
			string godotUserDir = Godot.OS.GetUserDataDir();
			if (!string.IsNullOrWhiteSpace(godotUserDir))
			{
				return Path.Combine(godotUserDir, ModInfo.Id);
			}
		}
		catch
		{
			// Fall back to a normal per-user directory when Godot paths are unavailable.
		}

		string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
		if (string.IsNullOrWhiteSpace(baseDir))
		{
			baseDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		}

		return Path.Combine(baseDir, "SlayTheSpire2", ModInfo.Id);
	}

	private static string GetConfigPath()
	{
		return Path.Combine(GetDataDirectory(), ConfigFileName);
	}

	private static string GetPendingPath()
	{
		return Path.Combine(GetDataDirectory(), PendingFileName);
	}

	private static HextechMayhemModifier? GetMayhemModifier(RunState runState)
	{
		return runState.Modifiers.OfType<HextechMayhemModifier>().LastOrDefault();
	}

	private static int GetPlayerSlot(RunState runState, Player player)
	{
		for (int i = 0; i < runState.Players.Count; i++)
		{
			if (ReferenceEquals(runState.Players[i], player))
			{
				return i;
			}
		}

		return Math.Max(0, runState.GetPlayerSlotIndex(player));
	}

	private static string GetRelicId(RelicModel relic)
	{
		return (relic.CanonicalInstance?.Id ?? relic.Id).Entry;
	}

	private static string Sha256Hex(string value)
	{
		byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
		return Convert.ToHexString(bytes).ToLowerInvariant();
	}
}
