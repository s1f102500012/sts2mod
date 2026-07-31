namespace HextechRunes;

public sealed class ReanimateUpgradeRune : CardUpgradeRuneBase<Reanimate>
{
	private int _deathsThisCombat;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedDeathsThisCombat
	{
		get => _deathsThisCombat;
		set => _deathsThisCombat = Math.Max(0, value);
	}

	protected override bool IsAvailableForCharacter(Player player)
	{
		return IsNecrobinderPlayer(player);
	}

	public override Task BeforeCombatStart()
	{
		_deathsThisCombat = 0;
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		_deathsThisCombat = 0;
		return Task.CompletedTask;
	}

	public override Task AfterDeath(PlayerChoiceContext choiceContext, Creature target, bool wasRemovalPrevented, float deathAnimLength)
	{
		if (ShouldCountDeath(wasRemovalPrevented))
		{
			_deathsThisCombat++;
			Flash();
			RefreshReanimateCostsInHand();
		}

		return Task.CompletedTask;
	}

	// 原版忧郁只排除被阻止的死亡；爪牙和小手回调时可能已经脱离 CombatState。
	internal static bool ShouldCountDeath(bool wasRemovalPrevented) => !wasRemovalPrevented;

	public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
	{
		modifiedCost = originalCost;
		if (Owner == null
			|| card.Owner != Owner
			|| card is not Reanimate
			|| card.EnergyCost.CostsX)
		{
			return false;
		}

		decimal reducedCost = Math.Max(0m, originalCost - 1m - _deathsThisCombat);
		if (reducedCost == originalCost)
		{
			return false;
		}

		modifiedCost = reducedCost;
		return true;
	}

	private void RefreshReanimateCostsInHand()
	{
		if (Owner == null)
		{
			return;
		}

		foreach (CardModel card in PileType.Hand.GetPile(Owner).Cards)
		{
			if (card is Reanimate)
			{
				card.InvokeEnergyCostChanged();
			}
		}
	}
}
