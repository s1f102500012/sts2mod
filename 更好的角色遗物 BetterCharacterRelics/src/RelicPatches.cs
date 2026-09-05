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

[RelicPatch("blood.burning", "战后治疗", "BurningBlood")]
[HarmonyPatch(typeof(BurningBlood), "AfterCombatVictory", new Type[] { typeof(CombatRoom) })]
internal static class BurningBloodAfterCombatVictoryPatch
{
	[HarmonyPrefix, HarmonyPriority(Priority.Low)]
	internal static bool Prefix(BurningBlood __instance, ref Task __result)
		=> RelicEffects.BurningBloodAfterCombatVictoryPrefix(__instance, ref __result);
}

[RelicPatch("blood.black", "战后治疗", "BlackBlood")]
[HarmonyPatch(typeof(BlackBlood), "AfterCombatVictory", new Type[] { typeof(CombatRoom) })]
internal static class BlackBloodAfterCombatVictoryPatch
{
	[HarmonyPrefix, HarmonyPriority(Priority.Low)]
	internal static bool Prefix(BlackBlood __instance, ref Task __result)
		=> RelicEffects.BlackBloodAfterCombatVictoryPrefix(__instance, ref __result);
}

[RelicPatch("stars.right", "神圣权利", "DivineRight")]
[HarmonyPatch(typeof(AbstractModel), "AfterEnergyResetLate", new Type[] { typeof(Player) })]
internal static class AfterEnergyResetLatePatch
{
	[HarmonyPostfix, HarmonyPriority(Priority.Normal)]
	internal static void Postfix(AbstractModel __instance, Player player, ref Task __result)
		=> RelicEffects.AfterEnergyResetLatePostfix(__instance, player, ref __result);
}

[RelicPatch("stars.destiny.entry", "神圣命运", "DivineDestiny")]
[HarmonyPatch(typeof(AbstractModel), "AfterRoomEntered", new Type[] { typeof(AbstractRoom) })]
internal static class AfterRoomEnteredPatch
{
	[HarmonyPostfix, HarmonyPriority(Priority.Normal)]
	internal static void Postfix(AbstractModel __instance, AbstractRoom room, ref Task __result)
		=> RelicEffects.AfterRoomEnteredPostfix(__instance, room, ref __result);
}

[RelicPatch("rings.discard", "戒指弃牌", "RingOfTheSnake|RingOfTheDrake")]
[HarmonyPatch(typeof(AbstractModel), "AfterPlayerTurnStart", new Type[] { typeof(PlayerChoiceContext), typeof(Player) })]
internal static class AfterPlayerTurnStartPatch
{
	[HarmonyPostfix, HarmonyPriority(Priority.Normal)]
	internal static void Postfix(AbstractModel __instance, PlayerChoiceContext choiceContext, Player player, ref Task __result)
		=> RelicEffects.AfterPlayerTurnStartPostfix(__instance, choiceContext, player, ref __result);
}

[RelicPatch("rings.snake.draw", "蛇戒", "RingOfTheSnake")]
[HarmonyPatch(typeof(RingOfTheSnake), "ModifyHandDraw", new Type[] { typeof(Player), typeof(decimal) })]
internal static class RingOfTheSnakeModifyHandDrawPatch
{
	[HarmonyPostfix, HarmonyPriority(Priority.Normal)]
	internal static void Postfix(RingOfTheSnake __instance, Player player, decimal count, ref decimal __result, bool __runOriginal)
		=> RelicEffects.RingOfTheSnakeModifyHandDrawPostfix(__instance, player, count, ref __result, __runOriginal);
}

[RelicPatch("rings.drake.draw", "龙戒", "RingOfTheDrake")]
[HarmonyPatch(typeof(RingOfTheDrake), "ModifyHandDraw", new Type[] { typeof(Player), typeof(decimal) })]
internal static class RingOfTheDrakeModifyHandDrawPatch
{
	[HarmonyPostfix, HarmonyPriority(Priority.Normal)]
	internal static void Postfix(RingOfTheDrake __instance, Player player, decimal count, ref decimal __result, bool __runOriginal)
		=> RelicEffects.RingOfTheDrakeModifyHandDrawPostfix(__instance, player, count, ref __result, __runOriginal);
}

[RelicPatch("rings.snake.vars", "蛇戒", "RingOfTheSnake")]
[HarmonyPatch(typeof(RingOfTheSnake), "CanonicalVars", MethodType.Getter)]
internal static class RingOfTheSnakeCanonicalVarsPatch
{
	[HarmonyPostfix, HarmonyPriority(Priority.Normal)]
	internal static void Postfix(ref IEnumerable<DynamicVar> __result)
		=> RelicEffects.RingOfTheSnakeCanonicalVarsPostfix(ref __result);
}

[RelicPatch("rings.drake.vars", "龙戒", "RingOfTheDrake")]
[HarmonyPatch(typeof(RingOfTheDrake), "CanonicalVars", MethodType.Getter)]
internal static class RingOfTheDrakeCanonicalVarsPatch
{
	[HarmonyPostfix, HarmonyPriority(Priority.Normal)]
	internal static void Postfix(ref IEnumerable<DynamicVar> __result)
		=> RelicEffects.RingOfTheDrakeCanonicalVarsPostfix(ref __result);
}

