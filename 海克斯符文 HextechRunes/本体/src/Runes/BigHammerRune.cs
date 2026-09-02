using HarmonyLib;
using MegaCrit.Sts2.Core.Hooks;

namespace HextechRunes;

public sealed class BigHammerRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("ForgeBonusPercent", 50m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips => HoverTipFactory.FromForge();

	public override bool IsAvailableForPlayer(Player player) => IsRegentPlayer(player);

	internal decimal ApplyForgeBonus(decimal amount, bool sourceAlreadyIncludesBonus)
	{
		return CalculateForgeAmount(amount, DynamicVars["ForgeBonusPercent"].BaseValue, sourceAlreadyIncludesBonus);
	}

	internal static decimal CalculateForgeAmount(decimal amount, decimal bonusPercent, bool sourceAlreadyIncludesBonus)
	{
		return sourceAlreadyIncludesBonus
			? amount
			: amount * (1m + bonusPercent / 100m);
	}

	[HarmonyPatch(typeof(ForgeCmd), nameof(ForgeCmd.Forge), typeof(decimal), typeof(Player), typeof(AbstractModel))]
	[HextechPatch("rune.big-hammer", "大锤", Rune = typeof(BigHammerRune))]
	private static class BigHammerPatch
	{
		[HarmonyPrefix]
		private static void Prefix(ref decimal amount, Player player, AbstractModel? source)
		{
			BigHammerRune? rune = player.GetRelic<BigHammerRune>();
			if (rune == null)
			{
				return;
			}

			bool sourceAlreadyIncludesBonus = source is HammerTimePower hammerTime
				&& hammerTime.Owner.Player?.GetRelic<BigHammerRune>() != null;
			decimal modifiedAmount = rune.ApplyForgeBonus(amount, sourceAlreadyIncludesBonus);
			if (modifiedAmount == amount)
			{
				return;
			}

			amount = modifiedAmount;
			rune.Flash();
		}
	}
}
