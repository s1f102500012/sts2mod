using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace AITeammate.Scripts;

internal static class AiTeammateCombatRoomNavigationPatches
{
    private static readonly FieldInfo? CreatureNodesField = AccessTools.Field(typeof(NCombatRoom), "_creatureNodes");

    [HarmonyPatch(typeof(NCombatRoom), "UpdateCreatureNavigation")]
    private static class UpdateCreatureNavigationPatch
    {
        private static bool Prefix(NCombatRoom __instance)
        {
            if (!ShouldUseSafeNavigation())
            {
                return true;
            }

            if (!TryGetCreatureNodes(__instance, out List<NCreature> creatureNodes))
            {
                return true;
            }

            PruneInvalidNodes(creatureNodes);
            List<NCreature> interactableNodes = creatureNodes
                .Where(IsNavigationReady)
                .Where(static node => node.IsInteractable)
                .OrderBy(static node => node.GlobalPosition.X)
                .ToList();

            for (int i = 0; i < interactableNodes.Count; i++)
            {
                NCreature current = interactableNodes[i];
                Control hitbox = current.Hitbox;
                Control left = interactableNodes[i <= 0 ? interactableNodes.Count - 1 : i - 1].Hitbox;
                Control right = interactableNodes[i < interactableNodes.Count - 1 ? i + 1 : 0].Hitbox;
                hitbox.FocusNeighborLeft = left.GetPath();
                hitbox.FocusNeighborRight = right.GetPath();
                hitbox.FocusNeighborTop = hitbox.GetPath();
                if (__instance.Ui?.Hand?.CardHolderContainer != null)
                {
                    hitbox.FocusNeighborBottom = __instance.Ui.Hand.CardHolderContainer.GetPath();
                }

                current.UpdateNavigation();
            }

            Control? handContainer = __instance.Ui?.Hand?.CardHolderContainer;
            Control? firstHitbox = creatureNodes.FirstOrDefault(IsNavigationReady)?.Hitbox;
            if (handContainer != null && firstHitbox != null)
            {
                handContainer.FocusNeighborTop = firstHitbox.GetPath();
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom.GetCreatureNode))]
    private static class GetCreatureNodePatch
    {
        private static bool Prefix(NCombatRoom __instance, Creature? creature, ref NCreature? __result)
        {
            if (!ShouldUseSafeNavigation())
            {
                return true;
            }

            if (creature == null)
            {
                __result = null;
                return false;
            }

            if (!TryGetCreatureNodes(__instance, out List<NCreature> creatureNodes))
            {
                return true;
            }

            PruneInvalidNodes(creatureNodes);
            __result = creatureNodes.FirstOrDefault(node =>
                IsCreatureNodeReady(node) &&
                ReferenceEquals(node.Entity, creature));
            return false;
        }
    }

    private static bool ShouldUseSafeNavigation()
    {
        return AiTeammateSessionRegistry.ActiveRunSession != null ||
               AiTeammateSessionRegistry.AutopilotEnabled;
    }

    private static bool TryGetCreatureNodes(NCombatRoom room, out List<NCreature> creatureNodes)
    {
        if (CreatureNodesField?.GetValue(room) is List<NCreature> nodes)
        {
            creatureNodes = nodes;
            return true;
        }

        creatureNodes = null!;
        Log.Warn("[AITeammate] Could not access NCombatRoom._creatureNodes for safe navigation patch.");
        return false;
    }

    private static void PruneInvalidNodes(List<NCreature> creatureNodes)
    {
        int removed = creatureNodes.RemoveAll(static node => !IsCreatureNodeReady(node));
        if (removed > 0)
        {
            Log.Warn($"[AITeammate] Removed {removed} invalid combat creature node(s) before updating navigation.");
        }
    }

    private static bool IsCreatureNodeReady(NCreature? node)
    {
        return node != null &&
               GodotObject.IsInstanceValid(node) &&
               node.Entity != null;
    }

    private static bool IsNavigationReady(NCreature? node)
    {
        return IsCreatureNodeReady(node) &&
               node!.Hitbox != null &&
               GodotObject.IsInstanceValid(node.Hitbox);
    }
}
