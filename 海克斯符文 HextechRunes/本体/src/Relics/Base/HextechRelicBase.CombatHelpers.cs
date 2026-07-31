using MegaCrit.Sts2.Core.Models.Exceptions;

namespace HextechRunes;

public abstract partial class HextechRelicBase
{
	protected static bool DeckContains<TCard>(Player player)
		where TCard : CardModel
	{
		ModelId cardId = ModelDb.GetId<TCard>();
		return player.Deck.Cards.Any(card => (card.CanonicalInstance?.Id ?? card.Id) == cardId);
	}

	protected static int FloorToInt(decimal value)
	{
		return (int)decimal.Floor(value);
	}

	protected bool IsOwnedCard(CardModel? card)
	{
		return card?.Owner == Owner;
	}

	protected bool IsOwnedAttack(CardModel? card)
	{
		return Owner != null && card?.Owner == Owner && IllusoryWeaponRune.IsAttackForEffects(card, Owner);
	}

	protected bool IsOwnedSkill(CardModel? card)
	{
		return card != null && card.Owner == Owner && IllusoryWeaponRune.IsSkillForEffects(card);
	}

	protected bool IsAttackDamageForRuneEffects(ValueProp props, CardModel? cardSource)
	{
		if (HextechSts2Compat.IsPoweredAttack(props))
		{
			return true;
		}

		return Owner != null && IllusoryWeaponRune.IsOriginalOwnedSkill(cardSource, Owner);
	}

	protected int CountOwnedAttackCardsPlayedFromHistory(bool firstInSeriesOnly = true, bool includeAutoPlay = false)
	{
		return HextechCombatHistoryHelper.CountOwnedAttackCardsPlayed(Owner, firstInSeriesOnly, includeAutoPlay);
	}

	protected int CountOwnedCardsDrawnFromHistory()
	{
		return HextechCombatHistoryHelper.CountOwnedCardsDrawn(Owner);
	}

	protected bool IsOwnedCardWithEffectiveCostAtLeast(CardModel? card, decimal minimumCost)
	{
		// 含 X 费卡:X 费卡按本次实付的 X(GetEnergyCostForCurrentCardPlay 在打出期间返回实付能量)参与判定,
		// 不再用 !CostsX 排除。本方法仅由 5 个"费用≥N"符文(终极刷新/终极不可阻挡/最终形态/妖精魔法/碰不到我)共用,
		// 放开 X 费卡正是期望行为;其它 CostsX 排除逻辑都各自直接判 card.EnergyCost.CostsX,不经本方法。
		// 实付 X 由同步的打出动作决定,故各端判定一致,不引入联机分叉。
		return card != null
			&& card.Owner == Owner
			&& HextechCombatHooks.GetEnergyCostForCurrentCardPlay(card) >= minimumCost;
	}

	protected bool IsOwnerOrPet(Creature? dealer)
	{
		return HextechCombatHistoryHelper.IsOwnerOrPet(Owner, dealer);
	}

	protected bool IsDamageFromOwner(Creature? dealer, CardModel? cardSource)
	{
		return HextechCombatHistoryHelper.IsDamageFromOwner(Owner, dealer, cardSource);
	}

	protected bool IsDamageFromOwnerToEnemyOrPreview(Creature? target, Creature? dealer, CardModel? cardSource)
	{
		return (target == null || target.Side == CombatSide.Enemy)
			&& IsDamageFromOwner(dealer, cardSource);
	}

	protected bool IsPotionUseOwnedByOrTargetingOwner(PotionModel? potion, Creature? target)
	{
		if (Owner == null)
		{
			return false;
		}

		if (target == Owner.Creature)
		{
			return true;
		}

		try
		{
			return potion?.Owner == Owner;
		}
		catch (CanonicalModelException)
		{
			return false;
		}
	}

	protected bool TryGetOwnedEnemyDebuffTarget(PowerModel power, decimal amount, Creature? applier, out Creature? target)
	{
		target = power.Owner;
		return amount > 0m
			&& target?.Side == CombatSide.Enemy
			&& applier == Owner?.Creature
			&& power.GetTypeForAmount(amount) == PowerType.Debuff
			&& power is not ITemporaryPower;
	}
}
