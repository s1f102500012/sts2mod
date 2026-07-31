using System.Text.Json;

namespace HextechRunes;

internal sealed partial class HextechMayhemModifier
{
	public int[] PlayerHexCountsByAct => _runContext.PlayerHexCounts.Snapshot;

	internal IReadOnlySet<string> DisabledMonsterHexIdsForPool => GetEffectiveRunConfigurationSnapshot().DisabledMonsterHexIds;

	internal IReadOnlySet<string> DisabledForgeIdsForPool => GetEffectiveRunConfigurationSnapshot().DisabledForgeIds;

	internal HextechRarityWeights FirstActRuneRarityWeights => GetEffectiveRunConfigurationSnapshot().FirstActRuneRarityWeights;

	internal HextechRarityWeights NormalRuneRarityWeights => GetEffectiveRunConfigurationSnapshot().NormalRuneRarityWeights;

	internal HextechRarityWeights SecondActAfterSilverRuneRarityWeights => GetEffectiveRunConfigurationSnapshot().SecondActAfterSilverRuneRarityWeights;

	internal HextechForgeRarityWeights ForgeRarityWeights => GetEffectiveRunConfigurationSnapshot().ForgeRarityWeights;

	internal int RandomForgeShopPrice => GetEffectiveRunConfigurationSnapshot().RandomForgeShopPrice;

	internal bool RandomForgeDirectGrant => GetEffectiveRunConfigurationSnapshot().RandomForgeDirectGrant;

	// 模组总开关:本局逐 act 的「有效配置值」(联机里来自房主的同步快照)。
	internal bool ModEnabled => GetEffectiveRunConfigurationSnapshot().ModEnabled;

	// 本局是否激活模组玩法。开局(act1)首次进 HandleActSelection 时冻结一次,
	// 之后不随 act 推进或局内改配置而变,保证「按下一局开始时应用」「无半残态」。未冻结时默认开启。
	internal bool IsModActiveForRun => _runContext.ModActiveForRun ?? true;

	// 在 act1 的 act-roll 之后调用:首次把有效(房主)值冻结进 run context。返回 true = 本局禁用模组。
	internal bool FreezeModActiveForRunAndCheckDisabled()
	{
		if (_runContext.ModActiveForRun == null)
		{
			_runContext.ModActiveForRun = ModEnabled;
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] Mod-active frozen for run: active={_runContext.ModActiveForRun}");
		}

