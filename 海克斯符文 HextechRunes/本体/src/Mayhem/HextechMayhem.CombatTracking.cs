namespace HextechRunes;

internal sealed partial class HextechMayhemModifier
{
	private void ResetCombatTracking()
	{
		HextechEnemyHexEffects.ResetAllRunScopedState();
		_runContext.ResetCombatTracking();
	}
}
