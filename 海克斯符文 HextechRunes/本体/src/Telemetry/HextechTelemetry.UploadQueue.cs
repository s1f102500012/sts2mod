using System.Text;
using System.Text.Json;

namespace HextechRunes;

internal static partial class HextechTelemetry
{
	private static readonly HttpClient HttpClient = new()
	{
		Timeout = TimeSpan.FromSeconds(5)
	};

	private static readonly object QueueLock = new();

	private static async Task UploadPendingThenCurrentAsync(string endpoint, string currentJson, string runId)
	{
		List<string> pending = ReadPendingPayloads();
		pending.Add(currentJson);

		List<string> unsent = [];
		foreach (string payload in pending.TakeLast(MaxPendingLines))
		{
			if (IsShortRunPayload(payload))
			{
				continue;
			}

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
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] Telemetry uploaded run={runId}");
		}
		else
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Telemetry upload deferred unsent={unsent.Count}");
		}
	}

	private static bool IsShortRunPayload(string json)
	{
		try
		{
			using JsonDocument document = JsonDocument.Parse(json);
			if (document.RootElement.TryGetProperty("run", out JsonElement run)
				&& run.TryGetProperty("runTime", out JsonElement runTimeElement)
				&& runTimeElement.TryGetInt64(out long runTime))
			{
				return runTime < MinRunTimeForUploadSeconds;
			}
		}
		catch
		{
			return false;
		}

		return false;
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
			Directory.CreateDirectory(HextechDataPaths.GetDataDirectory());
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
}
