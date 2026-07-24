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
}
