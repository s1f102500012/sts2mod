namespace HextechRunes;

public sealed class HextechNextTurnDamagePower : HextechPowerBase
{
	private static readonly HextechScopedDepthGuard DamageResolutionGuard = new();

	internal static bool IsResolvingDamage => DamageResolutionGuard.IsActive;

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

	internal static Task RunWithDamageResolutionGuard(Func<Task> action)
	{
		return DamageResolutionGuard.RunAsync(action);
	}
}
