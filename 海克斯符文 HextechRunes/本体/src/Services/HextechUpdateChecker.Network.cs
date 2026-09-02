using System.Text.Json;

namespace HextechRunes;

internal static partial class HextechUpdateChecker
{
	private static async Task<UpdateCheckResult> CheckLatestVersionAsync()
	{
		List<string> failures = [];
		for (int attempt = 1; attempt <= MaxCheckAttempts; attempt++)
		{
			foreach (string endpoint in VersionEndpoints)
			{
				UpdateCheckResult? result = await TryCheckEndpointAsync(endpoint, failures).ConfigureAwait(false);
				if (result != null)
				{
					return CacheResult(result);
				}
			}

			if (attempt < MaxCheckAttempts)
			{
				await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
			}
		}

		Log.Warn($"[{ModInfo.Id}][Mayhem] Update check failed: {string.Join("; ", failures)}");
		return new UpdateCheckResult("海克斯大乱斗模组更新检查暂不可用", false);
	}

	private static async Task<UpdateCheckResult?> TryCheckEndpointAsync(string endpoint, List<string> failures)
	{
		try
		{
			using HttpResponseMessage response = await HttpClient.GetAsync(endpoint).ConfigureAwait(false);
			if (!response.IsSuccessStatusCode)
			{
				failures.Add($"{endpoint}: HTTP {(int)response.StatusCode}");
				return null;
			}

			string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
			using JsonDocument document = JsonDocument.Parse(json);
			JsonElement root = document.RootElement;
			string? latestVersion = TryReadLatestVersion(root);
			if (string.IsNullOrWhiteSpace(latestVersion))
			{
				failures.Add($"{endpoint}: missing latestVersion");
				return null;
			}

			return BuildVersionResult(latestVersion);
		}
		catch (Exception ex)
		{
			failures.Add($"{endpoint}: {ex.Message}");
			return null;
		}
	}

	private static UpdateCheckResult BuildVersionResult(string latestVersion)
	{
		string normalizedLatest = latestVersion.Trim();
		string currentVersion = ModInfo.Version;
		string text = CompareVersions(normalizedLatest, currentVersion) > 0
			? $"海克斯大乱斗模组有新版{normalizedLatest}，当前版本为{currentVersion}"
			: $"海克斯大乱斗模组为最新版{currentVersion}";
		HextechLog.Info($"[{ModInfo.Id}][Mayhem] Update check succeeded: latest={normalizedLatest}, current={currentVersion}");
		return new UpdateCheckResult(text, true);
	}

	private static UpdateCheckResult CacheResult(UpdateCheckResult result)
	{
		lock (StateLock)
		{
			_cachedResult = result;
			return result;
		}
	}

	private static string? TryReadLatestVersion(JsonElement root)
	{
		return TryGetString(root, "latestVersion")
			?? TryGetString(root, "version")
			?? TryGetString(root, "latest");
	}

	private static string? TryGetString(JsonElement element, string propertyName)
	{
		return element.ValueKind == JsonValueKind.Object
			&& element.TryGetProperty(propertyName, out JsonElement property)
			&& property.ValueKind == JsonValueKind.String
				? property.GetString()
				: null;
	}

	private static int CompareVersions(string left, string right)
	{
		int[] leftParts = ParseVersionParts(left);
		int[] rightParts = ParseVersionParts(right);
		int count = Math.Max(leftParts.Length, rightParts.Length);
		for (int i = 0; i < count; i++)
		{
			int leftPart = i < leftParts.Length ? leftParts[i] : 0;
			int rightPart = i < rightParts.Length ? rightParts[i] : 0;
			int comparison = leftPart.CompareTo(rightPart);
			if (comparison != 0)
			{
				return comparison;
			}
		}

		return 0;
	}

	private static int[] ParseVersionParts(string version)
	{
		string trimmed = version.Trim().TrimStart('v', 'V');
		List<int> parts = [];
		foreach (string segment in trimmed.Split(['.', '-', '+'], StringSplitOptions.RemoveEmptyEntries))
		{
			int value = 0;
			bool hasDigit = false;
			foreach (char c in segment)
			{
				if (!char.IsDigit(c))
				{
					break;
				}

				hasDigit = true;
				value = checked((value * 10) + (c - '0'));
			}

			if (!hasDigit)
			{
				break;
			}

			parts.Add(value);
		}

		return parts.Count == 0 ? [0] : parts.ToArray();
	}
}
