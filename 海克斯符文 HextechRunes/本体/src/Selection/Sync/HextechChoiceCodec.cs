using MegaCrit.Sts2.Core.GameActions;
using System.Text;

namespace HextechRunes;

internal readonly record struct EnemyHexAdjustmentPayload(
	int ActIndex,
	int Sequence,
	IReadOnlyList<MonsterHexKind?> MonsterHexes,
	IReadOnlyList<int> RerollCounts,
	bool IsFinal);

internal static class HextechChoiceCodec
{
	private const int Magic = 0x48585452; // HXTR
	private const int ChoiceKindActRoll = 1;
	private const int ChoiceKindRuneSelection = 2;
	private const int ChoiceKindActSelectionApplied = 3;
	private const int ChoiceKindEnemyHexAdjustment = 4;
	private const int ChoiceKindForgeSelection = 5;
	private const int ChoiceKindRandomRuneGrant = 6;
	private const int ChoiceKindRelicOptionSelection = 7;
	private const int EnemyHexAdjustmentListVersion = -2;
	private const int PlayerRuneConfigBitsetVersion = -4;
	private const int LegacyRunConfigurationSnapshotVersion = -5;
	private const int LegacyRerollRunConfigurationSnapshotVersion = -6;
	private const int PreviousRunConfigurationSnapshotVersion = -7;
	private const int PreviousSingleRarityRunConfigurationSnapshotVersion = -8;
	private const int RunConfigurationSnapshotVersion = -9;
	private const int PlayerRuneConfigBitsPerWord = 30;
	private const int MaxPlayerRuneConfigBitsetWords = 64;
	private const int MaxDisabledMonsterHexes = 128;
	private const int MaxChoiceListCount = HextechStableModelIdListCodec.MaxCount;
	private const uint Fnv1aOffsetBasis = 2166136261U;
	private const uint Fnv1aPrime = 16777619U;

	public static int ComputeOperationToken(
		string operationKind,
		uint choiceId,
		ulong playerNetId,
		string context)
	{
		ArgumentNullException.ThrowIfNull(operationKind);
		ArgumentNullException.ThrowIfNull(context);

		uint hash = Fnv1aOffsetBasis;
		AppendFnvString(ref hash, operationKind);
		AppendFnvUInt32(ref hash, choiceId);
		AppendFnvUInt64(ref hash, playerNetId);
		AppendFnvString(ref hash, context);
		return unchecked((int)hash);
	}

	public static PlayerChoiceResult CreateActRoll(
		int actIndex,
		HextechRarityTier rarity,
		MonsterHexKind? monsterHex,
		bool hostUsesBetterMultiplayerScaling,
		IReadOnlyList<int> enemyHexCountsByAct,
		IReadOnlySet<string> disabledPlayerRuneIds,
		HextechRunConfigurationSnapshot runConfigurationSnapshot)
	{
		HextechRunConfigurationSnapshot normalizedSnapshot = HextechRuneConfiguration.NormalizeSnapshot(runConfigurationSnapshot with
		{
			EnemyHexCountsByAct = enemyHexCountsByAct.ToArray(),
			DisabledPlayerRuneIds = disabledPlayerRuneIds.ToHashSet(StringComparer.Ordinal)
		});
		List<int> payload =
		[
			Magic,
			ChoiceKindActRoll,
			actIndex,
			(int)rarity,
			monsterHex.HasValue ? (int)monsterHex.Value : -1,
			hostUsesBetterMultiplayerScaling ? 1 : 0
		];
		int[] normalizedCounts = HextechEnemyHexCountState.Normalize(enemyHexCountsByAct);
		payload.AddRange(normalizedCounts);
		AppendDisabledPlayerRuneConfig(payload, disabledPlayerRuneIds);
		AppendRunConfigurationSnapshot(payload, normalizedSnapshot);
		return PlayerChoiceResult.FromIndexes(payload);
	}

	public static bool TryDecodeActRoll(
		PlayerChoiceResult result,
		int expectedActIndex,
		out HextechRarityTier rarity,
		out MonsterHexKind? monsterHex,
		out bool hostUsesBetterMultiplayerScaling,
		out int[] enemyHexCountsByAct,
		out HashSet<string> disabledPlayerRuneIds)
	{
		return TryDecodeActRoll(
			result,
			expectedActIndex,
			out rarity,
			out monsterHex,
			out hostUsesBetterMultiplayerScaling,
			out enemyHexCountsByAct,
			out disabledPlayerRuneIds,
			out _);
	}

