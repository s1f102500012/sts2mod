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


	// 承载 OnSelect 前后所需状态:DoubleVision 的追踪 scope + 进入 OnSelect 前的卡数(供禁忌魔典判别是否真选走了卡)。
	private sealed record CardRewardOnSelectState(object? DoubleVisionScope, int CardCountBeforeSelect);


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


	internal static bool TryRestoreForgeChoiceReward(SerializableReward save, Player player, ref Reward result)
	{
		if (save.RewardType != RewardType.Gold
			|| result is not GoldReward
			|| save.CustomDescriptionEncounterSourceId != ModelDb.GetId<RandomForgeShopRelic>()
			|| save.CardPoolIds.Count == 0
			|| !HextechForgeChoiceReward.TryFromSavedReward(save, player, out HextechForgeChoiceReward? restored)
			|| restored == null)
		{
			return false;
		}

		result = restored;
		return true;
	}

	[HarmonyPatch(typeof(CardRewardAlternative), nameof(CardRewardAlternative.Generate), typeof(CardReward))]
	[HextechPatch("reward.card-alternatives", "卡牌奖励备选项")]
	private static class CardRewardAlternativesPatch
	{
		[HarmonyPrefix]
		[HarmonyPriority(Priority.Low)]
		private static bool Prefix(CardReward cardReward, ref IReadOnlyList<CardRewardAlternative> __result)
		{
			__result = GenerateCardRewardAlternativesWithoutVanillaLimit(cardReward);
			return false;
		}
	}

	[HarmonyPatch(typeof(Reward), nameof(Reward.FromSerializable), typeof(SerializableReward), typeof(Player))]
	[HextechPatch("reward.from-serializable", "奖励序列化恢复")]
	private static class RewardFromSerializablePatch
	{
		[HarmonyPostfix]
		private static void Postfix(SerializableReward save, Player player, ref Reward __result)
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
				&& save.SpecialCard != null
				&& ColorDiscoveryCardReward.TryFromSavedSpecialCardReward(
					save,
					__result,
					player,
					out ColorDiscoveryCardReward? restoredColorDiscoveryReward)
				&& restoredColorDiscoveryReward != null)
			{
				__result = restoredColorDiscoveryReward;
				return;
			}

			TryRestoreForgeChoiceReward(save, player, ref __result);
		}
	}

	[HarmonyPatch(typeof(Reward), nameof(Reward.SelectUnsynchronized), new Type[0])]
	[HextechPatch("reward.select-unsynchronized", "复视奖励事务")]
	private static class RewardSelectUnsynchronizedPatch
	{
		[HarmonyPrefix]
		private static void Prefix(out object? __state)
		{
			__state = DoubleVisionRune.BeginRewardCommandSuppression();
		}

		[HarmonyPostfix]
		private static void Postfix(object? __state)
		{
			DoubleVisionRune.CompleteRewardCommandSuppression(__state);
		}
	}

	[HarmonyPatch(typeof(EventOption), nameof(EventOption.Chosen), new Type[0])]
	[HextechPatch("reward.event-option", "复视奖励事务")]
	private static class EventOptionChosenPatch
	{
		[HarmonyPrefix]
		private static void Prefix(EventOption __instance, out object? __state)
		{
			__state = DoubleVisionRune.BeginEventOptionRelicTransaction(__instance);
		}

		[HarmonyPostfix]
		private static void Postfix(object? __state, ref Task __result)
		{
			__result = DoubleVisionRune.CompleteEventOptionRelicTransactionAsync(__result, __state);
		}
	}

	[HarmonyPatch(typeof(DustyTome), nameof(DustyTome.AfterObtained), new Type[0])]
	[HextechPatch("reward.dusty-tome", "复视奖励事务")]
	internal static class DustyTomePatch
	{
		[HarmonyPrefix]
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(DustyTome __instance, ref Task __result)
		{
			if (!DoubleVisionRune.ShouldSuppressDustyTomeAfterObtained(__instance))
			{
				return true;
			}

			__result = Task.CompletedTask;
			return false;
		}
	}

	[HarmonyPatch(typeof(CardReward), "OnSelect")]
	[HextechPatch("reward.card-select", "复视奖励事务")]
	private static class CardRewardSelectPatch
	{
		[HarmonyPrefix]
		private static void Prefix(CardReward __instance, out object? __state)
		{
			__state = new CardRewardOnSelectState(
				DoubleVisionRune.BeginCardRewardTracking(__instance.Player),
				__instance.Cards.Count());
		}

		[HarmonyPostfix]
		private static void Postfix(CardReward __instance, object? __state, ref Task<bool> __result)
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
	}

	[HarmonyPatch(typeof(SpecialCardReward), "OnSelect")]
	[HextechPatch("reward.special-card-select", "复视奖励事务")]
	private static class SpecialCardRewardSelectPatch
	{
		[HarmonyPrefix]
		private static void Prefix(SpecialCardReward __instance, out object? __state)
		{
			__state = DoubleVisionRune.BeginCardRewardTracking(__instance.Player);
		}

		[HarmonyPostfix]
		private static void Postfix(object? __state, ref Task<bool> __result)
		{
			__result = DoubleVisionRune.CompleteCardRewardAsync(__result, __state);
		}
	}

	[HarmonyPatch(typeof(RelicReward), "OnSelect")]
	[HextechPatch("reward.relic-select", "复视奖励事务")]
	private static class RelicRewardSelectPatch
	{
		[HarmonyPrefix]
		private static void Prefix(RelicReward __instance, out object? __state)
		{
			__state = DoubleVisionRune.CaptureRewardDuplicationState(__instance.Player);
		}

		[HarmonyPostfix]
		private static void Postfix(RelicReward __instance, object? __state, ref Task<bool> __result)
		{
			__result = DoubleVisionRune.CompleteRelicRewardAsync(__instance, __result, __state);
		}
	}

	[HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.Add), typeof(CardModel), typeof(PileType), typeof(CardPilePosition), typeof(AbstractModel), typeof(bool))]
	[HextechPatch("reward.card-pile-add", "复视奖励事务")]
	private static class CardPileAddPatch
	{
		[HarmonyPostfix]
		private static void Postfix(CardModel card, PileType newPileType, AbstractModel? clonedBy, ref Task<CardPileAddResult> __result)
		{
			DoubleVisionRune.TrackCardPileAdd(card, newPileType, clonedBy, ref __result);
		}
	}

	[HarmonyPatch(typeof(RelicCmd), nameof(RelicCmd.Obtain), typeof(RelicModel), typeof(Player), typeof(int))]
	[HextechPatch("reward.relic-obtain", "复视奖励事务")]
	private static class RelicObtainPatch
	{
		[HarmonyPrefix]
		[HarmonyPriority(Priority.High)]
		private static void Prefix(RelicModel relic, Player player, out object? __state)
		{
			__state = DoubleVisionRune.BeginDirectRelicReward(relic, player);
		}

		[HarmonyPostfix]
		private static void Postfix(object? __state, ref Task<RelicModel> __result)
		{
			__result = DoubleVisionRune.CompleteDirectRelicRewardAsync(__result, __state);
		}
	}

	[HarmonyPatch(typeof(PotionCmd), nameof(PotionCmd.TryToProcure), typeof(PotionModel), typeof(Player), typeof(int))]
	[HextechPatch("reward.potion-procure", "复视奖励事务")]
	private static class PotionProcurePatch
	{
		[HarmonyPrefix]
		private static void Prefix(Player player, out object? __state)
		{
			__state = DoubleVisionRune.BeginDirectPotionReward(player);
		}

		[HarmonyPostfix]
		private static void Postfix(object? __state, ref Task<PotionProcureResult> __result)
		{
			__result = DoubleVisionRune.CompleteDirectPotionRewardAsync(__result, __state);
		}
	}

	[HarmonyPatch(typeof(PlayerCmd), nameof(PlayerCmd.GainGold), typeof(decimal), typeof(Player), typeof(bool))]
	[HextechPatch("reward.gain-gold", "复视奖励事务")]
	private static class GainGoldPatch
	{
		[HarmonyPrefix]
		private static void Prefix(decimal amount, Player player, bool wasStolenBack, out object? __state)
		{
			__state = DoubleVisionRune.BeginDirectGoldReward(player, amount, wasStolenBack);
		}

		[HarmonyPostfix]
		private static void Postfix(object? __state, ref Task __result)
		{
			__result = DoubleVisionRune.CompleteDirectGoldRewardAsync(__result, __state);
		}
	}
}
