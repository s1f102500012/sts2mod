using MegaCrit.Sts2.Core.Hooks;

namespace HextechRunes;

internal readonly record struct HextechCardPlayResourceSpend(decimal Energy, decimal Stars)
{
	public bool HasAny => Energy > 0m || Stars > 0m;
}

internal static partial class HextechCombatHooks
{
	private static readonly Dictionary<CardModel, Stack<int>> ActivePlayEnergyValues = new();
	private static readonly Dictionary<CardModel, int> PendingManualPlayEnergyValues = new();
	private static readonly Dictionary<CardModel, Stack<HextechCardPlayResourceSpend>> ActivePlayResourceSpends = new();
	private static readonly Dictionary<CardModel, HextechCardPlayResourceSpend> PendingManualPlayResourceSpends = new();

	internal static void ResetTransientCombatState()
	{
		ActivePlayEnergyValues.Clear();
		ActivePlayResourceSpends.Clear();
		ClearPendingManualPlayState();
		PendingInstantDeathDoomKills.Clear();
	}

	internal static void ClearPendingManualPlayState()
	{
		PendingManualPlayEnergyValues.Clear();
		PendingManualPlayResourceSpends.Clear();
	}

	internal static bool TryGetActivePlayEnergyValue(CardModel? card, out decimal energyValue)
	{
		energyValue = 0m;
		if (card == null)
		{
			return false;
		}

		if (!ActivePlayEnergyValues.TryGetValue(card, out Stack<int>? energyValues) || energyValues.Count == 0)
		{
			return false;
		}

		energyValue = energyValues.Peek();
		return true;
	}

	internal static decimal GetEnergyCostForCurrentCardPlay(CardModel card)
	{
		return TryGetActivePlayEnergyValue(card, out decimal energyValue)
			? energyValue
			: card.EnergyCost.GetAmountToSpend();
	}

	internal static HextechCardPlayResourceSpend GetResourceSpendForCurrentCardPlay(CardModel card)
	{
		if (ActivePlayResourceSpends.TryGetValue(card, out Stack<HextechCardPlayResourceSpend>? resourceSpends)
			&& resourceSpends.Count > 0)
		{
			return resourceSpends.Peek();
		}

		return CaptureResourceSpend(card);
	}


	private static HextechCardPlayResourceSpend CaptureResourceSpend(CardModel card)
	{
		int energyToSpend = card.EnergyCost.GetAmountToSpend();
		int starsToSpend = card.Owner == null ? 0 : Math.Max(0, card.GetStarCostWithModifiers());
		if (card.Owner?.PlayerCombatState == null || card.CombatState == null)
		{
			return new HextechCardPlayResourceSpend(Math.Max(0, energyToSpend), starsToSpend);
		}

		int currentEnergy = card.Owner.PlayerCombatState.Energy;
		if (energyToSpend > currentEnergy && Hook.ShouldPayExcessEnergyCostWithStars(card.CombatState, card.Owner))
		{
			starsToSpend += (energyToSpend - currentEnergy) * 2;
			energyToSpend = currentEnergy;
		}

		return new HextechCardPlayResourceSpend(Math.Max(0, energyToSpend), Math.Max(0, starsToSpend));
	}

	private static async Task<ValueTuple<int, int>> SpendResourcesPreservingStars(CardModel card)
	{
		var owner = card.Owner!;
		var combatState = card.CombatState!;
		var playerCombatState = owner.PlayerCombatState!;
		int energy = playerCombatState.Energy;
		int energyToSpend = card.EnergyCost.GetAmountToSpend();
		int starsToSpend = Math.Max(0, card.GetStarCostWithModifiers());
		if (energyToSpend > energy && Hook.ShouldPayExcessEnergyCostWithStars(combatState, owner))
		{
			starsToSpend += (energyToSpend - energy) * 2;
			energyToSpend = energy;
		}

		if (card.EnergyCost.CostsX)
		{
			card.EnergyCost.CapturedXValue = energyToSpend;
		}

		if (energyToSpend > 0)
		{
			CombatManager.Instance.History.EnergySpent(combatState, energyToSpend, owner);
			playerCombatState.LoseEnergy(Math.Max(0, energyToSpend));
		}

		await Hook.AfterEnergySpent(combatState, card, energyToSpend);
		card.LastStarsSpent = starsToSpend;
		owner.GetRelic<StardustUpgradeRune>()?.Flash();
		return new ValueTuple<int, int>(energyToSpend, starsToSpend);
	}


	private static void PushActivePlayEnergyValue(CardModel card, int energyValue)
	{
		if (!ActivePlayEnergyValues.TryGetValue(card, out Stack<int>? energyValues))
		{
			energyValues = new Stack<int>();
			ActivePlayEnergyValues[card] = energyValues;
		}

		energyValues.Push(Math.Max(0, energyValue));
	}

	private static void PushActivePlayResourceSpend(CardModel card, HextechCardPlayResourceSpend resourceSpend)
	{
		if (!ActivePlayResourceSpends.TryGetValue(card, out Stack<HextechCardPlayResourceSpend>? resourceSpends))
		{
			resourceSpends = new Stack<HextechCardPlayResourceSpend>();
			ActivePlayResourceSpends[card] = resourceSpends;
		}

		resourceSpends.Push(resourceSpend);
	}

