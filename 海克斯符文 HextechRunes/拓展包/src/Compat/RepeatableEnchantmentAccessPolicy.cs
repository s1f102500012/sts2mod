using HextechRunes;
using MegaCrit.Sts2.Core.Entities.Players;

namespace HextechRunesSponsorPack;

internal static class RepeatableEnchantmentAccessPolicy
{
	internal static bool IsEnabledFor(Player? player)
	{
		return player?.GetRelic<EnchantmentMasterRune>() != null;
	}
}
