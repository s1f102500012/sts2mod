using HarmonyLib;
using MegaCrit.Sts2.Core.Models.Afflictions;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Nodes.Cards;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

/// <summary>
/// 将开局自动打出的多张同类"形态"牌合并为一个逻辑出牌批次。
/// </summary>
/// <remarks>
/// 代表牌(有流电就选流电牌)完整走原版 <see cref="CardCmd.AutoPlay"/>:ShouldPlay、BeforeCardAutoPlayed、
/// 出牌次数、结果位置、Before/AfterCardPlayed 全部原样,第三方监听器看到的就是一次真实出牌。
/// 其余形态牌只允许裸牌或仅克隆附魔(克隆已折算进出牌次数),它们的贡献是纯数值,
/// 汇总后用 <see cref="PowerCmd"/> 直接施加,再按官方 <c>ModifyCardPlayResultLocation</c> 逐张送去各自的结果堆。
/// 含其他附魔或未知负面附着的批次回退到原版逐张自动打出,事件也逐张如实发出。
/// 因此这里不再拦截任何 <c>Hook.*</c> 分发点;剩下的补丁只有:虚空形态自带的 EndTurn 在批次内压掉、
/// 进场横向展开的落点偏移、以及用一组并行飞行动画代替逐张的内置动画。
/// </remarks>
internal static class HextechFormAutoPlayHooks
{
	private static readonly AsyncLocal<bool> SuppressEndTurn = new();
	private static readonly AsyncLocal<HextechFormAutoPlayBatchState?> ActiveBatch = new();
	private static readonly MethodInfo GeneratePlayCountMethod = RequireMethod(
		typeof(CardModel),
		"GeneratePlayCount",
		BindingFlags.Instance | BindingFlags.NonPublic,
		typeof(ICombatState),
		typeof(Creature));

	internal static IDisposable BeginEndTurnSuppression()
	{
		return new SuppressionScope();
	}

	internal static IDisposable BeginCardPlayBatch(IEnumerable<CardModel> cards)
	{
		return new CardPlayBatchScope(cards);
	}

	internal static async Task PlayCardBatchVfx(IReadOnlyList<CardModel> cards)
	{
		HextechFormAutoPlayBatchState? batch = ActiveBatch.Value;
		if (batch == null || cards.Count < 2)
		{
			return;
		}

		List<(CardModel Card, NCard Node)> visuals = [];
		foreach (CardModel card in cards)
		{
			NCard? node = NCard.FindOnTable(card);
			if (node != null)
			{
				visuals.Add((card, node));
			}
		}

		if (visuals.Count < 2)
		{
			return;
		}

		// 横向落点已在原版进场补间取目标坐标时注入;这里只留出阅读时间,再同时飞向角色。
		await Cmd.CustomScaledWait(0.2f, 0.35f);
		using (batch.BeginPowerCardFlyVfxPreview(visuals.Select(entry => entry.Card)))
		{
			Task[] tasks = visuals
				.Select(entry => InvokePowerCardFlyVfx(entry.Card))
				.ToArray();
			try
			{
				await Task.WhenAll(tasks);
			}
			catch (Exception ex)
			{
				// 视觉失败不能中断形态牌结算;后续逻辑仍按同一确定性批次执行。
				Log.Warn($"[{ModInfo.Id}][FormBatch] Group power-card VFX failed: {ex.GetType().Name}: {ex.Message}");
			}
		}
	}

