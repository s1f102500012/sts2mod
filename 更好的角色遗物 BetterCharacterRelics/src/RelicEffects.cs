using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rooms;

namespace BetterCharacterRelics;

internal static class RelicEffects
{
	internal static bool BurningBloodAfterCombatVictoryPrefix(BurningBlood __instance, ref Task __result)
	{

		if (__instance.Owner == null) return true;
		__result = BurningBloodAfterCombatVictoryReplacement(__instance);
		return false;
	}

	internal static async Task BurningBloodAfterCombatVictoryReplacement(BurningBlood self)
	{
		if (self.Owner == null || self.Owner.Creature.IsDead)
		{
			return;
		}

		await CreatureCmd.Heal(self.Owner.Creature, IsAtOrBelowHalfHp(self.Owner.Creature) ? 9m : 6m);
		Flash(self);
	}

	internal static bool BlackBloodAfterCombatVictoryPrefix(BlackBlood __instance, ref Task __result)
	{

		if (__instance.Owner == null) return true;
		__result = BlackBloodAfterCombatVictoryReplacement(__instance);
		return false;
	}

	internal static async Task BlackBloodAfterCombatVictoryReplacement(BlackBlood self)
	{
		if (self.Owner == null || self.Owner.Creature.IsDead)
		{
			return;
		}

		await CreatureCmd.Heal(self.Owner.Creature, IsAtOrBelowHalfHp(self.Owner.Creature) ? 18m : 12m);
		Flash(self);
	}

	internal static void RingOfTheSnakeCanonicalVarsPostfix(ref IEnumerable<DynamicVar> __result)
	{
		__result = MergeVars(__result,
		[
			new CardsVar(3)
		]);
	}

	internal static void RingOfTheSnakeModifyHandDrawPostfix(RingOfTheSnake __instance, Player player, decimal count, ref decimal __result, bool __runOriginal)
	{
		if (!__runOriginal || player != __instance.Owner || player.PlayerCombatState == null) return;
		__result = RelicRules.AdjustDraw(__result, player.Creature.CombatState?.RoundNumber,
			player.PlayerCombatState.TurnNumber, 1m, __instance.DynamicVars.Cards.BaseValue, 1);
	}

	internal static void RingOfTheDrakeModifyHandDrawPostfix(RingOfTheDrake __instance, Player player, decimal count, ref decimal __result, bool __runOriginal)
	{
		if (!__runOriginal || player != __instance.Owner || player.PlayerCombatState == null) return;
		__result = RelicRules.AdjustDraw(__result, player.Creature.CombatState?.RoundNumber,
			player.PlayerCombatState.TurnNumber, __instance.DynamicVars["Turns"].BaseValue,
			__instance.DynamicVars.Cards.BaseValue, 3);
	}

	internal static void RingOfTheDrakeCanonicalVarsPostfix(ref IEnumerable<DynamicVar> __result)
	{
		__result = MergeVars(__result,
		[
			new CardsVar(3),
			new DynamicVar("Turns", 3m)
		]);
	}

	internal static void AfterEnergyResetLatePostfix(AbstractModel __instance, Player player, ref Task __result)
	{
		if (__instance is DivineRight)
			__result = AfterEnergyResetLateAfterOriginal(__result, __instance, player);
	}

	internal static async Task AfterEnergyResetLateAfterOriginal(Task original, AbstractModel self, Player player)
	{
		await original;

		ICombatState? combatState = player.Creature.CombatState;
		if (combatState == null)
		{
			return;
		}

		switch (self)
		{
			case DivineRight divineRight when player == divineRight.Owner && player.PlayerCombatState is { Stars: < 3 }:
				await PlayerCmd.GainStars(1m, player);
				Flash(divineRight);
				break;
		}
	}

	internal static void AfterRoomEnteredPostfix(AbstractModel __instance, AbstractRoom room, ref Task __result)
	{
		if (__instance is DivineDestiny)
			__result = AfterRoomEnteredAfterOriginal(__result, __instance, room);
	}

	internal static async Task AfterRoomEnteredAfterOriginal(Task original, AbstractModel self, AbstractRoom room)
	{
		await original;

		if (self is DivineDestiny divineDestiny && room is CombatRoom && divineDestiny.Owner != null)
		{
			await PlayerCmd.GainStars(6m, divineDestiny.Owner);
			Flash(divineDestiny);
		}
	}

	internal static void AfterPlayerTurnStartPostfix(AbstractModel __instance, PlayerChoiceContext choiceContext, Player player, ref Task __result)
	{
		if (__instance is RingOfTheSnake or RingOfTheDrake)
			__result = AfterPlayerTurnStartAfterOriginal(__result, __instance, choiceContext, player);
	}

	internal static async Task AfterPlayerTurnStartAfterOriginal(Task original, AbstractModel self, PlayerChoiceContext choiceContext, Player player)
	{
		await original;

		ICombatState? combatState = player.Creature.CombatState;
		if (combatState == null)
		{
			return;
		}

		switch (self)
		{
			case RingOfTheSnake ringOfTheSnake when player == ringOfTheSnake.Owner && combatState.RoundNumber == 1:
				await SelectAndDiscardOne(choiceContext, ringOfTheSnake.Owner, ringOfTheSnake);
				break;
			case RingOfTheDrake ringOfTheDrake when player == ringOfTheDrake.Owner && combatState.RoundNumber <= 3:
				await SelectAndDiscardOne(choiceContext, ringOfTheDrake.Owner, ringOfTheDrake);
				break;
		}
	}

	internal static void DivineRightCanonicalVarsPostfix(ref IEnumerable<DynamicVar> __result)
	{
		__result = MergeVars(__result,
		[
			new StarsVar(3),
			new StarsVar("MinStars", 3),
			new StarsVar("TurnStartStars", 1)
		]);
	}

