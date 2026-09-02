using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Models.Exceptions;

namespace HextechRunes;

public sealed class IllusoryWeaponRune : HextechRelicBase
{
	private int _damageTargetsThisCombat;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DamageVar(2m, ValueProp.Move)
	];

	public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		if (Owner == null
			|| Owner.Creature.IsDead
			|| !IsOriginalOwnedSkill(cardPlay.Card, Owner)
			|| Owner.Creature.CombatState is not HextechCombatState combatState)
		{
			return;
		}

		try
		{
			int targetOrdinal = ConsumeCombatProcOrdinal(nameof(IllusoryWeaponRune), ref _damageTargetsThisCombat);
			Creature? target = HextechRuneTargeting.PickRandomHittableEnemy(
				Owner,
				combatState,
				"illusory-weapon",
				combatState.RoundNumber.ToString(),
				targetOrdinal.ToString(),
				cardPlay.Card.Id.Entry);
			if (target == null)
			{
				return;
			}

			Flash([target]);
			await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
				.FromCardCompat(cardPlay.Card)
				.Targeting(target)
				.WithNoAttackerAnim()
				.Execute(choiceContext);
		}
		finally
		{
			HextechPlayerRuneHooks.ClearIllusoryWeaponPendingPenNib(Owner, cardPlay.Card);
		}
	}

	internal static bool ShouldTreatSkillAsAttack(Player? owner)
	{
		return owner?.GetRelic<IllusoryWeaponRune>() != null;
	}

	internal static bool IsOriginalOwnedSkill(CardModel? card, Player owner)
	{
		return card?.Owner == owner && IsSkillForEffects(card);
	}

	internal static bool IsAttackForEffects(CardModel? card, Player? owner)
	{
		if (card == null)
		{
			return false;
		}

		if (card.Type == CardType.Attack)
		{
			return true;
		}

		return owner != null
			&& ShouldTreatSkillAsAttack(owner)
			&& IsOriginalOwnedSkill(card, owner);
	}

	internal static bool IsSkillForEffects(CardModel? card)
	{
		if (card == null)
		{
			return false;
		}

		try
		{
			return (card.CanonicalInstance?.Type ?? card.Type) == CardType.Skill;
		}
		catch (CanonicalModelException)
		{
			return card.Type == CardType.Skill;
		}
	}

	[HarmonyPatch(typeof(Finisher), "CanonicalVars", MethodType.Getter)]
	[HextechPatch("rune.illusory-weapon.finisher", "幻影武器", Rune = typeof(IllusoryWeaponRune))]
	private static class FinisherCanonicalVarsPatch
	{
		[HarmonyPrepare]
		private static bool Prepare() => HextechPlayerRuneHooks.IllusoryWeaponReflectionReady;

		[HarmonyPostfix]
		private static void Postfix(ref IEnumerable<DynamicVar> __result)
		{
			__result = __result.Select(static dynamicVar =>
				dynamicVar.Name == HextechPlayerRuneHooks.FinisherCalculatedHitsKey
					? new CalculatedVar(HextechPlayerRuneHooks.FinisherCalculatedHitsKey).WithMultiplier(HextechPlayerRuneHooks.CountFinisherAttackCardsPlayedThisTurn)
					: dynamicVar);
		}
	}

	[HarmonyPatch(typeof(Nunchaku), nameof(Nunchaku.AfterCardPlayed), typeof(PlayerChoiceContext), typeof(CardPlay))]
	[HextechPatch("rune.illusory-weapon.nunchaku", "幻影武器", Rune = typeof(IllusoryWeaponRune))]
	private static class NunchakuPatch
	{
		[HarmonyPrepare]
		private static bool Prepare() => HextechPlayerRuneHooks.IllusoryWeaponReflectionReady;

		[HarmonyPrefix]
		private static bool Prefix(Nunchaku __instance, CardPlay cardPlay, ref Task __result)
		{
			if (!HextechPlayerRuneHooks.ShouldHandleIllusoryWeaponSkill(cardPlay, __instance.Owner))
			{
				return true;
			}

			__result = HextechPlayerRuneHooks.ResolveIllusoryWeaponNunchaku(__instance);
			return false;
		}
	}

	[HarmonyPatch(typeof(Kunai), nameof(Kunai.AfterCardPlayed), typeof(PlayerChoiceContext), typeof(CardPlay))]
	[HextechPatch("rune.illusory-weapon.kunai", "幻影武器", Rune = typeof(IllusoryWeaponRune))]
	private static class KunaiPatch
	{
		[HarmonyPrepare]
		private static bool Prepare() => HextechPlayerRuneHooks.IllusoryWeaponReflectionReady;

		[HarmonyPrefix]
		private static bool Prefix(Kunai __instance, CardPlay cardPlay, ref Task __result)
		{
			if (!HextechPlayerRuneHooks.ShouldHandleIllusoryWeaponSkill(cardPlay, __instance.Owner) || !CombatManager.Instance.IsInProgress)
			{
				return true;
			}

			__result = HextechPlayerRuneHooks.ResolveIllusoryWeaponKunai(__instance);
			return false;
		}
	}

	[HarmonyPatch(typeof(Shuriken), nameof(Shuriken.AfterCardPlayed), typeof(PlayerChoiceContext), typeof(CardPlay))]
	[HextechPatch("rune.illusory-weapon.shuriken", "幻影武器", Rune = typeof(IllusoryWeaponRune))]
	private static class ShurikenPatch
	{
		[HarmonyPrepare]
		private static bool Prepare() => HextechPlayerRuneHooks.IllusoryWeaponReflectionReady;

		[HarmonyPrefix]
		private static bool Prefix(Shuriken __instance, CardPlay cardPlay, ref Task __result)
		{
			if (!HextechPlayerRuneHooks.ShouldHandleIllusoryWeaponSkill(cardPlay, __instance.Owner) || !CombatManager.Instance.IsInProgress)
			{
				return true;
			}

			__result = HextechPlayerRuneHooks.ResolveIllusoryWeaponShuriken(__instance);
			return false;
		}
	}

	[HarmonyPatch(typeof(OrnamentalFan), nameof(OrnamentalFan.AfterCardPlayed), typeof(PlayerChoiceContext), typeof(CardPlay))]
	[HextechPatch("rune.illusory-weapon.ornamental-fan", "幻影武器", Rune = typeof(IllusoryWeaponRune))]
	private static class OrnamentalFanPatch
	{
		[HarmonyPrepare]
		private static bool Prepare() => HextechPlayerRuneHooks.IllusoryWeaponReflectionReady;

		[HarmonyPrefix]
		private static bool Prefix(OrnamentalFan __instance, CardPlay cardPlay, ref Task __result)
		{
			if (!HextechPlayerRuneHooks.ShouldHandleIllusoryWeaponSkill(cardPlay, __instance.Owner) || !CombatManager.Instance.IsInProgress)
			{
				return true;
			}

			__result = HextechPlayerRuneHooks.ResolveIllusoryWeaponOrnamentalFan(__instance);
			return false;
		}
	}

	[HarmonyPatch(typeof(PenNib), nameof(PenNib.BeforeCardPlayed), typeof(CardPlay))]
	[HextechPatch("rune.illusory-weapon.pen-nib-before", "幻影武器", Rune = typeof(IllusoryWeaponRune))]
	private static class PenNibBeforeCardPlayedPatch
	{
		[HarmonyPrepare]
		private static bool Prepare() => HextechPlayerRuneHooks.IllusoryWeaponReflectionReady;

		[HarmonyPrefix]
		private static bool Prefix(PenNib __instance, CardPlay cardPlay, ref Task __result)
		{
			if (!HextechPlayerRuneHooks.ShouldHandleIllusoryWeaponSkill(cardPlay, __instance.Owner))
			{
				return true;
			}

			__instance.NotifyAttackPlayed();
			if (__instance.AttacksPlayed == 0)
			{
				HextechPlayerRuneHooks.SetPenNibAttackToDouble(__instance, cardPlay.Card);
			}
			__result = Task.CompletedTask;
			return false;
		}
	}

	[HarmonyPatch(typeof(PenNib), nameof(PenNib.AfterCardPlayed), typeof(PlayerChoiceContext), typeof(CardPlay))]
	[HextechPatch("rune.illusory-weapon.pen-nib-after", "幻影武器", Rune = typeof(IllusoryWeaponRune))]
	private static class PenNibAfterCardPlayedPatch
	{
		[HarmonyPrepare]
		private static bool Prepare() => HextechPlayerRuneHooks.IllusoryWeaponReflectionReady;

		[HarmonyPrefix]
		private static bool Prefix(PenNib __instance, CardPlay cardPlay, ref Task __result)
		{
			if (!HextechPlayerRuneHooks.ShouldHandleIllusoryWeaponSkill(cardPlay, __instance.Owner)
				|| !HextechPlayerRuneHooks.IsPenNibTracking(__instance, cardPlay.Card))
			{
				return true;
			}

			__result = Task.CompletedTask;
			return false;
		}
	}
}
