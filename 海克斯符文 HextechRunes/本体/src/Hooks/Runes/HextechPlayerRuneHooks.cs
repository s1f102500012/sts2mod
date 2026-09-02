using MegaCrit.Sts2.Core.Models.Exceptions;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HextechRunes;

internal static partial class HextechPlayerRuneHooks
{


	internal static async Task JuggernautUpgradeAfterBlockGained(JuggernautPower power, Creature creature, decimal amount)
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


	// 形参按游戏真实签名用 HextechCombatState(0.104+ 为 ICombatState);helper 需要具体 CombatState,
	// 拿不到时放行原版(与旧行为一致,不吞小刀)。


	internal static async Task PlayFanOfKnivesSovereignBlade(PlayerChoiceContext choiceContext, SovereignBlade card)
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
	}


	internal static List<CardModel>? TryApplyEnemyManipulateRealityStatusDoubling(IReadOnlyList<CardModel> cards, bool addedByPlayer)
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

	internal static bool ShouldDoubleEnemyGeneratedStatusCard(CardModel card)
	{
		return card.Type == CardType.Status
			&& card.Owner?.Creature.Side == CombatSide.Player
			&& card.Owner.Creature.CombatState?.RunState == card.Owner.RunState
			&& card.Owner.RunState.Modifiers.OfType<HextechMayhemModifier>().LastOrDefault()?.HasActiveMonsterHex(MonsterHexKind.ManipulateReality) == true;
	}

	internal static bool TryCreateManipulateRealityStatusCopy(CardModel card, out CardModel copy)
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


	internal static Player? TryGetMutableCardOwner(CardModel card)
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

	[HarmonyPatch(typeof(CardModel), nameof(CardModel.Tags), MethodType.Getter)]
	[HextechPatch("rune.card-tags", "卡牌标签", Runes = [typeof(DeviantCognitionRune), typeof(BigKnifeRune)])]
	internal static class CardTagsPatch
	{
		[HarmonyPostfix]
		private static void Postfix(CardModel __instance, ref IEnumerable<CardTag> __result)
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
	}
}
