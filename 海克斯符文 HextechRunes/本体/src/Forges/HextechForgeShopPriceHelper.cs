namespace HextechRunes;

internal static class HextechForgeShopPriceHelper
{
	public static int GetCurrentRandomForgeShopPrice()
	{
		try
		{
			if (RunManager.Instance.DebugOnlyGetState() is RunState runState
				&& TryGetRandomForgeShopPrice(runState, out int price))
			{
				return price;
			}
		}
		catch (InvalidOperationException ex)
		{
			if (HextechRunLogBudget.TryConsume("forge.shop-price-config-fallback", 3))
			{
				Log.Warn(
					$"[{ModInfo.Id}][Forge] Could not read synchronized random forge shop price; "
					+ $"using local configuration fallback: {ex.GetType().Name}: {ex.Message}");
			}
		}

		return HextechRuneConfiguration.GetSnapshot().RandomForgeShopPrice;
	}

	public static int GetRandomForgeShopPriceFor(RunState? runState)
	{
		return TryGetRandomForgeShopPrice(runState, out int price)
			? price
			: GetCurrentRandomForgeShopPrice();
	}

	public static void RefreshRandomForgeShopRelic(RandomForgeShopRelic shopRelic, RunState? runState = null)
	{
		shopRelic.SetDisplayedPrice(GetRandomForgeShopPriceFor(runState));
	}

	private static bool TryGetRandomForgeShopPrice(RunState? runState, out int price)
	{
		price = 0;
		HextechMayhemModifier? modifier = runState?.Modifiers.OfType<HextechMayhemModifier>().LastOrDefault();
		if (modifier == null)
		{
			return false;
		}

		price = modifier.RandomForgeShopPrice;
		return true;
	}
}