	internal static void DivineDestinyCanonicalVarsPostfix(ref IEnumerable<DynamicVar> __result)
	{
		__result = MergeVars(__result,
		[
			new StarsVar(6),
			new StarsVar("MinStars", 6),
			new StarsVar("TurnStartStars", 2)
		]);
	}

	internal static bool DivineDestinyAfterSideTurnStartPrefix(DivineDestiny __instance, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState, ref Task __result)
	{

		if (__instance.Owner == null) return true;
		__result = DivineDestinyAfterSideTurnStartReplacement(__instance, side, participants);
		return false;
	}

	internal static async Task DivineDestinyAfterSideTurnStartReplacement(DivineDestiny self, CombatSide side, IReadOnlyList<Creature> participants)
	{
		if (self.Owner == null || side != self.Owner.Creature.Side || !participants.Contains(self.Owner.Creature) || self.Owner.PlayerCombatState == null || self.Owner.PlayerCombatState.Stars >= 6)
		{
			return;
		}

		await PlayerCmd.GainStars(2m, self.Owner);
		Flash(self);
	}

	internal static bool BoundPhylacteryBeforeCombatStartPrefix(BoundPhylactery __instance, ref Task __result)
	{
		if (__instance.Owner == null) return true;
		__result = Task.CompletedTask;
		return false;
	}

	internal static bool BoundPhylacteryAfterEnergyResetLatePrefix(BoundPhylactery __instance, Player player, ref Task __result)
	{

		if (__instance.Owner == null) return true;
		__result = BoundPhylacteryAfterEnergyResetLateReplacement(__instance, player);
		return false;
	}

	internal static async Task BoundPhylacteryAfterEnergyResetLateReplacement(BoundPhylactery self, Player player)
	{
		if (self.Owner == null || player != self.Owner)
		{
			return;
		}

		await OstyCmd.Summon(new ThrowingPlayerChoiceContext(), self.Owner, self.Owner.IsOstyAlive ? 2m : 1m, self);
		Flash(self);
	}

	internal static bool PhylacteryUnboundAfterSideTurnStartPrefix(PhylacteryUnbound __instance, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState, ref Task __result)
	{

		if (__instance.Owner == null) return true;
		__result = PhylacteryUnboundAfterSideTurnStartReplacement(__instance, side, participants);
		return false;
	}

	internal static async Task PhylacteryUnboundAfterSideTurnStartReplacement(PhylacteryUnbound self, CombatSide side, IReadOnlyList<Creature> participants)
	{
		if (self.Owner == null || side != self.Owner.Creature.Side || !participants.Contains(self.Owner.Creature))
		{
			return;
		}

		await OstyCmd.Summon(new ThrowingPlayerChoiceContext(), self.Owner, self.Owner.IsOstyAlive ? 4m : 2m, self);
		Flash(self);
	}

	internal static void CrackedCoreBeforeSideTurnStartPostfix(CrackedCore __instance, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState, ref Task __result)
	{
		__result = CrackedCoreBeforeSideTurnStartAfterOriginal(__result, __instance, side, participants, combatState);
	}

	internal static async Task CrackedCoreBeforeSideTurnStartAfterOriginal(Task original, CrackedCore self, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
	{
		await original;

		if (self.Owner == null || side != self.Owner.Creature.Side || !participants.Contains(self.Owner.Creature) || combatState.RoundNumber != 3)
		{
			return;
		}

		await GainFocus(self.Owner, 1m);
		Flash(self);
	}

	internal static void InfusedCoreAfterSideTurnStartPostfix(InfusedCore __instance, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState, ref Task __result)
	{
		__result = InfusedCoreAfterSideTurnStartAfterOriginal(__result, __instance, side, participants, combatState);
	}

	internal static async Task InfusedCoreAfterSideTurnStartAfterOriginal(Task original, InfusedCore self, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
	{
		await original;

		if (self.Owner == null || side != self.Owner.Creature.Side || !participants.Contains(self.Owner.Creature) || combatState.RoundNumber > 1)
		{
			return;
		}

		await GainFocus(self.Owner, 1m);
		Flash(self);
	}

	internal static bool IsAtOrBelowHalfHp(Creature creature)
	{
		return creature.CurrentHp * 2 <= creature.MaxHp;
	}

	internal static async Task GainFocus(Player owner, decimal amount)
	{
		await PowerCmd.Apply<FocusPower>(new ThrowingPlayerChoiceContext(), owner.Creature, amount, owner.Creature, null);
	}

	internal static async Task SelectAndDiscardOne(PlayerChoiceContext choiceContext, Player? owner, RelicModel source)
	{
		if (owner == null || owner.Creature.IsDead || owner.PlayerCombatState == null)
		{
			return;
		}

		var selectedCards = (await CardSelectCmd.FromHandForDiscard(
			choiceContext,
			owner,
			new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, 1),
			filter: null,
			source)).ToList();
		if (selectedCards.Count == 0)
		{
			return;
		}

		await CardCmd.Discard(choiceContext, selectedCards);
		Flash(source);
	}

	internal static IEnumerable<DynamicVar> MergeVars(IEnumerable<DynamicVar> original, DynamicVar[] replacement)
	{
		var keys = replacement.Select(value => value.Name).ToHashSet(StringComparer.Ordinal);
		return original.Where(value => !keys.Contains(value.Name)).Concat(replacement).ToArray();
	}

	internal static void Flash(RelicModel relic)
	{
		// 表现失败不能让已经同步的结算中断；两个目标版本均提供公开 Flash()。
		try { relic.Flash(); }
		catch (Exception exception) { Log.Warn($"[BetterCharacterRelics] Relic flash failed: {exception.GetBaseException().Message}"); }
	}
}
