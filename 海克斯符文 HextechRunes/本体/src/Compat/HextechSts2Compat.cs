namespace HextechRunes;

internal static class HextechSts2Compat
{
	public static bool IsPoweredAttack(ValueProp props)
	{
		return MegaCrit.Sts2.Core.ValueProps.ValuePropExtensions.IsPoweredAttack(props);
	}

	public static bool IsPartOfPlayerTurn(Player player)
	{
		return CombatManager.Instance.IsPartOfPlayerTurn(player);
	}
}