	/// <summary>
	/// 合并结算。返回 false 表示本批次不满足合并条件,调用方回退到逐张自动打出。
	/// </summary>
	internal static async Task<bool> TryPlayCombinedFinalEffect(
		PlayerChoiceContext choiceContext,
		IReadOnlyList<CardModel> cards)
	{
		HextechFormAutoPlayBatchState? batch = ActiveBatch.Value;
		if (batch == null
			|| cards.Count < 2
			|| cards.Any(card => card.Pile?.Type != PileType.Play)
			|| !CanCombine(cards))
		{
			return false;
		}

		ICombatState? combatState = cards[0].CombatState ?? cards[0].Owner.Creature.CombatState;
		if (combatState == null)
		{
			return false;
		}

		foreach (CardModel card in cards)
		{
			if (card.Keywords.Contains(CardKeyword.Unplayable)
				|| !Hook.ShouldPlay(combatState, card, out _, AutoPlayType.Default))
			{
				return false;
			}
		}

		// 流电只在卡牌打出事件上工作,自身 OnPlay 为空;选它作为代表牌可保证整批仍只触发一次电击。
		CardModel primary = SelectPrimary(cards);
		List<CardModel> secondaries = cards.Where(card => !ReferenceEquals(card, primary)).ToList();

		// 次要牌:先按官方 Hook 逐张解析结果位置,再按各自的出牌次数(含第三方修饰器)折算贡献。
		Dictionary<CardModel, HextechFormCardResult> results = new(ReferenceEqualityComparer.Instance);
		List<(decimal Amount, int PlayCount)> contributions = [];
		foreach (CardModel card in secondaries)
		{
			results.Add(card, await ResolveResultLocation(combatState, card, CreateAutoPlayResources(card)));
			contributions.Add((GetFormAmount(card), await GeneratePlayCount(card, combatState)));
		}

		decimal secondaryAmount = SumSecondaryContribution(contributions);
		HextechLog.Info($"[{ModInfo.Id}][FormBatch] Combining {cards.Count}x {primary.GetType().Name}: primary plays via vanilla AutoPlay, secondary contribution={secondaryAmount}");

		// 代表牌完整走原版自动打出:它自己的数值 × 自己的出牌次数由原版结算,事件只发这一次。
		await CardCmd.AutoPlay(choiceContext, primary, target: null, AutoPlayType.Default, skipXCapture: false, skipCardPileVisuals: true);

		if (secondaryAmount > 0m)
		{
			await ApplySecondaryFormEffect(choiceContext, primary, secondaryAmount);
		}

		await MoveSecondaryCardsToResults(choiceContext, secondaries, results);
		return true;
	}

	internal static CardModel SelectPrimary(IReadOnlyList<CardModel> cards)
	{
		return cards.FirstOrDefault(card => card.Affliction is Galvanized) ?? cards[0];
	}

	/// <summary>次要牌贡献 = Σ(形态数值 × 出牌次数);出牌次数为 0 的牌不贡献。</summary>
	internal static decimal SumSecondaryContribution(IEnumerable<(decimal Amount, int PlayCount)> contributions)
	{
		decimal total = 0m;
		foreach ((decimal amount, int playCount) in contributions)
		{
			if (playCount > 0)
			{
				total += amount * playCount;
			}
		}

		return total;
	}

	private static MethodInfo GetPlayPowerCardFlyVfxMethod()
	{
		return RequireMethod(typeof(CardModel), "PlayPowerCardFlyVfx", BindingFlags.Instance | BindingFlags.NonPublic);
	}

	private static Task InvokePowerCardFlyVfx(CardModel card)
	{
		return (Task)(GetPlayPowerCardFlyVfxMethod().Invoke(card, null) ?? Task.CompletedTask);
	}

	private static Type[] GetSupportedFormTypes()
	{
		return
		[
			typeof(DemonForm),
			typeof(EchoForm),
			typeof(ReaperForm),
			typeof(SerpentForm),
			typeof(VoidForm)
		];
	}

	private static bool CanCombine(IReadOnlyList<CardModel> cards)
	{
		Type firstType = cards[0].GetType();
		return GetSupportedFormTypes().Contains(firstType)
			&& cards.All(card => card.GetType() == firstType
				&& card.Owner == cards[0].Owner
				&& IsCombinedEffectSafeEnchantment(card.Enchantment)
				&& card.Affliction is null or Galvanized);
	}

	internal static bool IsCombinedEffectSafeEnchantment(EnchantmentModel? enchantment)
	{
		return enchantment == null || enchantment is Clone;
	}

	private static ResourceInfo CreateAutoPlayResources(CardModel card)
	{
		return new ResourceInfo
		{
			EnergySpent = 0,
			EnergyValue = card.EnergyCost.GetAmountToSpend(),
			StarsSpent = 0,
			StarValue = Math.Max(0, card.GetStarCostWithModifiers())
		};
	}

	private static async Task<int> GeneratePlayCount(CardModel card, ICombatState combatState)
	{
		return await (Task<int>)(GeneratePlayCountMethod.Invoke(card, [combatState, null])
			?? Task.FromResult(1));
	}

	internal static decimal GetFormAmount(CardModel card)
	{
		return card switch
		{
			DemonForm => card.DynamicVars["StrengthPower"].BaseValue,
			EchoForm => card.DynamicVars["EchoForm"].BaseValue,
			ReaperForm => 1m,
			SerpentForm => card.DynamicVars["SerpentFormPower"].BaseValue,
			VoidForm => card.DynamicVars["VoidFormPower"].BaseValue,
			_ => throw new InvalidOperationException($"Unsupported combined form card: {card.GetType().FullName}")
		};
	}

