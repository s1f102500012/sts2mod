using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Rewards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models.Relics;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

internal static class HextechRewardSafetyHooks
{
	private const string PaelsWingSacrificeAlternativeId = "SACRIFICE";
	private static readonly ConditionalWeakTable<CardReward, CardRewardCompatibilityState> CardRewardStates = new();

	public static void Install(Harmony harmony)
	{
		harmony.Patch(
			RequireMethod(typeof(CardRewardAlternative), nameof(CardRewardAlternative.Generate), BindingFlags.Public | BindingFlags.Static, typeof(CardReward)),
			prefix: new HarmonyMethod(typeof(HextechRewardSafetyHooks), nameof(CardRewardAlternativeGeneratePrefix)));
		harmony.Patch(
			RequireMethod(typeof(Reward), nameof(Reward.FromSerializable), BindingFlags.Public | BindingFlags.Static, typeof(SerializableReward), typeof(Player)),
			postfix: new HarmonyMethod(typeof(HextechRewardSafetyHooks), nameof(RewardFromSerializablePostfix)));
		harmony.Patch(
			RequireMethod(typeof(Reward), nameof(Reward.SelectUnsynchronized), BindingFlags.Instance | BindingFlags.Public),
			prefix: new HarmonyMethod(typeof(HextechRewardSafetyHooks), nameof(RewardSelectUnsynchronizedPrefix)),
			postfix: new HarmonyMethod(typeof(HextechRewardSafetyHooks), nameof(RewardSelectUnsynchronizedPostfix)));
		harmony.Patch(
			RequireMethod(typeof(EventOption), nameof(EventOption.Chosen), BindingFlags.Instance | BindingFlags.Public),
			prefix: new HarmonyMethod(typeof(HextechRewardSafetyHooks), nameof(EventOptionChosenPrefix)),
			postfix: new HarmonyMethod(typeof(HextechRewardSafetyHooks), nameof(EventOptionChosenPostfix)));
		harmony.Patch(
			RequireMethod(typeof(DustyTome), nameof(DustyTome.AfterObtained), BindingFlags.Instance | BindingFlags.Public),
			prefix: new HarmonyMethod(typeof(HextechRewardSafetyHooks), nameof(DustyTomeAfterObtainedPrefix)));
		harmony.Patch(
			RequireMethod(typeof(CardReward), "OnSelect", BindingFlags.Instance | BindingFlags.NonPublic),
			prefix: new HarmonyMethod(typeof(HextechRewardSafetyHooks), nameof(CardRewardOnSelectPrefix)),
			postfix: new HarmonyMethod(typeof(HextechRewardSafetyHooks), nameof(CardRewardOnSelectPostfix)));
		harmony.Patch(
			RequireMethod(typeof(SpecialCardReward), "OnSelect", BindingFlags.Instance | BindingFlags.NonPublic),
			prefix: new HarmonyMethod(typeof(HextechRewardSafetyHooks), nameof(SpecialCardRewardOnSelectPrefix)),
			postfix: new HarmonyMethod(typeof(HextechRewardSafetyHooks), nameof(SpecialCardRewardOnSelectPostfix)));
		harmony.Patch(
			RequireMethod(typeof(RelicReward), "OnSelect", BindingFlags.Instance | BindingFlags.NonPublic),
			prefix: new HarmonyMethod(typeof(HextechRewardSafetyHooks), nameof(RelicRewardOnSelectPrefix)),
			postfix: new HarmonyMethod(typeof(HextechRewardSafetyHooks), nameof(RelicRewardOnSelectPostfix)));
		harmony.Patch(
			RequireMethod(typeof(CardPileCmd), nameof(CardPileCmd.Add), BindingFlags.Public | BindingFlags.Static, typeof(CardModel), typeof(PileType), typeof(CardPilePosition), typeof(AbstractModel), typeof(bool)),
			postfix: new HarmonyMethod(typeof(HextechRewardSafetyHooks), nameof(CardPileAddPostfix)));
		harmony.Patch(
			RequireMethod(typeof(RelicCmd), nameof(RelicCmd.Obtain), BindingFlags.Public | BindingFlags.Static, typeof(RelicModel), typeof(Player), typeof(int)),
			prefix: new HarmonyMethod(typeof(HextechRewardSafetyHooks), nameof(RelicCmdObtainPrefix)) { priority = Priority.High },
			postfix: new HarmonyMethod(typeof(HextechRewardSafetyHooks), nameof(RelicCmdObtainPostfix)));
		harmony.Patch(
			RequireMethod(typeof(PotionCmd), nameof(PotionCmd.TryToProcure), BindingFlags.Public | BindingFlags.Static, typeof(PotionModel), typeof(Player), typeof(int)),
			prefix: new HarmonyMethod(typeof(HextechRewardSafetyHooks), nameof(PotionCmdTryToProcurePrefix)),
			postfix: new HarmonyMethod(typeof(HextechRewardSafetyHooks), nameof(PotionCmdTryToProcurePostfix)));
		harmony.Patch(
			RequireMethod(typeof(PlayerCmd), nameof(PlayerCmd.GainGold), BindingFlags.Public | BindingFlags.Static, typeof(decimal), typeof(Player), typeof(bool)),
			prefix: new HarmonyMethod(typeof(HextechRewardSafetyHooks), nameof(PlayerCmdGainGoldPrefix)),
			postfix: new HarmonyMethod(typeof(HextechRewardSafetyHooks), nameof(PlayerCmdGainGoldPostfix)));
	}

