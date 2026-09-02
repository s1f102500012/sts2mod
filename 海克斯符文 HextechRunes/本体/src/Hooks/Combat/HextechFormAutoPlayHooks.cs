using HarmonyLib;
using MegaCrit.Sts2.Core.Models.Afflictions;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Nodes.Cards;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

/// <summary>
/// 将开局自动打出的多张同类"形态"牌合并为一个逻辑出牌批次:
/// 普通形态牌与仅有克隆附魔的形态牌会合并成一次最终 Power 结算;其他附魔或未知负面附着等
/// 无法证明等价的牌自动回退原版逐张路径。整批仅放行一次 Before/AfterCardPlayed,牌面从进入出牌区起就横向展开,
/// 再并行播放一组能力牌飞行动画。虚空形态 OnPlay 自带的 EndTurn 同样在批次内压掉。
/// 作用域仅覆盖本模组的自动打出窗口,手动出牌和嵌套打出的非批次牌不受影响。
/// </summary>
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
		CardModel primary = cards.FirstOrDefault(card => card.Affliction is Galvanized) ?? cards[0];
		ResourceInfo primaryResources = CreateAutoPlayResources(primary);
		await Hook.BeforeCardAutoPlayed(combatState, primary, null, AutoPlayType.Default);

		Dictionary<CardModel, HextechFormCardResult> results = new(ReferenceEqualityComparer.Instance);
		decimal combinedAmount = 0m;
		foreach (CardModel card in cards)
		{
			ResourceInfo resources = ReferenceEquals(card, primary)
				? primaryResources
				: CreateAutoPlayResources(card);
			HextechFormCardResult result = await ResolveResultLocation(combatState, card, resources);
			results.Add(card, result);

			int playCount = await GeneratePlayCount(card, combatState);
			if (playCount > 0)
			{
				combinedAmount += GetFormAmount(card) * playCount;
			}
		}

		batch.PrepareCombinedResolution(
			primary,
			combinedAmount,
			combinedAmount > 0m ? 1 : 0,
			results[primary]);
		try
		{
			await primary.OnPlayWrapper(
				choiceContext,
				target: null,
				isAutoPlay: true,
				primaryResources,
				skipCardPileVisuals: true);
		}
		finally
		{
			batch.FinishCombinedResolution();
		}

		await MoveSecondaryCardsToResults(
			choiceContext,
			cards.Where(card => !ReferenceEquals(card, primary)),
			results);
		return true;
	}

	private static bool EndTurnPrefix()
	{
		return !SuppressEndTurn.Value;
	}

	private static bool BeforeCardPlayedPrefix(CardPlay cardPlay, ref Task __result)
	{
		return ShouldRunCardPlayedHook(cardPlay, ref __result);
	}

	private static bool AfterCardPlayedPrefix(CardPlay cardPlay, ref Task __result)
	{
		return ShouldRunCardPlayedHook(cardPlay, ref __result);
	}

	private static bool ShouldRunCardPlayedHook(CardPlay cardPlay, ref Task result)
	{
		HextechFormAutoPlayBatchState? batch = ActiveBatch.Value;
		if (batch == null || batch.ShouldDispatchCardPlayedHook(cardPlay))
		{
			return true;
		}

		result = Task.CompletedTask;
		return false;
	}

	private static bool AfterCardChangedPilesPrefix(CardModel card, PileType oldPile, ref Task __result)
	{
		HextechFormAutoPlayBatchState? batch = ActiveBatch.Value;
		if (batch == null || batch.ShouldDispatchCardChangedPilesHook(card, oldPile, card.Pile?.Type ?? PileType.None))
		{
			return true;
		}

		__result = Task.CompletedTask;
		return false;
	}

	private static bool PlayPowerCardFlyVfxPrefix(CardModel __instance, ref Task __result)
	{
		HextechFormAutoPlayBatchState? batch = ActiveBatch.Value;
		if (batch == null || batch.ShouldPlayPowerCardFlyVfx(__instance))
		{
			return true;
		}

		__result = Task.CompletedTask;
		return false;
	}

	private static void GetTargetPositionPostfix(PileType pileType, NCard? node, ref Godot.Vector2 __result)
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

	private static bool ModifyCardPlayCountPrefix(
		CardModel card,
		ref List<AbstractModel> modifyingModels,
		ref int __result)
	{
		HextechFormAutoPlayBatchState? batch = ActiveBatch.Value;
		if (batch == null || !batch.ShouldUsePreparedPlayCount(card))
		{
			return true;
		}

		modifyingModels = [];
		__result = batch.PreparedPlayCount;
		return false;
	}

#if STS2_109_OR_NEWER
	private static bool ModifyCardPlayResultLocationPrefix(
		CardModel card,
		ref IEnumerable<AbstractModel> modifiers,
		ref CardLocation __result)
	{
		HextechFormAutoPlayBatchState? batch = ActiveBatch.Value;
		if (batch == null || !batch.TryGetPreparedResult(card, out HextechFormCardResult result))
		{
			return true;
		}

		modifiers = [];
		__result = new CardLocation(result.Player, result.PileType, result.Position);
		return false;
	}