	public static bool TryDecodeActRoll(
		PlayerChoiceResult result,
		int expectedActIndex,
		out HextechRarityTier rarity,
		out MonsterHexKind? monsterHex,
		out bool hostUsesBetterMultiplayerScaling,
		out int[] enemyHexCountsByAct,
		out HashSet<string> disabledPlayerRuneIds,
		out HextechRunConfigurationSnapshot runConfigurationSnapshot)
	{
		rarity = default;
		monsterHex = null;
		hostUsesBetterMultiplayerScaling = false;
		enemyHexCountsByAct = HextechRuneConfiguration.GetDefaultEnemyHexCountsByAct();
		disabledPlayerRuneIds = [];
		runConfigurationSnapshot = HextechRuneConfiguration.GetDefaultSnapshot();
		if (!TryGetIndexPayload(result, out List<int> payload)
			|| payload.Count < 5
			|| payload[0] != Magic
			|| payload[1] != ChoiceKindActRoll
			|| payload[2] != expectedActIndex)
		{
			return false;
		}

		if (!Enum.IsDefined(typeof(HextechRarityTier), payload[3]))
		{
			return false;
		}

		if (payload[4] >= 0)
		{
			if (!Enum.IsDefined(typeof(MonsterHexKind), payload[4]))
			{
				return false;
			}

			monsterHex = (MonsterHexKind)payload[4];
		}

		rarity = (HextechRarityTier)payload[3];
		hostUsesBetterMultiplayerScaling = payload.Count >= 6 && payload[5] != 0;
		if (payload.Count >= 9)
		{
			enemyHexCountsByAct = HextechEnemyHexCountState.Normalize(payload.Skip(6).Take(3).ToArray());
			if (!TryDecodeDisabledPlayerRuneConfig(payload, 9, out disabledPlayerRuneIds, out int nextCursor))
			{
				return false;
			}

			runConfigurationSnapshot = HextechRuneConfiguration.NormalizeSnapshot(runConfigurationSnapshot with
			{
				EnemyHexCountsByAct = enemyHexCountsByAct,
				DisabledPlayerRuneIds = disabledPlayerRuneIds
			});
			return TryDecodeRunConfigurationSnapshot(payload, nextCursor, runConfigurationSnapshot, out runConfigurationSnapshot);
		}

		return true;
	}

	private static void AppendDisabledPlayerRuneConfig(List<int> payload, IReadOnlySet<string> disabledPlayerRuneIds)
	{
		IReadOnlyList<ModelId> ids = PlayerRuneIdsByOrdinal.Value;
		int wordCount = ids.Count / PlayerRuneConfigBitsPerWord
			+ (ids.Count % PlayerRuneConfigBitsPerWord == 0 ? 0 : 1);
		ValidateProtocolCount(wordCount, MaxPlayerRuneConfigBitsetWords, nameof(disabledPlayerRuneIds));
		int[] words = new int[wordCount];
		for (int i = 0; i < ids.Count; i++)
		{
			if (!disabledPlayerRuneIds.Contains(ids[i].Entry))
			{
				continue;
			}

			words[i / PlayerRuneConfigBitsPerWord] |= 1 << (i % PlayerRuneConfigBitsPerWord);
		}

		payload.Add(PlayerRuneConfigBitsetVersion);
		payload.Add(wordCount);
		payload.AddRange(words);
	}

	private static bool TryDecodeDisabledPlayerRuneConfig(List<int> payload, int cursor, out HashSet<string> disabledPlayerRuneIds, out int nextCursor)
	{
		disabledPlayerRuneIds = [];
		nextCursor = cursor;
		if (payload.Count <= cursor)
		{
			return true;
		}

		if (payload[cursor] != PlayerRuneConfigBitsetVersion)
		{
			return true;
		}

		cursor++;
		if (payload.Count <= cursor)
		{
			return false;
		}

		int wordCount = payload[cursor++];
		if (wordCount < 0
			|| wordCount > MaxPlayerRuneConfigBitsetWords
			|| !HasRemaining(payload, cursor, wordCount))
		{
			return false;
		}

		IReadOnlyList<ModelId> ids = PlayerRuneIdsByOrdinal.Value;
		for (int i = 0; i < ids.Count; i++)
		{
			int wordIndex = i / PlayerRuneConfigBitsPerWord;
			int bitIndex = i % PlayerRuneConfigBitsPerWord;
			if (wordIndex < wordCount && (payload[cursor + wordIndex] & (1 << bitIndex)) != 0)
			{
				disabledPlayerRuneIds.Add(ids[i].Entry);
			}
		}

		nextCursor = cursor + wordCount;
		return true;
	}

