namespace HextechRunes;

public sealed class ExposeUpgradeRune : CardUpgradeRuneBase<Expose>
{
	protected override bool IsAvailableForCharacter(Player player)
	{
		return IsSilentPlayer(player);
	}

	public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		if (Owner == null
			|| cardPlay.Card.Owner != Owner
			|| cardPlay.Card is not Expose
			|| cardPlay.Target == null
			|| cardPlay.Target.Side == Owner.Creature.Side)
		{
			return;
		}

		List<PowerModel> buffs = cardPlay.Target.Powers
			.Where(static power => power.GetTypeForAmount(power.Amount) == PowerType.Buff
				&& !HextechMonsterInteractionPolicy.ShouldPreserveFromBuffRemoval(power))
			.ToList();
		if (buffs.Count == 0)
		{
			return;
		}

		Flash([cardPlay.Target]);
		foreach (PowerModel power in buffs)
		{
			await HextechMonsterInteractionPolicy.RemoveMonsterBuffSafely(power);
		}
	}
}