	private static async Task PopActivePlayEnergyValueWhenDone(CardModel card, PlayerChoiceContext choiceContext, Task task)
	{
		try
		{
			await task;
			await EnsureTransformingSkillLeavesPlayPileInMultiplayer(card, choiceContext);
		}
		finally
		{
			PopActivePlayEnergyValue(card);
			PopActivePlayResourceSpend(card);
		}
	}

	private static Task EnsureTransformingSkillLeavesPlayPileInMultiplayer(CardModel card, PlayerChoiceContext choiceContext)
	{
		if (!HextechRelicBase.IsNetworkMultiplayerRun()
			|| !IsCleanupSensitiveTransformingSkill(card)
			|| card.Owner?.Creature.IsDead == true
			|| card.Pile?.Type != PileType.Play)
		{
			return Task.CompletedTask;
		}

		if (ShouldForceExhaustStuckPlayCard(card))
		{
			return CardCmd.Exhaust(choiceContext, card, false, true);
		}

		return CardPileCmd.Add(card, PileType.Discard);
	}

	private static bool IsCleanupSensitiveTransformingSkill(CardModel card)
	{
		return card is Begone
			or Charge
			or Compact
			or Guards
			or PrimalForce
			or Seance;
	}

	private static bool ShouldForceExhaustStuckPlayCard(CardModel card)
	{
		// (0.8.4 遗忘之魂重做后不再阻止消耗,原 ShouldPreventPlayExhaust 短路已移除。)
		return card.ExhaustOnNextPlay
			|| card.Keywords.Contains(CardKeyword.Exhaust)
			|| card.Owner?.GetRelic<EightPennyGateRune>() != null;
	}

	private static void PopActivePlayEnergyValue(CardModel card)
	{
		if (!ActivePlayEnergyValues.TryGetValue(card, out Stack<int>? energyValues) || energyValues.Count == 0)
		{
			return;
		}

		energyValues.Pop();
		if (energyValues.Count == 0)
		{
			ActivePlayEnergyValues.Remove(card);
		}
	}

	private static void PopActivePlayResourceSpend(CardModel card)
	{
		if (!ActivePlayResourceSpends.TryGetValue(card, out Stack<HextechCardPlayResourceSpend>? resourceSpends) || resourceSpends.Count == 0)
		{
			return;
		}

		resourceSpends.Pop();
		if (resourceSpends.Count == 0)
		{
			ActivePlayResourceSpends.Remove(card);
		}
	}

	[HarmonyPatch(typeof(CardModel), nameof(CardModel.SpendResources), new Type[0])]
	[HextechPatch("combat.spend-resources", "出牌费用记账")]
	private static class SpendResourcesPatch
	{
		[HarmonyPrefix]
		private static bool Prefix(CardModel __instance, ref Task<ValueTuple<int, int>> __result)
		{
			PendingManualPlayEnergyValues[__instance] = __instance.EnergyCost.GetAmountToSpend();
			HextechCardPlayResourceSpend resourceSpend = CaptureResourceSpend(__instance);
			if (!StardustUpgradeRune.ShouldPreserveStars(__instance))
			{
				PendingManualPlayResourceSpends[__instance] = resourceSpend;
				return true;
			}

			PendingManualPlayResourceSpends[__instance] = resourceSpend with { Stars = 0m };
			__result = SpendResourcesPreservingStars(__instance);
			return false;
		}
	}

	[HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper), typeof(PlayerChoiceContext), typeof(Creature), typeof(bool), typeof(ResourceInfo), typeof(bool))]
	[HextechPatch("combat.on-play-wrapper", "出牌费用记账")]
	private static class OnPlayWrapperPatch
	{
		[HarmonyPrefix]
		private static void Prefix(CardModel __instance, ResourceInfo resources, bool isAutoPlay)
		{
			int energyValue = resources.EnergyValue;
			if (PendingManualPlayEnergyValues.Remove(__instance, out int pendingEnergyValue))
			{
				energyValue = pendingEnergyValue;
			}

			// 本次实付能量:自动打出(Hellraiser 等)实际花 0 能量(EnergySpent=0),其 EnergyValue 只是名义费用,
			// 不能用来返还,否则免费打的牌会凭空返还能量。非自动打出的牌用 energyValue:对 X 费牌是实付的 X,
			// 对普通牌等于费用(EnergySpent 对 X 费牌会退化成 1,故不用它)。星费仍走精确路径(下方 pending 覆盖)。
			int spentEnergy = isAutoPlay ? Math.Max(0, resources.EnergySpent) : Math.Max(0, energyValue);
			HextechCardPlayResourceSpend resourceSpend = new(
				spentEnergy,
				Math.Max(0, resources.StarsSpent));
			if (PendingManualPlayResourceSpends.Remove(__instance, out HextechCardPlayResourceSpend pendingResourceSpend))
			{
				resourceSpend = pendingResourceSpend;
			}

			PushActivePlayEnergyValue(__instance, energyValue);
			PushActivePlayResourceSpend(__instance, resourceSpend);
		}

		[HarmonyPostfix]
		private static void Postfix(CardModel __instance, PlayerChoiceContext choiceContext, ref Task __result)
		{
			__result = PopActivePlayEnergyValueWhenDone(__instance, choiceContext, __result);
		}
	}
}
