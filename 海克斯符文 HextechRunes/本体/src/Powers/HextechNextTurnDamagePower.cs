namespace HextechRunes;

public sealed class HextechNextTurnDamagePower : HextechPowerBase
{
	private static readonly AsyncLocal<int> DamageResolutionDepth = new();

	internal static bool IsResolvingDamage => DamageResolutionDepth.Value > 0;

	public override PowerType Type => PowerType.Debuff;

	public override PowerStackType StackType => PowerStackType.Counter;

	public override async Task AfterSideTurnStart(CombatSide side, HextechCombatState combatState)
	{
		int damage = GetDamageToResolve(Amount, AmountOnTurnStart);
		if (side != CombatSide.Player
			|| Owner.Side != CombatSide.Enemy
			|| !Owner.IsAlive
			|| !ReferenceEquals(Owner.CombatState, combatState)
			|| damage <= 0)
		{
			return;
		}

		Creature target = Owner;
		Flash();
		await PowerCmd.Apply<HextechNextTurnDamagePower>(target, -damage, null, null, silent: true);
		await RunWithDamageResolutionGuard(
			() => HextechGameApiCompat.Damage(
				new ThrowingPlayerChoiceContext(),
				target,
				damage,
				ValueProp.Unblockable | ValueProp.Unpowered,
				null,
				null));
	}

	internal static int GetDamageToResolve(int amount, int amountOnTurnStart)
	{
		return Math.Max(0, Math.Min(amount, amountOnTurnStart));
	}

	internal static async Task RunWithDamageResolutionGuard(Func<Task> action)
	{
		DamageResolutionDepth.Value++;
		try
		{
			await action();
		}
		finally
		{
			DamageResolutionDepth.Value = Math.Max(0, DamageResolutionDepth.Value - 1);
		}
	}
}
