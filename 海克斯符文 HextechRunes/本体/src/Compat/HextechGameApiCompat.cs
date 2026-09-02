#if STS2_108_OR_NEWER
// ColorlessCardPool 仅 0.108 分支使用;用 #if 包住让 0.107.1 下的死码分析(IDE0005)不再误删。
using MegaCrit.Sts2.Core.Models.CardPools;
#endif

namespace HextechRunes;

/// <summary>
/// 0.107.1↔0.108.0 直接调用类 API 差异的集中适配(签名统一为 0.107 形态,0.108 下补齐新参数):
/// CreatureCmd.Damage 带 cardSource 的重载追加 CardPlay、AttackCommand.FromCard 追加 CardPlay、
/// PotionFactory.GetPotionOptions 移除 blacklist、CardCreationOptions 移除自定义卡列表构造器
/// (改为池+过滤器)与 WithCardPools 的过滤器参数分离。
/// </summary>
internal static class HextechGameApiCompat
{
	internal static Task<IEnumerable<DamageResult>> Damage(PlayerChoiceContext choiceContext, Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay = null)
	{
#if STS2_108_OR_NEWER
		return CreatureCmd.Damage(choiceContext, target, amount, props, dealer, cardSource, cardPlay);
#else
		return CreatureCmd.Damage(choiceContext, target, amount, props, dealer, cardSource);
#endif
	}

	internal static Task<IEnumerable<DamageResult>> Damage(PlayerChoiceContext choiceContext, IEnumerable<Creature> targets, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay = null)
	{
#if STS2_108_OR_NEWER
		return CreatureCmd.Damage(choiceContext, targets, amount, props, dealer, cardSource, cardPlay);
#else
		return CreatureCmd.Damage(choiceContext, targets, amount, props, dealer, cardSource);
#endif
	}

	internal static Task<IEnumerable<DamageResult>> Damage(PlayerChoiceContext choiceContext, Creature target, DamageVar damageVar, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay = null)
	{
#if STS2_108_OR_NEWER
		return CreatureCmd.Damage(choiceContext, target, damageVar, dealer, cardSource, cardPlay);
#else
		return CreatureCmd.Damage(choiceContext, target, damageVar, dealer, cardSource);
#endif
	}

	internal static Task<IEnumerable<DamageResult>> Damage(PlayerChoiceContext choiceContext, IEnumerable<Creature> targets, DamageVar damageVar, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay = null)
	{
#if STS2_108_OR_NEWER
		return CreatureCmd.Damage(choiceContext, targets, damageVar, dealer, cardSource, cardPlay);
#else
		return CreatureCmd.Damage(choiceContext, targets, damageVar, dealer, cardSource);
#endif
	}

	internal static AttackCommand FromCardCompat(this AttackCommand command, CardModel card, CardPlay? cardPlay = null)
	{
#if STS2_108_OR_NEWER
		return command.FromCard(card, cardPlay);
#else
		return command.FromCard(card);
#endif
	}

	internal static IEnumerable<PotionModel> GetPotionOptions(Player player)
	{
#if STS2_108_OR_NEWER
		return PotionFactory.GetPotionOptions(player);
#else
		return PotionFactory.GetPotionOptions(player, Array.Empty<PotionModel>());
#endif
	}

	/// <summary>
	/// 0.107 的"自定义卡列表"构造在 0.108 被移除:改由"全部角色池+无色池"超集配 Contains 过滤
	/// 等价表达(过滤集精确圈定目标卡,池只需覆盖列表来源)。
	/// </summary>
	internal static CardCreationOptions CreateOptionsFromCards(Player player, IEnumerable<CardModel> cards, CardCreationSource source, CardRarityOddsType rarityOdds)
	{
#if STS2_108_OR_NEWER
		HashSet<CardModel> allowed = [.. cards];
		IEnumerable<CardPoolModel> pools = player.UnlockState.CharacterCardPools
			.Concat<CardPoolModel>([ModelDb.CardPool<ColorlessCardPool>()]);
		return new CardCreationOptions(pools, source, rarityOdds, allowed.Contains);
#else
		_ = player;
		return new CardCreationOptions(cards, source, rarityOdds);
#endif
	}

	internal static CardCreationOptions WithCardPoolsCompat(this CardCreationOptions options, IEnumerable<CardPoolModel> pools, Func<CardModel, bool>? cardPoolFilter)
	{
#if STS2_108_OR_NEWER
		CardCreationOptions result = options.WithCardPools(pools);
		return cardPoolFilter != null ? result.WithFilter(cardPoolFilter) : result;
#else
		return options.WithCardPools(pools, cardPoolFilter);
#endif
	}
}