	private static Task ApplySecondaryFormEffect(
		PlayerChoiceContext choiceContext,
		CardModel card,
		decimal amount)
	{
		Creature owner = card.Owner.Creature;
		return card switch
		{
			DemonForm => PowerCmd.Apply<DemonFormPower>(choiceContext, owner, amount, owner, card),
			EchoForm => PowerCmd.Apply<EchoFormPower>(choiceContext, owner, amount, owner, card),
			ReaperForm => PowerCmd.Apply<ReaperFormPower>(choiceContext, owner, amount, owner, card),
			SerpentForm => PowerCmd.Apply<SerpentFormPower>(choiceContext, owner, amount, owner, card),
			VoidForm => PowerCmd.Apply<VoidFormPower>(choiceContext, owner, amount, owner, card),
			_ => throw new InvalidOperationException($"Unsupported combined form card: {card.GetType().FullName}")
		};
	}

	private static async Task<HextechFormCardResult> ResolveResultLocation(
		ICombatState combatState,
		CardModel card,
		ResourceInfo resources)
	{
#if STS2_109_OR_NEWER
		CardLocation location = Hook.ModifyCardPlayResultLocation(
			combatState,
			card,
			isAutoPlay: true,
			resources,
			new CardLocation(card.Owner, PileType.None, CardPilePosition.Bottom),
			out IEnumerable<AbstractModel> modifiers);
		foreach (AbstractModel modifier in modifiers)
		{
			await modifier.AfterModifyingCardPlayResultLocation(card, location);
		}
		return new HextechFormCardResult(location.player, location.pileType, location.position);
#else
		(PileType pileType, CardPilePosition position) = Hook.ModifyCardPlayResultPileTypeAndPosition(
			combatState,
			card,
			isAutoPlay: true,
			resources,
			PileType.None,
			CardPilePosition.Bottom,
			out IEnumerable<AbstractModel> modifiers);
		foreach (AbstractModel modifier in modifiers)
		{
			await modifier.AfterModifyingCardPlayResultPileOrPosition(card, pileType, position);
		}
		return new HextechFormCardResult(card.Owner, pileType, position);
#endif
	}

	private static async Task MoveSecondaryCardsToResults(
		PlayerChoiceContext choiceContext,
		IEnumerable<CardModel> cards,
		IReadOnlyDictionary<CardModel, HextechFormCardResult> results)
	{
		List<CardModel> removedCards = [];
		foreach (CardModel card in cards)
		{
			if (card.Pile?.Type != PileType.Play)
			{
				continue;
			}

			HextechFormCardResult result = results[card];
#if STS2_109_OR_NEWER
			if (result.Player != card.Owner && result.PileType != PileType.None)
			{
				await CardPileCmd.GiveToAnotherPlayer(card, result.Player, result.PileType, result.Position);
				continue;
			}
#endif

			switch (result.PileType)
			{
			case PileType.None:
				removedCards.Add(card);
				break;
			case PileType.Exhaust:
				await CardCmd.Exhaust(choiceContext, card, causedByEthereal: false, skipVisuals: true);
				break;
			default:
				await CardPileCmd.Add(card, result.PileType, result.Position, clonedBy: null, skipVisuals: true);
				break;
			}
		}

		if (removedCards.Count > 0)
		{
			await CardPileCmd.RemoveFromCombat(removedCards, skipVisuals: true);
		}
	}

	private sealed class SuppressionScope : IDisposable
	{
		private readonly bool _previous;

		public SuppressionScope()
		{
			_previous = SuppressEndTurn.Value;
			SuppressEndTurn.Value = true;
		}

		public void Dispose()
		{
			SuppressEndTurn.Value = _previous;
		}
	}

	private sealed class CardPlayBatchScope : IDisposable
	{
		private readonly HextechFormAutoPlayBatchState? _previous;

		public CardPlayBatchScope(IEnumerable<CardModel> cards)
		{
			_previous = ActiveBatch.Value;
			ActiveBatch.Value = new HextechFormAutoPlayBatchState(cards);
		}

		public void Dispose()
		{
			ActiveBatch.Value = _previous;
		}
	}

