namespace HextechRunes;

internal sealed class HextechEnemyHexAdjustmentOptions
{
	public MonsterHexKind? InitialHex { get; init; }

	public IReadOnlyList<MonsterHexKind> InitialHexes { get; init; } = [];

	public IReadOnlyList<MonsterHexKind> ExcludedHexes { get; init; } = [];

	public bool ControlsEnabled { get; init; }

	public Func<IReadOnlyList<MonsterHexKind?>, int, int, MonsterHexKind?>? RerollFunc { get; init; }

	public int RerollLimit { get; init; } = HextechRuneConfiguration.GetDefaultMonsterHexRerollLimit();

	public Action<IReadOnlyList<MonsterHexKind?>, IReadOnlyList<int>>? Changed { get; init; }

	public Action<HextechRuneSelectionScreen>? ScreenCreated { get; init; }
}