	private static void AppendRunConfigurationSnapshot(List<int> payload, HextechRunConfigurationSnapshot snapshot)
	{
		payload.Add(RunConfigurationSnapshotVersion);
		payload.AddRange(HextechPlayerHexCountState.Normalize(snapshot.PlayerHexCountsByAct));
		payload.AddRange(HextechEnemyHexCountState.Normalize(snapshot.EnemyHexCountsByAct));
		payload.Add(HextechRuneConfiguration.ClampRerollLimit(snapshot.PlayerRuneRerollLimit));
		payload.Add(HextechRuneConfiguration.ClampRerollLimit(snapshot.MonsterHexRerollLimit));
		foreach (HextechRarityWeights weights in snapshot.RuneRarityWeightsByAct)
		{
			AppendRarityWeights(payload, weights);
		}
		payload.Add(snapshot.PreventConsecutiveSilverRunes ? 1 : 0);
		payload.Add(HextechRuneConfiguration.ClampGoldenRerollChancePercent(snapshot.GoldenRerollChancePercent));
		AppendForgeRarityWeights(payload, snapshot.ForgeRarityWeights);
		payload.Add(HextechRuneConfiguration.ClampRandomForgeShopPrice(snapshot.RandomForgeShopPrice));
		payload.Add(snapshot.RandomForgeDirectGrant ? 1 : 0);

		MonsterHexKind[] disabledMonsterHexes = snapshot.DisabledMonsterHexIds
			.Select(static id => Enum.TryParse(id, out MonsterHexKind kind) ? (MonsterHexKind?)kind : null)
			.Where(static kind => kind.HasValue)
			.Select(static kind => kind!.Value)
			.OrderBy(static kind => (int)kind)
			.ToArray();
		ValidateProtocolCount(disabledMonsterHexes.Length, MaxDisabledMonsterHexes, nameof(snapshot));
		payload.Add(disabledMonsterHexes.Length);
		payload.AddRange(disabledMonsterHexes.Select(static kind => (int)kind));

		HextechStableModelIdListCodec.Append(
			payload,
			snapshot.DisabledForgeIds
				.Select(static entry => new ModelId(ModInfo.Id, entry))
				.OrderBy(static id => id.Entry, StringComparer.Ordinal));

		// 模组总开关:作为尾部可选 int 追加,避免改 snapshot 版本号/定长计数。
		// 旧 payload 无此尾巴时解码端回退到 fallback(默认开启)。
		payload.Add(snapshot.ModEnabled ? 1 : 0);
	}

