using System.Text.Json;
using System.Security.Cryptography;

namespace HextechRunes;

internal static class HextechIntegrityCheck
{
	public const string OfficialServerIdentity = "Natsuki.HextechRunes.official";
	private static bool _loggedOfficialServer;
	private static bool _loggedOfficialBuildCheck;

	private sealed record ArtifactHashes(string? DllSha256, string? PckSha256, string? ManifestSha256)
	{
		public bool HasAny => !string.IsNullOrWhiteSpace(DllSha256)
			|| !string.IsNullOrWhiteSpace(PckSha256)
			|| !string.IsNullOrWhiteSpace(ManifestSha256);
	}

	public static bool IsOfficialServerResponse(string endpoint, JsonElement root)
	{
		return HextechServerEndpoints.IsOfficialEndpoint(endpoint)
			&& GetString(root, "serverIdentity") == OfficialServerIdentity
			&& GetString(root, "modId") == ModInfo.Id;
	}

	public static void LogOfficialServerConnection(string endpoint, string latestVersion)
	{
		if (_loggedOfficialServer)
		{
			return;
		}

		_loggedOfficialServer = true;
		HextechLog.Info($"[{ModInfo.Id}][Integrity] Connected to Natsuki official HextechRunes server: endpoint={endpoint} latest={latestVersion} current={ModInfo.Version}");
	}

	public static void VerifyOfficialBuild(JsonElement root)
	{
		if (_loggedOfficialBuildCheck)
		{
			return;
		}

		_loggedOfficialBuildCheck = true;
		if (!TryReadExpectedBuildHashes(root, out ArtifactHashes expected, out string reason))
		{
			HextechLog.Info($"[{ModInfo.Id}][Integrity] Official build fingerprint skipped: version={ModInfo.Version} target={ModInfo.TargetGameVersion} reason={reason}");
			return;
		}

		if (!TryComputeLocalBuildHashes(out ArtifactHashes actual, out string error))
		{
			Log.Warn($"[{ModInfo.Id}][Integrity] Official build fingerprint failed: version={ModInfo.Version} target={ModInfo.TargetGameVersion} reason={error}");
			return;
		}

		List<string> mismatches = [];
		AddHashMismatch(mismatches, "dll", expected.DllSha256, actual.DllSha256);
		AddHashMismatch(mismatches, "pck", expected.PckSha256, actual.PckSha256);
		AddHashMismatch(mismatches, "manifest", expected.ManifestSha256, actual.ManifestSha256);
		if (mismatches.Count > 0)
		{
			Log.Warn($"[{ModInfo.Id}][Integrity] Official build fingerprint mismatch: version={ModInfo.Version} target={ModInfo.TargetGameVersion} {string.Join("; ", mismatches)}");
			return;
		}

		HextechLog.Info($"[{ModInfo.Id}][Integrity] Official build fingerprint OK: version={ModInfo.Version} target={ModInfo.TargetGameVersion} dll={ShortHash(actual.DllSha256)} pck={ShortHash(actual.PckSha256)} manifest={ShortHash(actual.ManifestSha256)}");
	}

	private static bool TryReadExpectedBuildHashes(JsonElement root, out ArtifactHashes hashes, out string reason)
	{
		hashes = new ArtifactHashes(null, null, null);
		reason = "missing officialBuilds";
		if (!root.TryGetProperty("officialBuilds", out JsonElement builds))
		{
			return false;
		}

		if (TryReadExpectedBuildHashesFromArray(builds, out hashes))
		{
			reason = string.Empty;
			return true;
		}

		if (TryReadExpectedBuildHashesFromMap(builds, out hashes))
		{
			reason = string.Empty;
			return true;
		}

		reason = $"no fingerprint for version={ModInfo.Version} target={ModInfo.TargetGameVersion}";
		return false;
	}

	private static bool TryReadExpectedBuildHashesFromArray(JsonElement builds, out ArtifactHashes hashes)
	{
		hashes = new ArtifactHashes(null, null, null);
		if (builds.ValueKind != JsonValueKind.Array)
		{
			return false;
		}

		foreach (JsonElement build in builds.EnumerateArray())
		{
			if (GetString(build, "modVersion") != ModInfo.Version
				|| GetString(build, "gameVersion") != ModInfo.TargetGameVersion)
			{
				continue;
			}

			hashes = ReadBuildHashes(build);
			return hashes.HasAny;
		}

		return false;
	}

