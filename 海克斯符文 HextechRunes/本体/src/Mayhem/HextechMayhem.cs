namespace HextechRunes;

internal sealed partial class HextechMayhemModifier : HextechModifierBase
{
	internal static HextechMayhemModifier? FindIn(IRunState? runState)
	{
		return runState?.Modifiers.OfType<HextechMayhemModifier>().LastOrDefault();
	}
}
