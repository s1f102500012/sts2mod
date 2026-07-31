namespace HextechRunes;

internal static partial class HextechRuneSelectionCoordinator
{
	internal static T RequireCompletedSelection<T>(T? selected, string context)
		where T : class
	{
		return selected ?? throw new OperationCanceledException(
			$"Hextech selection ended without a confirmed choice: {context}");
	}

	private readonly record struct PendingRuneSelection(Player Player, List<RelicModel> Options, uint ChoiceId, bool IsLocal);

	private readonly record struct RuneSelectionResult(
		RelicModel? SelectedRelic,
		IReadOnlyList<RelicModel> FinalOptions,
		int RerollCount,
		MonsterHexKind? FinalMonsterHex,
		IReadOnlyList<MonsterHexKind>? FinalMonsterHexes = null,
		HextechRuneSelectionScreen? BlockingScreen = null)
	{
		public IReadOnlyList<MonsterHexKind> ResolvedMonsterHexes => FinalMonsterHexes
			?? (FinalMonsterHex.HasValue ? [ FinalMonsterHex.Value ] : []);
	}

	private sealed class EnemyHexAdjustmentSyncContext(
		PlayerChoiceSynchronizer synchronizer,
		Player authorityPlayer,
		uint initialChoiceId,
		int actIndex,
		IReadOnlyList<MonsterHexKind> initialMonsterHexes)
	{
		public PlayerChoiceSynchronizer Synchronizer { get; } = synchronizer;
		public Player AuthorityPlayer { get; } = authorityPlayer;
		public uint NextChoiceId { get; set; } = initialChoiceId;
		public int ActIndex { get; } = actIndex;
		public int Sequence { get; set; }
		public List<MonsterHexKind?> CurrentMonsterHexSlots { get; } = initialMonsterHexes.Select(static hex => (MonsterHexKind?)hex).ToList();
		public List<int> RerollCounts { get; } = initialMonsterHexes.Select(static _ => 0).ToList();
		public IReadOnlyList<MonsterHexKind> CurrentMonsterHexes => CurrentMonsterHexSlots
			.Where(static hex => hex.HasValue)
			.Select(static hex => hex!.Value)
			.ToArray();
		public bool FinalSent { get; set; }
		public Task? RemoteReceiveTask { get; set; }
	}

	private const int FirstActSilverWeight = 20;
	private const int FirstActGoldWeight = 50;
	private const int FirstActPrismaticWeight = 30;
	private const int RemoteRuneChoicePollFrames = 1800;
}