	// 虚空形态 OnPlay 自带 EndTurn;批次作用域内压掉,不吃掉首回合。作用域由 AsyncLocal 限定,手动出牌不受影响。
	[HarmonyPatch(typeof(PlayerCmd), nameof(PlayerCmd.EndTurn), typeof(Player), typeof(bool), typeof(Func<Task>))]
	[HextechPatch("combat.form-auto-play.end-turn", "形态开局自动打出批处理")]
	private static class EndTurnPatch
	{
		[HarmonyPrefix]
		[HarmonyPriority(Priority.Low)]
		private static bool Prefix()
		{
			return !SuppressEndTurn.Value;
		}
	}

	// 批次内的牌已经用一组并行飞行动画代替了逐张的内置动画。
	[HarmonyPatch(typeof(CardModel), "PlayPowerCardFlyVfx")]
	[HextechPatch("combat.form-auto-play.fly-vfx", "形态开局自动打出批处理")]
	private static class PowerCardFlyVfxPatch
	{
		[HarmonyPrefix]
		[HarmonyPriority(Priority.Low)]
		private static bool Prefix(CardModel __instance, ref Task __result)
		{
			HextechFormAutoPlayBatchState? batch = ActiveBatch.Value;
			if (batch == null || batch.ShouldPlayPowerCardFlyVfx(__instance))
			{
				return true;
			}

			__result = Task.CompletedTask;
			return false;
		}
	}

	// 进场时把整批横向展开,落点在原版取目标坐标时叠加偏移。
	[HarmonyPatch(typeof(PileTypeExtensions), nameof(PileTypeExtensions.GetTargetPosition), typeof(PileType), typeof(NCard))]
	[HextechPatch("combat.form-auto-play.entry-offset", "形态开局自动打出批处理")]
	private static class CardTargetPositionPatch
	{
		[HarmonyPostfix]
		[HarmonyPriority(Priority.Last)]
		private static void Postfix(PileType pileType, NCard? node, ref Godot.Vector2 __result)
		{
			if (pileType != PileType.Play || node == null)
			{
				return;
			}

			HextechFormAutoPlayBatchState? batch = ActiveBatch.Value;
			if (batch != null
				&& node.Model != null
				&& batch.TryGetHorizontalOffset(node.Model, out float offset))
			{
				__result.X += offset;
			}
		}
	}
}

internal sealed class HextechFormAutoPlayBatchState
{
	private readonly HashSet<CardModel> _cards;
	private readonly Dictionary<CardModel, float> _horizontalOffsets = new(ReferenceEqualityComparer.Instance);
	private readonly HashSet<CardModel> _batchVfxCards = new(ReferenceEqualityComparer.Instance);
	private bool _isPlayingBatchVfx;

	internal HextechFormAutoPlayBatchState(IEnumerable<CardModel> cards)
	{
		List<CardModel> cardList = cards.ToList();
		_cards = new HashSet<CardModel>(cardList, ReferenceEqualityComparer.Instance);
		if (cardList.Count < 2)
		{
			return;
		}

		float spacing = Math.Min(190f, 760f / (cardList.Count - 1));
		float centerIndex = (cardList.Count - 1) * 0.5f;
		for (int index = 0; index < cardList.Count; index++)
		{
			_horizontalOffsets[cardList[index]] = (index - centerIndex) * spacing;
		}
	}

	internal bool Contains(CardModel card) => _cards.Contains(card);

	internal bool TryGetHorizontalOffset(CardModel card, out float offset)
	{
		return _horizontalOffsets.TryGetValue(card, out offset);
	}

	internal bool ShouldPlayPowerCardFlyVfx(CardModel card)
	{
		if (!_cards.Contains(card))
		{
			return true;
		}

		return _isPlayingBatchVfx || !_batchVfxCards.Contains(card);
	}

	internal IDisposable BeginPowerCardFlyVfxPreview(IEnumerable<CardModel> cards)
	{
		foreach (CardModel card in cards)
		{
			_batchVfxCards.Add(card);
		}

		return new PowerCardFlyVfxPreviewScope(this);
	}

	private sealed class PowerCardFlyVfxPreviewScope : IDisposable
	{
		private readonly HextechFormAutoPlayBatchState _owner;
		private readonly bool _previous;

		public PowerCardFlyVfxPreviewScope(HextechFormAutoPlayBatchState owner)
		{
			_owner = owner;
			_previous = owner._isPlayingBatchVfx;
			owner._isPlayingBatchVfx = true;
		}

		public void Dispose()
		{
			_owner._isPlayingBatchVfx = _previous;
		}
	}
}

internal readonly record struct HextechFormCardResult(
	Player Player,
	PileType PileType,
	CardPilePosition Position);
