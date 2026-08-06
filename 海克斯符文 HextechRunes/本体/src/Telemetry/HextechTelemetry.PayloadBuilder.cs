using System.Security.Cryptography;
using System.Text;
using MegaCrit.Sts2.Core.Saves;

namespace HextechRunes;

internal static partial class HextechTelemetry
{
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

		HextechMayhemModifier? modifier = HextechMayhemModifier.FindIn(runState);
		IReadOnlyList<RuneChoiceRecord> runeChoices = modifier?.GetTelemetryChoiceRecords() ?? [];

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
			BuildPlayerPayloads(players),
			runeChoices,
			BuildMonsterHexPayloads(modifier));
	}

	private static IReadOnlyList<PlayerTelemetry> BuildPlayerPayloads(IReadOnlyList<Player> players)
	{
		List<PlayerTelemetry> payloads = [];
		for (int i = 0; i < players.Count; i++)
		{
			Player player = players[i];
			payloads.Add(new PlayerTelemetry(
				i,
				player.Character.Id.Entry,
				player.Relics
					.Where(HextechCatalog.IsHextechRelic)
					.Select(GetRelicId)
					.Distinct(StringComparer.Ordinal)
					.OrderBy(static id => id, StringComparer.Ordinal)
					.ToArray()));
		}

		return payloads;
	}

	private static IReadOnlyList<MonsterHexTelemetry> BuildMonsterHexPayloads(HextechMayhemModifier? modifier)
	{
		List<MonsterHexTelemetry> payloads = [];
		if (modifier == null)
		{
			return payloads;
		}

		for (int actIndex = 0; actIndex < modifier.StageCount; actIndex++)
		{
			HextechRarityTier? rarity = modifier.GetRarityForAct(actIndex);
			if (!rarity.HasValue)
			{
				continue;
			}

			foreach (MonsterHexKind hex in modifier.GetMonsterHexesForAct(actIndex))
			{
				payloads.Add(new MonsterHexTelemetry(actIndex, rarity.Value.ToString(), hex.ToString()));
			}
		}

		return payloads;
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
