using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using MegaCrit.Sts2.Core.Logging;

namespace EndlessMode;

internal enum EndlessOptionalReward
{
	MimicInfestation,
	TimeMaze,
	Muzzle,
	HorribleTrophy
}

internal sealed class EndlessModeConfig
{
	public const int DefaultPlagueSpearPercent = 50;
	public const int DefaultPlagueShieldPercent = 75;
	public const int MinPlagueScalingPercent = 0;
	public const int MaxPlagueScalingPercent = 300;
	public const int PlagueScalingPercentStep = 5;

	private const string ConfigFileName = "config.json";
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		WriteIndented = true
	};

	private static EndlessModeConfig _current = LoadOrCreate();

	[JsonPropertyName("grant_mimic_infestation")]
	public bool GrantMimicInfestation { get; set; } = true;

	[JsonPropertyName("grant_time_maze")]
	public bool GrantTimeMaze { get; set; } = true;

	[JsonPropertyName("grant_muzzle")]
	public bool GrantMuzzle { get; set; } = true;

	[JsonPropertyName("grant_horrible_trophy")]
	public bool GrantHorribleTrophy { get; set; } = true;

	[JsonPropertyName("plague_spear_percent")]
	public int PlagueSpearPercent { get; set; } = DefaultPlagueSpearPercent;

	[JsonPropertyName("plague_shield_percent")]
	public int PlagueShieldPercent { get; set; } = DefaultPlagueShieldPercent;

	internal static EndlessModeConfig Current => _current;

	internal static int CurrentPlagueSpearPercent => ClampPlagueScalingPercent(Current.PlagueSpearPercent);

	internal static int CurrentPlagueShieldPercent => ClampPlagueScalingPercent(Current.PlagueShieldPercent);

	internal static EndlessOptionalReward GetOptionalRewardForTier(int rewardTier)
	{
		return Math.Clamp(rewardTier, 1, 4) switch
		{
			1 => EndlessOptionalReward.MimicInfestation,
			2 => EndlessOptionalReward.TimeMaze,
			3 => EndlessOptionalReward.Muzzle,
			_ => EndlessOptionalReward.HorribleTrophy
		};
	}

	internal static bool IsRewardEnabled(EndlessOptionalReward reward)
	{
		return reward switch
		{
			EndlessOptionalReward.MimicInfestation => Current.GrantMimicInfestation,
			EndlessOptionalReward.TimeMaze => Current.GrantTimeMaze,
			EndlessOptionalReward.Muzzle => Current.GrantMuzzle,
			EndlessOptionalReward.HorribleTrophy => Current.GrantHorribleTrophy,
			_ => true
		};
	}

	internal static int GetEnabledRewardFlags()
	{
		int flags = 0;
		foreach (EndlessOptionalReward reward in Enum.GetValues<EndlessOptionalReward>())
		{
			if (IsRewardEnabled(reward))
			{
				flags |= GetRewardFlag(reward);
			}
		}

		return flags;
	}

	// 联机下同步不可用时的兜底值必须两端一致，只能用编译期默认（全部启用），
	// 绝不能读本地配置——主客本地配置不同会导致发放遗物分叉。
	internal static int GetDefaultEnabledRewardFlags()
	{
		int flags = 0;
		foreach (EndlessOptionalReward reward in Enum.GetValues<EndlessOptionalReward>())
		{
			flags |= GetRewardFlag(reward);
		}

		return flags;
	}

	internal static bool IsRewardEnabled(int enabledRewardFlags, EndlessOptionalReward reward)
	{
		return (enabledRewardFlags & GetRewardFlag(reward)) != 0;
	}

	internal static void SetRewardEnabled(EndlessOptionalReward reward, bool enabled)
	{
		switch (reward)
		{
			case EndlessOptionalReward.MimicInfestation:
				Current.GrantMimicInfestation = enabled;
				break;
			case EndlessOptionalReward.TimeMaze:
				Current.GrantTimeMaze = enabled;
				break;
			case EndlessOptionalReward.Muzzle:
				Current.GrantMuzzle = enabled;
				break;
			case EndlessOptionalReward.HorribleTrophy:
				Current.GrantHorribleTrophy = enabled;
				break;
		}

		SaveCurrent();
	}

	internal static void SetPlagueSpearPercent(int percent)
	{
		int clamped = ClampPlagueScalingPercent(percent);
		if (Current.PlagueSpearPercent == clamped)
		{
			return;
		}

		Current.PlagueSpearPercent = clamped;
		SaveCurrent();
	}

	internal static void SetPlagueShieldPercent(int percent)
	{
		int clamped = ClampPlagueScalingPercent(percent);
		if (Current.PlagueShieldPercent == clamped)
		{
			return;
		}

		Current.PlagueShieldPercent = clamped;
		SaveCurrent();
	}

	internal static int ClampPlagueScalingPercent(int percent)
	{
		return Math.Clamp(percent, MinPlagueScalingPercent, MaxPlagueScalingPercent);
	}

	private static int GetRewardFlag(EndlessOptionalReward reward)
	{
		return 1 << (int)reward;
	}

	internal static string GetConfigPath()
	{
		return Path.Combine(ProjectSettings.GlobalizePath("user://"), ModEntryConstants.ModId, ConfigFileName);
	}

	private static EndlessModeConfig LoadOrCreate()
	{
		string configPath = GetConfigPath();
		if (!File.Exists(configPath))
		{
			EndlessModeConfig config = new();
			WriteConfig(configPath, config);
			return config;
		}

		try
		{
			EndlessModeConfig? config = JsonSerializer.Deserialize<EndlessModeConfig>(File.ReadAllText(configPath), JsonOptions);
			if (config != null)
			{
				Normalize(config);
				WriteConfig(configPath, config);
				return config;
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModEntryConstants.ModId}] Failed to load config; rewriting defaults: {ex.Message}");
		}

		EndlessModeConfig defaultConfig = new();
		WriteConfig(configPath, defaultConfig);
		return defaultConfig;
	}

	private static void SaveCurrent()
	{
		Normalize(Current);
		WriteConfig(GetConfigPath(), Current);
	}

	private static void Normalize(EndlessModeConfig config)
	{
		config.PlagueSpearPercent = ClampPlagueScalingPercent(config.PlagueSpearPercent);
		config.PlagueShieldPercent = ClampPlagueScalingPercent(config.PlagueShieldPercent);
	}

	// 本类静态构造器路径上会走到这里；IO 异常若外泄会变成 TypeInitializationException
	// 毒化整个配置类型（之后每次访问都抛），所以只告警不抛。
	private static void WriteConfig(string configPath, EndlessModeConfig config)
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
			File.WriteAllText(configPath, JsonSerializer.Serialize(config, JsonOptions));
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModEntryConstants.ModId}] Failed to write config {configPath}: {ex.Message}");
		}
	}
}