	private static bool TryReadExpectedBuildHashesFromMap(JsonElement builds, out ArtifactHashes hashes)
	{
		hashes = new ArtifactHashes(null, null, null);
		if (builds.ValueKind != JsonValueKind.Object
			|| !builds.TryGetProperty(ModInfo.Version, out JsonElement versionBuilds)
			|| versionBuilds.ValueKind != JsonValueKind.Object
			|| !versionBuilds.TryGetProperty(ModInfo.TargetGameVersion, out JsonElement build))
		{
			return false;
		}

		hashes = ReadBuildHashes(build);
		return hashes.HasAny;
	}

	private static ArtifactHashes ReadBuildHashes(JsonElement build)
	{
		return new ArtifactHashes(
			GetString(build, "dllSha256") ?? GetNestedString(build, "artifacts", "dllSha256") ?? GetNestedString(build, "artifacts", "dll"),
			GetString(build, "pckSha256") ?? GetNestedString(build, "artifacts", "pckSha256") ?? GetNestedString(build, "artifacts", "pck"),
			GetString(build, "manifestSha256") ?? GetNestedString(build, "artifacts", "manifestSha256") ?? GetNestedString(build, "artifacts", "manifest"));
	}

	private static bool TryComputeLocalBuildHashes(out ArtifactHashes hashes, out string error)
	{
		hashes = new ArtifactHashes(null, null, null);
		error = string.Empty;
		string? dllPath = Assembly.GetExecutingAssembly().Location;
		if (string.IsNullOrWhiteSpace(dllPath) || !File.Exists(dllPath))
		{
			error = "loaded DLL path not found";
			return false;
		}

		if (!TryFindBundleRoot(dllPath, out string modDir))
		{
			error = "bundle root containing PCK and manifest not found";
			return false;
		}

		string pckPath = Path.Combine(modDir, $"{ModInfo.Id}.pck");
		string manifestPath = Path.Combine(modDir, $"{ModInfo.Id}.json");
		if (!File.Exists(pckPath))
		{
			error = $"PCK not found at {pckPath}";
			return false;
		}

		if (!File.Exists(manifestPath))
		{
			error = $"manifest not found at {manifestPath}";
			return false;
		}

		hashes = new ArtifactHashes(
			ComputeSha256(dllPath),
			ComputeSha256(pckPath),
			ComputeSha256(manifestPath));
		return true;
	}

	private static bool TryFindBundleRoot(string dllPath, out string modDir)
	{
		modDir = string.Empty;
		DirectoryInfo? directory = new FileInfo(dllPath).Directory;
		for (int depth = 0; directory != null && depth <= 3; depth++, directory = directory.Parent)
		{
			string pckPath = Path.Combine(directory.FullName, $"{ModInfo.Id}.pck");
			string manifestPath = Path.Combine(directory.FullName, $"{ModInfo.Id}.json");
			if (File.Exists(pckPath) && File.Exists(manifestPath))
			{
				modDir = directory.FullName;
				return true;
			}
		}

		return false;
	}

	private static string ComputeSha256(string path)
	{
		using FileStream stream = File.OpenRead(path);
		return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
	}

	private static void AddHashMismatch(List<string> mismatches, string name, string? expected, string? actual)
	{
		if (string.IsNullOrWhiteSpace(expected))
		{
			return;
		}

		if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
		{
			mismatches.Add($"{name}=expected:{ShortHash(expected)} actual:{ShortHash(actual)}");
		}
	}

	private static string ShortHash(string? hash)
	{
		if (string.IsNullOrWhiteSpace(hash))
		{
			return "<missing>";
		}

		string trimmed = hash.Trim();
		return trimmed.Length <= 12 ? trimmed : trimmed[..12];
	}

	private static string? GetString(JsonElement root, string propertyName)
	{
		return root.ValueKind == JsonValueKind.Object
			&& root.TryGetProperty(propertyName, out JsonElement property)
			&& property.ValueKind == JsonValueKind.String
			? property.GetString()
			: null;
	}

	private static string? GetNestedString(JsonElement root, string objectName, string propertyName)
	{
		return root.ValueKind == JsonValueKind.Object && root.TryGetProperty(objectName, out JsonElement nested)
			? GetString(nested, propertyName)
			: null;
	}
}
