using HarmonyLib;
using MegaCrit.Sts2.Core.Models.Enchantments;

namespace HextechRunes;

public sealed class InkshadowRune : HextechRelicBase
{
	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromCard<Shiv>(),
		.. HoverTipFactory.FromEnchantment<Inky>()
	];

	public override bool IsAvailableForPlayer(Player player)
	{
		return IsSilentPlayer(player);
	}

	internal static bool TryApplyForOwner(CardModel? card, Player? owner, bool flash = true)
	{
		return owner?.GetRelic<InkshadowRune>() is InkshadowRune rune
			&& rune.TryApplyInkshadow(card, flash);
	}

	public override Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
	{
		TryApplyInkshadow(card);
		return Task.CompletedTask;
	}

	public override Task AfterCardEnteredCombat(CardModel card)
	{
		TryApplyInkshadow(card, flash: false);
		return Task.CompletedTask;
	}

	public override bool TryModifyCardBeingAddedToDeck(CardModel card, out CardModel? newCard)
	{
		newCard = null;
		if (!TryApplyInkshadow(card))
		{
			return false;
		}

		newCard = card;
		return true;
	}

	private bool TryApplyInkshadow(CardModel? card, bool flash = true)
	{
		if (Owner == null
			|| card == null
			|| card.Owner != Owner
			|| !HextechKnifeHelper.IsShivLike(card, Owner)
			|| card.Enchantment is Inky
			|| card.Enchantment != null)
		{
			return false;
		}

		Inky enchantment = (Inky)ModelDb.Enchantment<Inky>().ToMutable();
		if (!enchantment.CanEnchant(card))
		{
			return false;
		}

		CardCmd.Enchant(enchantment, card, 1m);
		if (flash)
		{
			Flash();
		}

		return true;
	}

	[HarmonyPatch(typeof(BladeOfInk), "OnPlay", typeof(PlayerChoiceContext), typeof(CardPlay))]
	[HextechPatch("rune.inkshadow", "墨影")]
	private static class BladeOfInkPatch
	{
		[HarmonyPrefix]
		private static bool Prefix(BladeOfInk __instance, PlayerChoiceContext choiceContext, ref Task __result)
		{
			if (__instance.Owner is not { } owner
				|| __instance.CombatState is not { } combatState
				|| owner.GetRelic<InkshadowRune>() == null)
			{
				return true;
			}

			__result = HextechInkshadowHooks.PlayWithGuardedEnchant(__instance, owner, combatState);
			return false;
		}
	}
}