	private static bool TryDecodeRunConfigurationSnapshot(
		List<int> payload,
		int cursor,
		HextechRunConfigurationSnapshot fallback,
		out HextechRunConfigurationSnapshot snapshot)
	{
		snapshot = fallback;
		if (payload.Count <= cursor)
		{
			return true;
		}

		int snapshotVersion = payload[cursor];
		if (snapshotVersion != RunConfigurationSnapshotVersion
			&& snapshotVersion != PreviousSingleRarityRunConfigurationSnapshotVersion
			&& snapshotVersion != PreviousRunConfigurationSnapshotVersion
			&& snapshotVersion != LegacyRerollRunConfigurationSnapshotVersion
			&& snapshotVersion != LegacyRunConfigurationSnapshotVersion)
		{
			return true;
		}

		cursor++;
		int fixedIntCount = snapshotVersion switch
		{
			RunConfigurationSnapshotVersion => 3 + 3 + 2 + 9 + 1 + 1 + 3 + 1 + 1,
			PreviousSingleRarityRunConfigurationSnapshotVersion => 3 + 3 + 2 + 3 + 1 + 1 + 3 + 1 + 1,
			PreviousRunConfigurationSnapshotVersion => 3 + 3 + 2 + 3 + 1 + 3 + 1 + 1,
			LegacyRerollRunConfigurationSnapshotVersion => 3 + 3 + 2 + 3 + 3 + 3 + 3 + 1 + 1,
			_ => 3 + 3 + 3 + 3 + 3 + 3 + 1
		};
		if (!HasRemaining(payload, cursor, fixedIntCount))
		{
			return false;
		}

		int[] playerHexCounts = HextechPlayerHexCountState.Normalize(payload.Skip(cursor).Take(3).ToArray());
		cursor += 3;
		int[] enemyHexCounts = HextechEnemyHexCountState.Normalize(payload.Skip(cursor).Take(3).ToArray());
		cursor += 3;
		int playerRuneRerollLimit = fallback.PlayerRuneRerollLimit;
		int monsterHexRerollLimit = fallback.MonsterHexRerollLimit;
		if (snapshotVersion is RunConfigurationSnapshotVersion or PreviousSingleRarityRunConfigurationSnapshotVersion or PreviousRunConfigurationSnapshotVersion or LegacyRerollRunConfigurationSnapshotVersion)
		{
			playerRuneRerollLimit = HextechRuneConfiguration.ClampRerollLimit(payload[cursor++]);
			monsterHexRerollLimit = HextechRuneConfiguration.ClampRerollLimit(payload[cursor++]);
		}

		HextechRarityWeights[] runeWeightsByAct;
		bool preventConsecutiveSilverRunes;
		int goldenRerollChancePercent = HextechRuneConfiguration.GetDefaultGoldenRerollChancePercent();
		if (snapshotVersion == RunConfigurationSnapshotVersion)
		{
			runeWeightsByAct =
			[
				ReadRarityWeights(payload, ref cursor),
				ReadRarityWeights(payload, ref cursor),
				ReadRarityWeights(payload, ref cursor)
			];
			preventConsecutiveSilverRunes = payload[cursor++] != 0;
			goldenRerollChancePercent = HextechRuneConfiguration.ClampGoldenRerollChancePercent(payload[cursor++]);
		}
		else if (snapshotVersion is PreviousSingleRarityRunConfigurationSnapshotVersion or PreviousRunConfigurationSnapshotVersion)
		{
			HextechRarityWeights singleWeights = ReadRarityWeights(payload, ref cursor);
			runeWeightsByAct = [ singleWeights, singleWeights, singleWeights ];
			preventConsecutiveSilverRunes = payload[cursor++] != 0;
			if (snapshotVersion == PreviousSingleRarityRunConfigurationSnapshotVersion)
			{
				goldenRerollChancePercent = HextechRuneConfiguration.ClampGoldenRerollChancePercent(payload[cursor++]);
			}
		}
		else
		{
			_ = ReadRarityWeights(payload, ref cursor);
			HextechRarityWeights legacyNormalWeights = ReadRarityWeights(payload, ref cursor);
			_ = ReadRarityWeights(payload, ref cursor);
			runeWeightsByAct = [ legacyNormalWeights, legacyNormalWeights, legacyNormalWeights ];
			preventConsecutiveSilverRunes = HextechRuneConfiguration.GetDefaultPreventConsecutiveSilverRunes();
		}

		HextechForgeRarityWeights forgeWeights = ReadForgeRarityWeights(payload, ref cursor);
		int forgePrice = payload[cursor++];
		bool randomForgeDirectGrant = fallback.RandomForgeDirectGrant;
		if (snapshotVersion is RunConfigurationSnapshotVersion or PreviousSingleRarityRunConfigurationSnapshotVersion or PreviousRunConfigurationSnapshotVersion or LegacyRerollRunConfigurationSnapshotVersion)
		{
			randomForgeDirectGrant = payload[cursor++] != 0;
		}

		if (payload.Count <= cursor)
		{
			return false;
		}

		int disabledMonsterHexCount = payload[cursor++];
		if (disabledMonsterHexCount < 0
			|| disabledMonsterHexCount > MaxDisabledMonsterHexes
			|| !HasRemaining(payload, cursor, disabledMonsterHexCount))
		{
			return false;
		}

		HashSet<string> disabledMonsterHexIds = [];
		for (int i = 0; i < disabledMonsterHexCount; i++)
		{
			int value = payload[cursor + i];
			if (Enum.IsDefined(typeof(MonsterHexKind), value))
			{
				disabledMonsterHexIds.Add(((MonsterHexKind)value).ToString());
			}
		}

		cursor += disabledMonsterHexCount;
		if (!HextechStableModelIdListCodec.TryDecode(payload, cursor, out List<ModelId> disabledForgeIds, out int forgeListNextCursor))
		{
			return false;
		}

		// 模组总开关:尾部可选 int。旧 payload 没有这一项时回退到 fallback(默认开启)。
		bool modEnabled = fallback.ModEnabled;
		if (payload.Count > forgeListNextCursor)
		{
			modEnabled = payload[forgeListNextCursor] != 0;
		}

		snapshot = HextechRuneConfiguration.NormalizeSnapshot(new HextechRunConfigurationSnapshot(
			playerHexCounts,
			enemyHexCounts,
			playerRuneRerollLimit,
			monsterHexRerollLimit,
			fallback.DisabledPlayerRuneIds,
			disabledMonsterHexIds,
			disabledForgeIds.Select(static id => id.Entry).ToHashSet(StringComparer.Ordinal),
			runeWeightsByAct,
			preventConsecutiveSilverRunes,
			goldenRerollChancePercent,
			forgeWeights,
			forgePrice,
			randomForgeDirectGrant,
			modEnabled));
		return true;
	}

