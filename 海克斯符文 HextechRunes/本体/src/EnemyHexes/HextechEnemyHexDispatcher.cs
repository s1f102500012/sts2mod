namespace HextechRunes;

internal static class HextechEnemyHexDispatcher
{
	internal static Task ForEachActive(
		HextechMayhemModifier modifier,
		Func<HextechEnemyHexEffect, HextechEnemyHexContext, Task> handler)
	{
		return ForEachActive(modifier, turnParticipants: null, handler);
	}

	internal static async Task ForEachActive(
		HextechMayhemModifier modifier,
		IEnumerable<Creature>? turnParticipants,
		Func<HextechEnemyHexEffect, HextechEnemyHexContext, Task> handler)
	{
		HextechEnemyHexContext context = new(modifier, turnParticipants);
		foreach (HextechEnemyHexEffect effect in HextechEnemyHexEffects.GetActive(modifier))
		{
			await handler(effect, context);
		}
	}

	internal static async Task ForEachActiveOrdered(
		HextechMayhemModifier modifier,
		Func<HextechEnemyHexEffect, int> orderSelector,
		Func<HextechEnemyHexEffect, HextechEnemyHexContext, Task> handler)
	{
		HextechEnemyHexContext context = new(modifier);
		foreach (HextechEnemyHexEffect effect in HextechEnemyHexEffects.GetActive(modifier).OrderBy(orderSelector))
		{
			await handler(effect, context);
		}
	}

	internal static bool AnyModified(
		HextechMayhemModifier modifier,
		Func<HextechEnemyHexEffect, HextechEnemyHexContext, bool> handler)
	{
		HextechEnemyHexContext context = new(modifier);
		bool modified = false;
		foreach (HextechEnemyHexEffect effect in HextechEnemyHexEffects.GetActive(modifier))
		{
			modified |= handler(effect, context);
		}

		return modified;
	}

	internal static bool All(
		HextechMayhemModifier modifier,
		Func<HextechEnemyHexEffect, HextechEnemyHexContext, bool> predicate)
	{
		HextechEnemyHexContext context = new(modifier);
		foreach (HextechEnemyHexEffect effect in HextechEnemyHexEffects.GetActive(modifier))
		{
			if (!predicate(effect, context))
			{
				return false;
			}
		}

		return true;
	}

	internal static T Transform<T>(
		HextechMayhemModifier modifier,
		T value,
		Func<HextechEnemyHexEffect, HextechEnemyHexContext, T, T> transform)
	{
		HextechEnemyHexContext context = new(modifier);
		foreach (HextechEnemyHexEffect effect in HextechEnemyHexEffects.GetActive(modifier))
		{
			value = transform(effect, context, value);
		}

		return value;
	}
}