		return _runContext.ModActiveForRun == false;
	}

	internal int PlayerRuneRerollLimit => GetEffectiveRunConfigurationSnapshot().PlayerRuneRerollLimit;

	internal int MonsterHexRerollLimit => GetEffectiveRunConfigurationSnapshot().MonsterHexRerollLimit;

	internal int GetPlayerHexCountForAct(int actIndex)
	{
		return _runContext.PlayerHexCounts.GetForAct(actIndex, IsEndlessLoopActive);
	}

	internal HextechRunConfigurationSnapshot GetEffectiveRunConfigurationSnapshot()
	{
		HextechRunConfigurationSnapshot? snapshot = _runContext.RunConfigurationSnapshot;
		if (snapshot != null)
		{
			return HextechRuneConfiguration.NormalizeSnapshot(snapshot with
			{
				PlayerHexCountsByAct = _runContext.PlayerHexCounts.Snapshot,
				EnemyHexCountsByAct = _runContext.EnemyHexCounts.Snapshot,
				DisabledPlayerRuneIds = PlayerRuneConfigDisabledIds.ToHashSet(StringComparer.Ordinal)
			});
		}

		HextechRunConfigurationSnapshot local = HextechPlayerContextHelper.IsClientRun()
			? HextechRuneConfiguration.GetDefaultSnapshot()
			: HextechRuneConfiguration.GetSnapshot();
		return HextechRuneConfiguration.NormalizeSnapshot(local with
		{
			PlayerHexCountsByAct = _runContext.PlayerHexCounts.Snapshot,
			EnemyHexCountsByAct = _runContext.EnemyHexCounts.Snapshot,
			DisabledPlayerRuneIds = PlayerRuneConfigDisabledIds.ToHashSet(StringComparer.Ordinal)
		});
	}

	internal void InitializeRunConfigurationSnapshotForNewRun(string reason)
	{
		SetRunConfigurationSnapshot(CreateNewRunConfigurationSnapshot(), reason);
	}

	internal void SetRunConfigurationSnapshot(HextechRunConfigurationSnapshot snapshot, string reason)
	{
		HextechRunConfigurationSnapshot normalized = HextechRuneConfiguration.NormalizeSnapshot(snapshot);
		_runContext.RunConfigurationSnapshot = normalized.Copy();
		_runContext.PlayerHexCounts.Set(normalized.PlayerHexCountsByAct);
		_runContext.EnemyHexCounts.Set(normalized.EnemyHexCountsByAct);
		_runContext.PlayerRuneConfig.Set(normalized.DisabledPlayerRuneIds);
		HextechLog.Info($"[{ModInfo.Id}][Mayhem] Run config snapshot set: reason={reason} playerCounts={string.Join(",", PlayerHexCountsByAct)} enemyCounts={string.Join(",", EnemyHexCountsByAct)} playerRerolls={normalized.PlayerRuneRerollLimit} monsterRerolls={normalized.MonsterHexRerollLimit} playerDisabled={PlayerRuneConfigDisabledIds.Count} enemyDisabled={normalized.DisabledMonsterHexIds.Count} forgeDisabled={normalized.DisabledForgeIds.Count} forgePrice={normalized.RandomForgeShopPrice} forgeDirect={normalized.RandomForgeDirectGrant}");
	}

	private static HextechRunConfigurationSnapshot CreateNewRunConfigurationSnapshot()
	{
		return HextechPlayerContextHelper.IsClientRun(fallbackWhenUnavailable: true)
			? HextechRuneConfiguration.GetDefaultSnapshot()
			: HextechRuneConfiguration.GetSnapshot();
	}

	private string SerializeRunConfigurationSnapshot()
	{
		HextechRunConfigurationSnapshot? snapshot = _runContext.RunConfigurationSnapshot;
		if (snapshot == null)
		{
			return "";
		}

		// 该 JSON 进 SavedProperty 参与双端比对，集合字段用排序数组序列化，
		// 不依赖 HashSet 未文档化的插入序枚举；属性名与原 record 一致，Restore 仍反序列化回原类型。
		HextechRunConfigurationSnapshot normalized = HextechRuneConfiguration.NormalizeSnapshot(snapshot);
		return JsonSerializer.Serialize(new RunConfigurationSnapshotJson(normalized), HextechTelemetry.JsonOptions);
	}

	private sealed record RunConfigurationSnapshotJson(
		int[] PlayerHexCountsByAct,
		int[] EnemyHexCountsByAct,
		int PlayerRuneRerollLimit,
		int MonsterHexRerollLimit,
		string[] DisabledPlayerRuneIds,
		string[] DisabledMonsterHexIds,
		string[] DisabledForgeIds,
		HextechRarityWeights FirstActRuneRarityWeights,
		HextechRarityWeights NormalRuneRarityWeights,
		HextechRarityWeights SecondActAfterSilverRuneRarityWeights,
		HextechForgeRarityWeights ForgeRarityWeights,
		int RandomForgeShopPrice,
		bool RandomForgeDirectGrant,
		bool ModEnabled)
	{
		public RunConfigurationSnapshotJson(HextechRunConfigurationSnapshot snapshot)
			: this(
				snapshot.PlayerHexCountsByAct,
				snapshot.EnemyHexCountsByAct,
				snapshot.PlayerRuneRerollLimit,
				snapshot.MonsterHexRerollLimit,
				OrderedIds(snapshot.DisabledPlayerRuneIds),
				OrderedIds(snapshot.DisabledMonsterHexIds),
				OrderedIds(snapshot.DisabledForgeIds),
				snapshot.FirstActRuneRarityWeights,
				snapshot.NormalRuneRarityWeights,
				snapshot.SecondActAfterSilverRuneRarityWeights,
				snapshot.ForgeRarityWeights,
				snapshot.RandomForgeShopPrice,
				snapshot.RandomForgeDirectGrant,
				snapshot.ModEnabled)
		{
		}

		private static string[] OrderedIds(IEnumerable<string> ids)
		{
			return ids.OrderBy(static id => id, StringComparer.Ordinal).ToArray();
		}
	}

	private void RestoreRunConfigurationSnapshot(string json)
	{
		if (string.IsNullOrWhiteSpace(json))
		{
			_runContext.RunConfigurationSnapshot = null;
			return;
		}

		try
		{
			HextechRunConfigurationSnapshot? snapshot = JsonSerializer.Deserialize<HextechRunConfigurationSnapshot>(json, HextechTelemetry.JsonOptions);
			if (snapshot != null)
			{
				SetRunConfigurationSnapshot(snapshot, "restore saved run config");
			}
		}
		catch (Exception ex)
		{
			_runContext.RunConfigurationSnapshot = null;
			Log.Warn($"[{ModInfo.Id}][Mayhem] Run config snapshot restore failed; using runtime fallback: {ex.Message}", 2);
		}
	}
}
