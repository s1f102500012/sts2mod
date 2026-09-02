using MegaCrit.Sts2.Core.Models.Exceptions;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace HextechRunes;

/// <summary>升级：创造性AI——仅升级由创造性AI能力生成的能力牌。</summary>
public sealed class CreativeAiUpgradeRune : CardUpgradeRuneBase<CreativeAi>
{
	protected override bool IsAvailableForCharacter(Player player)
	{
		return IsDefectPlayer(player);
	}

	internal static bool ShouldUseUpgradedGeneration(CreativeAiPower power, Player player)
	{
		Player? owner = power.Owner?.Player;
		return owner != null
			&& player == owner
			&& owner.GetRelic<CreativeAiUpgradeRune>() != null;
	}

	internal static async Task GenerateUpgradedPowerCards(CreativeAiPower power, Player player)
	{
		CreativeAiUpgradeRune? rune = player.GetRelic<CreativeAiUpgradeRune>();
		bool flashed = false;
		for (int i = 0; i < power.Amount; i++)
		{
			CardModel? card = CardFactory.GetDistinctForCombat(
				player,
				player.Character.CardPool
					.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)
					.Where(static candidate => candidate.Type == CardType.Power),
				1,
				player.RunState.Rng.CombatCardGeneration)
				.FirstOrDefault();
			if (card == null)
			{
				continue;
			}

			if (UpgradeGeneratedCard(card))
			{
				if (!flashed)
				{
					rune?.Flash();
					flashed = true;
				}
			}

			await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player);
		}
	}

	internal static bool UpgradeGeneratedCard(CardModel card)
	{
		if (!card.IsUpgradable)
		{
			return false;
		}

		CardCmd.Upgrade(card, CardPreviewStyle.None);
		return true;
	}

	[HarmonyPatch(typeof(CreativeAiPower), nameof(CreativeAiPower.BeforeHandDraw), typeof(Player), typeof(PlayerChoiceContext), typeof(HextechCombatState))]
	[HextechPatch("rune.creative-ai", "升级创意AI", Rune = typeof(CreativeAiUpgradeRune))]
	private static class CreativeAiPatch
	{
		[HarmonyPrefix]
		private static bool Prefix(CreativeAiPower __instance, Player player, ref Task __result)
		{
			if (!CreativeAiUpgradeRune.ShouldUseUpgradedGeneration(__instance, player))
			{
				return true;
			}

			__result = CreativeAiUpgradeRune.GenerateUpgradedPowerCards(__instance, player);
			return false;
		}
	}
}