	private static void AppendRarityWeights(List<int> payload, HextechRarityWeights weights)
	{
		payload.Add(HextechRuneConfiguration.ClampRarityWeight(weights.Silver));
		payload.Add(HextechRuneConfiguration.ClampRarityWeight(weights.Gold));
		payload.Add(HextechRuneConfiguration.ClampRarityWeight(weights.Prismatic));
	}

	private static void AppendForgeRarityWeights(List<int> payload, HextechForgeRarityWeights weights)
	{
		payload.Add(HextechRuneConfiguration.ClampRarityWeight(weights.Silver));
		payload.Add(HextechRuneConfiguration.ClampRarityWeight(weights.Gold));
		payload.Add(HextechRuneConfiguration.ClampRarityWeight(weights.Prismatic));
	}

	private static HextechRarityWeights ReadRarityWeights(List<int> payload, ref int cursor)
	{
		HextechRarityWeights weights = new(payload[cursor], payload[cursor + 1], payload[cursor + 2]);
		cursor += 3;
		return weights;
	}

	private static HextechForgeRarityWeights ReadForgeRarityWeights(List<int> payload, ref int cursor)
	{
		HextechForgeRarityWeights weights = new(payload[cursor], payload[cursor + 1], payload[cursor + 2]);
		cursor += 3;
		return weights;
	}

	private static readonly Lazy<IReadOnlyList<ModelId>> PlayerRuneIdsByOrdinal = new(
		() => HextechCatalog.GetConfigurablePlayerRuneIds()
			.OrderBy(static id => id.Category, StringComparer.Ordinal)
			.ThenBy(static id => id.Entry, StringComparer.Ordinal)
			.ToArray());

	public static PlayerChoiceResult CreateRuneSelection(int actIndex, int choiceOrdinal, int selectedIndex, IReadOnlyList<int> rerollHistory, IReadOnlyList<RelicModel> finalOptions)
	{
		ValidateProtocolCount(rerollHistory.Count, MaxChoiceListCount, nameof(rerollHistory));
		List<int> payload = [ Magic, ChoiceKindRuneSelection, actIndex, choiceOrdinal, selectedIndex, rerollHistory.Count ];
		payload.AddRange(rerollHistory);
		HextechStableModelIdListCodec.Append(payload, finalOptions.Select(static relic => relic.CanonicalInstance?.Id ?? relic.Id));

		return PlayerChoiceResult.FromIndexes(payload);
	}

	public static bool IsRuneSelection(PlayerChoiceResult result)
	{
		return TryGetIndexPayload(result, out List<int> payload)
			&& payload.Count >= 2
			&& payload[0] == Magic
			&& payload[1] == ChoiceKindRuneSelection;
	}

	public static bool IsRuneSelection(PlayerChoiceResult result, int expectedActIndex, int expectedChoiceOrdinal)
	{
		return TryDecodeRuneSelection(result, expectedActIndex, expectedChoiceOrdinal, out _, out _, out _);
	}

	public static bool TryDecodeRuneSelection(
		PlayerChoiceResult result,
		int expectedActIndex,
		int expectedChoiceOrdinal,
		out int selectedIndex,
		out List<int> rerollHistory,
		out List<ModelId> finalOptionIds)
	{
		selectedIndex = -1;
		rerollHistory = [];
		finalOptionIds = [];
		if (!TryGetIndexPayload(result, out List<int> payload)
			|| payload.Count < 6
			|| payload[0] != Magic
			|| payload[1] != ChoiceKindRuneSelection
			|| payload[2] != expectedActIndex
			|| payload[3] != expectedChoiceOrdinal)
		{
			return false;
		}

		selectedIndex = payload[4];
		int rerollCount = payload[5];
		const int headerCount = 6;
		if (rerollCount < 0
			|| rerollCount > MaxChoiceListCount
			|| !HasRemaining(payload, headerCount, rerollCount))
		{
			return false;
		}

		rerollHistory = payload.Skip(headerCount).Take(rerollCount).ToList();
		int cursor = rerollCount + headerCount;
		return TryDecodeRuneSelectionFinalOptions(payload, cursor, out finalOptionIds);
	}

	private static bool TryDecodeRuneSelectionFinalOptions(List<int> payload, int cursor, out List<ModelId> finalOptionIds)
	{
		finalOptionIds = [];
		if (payload.Count <= cursor)
		{
			return true;
		}

		if (payload[cursor] == HextechStableModelIdListCodec.Version)
		{
			return HextechStableModelIdListCodec.TryDecode(payload, cursor, out finalOptionIds, out _);
		}

		int optionCount = payload[cursor];
		cursor++;
		if (optionCount < 0
			|| optionCount > MaxChoiceListCount
			|| !HasRemaining(payload, cursor, optionCount))
		{
			return false;
		}

		for (int i = 0; i < optionCount; i++)
		{
			if (!TryGetRuneIdForOrdinal(payload[cursor + i], out ModelId id))
			{
				finalOptionIds.Clear();
				return false;
			}

			finalOptionIds.Add(id);
		}

		return true;
	}

