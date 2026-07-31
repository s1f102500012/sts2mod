using System.Text.Json;
using MegaCrit.Sts2.Core.Saves;

namespace HextechRunes;

internal static partial class HextechTelemetry
{
	private const string DefaultEndpoint = HextechServerEndpoints.TelemetryEndpoint;
	private const string ConfigFileName = "telemetry_config.json";
	private const string PendingFileName = "telemetry_pending.jsonl";
	private const int MaxPendingLines = 64;
	private const long MinRunTimeForUploadSeconds = 60;

	internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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
		int rerollCount,
		int choiceOrdinal = 0)
	{
		try
		{
			HextechMayhemModifier? modifier = HextechMayhemModifier.FindIn(runState);
			if (modifier == null)
			{
				return;
			}

			int playerSlot = GetPlayerSlot(runState, player);
			RuneChoiceRecord record = new(
				actIndex,
				playerSlot,
				Math.Max(0, choiceOrdinal),
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

			if (serializableRun.RunTime < MinRunTimeForUploadSeconds)
			{
				HextechLog.Info($"[{ModInfo.Id}][Mayhem] Telemetry upload skipped for short run runTime={serializableRun.RunTime}s");
				return;
			}

			NetGameType gameType = RunManager.Instance.NetService.Type;
			if (gameType is NetGameType.Client or NetGameType.Replay)
			{
				HextechLog.Info($"[{ModInfo.Id}][Mayhem] Telemetry upload skipped for netMode={gameType}");
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
}