[RelicPatch("stars.right.vars", "神圣权利", "DivineRight")]
[HarmonyPatch(typeof(DivineRight), "CanonicalVars", MethodType.Getter)]
internal static class DivineRightCanonicalVarsPatch
{
	[HarmonyPostfix, HarmonyPriority(Priority.Normal)]
	internal static void Postfix(ref IEnumerable<DynamicVar> __result)
		=> RelicEffects.DivineRightCanonicalVarsPostfix(ref __result);
}

[RelicPatch("stars.destiny.vars", "神圣命运", "DivineDestiny")]
[HarmonyPatch(typeof(DivineDestiny), "CanonicalVars", MethodType.Getter)]
internal static class DivineDestinyCanonicalVarsPatch
{
	[HarmonyPostfix, HarmonyPriority(Priority.Normal)]
	internal static void Postfix(ref IEnumerable<DynamicVar> __result)
		=> RelicEffects.DivineDestinyCanonicalVarsPostfix(ref __result);
}

[RelicPatch("stars.destiny.turn", "神圣命运", "DivineDestiny")]
[HarmonyPatch(typeof(DivineDestiny), "AfterSideTurnStart", new Type[] { typeof(CombatSide), typeof(IReadOnlyList<Creature>), typeof(ICombatState) })]
internal static class DivineDestinyAfterSideTurnStartPatch
{
	[HarmonyPrefix, HarmonyPriority(Priority.Low)]
	internal static bool Prefix(DivineDestiny __instance, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState, ref Task __result)
		=> RelicEffects.DivineDestinyAfterSideTurnStartPrefix(__instance, side, participants, combatState, ref __result);
}

[RelicPatch("osty.bound.entry", "束缚命匣", "BoundPhylactery")]
[HarmonyPatch(typeof(BoundPhylactery), "BeforeCombatStart", new Type[] {  })]
internal static class BoundPhylacteryBeforeCombatStartPatch
{
	[HarmonyPrefix, HarmonyPriority(Priority.Low)]
	internal static bool Prefix(BoundPhylactery __instance, ref Task __result)
		=> RelicEffects.BoundPhylacteryBeforeCombatStartPrefix(__instance, ref __result);
}

[RelicPatch("osty.bound.turn", "束缚命匣", "BoundPhylactery")]
[HarmonyPatch(typeof(BoundPhylactery), "AfterEnergyResetLate", new Type[] { typeof(Player) })]
internal static class BoundPhylacteryAfterEnergyResetLatePatch
{
	[HarmonyPrefix, HarmonyPriority(Priority.Low)]
	internal static bool Prefix(BoundPhylactery __instance, Player player, ref Task __result)
		=> RelicEffects.BoundPhylacteryAfterEnergyResetLatePrefix(__instance, player, ref __result);
}

[RelicPatch("osty.unbound.turn", "解放命匣", "PhylacteryUnbound")]
[HarmonyPatch(typeof(PhylacteryUnbound), "AfterSideTurnStart", new Type[] { typeof(CombatSide), typeof(IReadOnlyList<Creature>), typeof(ICombatState) })]
internal static class PhylacteryUnboundAfterSideTurnStartPatch
{
	[HarmonyPrefix, HarmonyPriority(Priority.Low)]
	internal static bool Prefix(PhylacteryUnbound __instance, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState, ref Task __result)
		=> RelicEffects.PhylacteryUnboundAfterSideTurnStartPrefix(__instance, side, participants, combatState, ref __result);
}

[RelicPatch("core.cracked", "破损核心", "CrackedCore")]
[HarmonyPatch(typeof(CrackedCore), "BeforeSideTurnStart", new Type[] { typeof(PlayerChoiceContext), typeof(CombatSide), typeof(IReadOnlyList<Creature>), typeof(ICombatState) })]
internal static class CrackedCoreBeforeSideTurnStartPatch
{
	[HarmonyPostfix, HarmonyPriority(Priority.Normal)]
	internal static void Postfix(CrackedCore __instance, PlayerChoiceContext choiceContext, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState, ref Task __result)
		=> RelicEffects.CrackedCoreBeforeSideTurnStartPostfix(__instance, choiceContext, side, participants, combatState, ref __result);
}

[RelicPatch("core.infused", "注能核心", "InfusedCore")]
[HarmonyPatch(typeof(InfusedCore), "AfterSideTurnStart", new Type[] { typeof(CombatSide), typeof(IReadOnlyList<Creature>), typeof(ICombatState) })]
internal static class InfusedCoreAfterSideTurnStartPatch
{
	[HarmonyPostfix, HarmonyPriority(Priority.Normal)]
	internal static void Postfix(InfusedCore __instance, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState, ref Task __result)
		=> RelicEffects.InfusedCoreAfterSideTurnStartPostfix(__instance, side, participants, combatState, ref __result);
}