	private static bool CardRewardAlternativeGeneratePrefix(CardReward cardReward, ref IReadOnlyList<CardRewardAlternative> __result)
	{
		__result = GenerateCardRewardAlternativesWithoutVanillaLimit(cardReward);
		return false;
	}

	private static IReadOnlyList<CardRewardAlternative> GenerateCardRewardAlternativesWithoutVanillaLimit(CardReward cardReward)
	{
		List<CardRewardAlternative> alternatives = [];
		if (cardReward.CanSkip)
		{
			alternatives.Add(new CardRewardAlternative("Skip", PostAlternateCardRewardAction.EndSelectionAndDoNotCompleteReward));
		}

		if (cardReward.CanReroll)
		{
			alternatives.Add(CreateDriftwoodRerollAlternative(cardReward));
		}
		else if (GetRemainingDriftwoodRerolls(cardReward) > 0)
		{
			alternatives.Add(CreateDriftwoodRerollAlternative(cardReward));
		}

		Hook.ModifyCardRewardAlternatives(cardReward.Player.RunState, cardReward.Player, cardReward, alternatives);
		IReadOnlyList<CardRewardAlternative> normalized = NormalizeCardRewardAlternativesForCompatibility(alternatives);
		CardRewardCompatibilityState state = CardRewardStates.GetOrCreateValue(cardReward);
		state.Alternatives.Clear();
		state.Alternatives.AddRange(normalized);
		return state.Alternatives;
	}

	private static CardRewardAlternative CreateDriftwoodRerollAlternative(CardReward cardReward)
	{
		return new CardRewardAlternative("REROLL", () =>
		{
			ConsumeDriftwoodReroll(cardReward);
			cardReward.Reroll();
			return Task.CompletedTask;
		}, PostAlternateCardRewardAction.DoNothing);
	}

	private static int GetRemainingDriftwoodRerolls(CardReward cardReward)
	{
		if (CardRewardStates.TryGetValue(cardReward, out CardRewardCompatibilityState? state)
			&& state.RemainingDriftwoodRerolls.HasValue)
		{
			return Math.Max(0, state.RemainingDriftwoodRerolls.Value);
		}

		if (!cardReward.CanReroll)
		{
			return 0;
		}

		int driftwoodCount = CountOwnedDriftwood(cardReward.Player);
		if (driftwoodCount <= 0)
		{
			return 0;
		}

		state = CardRewardStates.GetOrCreateValue(cardReward);
		state.RemainingDriftwoodRerolls = driftwoodCount;
		return driftwoodCount;
	}

	private static void ConsumeDriftwoodReroll(CardReward cardReward)
	{
		int remaining = GetRemainingDriftwoodRerolls(cardReward);
		if (remaining <= 0)
		{
			return;
		}

		CardRewardCompatibilityState state = CardRewardStates.GetOrCreateValue(cardReward);
		state.RemainingDriftwoodRerolls = remaining - 1;
	}

	private static int CountOwnedDriftwood(Player player)
	{
		return player.Relics.Count(static relic => relic is Driftwood);
	}

	internal static IReadOnlyList<CardRewardAlternative> NormalizeCardRewardAlternativesForCompatibility(
		IReadOnlyList<CardRewardAlternative> alternatives)
	{
		CardRewardAlternative[] sacrificeAlternatives = alternatives
			.Where(static alternative => IsPaelsWingSacrificeAlternative(alternative))
			.ToArray();

		if (sacrificeAlternatives.Length <= 1)
		{
			return alternatives;
		}

		List<CardRewardAlternative> normalized = new(alternatives.Count - sacrificeAlternatives.Length + 1);
		bool addedMergedSacrifice = false;
		foreach (CardRewardAlternative alternative in alternatives)
		{
			if (!IsPaelsWingSacrificeAlternative(alternative))
			{
				normalized.Add(alternative);
				continue;
			}

			if (addedMergedSacrifice)
			{
				continue;
			}

			normalized.Add(CreateMergedPaelsWingSacrificeAlternative(sacrificeAlternatives));
			addedMergedSacrifice = true;
		}

		return normalized;
	}

