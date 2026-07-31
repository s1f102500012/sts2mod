using System.Text.Json;

namespace HextechRunes;

internal static partial class HextechTelemetry
{
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

		Directory.CreateDirectory(HextechDataPaths.GetDataDirectory());
		TelemetryConfig config = new(true, DefaultEndpoint);
		File.WriteAllText(configPath, JsonSerializer.Serialize(config, new JsonSerializerOptions(JsonOptions) { WriteIndented = true }));
	}

	private static string GetConfigPath()
	{
		return HextechDataPaths.GetFilePath(ConfigFileName);
	}

	private static string GetPendingPath()
	{
		return HextechDataPaths.GetFilePath(PendingFileName);
	}
}
