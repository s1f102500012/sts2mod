using MegaCrit.Sts2.Core.Models.Exceptions;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HextechRunes;

public sealed class BigKnifeRune : HextechRelicBase
{
	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromCard<Shiv>(),
		HoverTipFactory.FromCard<SovereignBlade>(),
		HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
	];

	public override bool IsAvailableForPlayer(Player player)
	{
		return IsSilentPlayer(player);
	}

	public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
	{
		modifiedCost = originalCost;
		if (!ShouldMakeBladeFree(card) || card.EnergyCost.CostsX)
		{
			return false;
		}

		modifiedCost = 0m;
		return true;
	}

	public override bool TryModifyStarCost(CardModel card, decimal originalCost, out decimal modifiedCost)
	{
		modifiedCost = originalCost;
		if (!ShouldMakeBladeFree(card) || card.HasStarCostX)
		{
			return false;
		}

		modifiedCost = 0m;
		return true;
	}

	private bool ShouldMakeBladeFree(CardModel card)
	{
		return Owner != null
			&& card.Owner == Owner
			&& card is SovereignBlade
			&& card.Pile?.Type is PileType.Hand or PileType.Play;
	}

	// 0.109 起 Shiv.CreateInHand 尾部多一个可选参 Player creator。
#if STS2_109_OR_NEWER
	[HarmonyPatch(typeof(Shiv), nameof(Shiv.CreateInHand), typeof(Player), typeof(HextechCombatState), typeof(Player))]
#else
	[HarmonyPatch(typeof(Shiv), nameof(Shiv.CreateInHand), typeof(Player), typeof(HextechCombatState))]
#endif
	[HextechPatch("rune.big-knife.shiv-one", "大刀", Rune = typeof(BigKnifeRune))]
	private static class ShivCreateOnePatch
	{
		[HarmonyPrefix]
		[HarmonyPriority(Priority.Low)]
		private static bool Prefix(Player owner, HextechCombatState combatState, ref Task<CardModel?> __result)
		{
			if (owner.GetRelic<BigKnifeRune>() == null || combatState is not CombatState concreteState)
			{
				return true;
			}

			__result = HextechKnifeHelper.CreateOneBigKnifeBladeInHand(owner, concreteState);
			return false;
		}
	}

#if STS2_109_OR_NEWER
	[HarmonyPatch(typeof(Shiv), nameof(Shiv.CreateInHand), typeof(Player), typeof(int), typeof(HextechCombatState), typeof(Player))]
#else
	[HarmonyPatch(typeof(Shiv), nameof(Shiv.CreateInHand), typeof(Player), typeof(int), typeof(HextechCombatState))]
#endif
	[HextechPatch("rune.big-knife.shiv-many", "大刀", Rune = typeof(BigKnifeRune))]
	private static class ShivCreateManyPatch
	{
		[HarmonyPrefix]
		[HarmonyPriority(Priority.Low)]
		private static bool Prefix(Player owner, int count, HextechCombatState combatState, ref Task<IEnumerable<CardModel>> __result)
		{
			if (owner.GetRelic<BigKnifeRune>() == null || combatState is not CombatState concreteState)
			{
				return true;
			}

			__result = HextechKnifeHelper.CreateBigKnifeBladesInHand(owner, count, concreteState);
			return false;
		}
	}

	[HarmonyPatch(typeof(SovereignBlade), nameof(SovereignBlade.TargetType), MethodType.Getter)]
	[HextechPatch("rune.big-knife.sovereign-blade-target", "大刀", Rune = typeof(BigKnifeRune))]
	private static class SovereignBladeTargetTypePatch
	{
		[HarmonyPostfix]
		private static void Postfix(SovereignBlade __instance, ref TargetType __result)
		{
			if (HextechKnifeHelper.ShouldFanOfKnivesAffectSovereignBlade(__instance))
			{
				__result = TargetType.AllEnemies;
			}
		}
	}

	[HarmonyPatch(typeof(SovereignBlade), "OnPlay", typeof(PlayerChoiceContext), typeof(CardPlay))]
	[HextechPatch("rune.big-knife.sovereign-blade-play", "大刀", Rune = typeof(BigKnifeRune))]
	private static class SovereignBladeOnPlayPatch
	{
		[HarmonyPrefix]
		[HarmonyPriority(Priority.Low)]
		private static bool Prefix(SovereignBlade __instance, PlayerChoiceContext choiceContext, CardPlay cardPlay, ref Task __result)
		{
			if (!HextechKnifeHelper.ShouldFanOfKnivesAffectSovereignBlade(__instance) || __instance.CombatState is not CombatState)
			{
				return true;
			}

			__result = HextechPlayerRuneHooks.PlayFanOfKnivesSovereignBlade(choiceContext, __instance);
			return false;
		}
	}

	[HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.AddGeneratedCardsToCombat), typeof(IEnumerable<CardModel>), typeof(PileType), typeof(Player), typeof(CardPilePosition))]
	[HextechPatch("rune.big-knife.generated-cards", "大刀", Rune = typeof(BigKnifeRune))]
	private static class GeneratedCardsPatch
	{
		[HarmonyPrefix]
		private static void Prefix(ref IEnumerable<CardModel> cards, Player? creator)
		{
			// 整体兜底:本 prefix 在"敌人塞状态牌/生成卡进战斗"的必经路径上,任何异常都会让
			// 整个 AddGeneratedCardsToCombat 调用中断、上层塞牌任务链卡死(游戏卡住)。
			// 枚举外部传入的 cards(可能已被其他模组的 hook 改写为脆弱的惰性序列)是主要风险点;
			// 出错时放行原始参数、放弃本次改写(大刀替换/操控现实翻倍),绝不让塞牌流程断掉。
			try
			{
				List<CardModel> originals = cards.ToList();
				if (originals.Count == 0)
				{
					return;
				}

				bool addedByPlayer = creator != null;
				List<CardModel>? rewritten = null;
				for (int i = 0; i < originals.Count; i++)
				{
					CardModel card = originals[i];
					if (!HextechKnifeHelper.TryCreateBigKnifeReplacement(card, out CardModel replacement))
					{
						rewritten?.Add(card);
						continue;
					}

					if (rewritten == null)
					{
						rewritten = originals.Take(i).ToList();
					}
					rewritten.Add(replacement);
				}

				List<CardModel>? realityRewritten = HextechPlayerRuneHooks.TryApplyEnemyManipulateRealityStatusDoubling(rewritten ?? originals, addedByPlayer);
				if (realityRewritten != null)
				{
					cards = realityRewritten;
				}
				else if (rewritten != null)
				{
					cards = rewritten;
				}
			}
			catch (Exception ex)
			{
				Log.Warn($"[{ModInfo.Id}][Mayhem] AddGeneratedCardsToCombat prefix failed; passing cards through unmodified: {ex.GetType().Name}: {ex.Message}");
			}
		}
	}
}
