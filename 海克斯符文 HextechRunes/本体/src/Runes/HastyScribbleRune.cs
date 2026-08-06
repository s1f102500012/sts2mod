namespace HextechRunes;

public sealed class HastyScribbleRune : HextechRelicBase
{
	public override async Task AfterPlayerTurnStartLate(PlayerChoiceContext choiceContext, Player player)
	{
		var combatState = player.PlayerCombatState;
		if (player != Owner || combatState == null)
		{
			return;
		}

		int cardsToDraw = CalculateCardsToDraw(combatState.Hand.Cards.Count);
		if (cardsToDraw <= 0)
		{
			return;
		}

		Flash();
		await CardPileCmd.Draw(choiceContext, cardsToDraw, player);
	}

	internal static int CalculateCardsToDraw(int handCount)
	{
		return Math.Max(0, CardPile.MaxCardsInHand - handCount);
	}
}
