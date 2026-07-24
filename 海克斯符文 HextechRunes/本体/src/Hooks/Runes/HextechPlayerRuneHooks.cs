using MegaCrit.Sts2.Core.Models.Exceptions;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HextechRunes;

internal static partial class HextechPlayerRuneHooks
{
	private static bool CreativeAiBeforeHandDrawPrefix(CreativeAiPower __instance, Player player, ref Task __result)
	{
		if (!CreativeAiUpgradeRune.ShouldUseUpgradedGeneration(__instance, player))
		{
			return true;
		}

		__result = CreativeAiUpgradeRune.GenerateUpgradedPowerCards(__instance, player);
		return false;
	}

	private static void RelicDynamicDescriptionPrefix(RelicModel __instance)
	{
		if (__instance is FlyingKickRune flyingKickRune)
		{
			flyingKickRune.RefreshExecutePercentFromOwner();
		}
	}

	private static void NCreatureStartDeathAnimPostfix(NCreature __instance, bool shouldRemove)
	{
		if (!FlyingKickCorpseLaunchDriver.TryConsumePending(__instance.Entity))
		{
			return;
		}

		if (!shouldRemove
			|| __instance.Entity == null
			|| !HextechMonsterInteractionPolicy.IsTrueCombatDeath(__instance.Entity))
		{
			return;
		}

		FlyingKickCorpseLaunchDriver.TryAttach(__instance);
	}

	private static bool SurvivorOnPlayPrefix(Survivor __instance, PlayerChoiceContext choiceContext, CardPlay cardPlay, ref Task __result)
	{
		if (!SurvivorUpgradeRune.ShouldUseUpgradedPlay(__instance))
		{
			return true;
		}

		__result = SurvivorUpgradeRune.PlayUpgraded(choiceContext, __instance, cardPlay);
		return false;
	}

	private static bool CompactOnPlayPrefix(Compact __instance, PlayerChoiceContext choiceContext, CardPlay cardPlay, ref Task __result)
	{
		if (!CompactUpgradeRune.ShouldUseUpgradedPlay(__instance))
		{
			return true;
		}

		__result = CompactUpgradeRune.PlayUpgraded(choiceContext, __instance, cardPlay);
		return false;
	}

	private static bool JuggernautAfterBlockGainedPrefix(JuggernautPower __instance, Creature creature, decimal amount, ValueProp props, CardModel? cardSource, ref Task __result)
	{
		if (__instance.Owner?.Player?.GetRelic<JuggernautUpgradeRune>() == null)
		{
			return true;
		}

		__result = JuggernautUpgradeAfterBlockGained(__instance, creature, amount);
		return false;
	}

	private static async Task JuggernautUpgradeAfterBlockGained(JuggernautPower power, Creature creature, decimal amount)
	{
		if (amount <= 0m || creature != power.Owner)
		{
			return;
		}

		List<Creature> targets = power.CombatState.HittableEnemies.ToList();
		if (targets.Count == 0)
		{
			return;
		}

		power.Owner.Player?.GetRelic<JuggernautUpgradeRune>()?.Flash(targets);
		await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), targets, power.Amount, ValueProp.Unpowered, power.Owner);
	}

	private static bool HiddenGemOnPlayPrefix(HiddenGem __instance, PlayerChoiceContext choiceContext, CardPlay cardPlay, ref Task __result)
	{
		if (!HiddenGemUpgradeRune.ShouldUseUpgradedPlay(__instance))
		{
			return true;
		}

		__result = HiddenGemUpgradeRune.PlayUpgraded(choiceContext, __instance, cardPlay);
		return false;
	}

	private static bool AutomationAfterCardDrawnPrefix(AutomationPower __instance, PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw, ref Task __result)
	{
		if (!AutomationUpgradeRune.ShouldUseUpgradedDraw(__instance, card))
		{
			return true;
		}

		__result = AutomationUpgradeRune.AfterCardDrawnUpgraded(choiceContext, __instance, card, fromHandDraw);
		return false;
	}

	private static bool JackpotOnPlayPrefix(Jackpot __instance, PlayerChoiceContext choiceContext, CardPlay cardPlay, ref Task __result)
	{
		if (!JackpotUpgradeRune.ShouldUseUpgradedPlay(__instance))
		{
			return true;
		}

		__result = JackpotUpgradeRune.OnPlayUpgraded(__instance, choiceContext, cardPlay);
		return false;
	}

	private static bool VoltaicOnPlayPrefix(Voltaic __instance, PlayerChoiceContext choiceContext, CardPlay cardPlay, ref Task __result)
	{
		if (!VoltaicUpgradeRune.ShouldUseUpgradedPlay(__instance))
		{
			return true;
		}

		__result = VoltaicUpgradeRune.PlayUpgraded(choiceContext, __instance, cardPlay);
		return false;
	}

	private static bool GrandFinaleOnPlayPrefix(GrandFinale __instance, PlayerChoiceContext choiceContext, CardPlay cardPlay, ref Task __result)
	{
		if (!GrandFinaleUpgradeRune.AllowsPlaying(__instance))
		{
			return true;
		}

		__result = GrandFinaleUpgradeRune.PlayUpgradedSafely(choiceContext, __instance);
		return false;
	}

	// 形参按游戏真实签名用 HextechCombatState(0.104+ 为 ICombatState);helper 需要具体 CombatState,
	// 拿不到时放行原版(与旧行为一致,不吞小刀)。
	private static bool ShivCreateOneInHandPrefix(Player owner, HextechCombatState combatState, ref Task<CardModel?> __result)
	{
		if (owner.GetRelic<BigKnifeRune>() == null || combatState is not CombatState concreteState)
		{
			return true;
		}

		__result = HextechKnifeHelper.CreateOneBigKnifeBladeInHand(owner, concreteState);
		return false;
	}

	private static bool ShivCreateManyInHandPrefix(Player owner, int count, HextechCombatState combatState, ref Task<IEnumerable<CardModel>> __result)
	{
		if (owner.GetRelic<BigKnifeRune>() == null || combatState is not CombatState concreteState)
		{
			return true;
		}

		__result = HextechKnifeHelper.CreateBigKnifeBladesInHand(owner, count, concreteState);
		return false;
	}

	private static void SovereignBladeTargetTypePostfix(SovereignBlade __instance, ref TargetType __result)
	{
		if (HextechKnifeHelper.ShouldFanOfKnivesAffectSovereignBlade(__instance))
		{
			__result = TargetType.AllEnemies;
		}
	}

	private static bool SovereignBladeOnPlayPrefix(SovereignBlade __instance, PlayerChoiceContext choiceContext, CardPlay cardPlay, ref Task __result)
	{
		if (!HextechKnifeHelper.ShouldFanOfKnivesAffectSovereignBlade(__instance) || __instance.CombatState is not CombatState)
		{
			return true;
		}

		__result = PlayFanOfKnivesSovereignBlade(choiceContext, __instance);
		return false;
	}

	private static async Task PlayFanOfKnivesSovereignBlade(PlayerChoiceContext choiceContext, SovereignBlade card)
	{
		if (card.CombatState is not CombatState combatState)
		{
			return;
		}

		var attack = DamageCmd.Attack(card.DynamicVars.Damage.BaseValue)
			.FromCardCompat(card)
			.WithHitCount(card.DynamicVars.Repeat.IntValue)
			.WithAttackerAnim("Cast", card.Owner.Character.AttackAnimDelay)
			.WithAttackerFx(null, "event:/sfx/characters/regent/regent_sovereign_blade")
			.TargetingAllOpponents(combatState)
			.WithHitFx("vfx/vfx_giant_horizontal_slash", null, "slash_attack.mp3");

		await attack.Execute(choiceContext);
#if !STS2_107_OR_NEWER
		if (card.Owner.Creature.GetPower<ParryPower>() is { } parryPower)
		{
			await parryPower.AfterSovereignBladePlayed(card.Owner.Creature, attack.Results);
		}
#endif
	}