	private static bool IsPaelsWingSacrificeAlternative(CardRewardAlternative alternative)
	{
		return string.Equals(alternative.OptionId, PaelsWingSacrificeAlternativeId, StringComparison.OrdinalIgnoreCase);
	}

	private static CardRewardAlternative CreateMergedPaelsWingSacrificeAlternative(
		IReadOnlyList<CardRewardAlternative> sacrificeAlternatives)
	{
		return new CardRewardAlternative(
			PaelsWingSacrificeAlternativeId,
			async () =>
			{
				foreach (CardRewardAlternative alternative in sacrificeAlternatives)
				{
					await alternative.OnSelect();
				}
			},
			PostAlternateCardRewardAction.EndSelectionAndCompleteReward);
	}

	private sealed class CardRewardCompatibilityState
	{
		public List<CardRewardAlternative> Alternatives { get; } = [];

		public int? RemainingDriftwoodRerolls { get; set; }
	}

	private static void RewardSelectUnsynchronizedPrefix(out object? __state)
	{
		__state = DoubleVisionRune.BeginRewardCommandSuppression();
	}

	private static void RewardSelectUnsynchronizedPostfix(object? __state)
	{
		DoubleVisionRune.CompleteRewardCommandSuppression(__state);
	}

	private static void EventOptionChosenPrefix(EventOption __instance, out object? __state)
	{
		__state = DoubleVisionRune.BeginEventOptionRelicTransaction(__instance);
	}

	private static void EventOptionChosenPostfix(object? __state, ref Task __result)
	{
		__result = DoubleVisionRune.CompleteEventOptionRelicTransactionAsync(__result, __state);
	}

	internal static bool DustyTomeAfterObtainedPrefix(DustyTome __instance, ref Task __result)
	{
		if (!DoubleVisionRune.ShouldSuppressDustyTomeAfterObtained(__instance))
		{
			return true;
		}

		__result = Task.CompletedTask;
		return false;
	}

	// 承载 OnSelect 前后所需状态:DoubleVision 的追踪 scope + 进入 OnSelect 前的卡数(供禁忌魔典判别是否真选走了卡)。
	private sealed record CardRewardOnSelectState(object? DoubleVisionScope, int CardCountBeforeSelect);

	private static void CardRewardOnSelectPrefix(CardReward __instance, out object? __state)
	{
		__state = new CardRewardOnSelectState(
			DoubleVisionRune.BeginCardRewardTracking(__instance.Player),
			__instance.Cards.Count());
	}

	private static void CardRewardOnSelectPostfix(CardReward __instance, object? __state, ref Task<bool> __result)
	{
		CardRewardOnSelectState? state = __state as CardRewardOnSelectState;
		Task<bool> result = __result;
		if (ShouldApplyForbiddenGrimoire(__instance))
		{
			int cardCountBeforeSelect = state?.CardCountBeforeSelect ?? __instance.Cards.Count();
			result = CompleteForbiddenGrimoireCardRewardAsync(__instance, result, cardCountBeforeSelect);
		}

		__result = DoubleVisionRune.CompleteCardRewardAsync(result, state?.DoubleVisionScope);
	}

	private static void SpecialCardRewardOnSelectPrefix(SpecialCardReward __instance, out object? __state)
	{
		__state = DoubleVisionRune.BeginCardRewardTracking(__instance.Player);
	}

	private static void SpecialCardRewardOnSelectPostfix(object? __state, ref Task<bool> __result)
	{
		__result = DoubleVisionRune.CompleteCardRewardAsync(__result, __state);
	}

	private static void RelicRewardOnSelectPrefix(RelicReward __instance, out object? __state)
	{
		__state = DoubleVisionRune.CaptureRewardDuplicationState(__instance.Player);
	}

	private static void RelicRewardOnSelectPostfix(RelicReward __instance, object? __state, ref Task<bool> __result)
	{
		__result = DoubleVisionRune.CompleteRelicRewardAsync(__instance, __result, __state);
	}

	private static void CardPileAddPostfix(CardModel card, PileType newPileType, AbstractModel? clonedBy, ref Task<CardPileAddResult> __result)
	{
		DoubleVisionRune.TrackCardPileAdd(card, newPileType, clonedBy, ref __result);
	}

	private static void RelicCmdObtainPrefix(Player player, out object? __state)
	{
		__state = DoubleVisionRune.BeginDirectRelicReward(player);
	}

	private static void RelicCmdObtainPostfix(object? __state, ref Task<RelicModel> __result)
	{
		__result = DoubleVisionRune.CompleteDirectRelicRewardAsync(__result, __state);
	}

	private static void PotionCmdTryToProcurePrefix(Player player, out object? __state)
	{
		__state = DoubleVisionRune.BeginDirectPotionReward(player);
	}