	private static bool TryGetRuneIdForOrdinal(int ordinal, out ModelId id)
	{
		IReadOnlyList<ModelId> ids = PlayerRuneIdsByOrdinal.Value;
		if (ordinal < 0 || ordinal >= ids.Count)
		{
			id = null!;
			return false;
		}

		id = ids[ordinal];
		return true;
	}

	public static PlayerChoiceResult CreateRandomRuneGrant(int operationToken, IReadOnlyList<ModelId> runeIds)
	{
		List<int> payload = [ Magic, ChoiceKindRandomRuneGrant, operationToken ];
		HextechStableModelIdListCodec.Append(payload, runeIds);
		return PlayerChoiceResult.FromIndexes(payload);
	}

	public static bool IsRandomRuneGrant(PlayerChoiceResult result, int expectedOperationToken)
	{
		return TryDecodeRandomRuneGrant(result, expectedOperationToken, out _);
	}

	public static bool TryDecodeRandomRuneGrant(
		PlayerChoiceResult result,
		int expectedOperationToken,
		out List<ModelId> runeIds)
	{
		runeIds = [];
		if (!TryGetIndexPayload(result, out List<int> payload)
			|| payload.Count < 4
			|| payload[0] != Magic
			|| payload[1] != ChoiceKindRandomRuneGrant
			|| payload[2] != expectedOperationToken)
		{
			return false;
		}

		if (payload[3] == HextechStableModelIdListCodec.Version)
		{
			return HextechStableModelIdListCodec.TryDecode(payload, 3, out runeIds, out _);
		}

		return false;
	}

	private static readonly Lazy<IReadOnlyList<ModelId>> ForgeIdsByOrdinal = new(
		() => HextechCatalog.GetAllForgeTypes()
			.Select(ModelDb.GetId)
			.OrderBy(static id => id.Category, StringComparer.Ordinal)
			.ThenBy(static id => id.Entry, StringComparer.Ordinal)
			.ToArray());

	public static PlayerChoiceResult CreateForgeSelection(
		int operationToken,
		int selectedIndex,
		IReadOnlyList<RelicModel> options)
	{
		List<int> payload = [ Magic, ChoiceKindForgeSelection, operationToken, selectedIndex ];
		HextechStableModelIdListCodec.Append(payload, options.Select(static relic => relic.CanonicalInstance?.Id ?? relic.Id));

		return PlayerChoiceResult.FromIndexes(payload);
	}

	public static bool IsForgeSelection(PlayerChoiceResult result, int expectedOperationToken)
	{
		return TryDecodeForgeSelection(result, expectedOperationToken, out _, out _);
	}

	public static bool IsForgeSelection(
		PlayerChoiceResult result,
		int expectedOperationToken,
		IReadOnlyList<RelicModel> expectedOptions)
	{
		if (!TryDecodeForgeSelection(result, expectedOperationToken, out _, out List<ModelId> optionIds)
			|| optionIds.Count != expectedOptions.Count)
		{
			return false;
		}

		for (int i = 0; i < expectedOptions.Count; i++)
		{
			ModelId expectedId = expectedOptions[i].CanonicalInstance?.Id ?? expectedOptions[i].Id;
			if (optionIds[i] != expectedId)
			{
				return false;
			}
		}

		return true;
	}

	public static bool IsMalformedForgeSelectionEnvelope(PlayerChoiceResult result, int expectedOperationToken)
	{
		return IsChoiceEnvelope(result, ChoiceKindForgeSelection)
			&& !TryDecodeForgeSelection(result, expectedOperationToken, out _, out _);
	}

	public static bool TryDecodeForgeSelection(
		PlayerChoiceResult result,
		int expectedOperationToken,
		out int selectedIndex,
		out List<ModelId> optionIds)
	{
		selectedIndex = -1;
		optionIds = [];
		if (!TryGetIndexPayload(result, out List<int> payload)
			|| payload.Count < 5
			|| payload[0] != Magic
			|| payload[1] != ChoiceKindForgeSelection
			|| payload[2] != expectedOperationToken
			|| payload[4] != HextechStableModelIdListCodec.Version)
		{
			return false;
		}

		selectedIndex = payload[3];
		return HextechStableModelIdListCodec.TryDecode(payload, 4, out optionIds, out _);
	}

