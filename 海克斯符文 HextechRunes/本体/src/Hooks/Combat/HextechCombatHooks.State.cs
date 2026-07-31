namespace HextechRunes;

internal static partial class HextechCombatHooks
{
	private static readonly HextechScopedDepthGuard GoliathMaxHpGuard = new();

	internal static bool IsHandlingGoliathMaxHp => GoliathMaxHpGuard.IsActive;
}