#if STS2_104_OR_NEWER
	private static void CardPileCmdAddGeneratedCardsToCombatPrefix(ref IEnumerable<CardModel> cards, Player? creator)
#else
	private static void CardPileCmdAddGeneratedCardsToCombatPrefix(ref IEnumerable<CardModel> cards, bool addedByPlayer)
#endif
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

#if STS2_104_OR_NEWER
			bool addedByPlayer = creator != null;
#endif
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

			List<CardModel>? realityRewritten = TryApplyEnemyManipulateRealityStatusDoubling(rewritten ?? originals, addedByPlayer);
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

	private static List<CardModel>? TryApplyEnemyManipulateRealityStatusDoubling(IReadOnlyList<CardModel> cards, bool addedByPlayer)
	{
		if (addedByPlayer)
		{
			return null;
		}

		List<CardModel>? rewritten = null;
		for (int i = 0; i < cards.Count; i++)
		{
			CardModel card = cards[i];
			if (!ShouldDoubleEnemyGeneratedStatusCard(card))
			{
				rewritten?.Add(card);
				continue;
			}

			rewritten ??= cards.Take(i).ToList();
			rewritten.Add(card);
			if (TryCreateManipulateRealityStatusCopy(card, out CardModel copy))
			{
				rewritten.Add(copy);
			}
		}

		return rewritten;
	}

	private static bool ShouldDoubleEnemyGeneratedStatusCard(CardModel card)
	{
		return card.Type == CardType.Status
			&& card.Owner?.Creature.Side == CombatSide.Player
			&& card.Owner.Creature.CombatState?.RunState == card.Owner.RunState
			&& card.Owner.RunState.Modifiers.OfType<HextechMayhemModifier>().LastOrDefault()?.HasActiveMonsterHex(MonsterHexKind.ManipulateReality) == true;
	}

	private static bool TryCreateManipulateRealityStatusCopy(CardModel card, out CardModel copy)
	{
		copy = null!;
		try
		{
			if (card.Owner?.Creature.CombatState is not HextechCombatState combatState)
			{
				return false;
			}

			copy = combatState.CloneCard(card);
			return true;
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Failed to duplicate enemy generated status card for Manipulate Reality: card={card.Id.Entry} error={ex.GetType().Name}: {ex.Message}");
			return false;
		}
	}

	private static void CardResolveEnergyXValuePostfix(CardModel __instance, ref int __result)
	{
		WhirlwindUpgradeRune.TryDoubleResolvedX(__instance, ref __result);
	}

	private static void CardTagsPostfix(CardModel __instance, ref IEnumerable<CardTag> __result)
	{
		Player? owner = TryGetMutableCardOwner(__instance);
		if (!__result.Contains(CardTag.Shiv) && HextechKnifeHelper.ShouldTreatSovereignBladeAsShiv(__instance, owner))
		{
			__result = __result.Append(CardTag.Shiv);
		}

		if (__result.Contains(CardTag.Strike)
			|| owner?.GetRelic<DeviantCognitionRune>() == null
			|| !IllusoryWeaponRune.IsAttackForEffects(__instance, owner))
		{
			return;
		}

		__result = __result.Append(CardTag.Strike);
	}

	private static Player? TryGetMutableCardOwner(CardModel card)
	{
		try
		{
			return card.Owner;
		}
		catch (CanonicalModelException)
		{
			return null;
		}
	}
}