	public static PlayerChoiceResult CreateRelicOptionSelection(
		int operationToken,
		int selectedIndex,
		IReadOnlyList<RelicModel> options)
	{
		List<int> payload = [ Magic, ChoiceKindRelicOptionSelection, operationToken, selectedIndex ];
		HextechStableModelIdListCodec.Append(payload, options.Select(static relic => relic.CanonicalInstance?.Id ?? relic.Id));

		return PlayerChoiceResult.FromIndexes(payload);
	}

	public static bool IsRelicOptionSelection(
		PlayerChoiceResult result,
		int expectedOperationToken,
		IReadOnlyList<RelicModel> expectedOptions)
	{
		if (!TryDecodeRelicOptionSelection(result, expectedOperationToken, out _, out List<ModelId> optionIds)
			|| optionIds.Count != expectedOptions.Count)
		{
			return false;
		}

		for (int i = 0; i < expectedOptions.Count; i++)
		{
			ModelId expectedId = expectedOptions[i].CanonicalInstance?.Id ?? expectedOptions[i].Id;
			if (optionIds[i] != expectedId)
			{
				return false;
			}
		}

		return true;
	}

	public static bool IsMalformedRelicOptionSelectionEnvelope(PlayerChoiceResult result, int expectedOperationToken)
	{
		return IsChoiceEnvelope(result, ChoiceKindRelicOptionSelection)
			&& !TryDecodeRelicOptionSelection(result, expectedOperationToken, out _, out _);
	}

	public static bool TryDecodeRelicOptionSelection(
		PlayerChoiceResult result,
		int expectedOperationToken,
		out int selectedIndex,
		out List<ModelId> optionIds)
	{
		selectedIndex = -1;
		optionIds = [];
		if (!TryGetIndexPayload(result, out List<int> payload)
			|| payload.Count < 5
			|| payload[0] != Magic
			|| payload[1] != ChoiceKindRelicOptionSelection
			|| payload[2] != expectedOperationToken
			|| payload[4] != HextechStableModelIdListCodec.Version)
		{
			return false;
		}

		selectedIndex = payload[3];
		return HextechStableModelIdListCodec.TryDecode(payload, 4, out optionIds, out _);
	}

	private static bool TryGetForgeIdForOrdinal(int ordinal, out ModelId id)
	{
		IReadOnlyList<ModelId> ids = ForgeIdsByOrdinal.Value;
		if (ordinal < 0 || ordinal >= ids.Count)
		{
			id = null!;
			return false;
		}

		id = ids[ordinal];
		return true;
	}

	private static bool IsChoiceEnvelope(PlayerChoiceResult result, int choiceKind)
	{
		return TryGetIndexPayload(result, out List<int> payload)
			&& payload.Count >= 2
			&& payload[0] == Magic
			&& payload[1] == choiceKind;
	}

	public static PlayerChoiceResult CreateActSelectionApplied(int actIndex, int choiceOrdinal)
	{
		return PlayerChoiceResult.FromIndexes([ Magic, ChoiceKindActSelectionApplied, actIndex, choiceOrdinal, 1 ]);
	}

	public static bool TryDecodeActSelectionApplied(PlayerChoiceResult result, int expectedActIndex, int expectedChoiceOrdinal)
	{
		return TryGetIndexPayload(result, out List<int> payload)
			&& payload.Count >= 5
			&& payload[0] == Magic
			&& payload[1] == ChoiceKindActSelectionApplied
			&& payload[2] == expectedActIndex
			&& payload[3] == expectedChoiceOrdinal
			&& payload[4] == 1;
	}

	public static PlayerChoiceResult CreateEnemyHexAdjustment(
		int operationToken,
		EnemyHexAdjustmentPayload payload)
	{
		if (payload.Sequence < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(payload), payload.Sequence, "Enemy adjustment sequence must be non-negative.");
		}