#else
	private static bool ModifyCardPlayResultPileTypeAndPositionPrefix(
		CardModel card,
		ref IEnumerable<AbstractModel> modifiers,
		ref (PileType, CardPilePosition) __result)
	{
		HextechFormAutoPlayBatchState? batch = ActiveBatch.Value;
		if (batch == null || !batch.TryGetPreparedResult(card, out HextechFormCardResult result))
		{
			return true;
		}

		modifiers = [];
		__result = (result.PileType, result.Position);
		return false;
	}
#endif

	private static bool FormOnPlayPrefix(
		CardModel __instance,
		PlayerChoiceContext choiceContext,
		ref Task __result)
	{
		HextechFormAutoPlayBatchState? batch = ActiveBatch.Value;
		if (batch == null || !batch.TryGetCombinedAmount(__instance, out decimal amount))
		{
			return true;
		}

		__result = ApplyCombinedFormEffect(choiceContext, __instance, amount);
		return false;
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

	private static decimal GetFormAmount(CardModel card)
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

	private static async Task ApplyCombinedFormEffect(
		PlayerChoiceContext choiceContext,
		CardModel card,
		decimal amount)
	{
		await CreatureCmd.TriggerAnim(
			card.Owner.Creature,
			"PowerUp",
			card.Owner.Character.PowerUpAnimDelay);
		switch (card)
		{
		case DemonForm:
			await PowerCmd.Apply<DemonFormPower>(choiceContext, card.Owner.Creature, amount, card.Owner.Creature, card);
			break;
		case EchoForm:
			await PowerCmd.Apply<EchoFormPower>(choiceContext, card.Owner.Creature, amount, card.Owner.Creature, card);
			break;
		case ReaperForm:
			await PowerCmd.Apply<ReaperFormPower>(choiceContext, card.Owner.Creature, amount, card.Owner.Creature, card);
			break;
		case SerpentForm:
			await PowerCmd.Apply<SerpentFormPower>(choiceContext, card.Owner.Creature, amount, card.Owner.Creature, card);
			break;
		case VoidForm:
			await PowerCmd.Apply<VoidFormPower>(choiceContext, card.Owner.Creature, amount, card.Owner.Creature, card);
			break;
		default:
			throw new InvalidOperationException($"Unsupported combined form card: {card.GetType().FullName}");
		}
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

	[HextechPatch("combat.form-auto-play", "形态开局自动打出批处理")]
	private static class FormAutoPlayPatches
	{
		public static void Apply(Harmony harmony)
		{
			harmony.Patch(
				RequireMethod(typeof(PlayerCmd), nameof(PlayerCmd.EndTurn), BindingFlags.Public | BindingFlags.Static, typeof(Player), typeof(bool), typeof(Func<Task>)),
				prefix: new HarmonyMethod(typeof(HextechFormAutoPlayHooks), nameof(EndTurnPrefix)) { priority = Priority.First });
			harmony.Patch(
				RequireMethod(typeof(Hook), nameof(Hook.BeforeCardPlayed), BindingFlags.Public | BindingFlags.Static, typeof(ICombatState), typeof(CardPlay)),
				prefix: new HarmonyMethod(typeof(HextechFormAutoPlayHooks), nameof(BeforeCardPlayedPrefix)) { priority = Priority.First });
			harmony.Patch(
				RequireMethod(typeof(Hook), nameof(Hook.AfterCardPlayed), BindingFlags.Public | BindingFlags.Static, typeof(ICombatState), typeof(PlayerChoiceContext), typeof(CardPlay)),
				prefix: new HarmonyMethod(typeof(HextechFormAutoPlayHooks), nameof(AfterCardPlayedPrefix)) { priority = Priority.First });
			harmony.Patch(
				RequireMethod(typeof(Hook), nameof(Hook.AfterCardChangedPiles), BindingFlags.Public | BindingFlags.Static, typeof(IRunState), typeof(ICombatState), typeof(CardModel), typeof(PileType), typeof(AbstractModel)),
				prefix: new HarmonyMethod(typeof(HextechFormAutoPlayHooks), nameof(AfterCardChangedPilesPrefix)) { priority = Priority.First });
			harmony.Patch(
				GetPlayPowerCardFlyVfxMethod(),
				prefix: new HarmonyMethod(typeof(HextechFormAutoPlayHooks), nameof(PlayPowerCardFlyVfxPrefix)) { priority = Priority.First });
			harmony.Patch(
				RequireMethod(
					typeof(PileTypeExtensions),
					nameof(PileTypeExtensions.GetTargetPosition),
					BindingFlags.Public | BindingFlags.Static,
					typeof(PileType),
					typeof(NCard)),
				postfix: new HarmonyMethod(typeof(HextechFormAutoPlayHooks), nameof(GetTargetPositionPostfix)) { priority = Priority.Last });
			harmony.Patch(
				RequireMethod(
					typeof(Hook),
					nameof(Hook.ModifyCardPlayCount),
					BindingFlags.Public | BindingFlags.Static,
					typeof(ICombatState),
					typeof(CardModel),
					typeof(int),
					typeof(Creature),
					typeof(List<AbstractModel>).MakeByRefType()),
				prefix: new HarmonyMethod(typeof(HextechFormAutoPlayHooks), nameof(ModifyCardPlayCountPrefix)) { priority = Priority.First });
	#if STS2_109_OR_NEWER
			harmony.Patch(
				RequireMethod(
					typeof(Hook),
					nameof(Hook.ModifyCardPlayResultLocation),
					BindingFlags.Public | BindingFlags.Static,
					typeof(ICombatState),
					typeof(CardModel),
					typeof(bool),
					typeof(ResourceInfo),
					typeof(CardLocation),
					typeof(IEnumerable<AbstractModel>).MakeByRefType()),
				prefix: new HarmonyMethod(typeof(HextechFormAutoPlayHooks), nameof(ModifyCardPlayResultLocationPrefix)) { priority = Priority.First });
	#else
			harmony.Patch(
				RequireMethod(
					typeof(Hook),
					nameof(Hook.ModifyCardPlayResultPileTypeAndPosition),
					BindingFlags.Public | BindingFlags.Static,
					typeof(ICombatState),
					typeof(CardModel),
					typeof(bool),
					typeof(ResourceInfo),
					typeof(PileType),
					typeof(CardPilePosition),
					typeof(IEnumerable<AbstractModel>).MakeByRefType()),
				prefix: new HarmonyMethod(typeof(HextechFormAutoPlayHooks), nameof(ModifyCardPlayResultPileTypeAndPositionPrefix)) { priority = Priority.First });
	#endif
			foreach (Type formType in GetSupportedFormTypes())
			{
				harmony.Patch(
					RequireMethod(
						formType,
						"OnPlay",
						BindingFlags.Instance | BindingFlags.NonPublic,
						typeof(PlayerChoiceContext),
						typeof(CardPlay)),
					prefix: new HarmonyMethod(typeof(HextechFormAutoPlayHooks), nameof(FormOnPlayPrefix)) { priority = Priority.First });
			}
		}
	}
}

internal sealed class HextechFormAutoPlayBatchState
{
	private readonly HashSet<CardModel> _cards;
	private readonly Dictionary<CardModel, float> _horizontalOffsets = new(ReferenceEqualityComparer.Instance);
	private CardPlay? _dispatchedCardPlay;
	private readonly HashSet<CardModel> _batchVfxCards = new(ReferenceEqualityComparer.Instance);
	private bool _isPlayingBatchVfx;
	private CardModel? _combinedPrimary;
	private decimal _combinedAmount;
	private int _preparedPlayCount;
	private HextechFormCardResult _combinedResult;
	private bool _isResolvingCombinedEffect;

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

	internal bool TryGetHorizontalOffset(CardModel card, out float offset)
	{
		return _horizontalOffsets.TryGetValue(card, out offset);
	}

	internal bool ShouldDispatchCardPlayedHook(CardPlay cardPlay)
	{
		if (!_cards.Contains(cardPlay.Card))
		{
			return true;
		}

		if (_dispatchedCardPlay == null)
		{
			_dispatchedCardPlay = cardPlay;
		}

		return ReferenceEquals(_dispatchedCardPlay, cardPlay);
	}

	internal bool ShouldDispatchCardChangedPilesHook(CardModel card, PileType oldPile, PileType newPile)
	{
		return !_cards.Contains(card)
			|| oldPile != PileType.Play
			|| newPile != PileType.Play;
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

	internal void PrepareCombinedResolution(
		CardModel primary,
		decimal amount,
		int playCount,
		HextechFormCardResult result)
	{
		_combinedPrimary = primary;
		_combinedAmount = amount;
		_preparedPlayCount = playCount;
		_combinedResult = result;
		_isResolvingCombinedEffect = true;
	}

	internal void FinishCombinedResolution()
	{
		_isResolvingCombinedEffect = false;
		_combinedPrimary = null;
		_combinedAmount = 0m;
		_preparedPlayCount = 0;
	}

	internal int PreparedPlayCount => _preparedPlayCount;

	internal bool ShouldUsePreparedPlayCount(CardModel card)
	{
		return _isResolvingCombinedEffect && ReferenceEquals(card, _combinedPrimary);
	}

	internal bool TryGetPreparedResult(CardModel card, out HextechFormCardResult result)
	{
		if (_isResolvingCombinedEffect && ReferenceEquals(card, _combinedPrimary))
		{
			result = _combinedResult;
			return true;
		}

		result = default;
		return false;
	}

	internal bool TryGetCombinedAmount(CardModel card, out decimal amount)
	{
		if (_isResolvingCombinedEffect && ReferenceEquals(card, _combinedPrimary))
		{
			amount = _combinedAmount;
			return true;
		}

		amount = 0m;
		return false;
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