	private static void PotionCmdTryToProcurePostfix(object? __state, ref Task<PotionProcureResult> __result)
	{
		__result = DoubleVisionRune.CompleteDirectPotionRewardAsync(__result, __state);
	}

	private static void PlayerCmdGainGoldPrefix(decimal amount, Player player, bool wasStolenBack, out object? __state)
	{
		__state = DoubleVisionRune.BeginDirectGoldReward(player, amount, wasStolenBack);
	}

	private static void PlayerCmdGainGoldPostfix(object? __state, ref Task __result)
	{
		__result = DoubleVisionRune.CompleteDirectGoldRewardAsync(__result, __state);
	}

	private static async Task<bool> CompleteForbiddenGrimoireCardRewardAsync(CardReward reward, Task<bool> originalTask, int cardCountBeforeSelect)
	{
		bool rewardComplete = await originalTask;
		if (!rewardComplete || !ShouldApplyForbiddenGrimoire(reward))
		{
			return rewardComplete;
		}

		// 只在"确实选走了至少一张卡"时才补发其余未选卡。献祭(佩尔之翼)/锻体(百炼成钢)等走
		// EndSelectionAndCompleteReward 的 alternative 会让 rewardComplete=true 却不从 _cards 移除任何卡
		// (选前==选后),照旧补发会把整组卡白送。用"卡数减少"判别精确区分:正常选卡(含帽子戏法/Mayhem 多选)
		// 选后<选前→补发剩余;alternative 选后==选前→不补发。必须用 < 而非 ==(选前-1),否则一次移除多张的
		// 多选卡会被误判而漏发最后一张。判别只读 reward.Cards 计数(reward 状态、两端一致),不引入联机分叉。
		if (reward.Cards.Count() >= cardCountBeforeSelect)
		{
			return rewardComplete;
		}

		List<CardModel> remainingCards = reward.Cards.ToList();
		if (remainingCards.Count == 0)
		{
			return rewardComplete;
		}

		foreach (CardModel card in remainingCards)
		{
			CardPileAddResult result = await CardPileCmd.Add(card, PileType.Deck);
			if (result.success)
			{
				HextechLog.Info($"[{ModInfo.Id}][EnemyForbiddenGrimoire] Forced unpicked card reward: player={reward.Player.NetId} card={result.cardAdded.Id.Entry}");
			}
			else
			{
				Log.Warn($"[{ModInfo.Id}][EnemyForbiddenGrimoire] Failed to force unpicked card reward: player={reward.Player.NetId} card={card.Id.Entry}", 2);
			}
		}

		return rewardComplete;
	}

	private static bool ShouldApplyForbiddenGrimoire(CardReward reward)
	{
		Player player = reward.Player;
		return player.RunState is RunState runState
			&& !player.Creature.IsDead
			&& runState.Modifiers.OfType<HextechMayhemModifier>().LastOrDefault() is HextechMayhemModifier modifier
			&& modifier.HasActiveMonsterHex(MonsterHexKind.ForbiddenGrimoire);
	}

	private static void RewardFromSerializablePostfix(SerializableReward save, Player player, ref Reward __result)
	{
		if (save.RewardType == RewardType.Gold && save.GoldAmount < 0 && __result is GoldReward)
		{
			__result = new GoldReward(0, player, save.WasGoldStolenBack);
			Log.Warn($"[{ModInfo.Id}][Rewards] Repaired serialized gold reward with negative amount {save.GoldAmount}; defaulting to 0 gold.");
			return;
		}

		if (save.RewardType == RewardType.Relic
			&& save.WasGoldStolenBack
			&& save.PredeterminedModelId != ModelId.none)
		{
			RelicModel relic = ModelDb.GetById<RelicModel>(save.PredeterminedModelId).ToMutable();
			__result = new HextechWaxRelicReward(relic, player);
			return;
		}

		if (save.RewardType == RewardType.Card
			&& save.CustomDescriptionEncounterSourceId == ModelDb.GetId<ColorDiscoveryRune>()
			&& save.PredeterminedModelId != ModelId.none)
		{
			__result = ColorDiscoveryCardReward.FromSavedReward(save, player);
			return;
		}

		if (save.RewardType == RewardType.SpecialCard
			&& save.PredeterminedModelId == ModelDb.GetId<ColorDiscoveryRune>()
			&& save.SpecialCard != null)
		{
			__result = ColorDiscoveryCardReward.FromSavedSpecialCardReward(save, __result, player);
			return;
		}

		if (save.CustomDescriptionEncounterSourceId == ModelDb.GetId<RandomForgeShopRelic>()
			&& save.CardPoolIds.Count > 0)
		{
			__result = HextechForgeChoiceReward.FromSavedReward(save, player);
		}
	}
}