		ValidateProtocolCount(payload.MonsterHexes.Count, MaxChoiceListCount, nameof(payload));
		ValidateProtocolCount(payload.RerollCounts.Count, MaxChoiceListCount, nameof(payload));
		List<int> indexes =
		[
			Magic,
			ChoiceKindEnemyHexAdjustment,
			payload.ActIndex,
			payload.Sequence,
			operationToken,
			EnemyHexAdjustmentListVersion,
			payload.IsFinal ? 1 : 0,
			payload.MonsterHexes.Count
		];
		indexes.AddRange(payload.MonsterHexes.Select(static hex => hex.HasValue ? (int)hex.Value : -1));
		indexes.Add(payload.RerollCounts.Count);
		indexes.AddRange(payload.RerollCounts.Select(static count => Math.Max(0, count)));
		return PlayerChoiceResult.FromIndexes(indexes);
	}

	public static bool TryDecodeEnemyHexAdjustment(
		PlayerChoiceResult result,
		int expectedOperationToken,
		int expectedActIndex,
		int expectedSequence,
		out EnemyHexAdjustmentPayload payload)
	{
		payload = default;
		if (expectedSequence < 0
			|| !TryDecodeEnemyHexAdjustmentCore(
				result,
				expectedOperationToken,
				expectedActIndex,
				out EnemyHexAdjustmentPayload decoded)
			|| decoded.Sequence != expectedSequence)
		{
			return false;
		}

		payload = decoded;
		return true;
	}

	private static bool TryDecodeEnemyHexAdjustmentCore(
		PlayerChoiceResult result,
		int expectedOperationToken,
		int expectedActIndex,
		out EnemyHexAdjustmentPayload payload)
	{
		payload = default;
		if (!TryGetIndexPayload(result, out List<int> indexes)
			|| indexes.Count < 8
			|| indexes[0] != Magic
			|| indexes[1] != ChoiceKindEnemyHexAdjustment
			|| indexes[2] != expectedActIndex
			|| indexes[3] < 0
			|| indexes[4] != expectedOperationToken
			|| indexes[5] != EnemyHexAdjustmentListVersion)
		{
			return false;
		}

		bool isFinal = indexes[6] != 0;
		int hexCount = indexes[7];
		int cursor = 8;
		if (hexCount < 0
			|| hexCount > MaxChoiceListCount
			|| !HasRemaining(indexes, cursor, hexCount)
			|| !HasRemaining(indexes, cursor + hexCount, 1))
		{
			return false;
		}

		List<MonsterHexKind?> monsterHexes = new(hexCount);
		for (int i = 0; i < hexCount; i++)
		{
			int rawHex = indexes[cursor + i];
			if (rawHex < 0)
			{
				monsterHexes.Add(null);
				continue;
			}

			if (!Enum.IsDefined(typeof(MonsterHexKind), rawHex))
			{
				return false;
			}

			monsterHexes.Add((MonsterHexKind)rawHex);
		}

		cursor += hexCount;
		int rerollCount = indexes[cursor];
		cursor++;
		if (rerollCount < 0
			|| rerollCount > MaxChoiceListCount
			|| !HasRemaining(indexes, cursor, rerollCount))
		{
			return false;
		}

		List<int> rerollCounts = indexes.Skip(cursor).Take(rerollCount).Select(static count => Math.Max(0, count)).ToList();
		while (rerollCounts.Count < monsterHexes.Count)
		{
			rerollCounts.Add(0);
		}

		payload = new EnemyHexAdjustmentPayload(
			indexes[2],
			indexes[3],
			monsterHexes,
			rerollCounts,
			isFinal);
		return true;
	}

	public static bool TryGetIndexPayload(PlayerChoiceResult result, out List<int> payload)
	{
		payload = [];
		try
		{
			List<int>? indexes = result.AsIndexes();
			if (indexes == null)
			{
				return false;
			}

			payload = indexes;
			return true;
		}
		catch (InvalidOperationException)
		{
			return false;
		}
	}

	private static bool HasRemaining(IReadOnlyList<int> payload, int cursor, int count)
	{
		return cursor >= 0
			&& count >= 0
			&& cursor <= payload.Count
			&& count <= payload.Count - cursor;
	}

	private static void ValidateProtocolCount(int count, int maximum, string parameterName)
	{
		if (count < 0 || count > maximum)
		{
			throw new ArgumentOutOfRangeException(
				parameterName,
				count,
				$"Protocol list count must be between 0 and {maximum}.");
		}
	}

	private static void AppendFnvString(ref uint hash, string value)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(value);
		AppendFnvUInt32(ref hash, checked((uint)bytes.Length));
		foreach (byte valueByte in bytes)
		{
			AppendFnvByte(ref hash, valueByte);
		}
	}

	private static void AppendFnvUInt32(ref uint hash, uint value)
	{
		for (int shift = 0; shift < 32; shift += 8)
		{
			AppendFnvByte(ref hash, unchecked((byte)(value >> shift)));
		}
	}

	private static void AppendFnvUInt64(ref uint hash, ulong value)
	{
		for (int shift = 0; shift < 64; shift += 8)
		{
			AppendFnvByte(ref hash, unchecked((byte)(value >> shift)));
		}
	}

	private static void AppendFnvByte(ref uint hash, byte value)
	{
		hash = unchecked((hash ^ value) * Fnv1aPrime);
	}
}
