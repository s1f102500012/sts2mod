using System.Globalization;

namespace HextechRunes;

internal readonly record struct HextechPlayerCoefficients(
	decimal Health,
	decimal Damage,
	decimal Block,
	decimal Healing);

internal static class HextechPlayerCoefficientHelper
{
	private static readonly object FailureLogLock = new();
	private static readonly HashSet<(string Coefficient, Type ProviderType)> LoggedProviderFailures = [];

	public static HextechPlayerCoefficients Get(Player player)
	{
		return new HextechPlayerCoefficients(
			GetHealthMultiplier(player),
			GetDamageMultiplier(player),
			GetBlockMultiplier(player),
			GetHealingMultiplier(player));
	}

	public static decimal GetHealingMultiplier(Player player)
	{
		if (NearDeathFeastRune.ShouldPreventSustain(player.Creature))
		{
			return 0m;
		}

		decimal multiplier = 1m;
		if (player.GetRelic<OverflowRune>() != null)
		{
			multiplier *= 2m;
		}

		if (player.GetRelic<FirstAidKitRune>() != null)
		{
			multiplier *= 1.25m;
		}

		if (player.GetRelic<PacifistRune>() is PacifistRune pacifistRune)
		{
			multiplier *= pacifistRune.SustainMultiplier;
		}

		if (player.GetRelic<SacrificeRune>() is SacrificeRune sacrificeRune)
		{
			multiplier *= sacrificeRune.SustainMultiplier;
		}

		if (player.GetRelic<BackToBasicsRune>() != null)
		{
			multiplier *= 1.4m;
		}

		if (player.GetRelic<GoliathRune>() != null)
		{
			multiplier *= 1.2m;
		}

		if (player.GetRelic<ProteinShakeRune>() is ProteinShakeRune proteinShakeRune)
		{
			multiplier *= proteinShakeRune.SustainMultiplier;
		}

		multiplier *= HextechForgeCoefficientHelper.GetSustainMultiplier(player);

		if (player.GetRelic<MoreTheMerrierRune>() is MoreTheMerrierRune moreTheMerrierRune)
		{
			multiplier *= moreTheMerrierRune.SustainMultiplier;
		}

		if (player.GetRelic<GoldenSpatulaRune>() is GoldenSpatulaRune goldenSpatulaRune)
		{
			multiplier *= goldenSpatulaRune.SustainMultiplier;
		}

		if (player.GetRelic<AnthonyBiasRune>() is AnthonyBiasRune anthonyBiasRune)
		{
			multiplier *= anthonyBiasRune.SustainMultiplier;
		}

		if (player.GetRelic<NineDragonPowerRune>() is NineDragonPowerRune nineDragonPowerRune)
		{
			multiplier *= nineDragonPowerRune.SustainMultiplier;
		}

		foreach (RelicModel relic in player.Relics)
		{
			if (relic is not IHextechHealingMultiplierProvider provider)
			{
				continue;
			}

			try
			{
				multiplier *= provider.ModifyHealingMultiplicative(player, player.Creature, 1m);
			}
			catch (Exception ex)
			{
				WarnProviderFailureOnce("healing", relic.GetType(), ex);
			}
		}

		return multiplier;
	}

	public static string FormatPercent(decimal multiplier)
	{
		decimal percent = Math.Round(multiplier * 100m, 1, MidpointRounding.AwayFromZero);
		return decimal.Remainder(percent, 1m) == 0m
			? $"{decimal.ToInt32(percent)}%"
			: $"{percent.ToString("0.#", CultureInfo.InvariantCulture)}%";
	}

	private static decimal GetHealthMultiplier(Player player)
	{
		return HextechMaxHpScaling.GetScale(player);
	}

	private static decimal GetDamageMultiplier(Player player)
	{
		if (player.GetRelic<PacifistRune>() != null)
		{
			return 0m;
		}

		return MultiplyRelicModifiers(
			player,
			"damage",
#if STS2_108_OR_NEWER
			static (relic, owner) => relic.ModifyDamageMultiplicative(null, 1m, ValueProp.Unpowered, owner.Creature, null, null));
#else
			static (relic, owner) => relic.ModifyDamageMultiplicative(null, 1m, ValueProp.Unpowered, owner.Creature, null));
#endif
	}

	private static decimal GetBlockMultiplier(Player player)
	{
		return MultiplyRelicModifiers(
			player,
			"block",
			static (relic, owner) => relic.ModifyBlockMultiplicative(owner.Creature, 1m, ValueProp.Unpowered, null, null));
	}

	private static decimal MultiplyRelicModifiers(
		Player player,
		string coefficient,
		Func<RelicModel, Player, decimal> getMultiplier)
	{
		decimal multiplier = 1m;
		foreach (RelicModel relic in player.Relics)
		{
			try
			{
				multiplier *= getMultiplier(relic, player);
			}
			catch (Exception ex)
			{
				WarnProviderFailureOnce(coefficient, relic.GetType(), ex);
			}
		}

		return multiplier;
	}

	private static void WarnProviderFailureOnce(string coefficient, Type providerType, Exception exception)
	{
		lock (FailureLogLock)
		{
			if (!LoggedProviderFailures.Add((coefficient, providerType)))
			{
				return;
			}
		}

		Log.Warn(
			$"[{ModInfo.Id}][Coefficient] Ignored {coefficient} multiplier failure from " +
			$"{providerType.FullName}: {exception.GetType().Name}: {exception.Message}");
	}
}
