using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HextechRunes;

/// <summary>
/// 海克斯配置分享码：`HEXCFG1:` + base64(gzip(紧凑 JSON))。
/// 导出取当前配置快照（不含 ModEnabled 与 UI 偏好——导入别人的码不应关掉对方 mod 或改界面习惯）；
/// 导入只解析并生成快照与差异摘要，真正落盘由调用方确认后走 SaveSnapshot（内部全量 Normalize/Clamp，
/// 未知条目自动丢弃、数值自动收敛，脏码不会产生非法状态）。
/// </summary>
internal static class HextechConfigShareCodec
{
	private const string Prefix = "HEXCFG1:";
	private const int MaxEncodedLength = 64 * 1024;
	private const int MaxDecodedLength = 512 * 1024;

	internal sealed record ImportPreview(
		HextechRunConfigurationSnapshot Snapshot,
		int DisabledPlayerRuneCount,
		int DisabledMonsterHexCount,
		int DisabledForgeCount,
		int IgnoredUnknownCount);

	private sealed record SharePayload(
		[property: JsonPropertyName("v")] int Version,
		[property: JsonPropertyName("pc")] int[]? PlayerHexCountsByAct,
		[property: JsonPropertyName("ec")] int[]? EnemyHexCountsByAct,
		[property: JsonPropertyName("pr")] int PlayerRuneRerollLimit,
		[property: JsonPropertyName("mr")] int MonsterHexRerollLimit,
		[property: JsonPropertyName("dp")] string[]? DisabledPlayerRuneIds,
		[property: JsonPropertyName("dm")] string[]? DisabledMonsterHexIds,
		[property: JsonPropertyName("df")] string[]? DisabledForgeIds,
		[property: JsonPropertyName("wr")] int[]? RuneRarityWeights,
		[property: JsonPropertyName("wa")] int[][]? RuneRarityWeightsByAct,
		[property: JsonPropertyName("ns")] bool? PreventConsecutiveSilverRunes,
		[property: JsonPropertyName("gr")] int? GoldenRerollChancePercent,
		[property: JsonPropertyName("w1")] int[]? FirstActRuneRarityWeights,
		[property: JsonPropertyName("wn")] int[]? NormalRuneRarityWeights,
		[property: JsonPropertyName("w2")] int[]? SecondActAfterSilverRuneRarityWeights,
		[property: JsonPropertyName("wf")] int[]? ForgeRarityWeights,
		[property: JsonPropertyName("fp")] int RandomForgeShopPrice,
		[property: JsonPropertyName("fg")] bool RandomForgeDirectGrant);

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
	};

	public static string ExportCurrent()
	{
		return Export(HextechRuneConfiguration.GetSnapshot());
	}

	public static string Export(HextechRunConfigurationSnapshot snapshot)
	{
		SharePayload payload = new(
			Version: 4,
			PlayerHexCountsByAct: snapshot.PlayerHexCountsByAct.ToArray(),
			EnemyHexCountsByAct: snapshot.EnemyHexCountsByAct.ToArray(),
			PlayerRuneRerollLimit: snapshot.PlayerRuneRerollLimit,
			MonsterHexRerollLimit: snapshot.MonsterHexRerollLimit,
			DisabledPlayerRuneIds: snapshot.DisabledPlayerRuneIds.OrderBy(static id => id, StringComparer.Ordinal).ToArray(),
			DisabledMonsterHexIds: snapshot.DisabledMonsterHexIds.OrderBy(static id => id, StringComparer.Ordinal).ToArray(),
			DisabledForgeIds: snapshot.DisabledForgeIds.OrderBy(static id => id, StringComparer.Ordinal).ToArray(),
			RuneRarityWeights: null,
			RuneRarityWeightsByAct: snapshot.RuneRarityWeightsByAct.Select(ToArray).ToArray(),
			PreventConsecutiveSilverRunes: snapshot.PreventConsecutiveSilverRunes,
			GoldenRerollChancePercent: snapshot.GoldenRerollChancePercent,
			FirstActRuneRarityWeights: null,
			NormalRuneRarityWeights: null,
			SecondActAfterSilverRuneRarityWeights: null,
			ForgeRarityWeights: [snapshot.ForgeRarityWeights.Silver, snapshot.ForgeRarityWeights.Gold, snapshot.ForgeRarityWeights.Prismatic],
			RandomForgeShopPrice: snapshot.RandomForgeShopPrice,
			RandomForgeDirectGrant: snapshot.RandomForgeDirectGrant);

		byte[] json = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
		using MemoryStream output = new();
		using (GZipStream gzip = new(output, CompressionLevel.Optimal, leaveOpen: true))
		{
			gzip.Write(json, 0, json.Length);
		}

		return Prefix + Convert.ToBase64String(output.ToArray());
	}

	/// <summary>解析分享码并生成导入预览。失败返回 null（格式错/超限/解压失败等一律视为无效码）。</summary>
	public static ImportPreview? TryParse(string? code)
	{
		return TryParse(code, currentSnapshot: null);
	}

	internal static ImportPreview? TryParseForTests(string? code, HextechRunConfigurationSnapshot currentSnapshot)
	{
		return TryParse(code, currentSnapshot);
	}

	private static ImportPreview? TryParse(string? code, HextechRunConfigurationSnapshot? currentSnapshot)
	{
		try
		{
			string trimmed = code?.Trim() ?? string.Empty;
			if (!trimmed.StartsWith(Prefix, StringComparison.Ordinal) || trimmed.Length > MaxEncodedLength)
			{
				return null;
			}

			byte[] compressed = Convert.FromBase64String(trimmed[Prefix.Length..]);
			using MemoryStream input = new(compressed);
			using GZipStream gzip = new(input, CompressionMode.Decompress);
			using MemoryStream output = new();
			CopyBounded(gzip, output, MaxDecodedLength);
			SharePayload? payload = JsonSerializer.Deserialize<SharePayload>(output.ToArray(), JsonOptions);
			if (payload == null || payload.Version is not (1 or 2 or 3 or 4))
			{
				return null;
			}

			return BuildPreview(payload, currentSnapshot);
		}
		catch
		{
			return null;
		}
	}

	private static ImportPreview BuildPreview(SharePayload payload, HextechRunConfigurationSnapshot? currentSnapshot)
	{
		HextechRunConfigurationSnapshot current = currentSnapshot ?? HextechRuneConfiguration.GetSnapshot();
		int rawDisabledCount = (payload.DisabledPlayerRuneIds?.Length ?? 0)
			+ (payload.DisabledMonsterHexIds?.Length ?? 0)
			+ (payload.DisabledForgeIds?.Length ?? 0);

		HashSet<string> disabledPlayerRuneIds = HextechRuneConfiguration.NormalizeDisabledPlayerRuneIds(payload.DisabledPlayerRuneIds);
		HashSet<string> disabledMonsterHexIds = HextechRuneConfiguration.NormalizeDisabledMonsterHexIds(payload.DisabledMonsterHexIds);
		HashSet<string> disabledForgeIds = HextechRuneConfiguration.NormalizeDisabledForgeIds(payload.DisabledForgeIds);

		HextechRarityWeights legacyRuneRarityWeights = payload.Version >= 2
			? ToRarityWeights(payload.RuneRarityWeights, HextechRuneConfiguration.GetDefaultRuneRarityWeights())
			: ToRarityWeights(payload.NormalRuneRarityWeights, HextechRuneConfiguration.GetDefaultRuneRarityWeights());
		HextechRarityWeights[] runeRarityWeightsByAct = payload.Version >= 4
			? ToRarityWeightsByAct(payload.RuneRarityWeightsByAct, HextechRuneConfiguration.GetDefaultRuneRarityWeightsByAct())
			: [ legacyRuneRarityWeights, legacyRuneRarityWeights, legacyRuneRarityWeights ];
		bool preventConsecutiveSilverRunes = payload.Version >= 2
			? payload.PreventConsecutiveSilverRunes ?? HextechRuneConfiguration.GetDefaultPreventConsecutiveSilverRunes()
			: HextechRuneConfiguration.GetDefaultPreventConsecutiveSilverRunes();
		int goldenRerollChancePercent = payload.Version >= 3
			? HextechRuneConfiguration.ClampGoldenRerollChancePercent(
				payload.GoldenRerollChancePercent ?? HextechRuneConfiguration.GetDefaultGoldenRerollChancePercent())
			: HextechRuneConfiguration.GetDefaultGoldenRerollChancePercent();

		HextechRunConfigurationSnapshot snapshot = new(
			PlayerHexCountsByAct: NormalizeCounts(payload.PlayerHexCountsByAct, HextechRuneConfiguration.GetDefaultPlayerHexCountsByAct()),
			EnemyHexCountsByAct: NormalizeCounts(payload.EnemyHexCountsByAct, HextechRuneConfiguration.GetDefaultEnemyHexCountsByAct()),
			PlayerRuneRerollLimit: HextechRuneConfiguration.ClampRerollLimit(payload.PlayerRuneRerollLimit),
			MonsterHexRerollLimit: HextechRuneConfiguration.ClampRerollLimit(payload.MonsterHexRerollLimit),
			DisabledPlayerRuneIds: disabledPlayerRuneIds,
			DisabledMonsterHexIds: disabledMonsterHexIds,
			DisabledForgeIds: disabledForgeIds,
			RuneRarityWeightsByAct: runeRarityWeightsByAct,
			PreventConsecutiveSilverRunes: preventConsecutiveSilverRunes,
			GoldenRerollChancePercent: goldenRerollChancePercent,
			ForgeRarityWeights: ToForgeRarityWeights(payload.ForgeRarityWeights, HextechRuneConfiguration.GetDefaultForgeRarityWeights()),
			RandomForgeShopPrice: HextechRuneConfiguration.ClampRandomForgeShopPrice(payload.RandomForgeShopPrice),
			RandomForgeDirectGrant: payload.RandomForgeDirectGrant,
			ModEnabled: current.ModEnabled);

		int normalizedDisabledCount = disabledPlayerRuneIds.Count + disabledMonsterHexIds.Count + disabledForgeIds.Count;
		return new ImportPreview(
			snapshot,
			disabledPlayerRuneIds.Count,
			disabledMonsterHexIds.Count,
			disabledForgeIds.Count,
			Math.Max(0, rawDisabledCount - normalizedDisabledCount));
	}

	/// <summary>确认后应用（全量覆盖当前配置；SaveSnapshot 内部再做一次 Normalize/Clamp）。</summary>
	public static void Apply(ImportPreview preview)
	{
		HextechRuneConfiguration.SaveSnapshot(preview.Snapshot);
	}

	private static void CopyBounded(Stream source, MemoryStream destination, int maxBytes)
	{
		byte[] buffer = new byte[8192];
		int total = 0;
		int read;
		while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
		{
			total += read;
			if (total > maxBytes)
			{
				throw new InvalidDataException("Share code payload too large.");
			}

			destination.Write(buffer, 0, read);
		}
	}

	private static int[] ToArray(HextechRarityWeights weights)
	{
		return [weights.Silver, weights.Gold, weights.Prismatic];
	}

	private static int[] NormalizeCounts(int[]? values, int[] defaults)
	{
		int[] result = defaults.ToArray();
		if (values == null)
		{
			return result;
		}

		for (int i = 0; i < result.Length && i < values.Length; i++)
		{
			result[i] = HextechRuneConfiguration.ClampActHexCount(values[i]);
		}

		return result;
	}

	private static HextechRarityWeights ClampWeights(HextechRarityWeights weights)
	{
		return new HextechRarityWeights(
			HextechRuneConfiguration.ClampRarityWeight(weights.Silver),
			HextechRuneConfiguration.ClampRarityWeight(weights.Gold),
			HextechRuneConfiguration.ClampRarityWeight(weights.Prismatic));
	}

	private static HextechRarityWeights ToRarityWeights(int[]? values, HextechRarityWeights fallback)
	{
		return values is { Length: 3 }
			? ClampWeights(new HextechRarityWeights(values[0], values[1], values[2]))
			: fallback;
	}

	private static HextechRarityWeights[] ToRarityWeightsByAct(
		IReadOnlyList<int[]>? valuesByAct,
		IReadOnlyList<HextechRarityWeights> fallbackByAct)
	{
		return Enumerable.Range(0, 3)
			.Select(actIndex => valuesByAct != null && actIndex < valuesByAct.Count
				? ToRarityWeights(valuesByAct[actIndex], fallbackByAct[Math.Min(actIndex, fallbackByAct.Count - 1)])
				: fallbackByAct[Math.Min(actIndex, fallbackByAct.Count - 1)])
			.ToArray();
	}

	private static HextechForgeRarityWeights ToForgeRarityWeights(int[]? values, HextechForgeRarityWeights fallback)
	{
		return values is { Length: 3 } ? new HextechForgeRarityWeights(values[0], values[1], values[2]) : fallback;
	}
}
