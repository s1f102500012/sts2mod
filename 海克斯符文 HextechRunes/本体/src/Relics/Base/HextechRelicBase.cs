using Godot;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Helpers;

namespace HextechRunes;

public abstract partial class HextechRelicBase : RelicModel
{
	private static readonly string PlaceholderIconPath = ImageHelper.GetImagePath("powers/missing_power.png");

	public virtual decimal ModifyDamageAdditiveCompat(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		return 0m;
	}

#if STS2_108_OR_NEWER
	public sealed override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
	{
		return ModifyDamageAdditiveCompat(target, amount, props, dealer, cardSource);
	}
#else
	public sealed override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		return ModifyDamageAdditiveCompat(target, amount, props, dealer, cardSource);
	}
#endif

	// 0.108.0 起伤害管线加 CardPlay 参数:游戏侧 override 按版本二选一,子类统一重写版本无关的
	// Compat 虚方法(签名沿用 0.107,cardPlay 目前无子类需要,需要时再扩)。
	public virtual decimal ModifyDamageMultiplicativeCompat(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		return 1m;
	}

#if STS2_108_OR_NEWER
	public sealed override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
	{
		return ModifyDamageMultiplicativeCompat(target, amount, props, dealer, cardSource);
	}
#else
	public sealed override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		return ModifyDamageMultiplicativeCompat(target, amount, props, dealer, cardSource);
	}
#endif

	// 0.109.0 起卡牌打出去向从 (PileType, CardPilePosition) 元组改为 CardLocation(新增 player 字段):
	// 子类统一重写版本无关的 Compat 虚方法(签名沿用 0.108 元组口径,player 目前无子类需要,需要时再扩)。
	public virtual (PileType, CardPilePosition) ModifyCardPlayResultPileTypeAndPositionCompat(CardModel card, bool isAutoPlay, ResourceInfo resources, PileType pileType, CardPilePosition position)
	{
		return (pileType, position);
	}

	public virtual Task AfterModifyingCardPlayResultPileOrPositionCompat(CardModel card, PileType pileType, CardPilePosition position)
	{
		return Task.CompletedTask;
	}

#if STS2_109_OR_NEWER
	public sealed override CardLocation ModifyCardPlayResultLocation(CardModel card, bool isAutoPlay, ResourceInfo resources, CardLocation cardLocation)
	{
		(PileType pileType, CardPilePosition position) = ModifyCardPlayResultPileTypeAndPositionCompat(card, isAutoPlay, resources, cardLocation.pileType, cardLocation.position);
		cardLocation.pileType = pileType;
		cardLocation.position = position;
		return cardLocation;
	}

	public sealed override Task AfterModifyingCardPlayResultLocation(CardModel card, CardLocation cardLocation)
	{
		return AfterModifyingCardPlayResultPileOrPositionCompat(card, cardLocation.pileType, cardLocation.position);
	}
#else
	public sealed override (PileType, CardPilePosition) ModifyCardPlayResultPileTypeAndPosition(CardModel card, bool isAutoPlay, ResourceInfo resources, PileType pileType, CardPilePosition position)
	{
		return ModifyCardPlayResultPileTypeAndPositionCompat(card, isAutoPlay, resources, pileType, position);
	}

	public sealed override Task AfterModifyingCardPlayResultPileOrPosition(CardModel card, PileType pileType, CardPilePosition position)
	{
		return AfterModifyingCardPlayResultPileOrPositionCompat(card, pileType, position);
	}
#endif

#if STS2_106_OR_NEWER
	public virtual Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, HextechCombatState combatState)
	{
		return Task.CompletedTask;
	}

	public sealed override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, IReadOnlyList<Creature> participants, HextechCombatState combatState)
	{
		return BeforeSideTurnStart(choiceContext, side, combatState);
	}

	public virtual Task AfterSideTurnStart(CombatSide side, HextechCombatState combatState)
	{
		return Task.CompletedTask;
	}

	public sealed override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, HextechCombatState combatState)
	{
		return AfterSideTurnStart(side, combatState);
	}

	public virtual Task BeforeTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)
	{
		return Task.CompletedTask;
	}

	public sealed override Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
	{
		return BeforeTurnEnd(choiceContext, side);
	}

	public virtual Task AfterTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)
	{
		return Task.CompletedTask;
	}

	public sealed override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
	{
		return AfterTurnEnd(choiceContext, side);
	}
#endif

	public sealed override RelicRarity Rarity => RelicRarity.Starter;

	public override string PackedIconPath => GetResolvedIconPath();

	protected override string PackedIconOutlinePath => GetResolvedIconPath();

	protected override string BigIconPath => GetResolvedIconPath();

	public virtual bool IsAvailableForPlayer(Player player) => true;

	private static readonly HashSet<string> WarnedMissingIconPaths = new(StringComparer.Ordinal);

	private string GetResolvedIconPath()
	{
		string? customPath = HextechAssets.TryGetCustomRelicIconPath(this);
		if (!string.IsNullOrEmpty(customPath) && ResourceLoader.Exists(customPath))
		{
			return customPath;
		}

		// 缺图会静默落到原版占位图,肉眼难归因;每路径告警一次,把问题从"玩家看到占位"提前到日志。
		if (!string.IsNullOrEmpty(customPath) && WarnedMissingIconPaths.Add(customPath))
		{
			Log.Warn($"[{ModInfo.Id}][Assets] Relic icon missing, falling back to placeholder: {GetType().Name} -> {customPath}");
		}

		return PlaceholderIconPath;
	}
}
