using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

public static class HextechPowerCmdCompat
{
	private const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;

	public static MethodInfo RequireModifyAmountMethod()
	{
		return RequireMethod(
			typeof(MegaCrit.Sts2.Core.Commands.PowerCmd),
			nameof(MegaCrit.Sts2.Core.Commands.PowerCmd.ModifyAmount),
			PublicStatic,
			typeof(PlayerChoiceContext),
			typeof(PowerModel),
			typeof(decimal),
			typeof(Creature),
			typeof(CardModel),
			typeof(bool));
	}

	public static Task<IReadOnlyList<T>> Apply<T>(
		IEnumerable<Creature> targets,
		decimal amount,
		Creature? applier,
		CardModel? cardSource,
		bool silent = false)
		where T : PowerModel
	{
		return MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<T>(
			new BlockingPlayerChoiceContext(),
			targets,
			amount,
			applier,
			cardSource,
			silent);
	}

	public static Task<T?> Apply<T>(
		Creature target,
		decimal amount,
		Creature? applier,
		CardModel? cardSource,
		bool silent = false)
		where T : PowerModel
	{
		return MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<T>(
			new BlockingPlayerChoiceContext(),
			target,
			amount,
			applier,
			cardSource,
			silent);
	}

	public static Task<T?> Apply<T>(
		object? choiceContext,
		Creature target,
		decimal amount,
		Creature? applier,
		CardModel? cardSource,
		bool silent = false)
		where T : PowerModel
	{
		return MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<T>(
			choiceContext as PlayerChoiceContext ?? new BlockingPlayerChoiceContext(),
			target,
			amount,
			applier,
			cardSource,
			silent);
	}

	public static Task Apply(
		PowerModel power,
		Creature target,
		decimal amount,
		Creature? applier,
		CardModel? cardSource,
		bool silent = false)
	{
		return MegaCrit.Sts2.Core.Commands.PowerCmd.Apply(
			new BlockingPlayerChoiceContext(),
			power,
			target,
			amount,
			applier,
			cardSource,
			silent);
	}

	public static Task Remove<T>(Creature creature)
		where T : PowerModel
	{
		return MegaCrit.Sts2.Core.Commands.PowerCmd.Remove<T>(creature);
	}

	public static Task Remove(PowerModel power)
	{
		return MegaCrit.Sts2.Core.Commands.PowerCmd.Remove(power);
	}

	public static Task Decrement(PowerModel power)
	{
		return MegaCrit.Sts2.Core.Commands.PowerCmd.Decrement(power);
	}
}
