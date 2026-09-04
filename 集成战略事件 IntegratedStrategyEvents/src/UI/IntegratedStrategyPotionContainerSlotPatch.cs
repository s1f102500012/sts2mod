using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Potions;

namespace IntegratedStrategyEvents.UI;

internal static class IntegratedStrategyPotionContainerSlotUi
{
	private static readonly AccessTools.FieldRef<NPotionContainer, List<NPotionHolder>> HoldersRef =
		IntegratedStrategyPrivateMembers.FieldRef<NPotionContainer, List<NPotionHolder>>("_holders");
	private static readonly MethodInfo? UpdateNavigationMethod =
		IntegratedStrategyPrivateMembers.Method(typeof(NPotionContainer), "UpdateNavigation");

	public static void ShrinkTo(NPotionContainer container, int newMaxPotionSlots)
	{
		if (!IntegratedStrategyPatcher.IsAvailable("potion-ui")) return;
		List<NPotionHolder> holders = HoldersRef(container);
		int targetCount = Math.Max(0, newMaxPotionSlots);
		if (holders.Count <= targetCount)
		{
			return;
		}

		while (holders.Count > targetCount)
		{
			int index = holders.FindIndex(static holder => holder.Potion == null);
			if (index < 0)
			{
				index = holders.Count - 1;
			}

			RemoveHolderAt(holders, index);
		}

		UpdateNavigationMethod?.Invoke(container, null);
	}

	private static void RemoveHolderAt(List<NPotionHolder> holders, int index)
	{
		NPotionHolder holder = holders[index];
		holders.RemoveAt(index);
		Node? parent = holder.GetParent();
		parent?.RemoveChild(holder);
		holder.QueueFree();
	}
}

[HarmonyPatch(typeof(NPotionContainer), "GrowPotionHolders")]
[IntegratedStrategyPatch("IntegratedStrategyPotionContainerGrowPotionHoldersPatch", "potion-ui", "药水槽缩减显示")]
internal static class IntegratedStrategyPotionContainerGrowPotionHoldersPatch
{
	private static void Postfix(NPotionContainer __instance, int newMaxPotionSlots)
	{
		IntegratedStrategyPotionContainerSlotUi.ShrinkTo(__instance, newMaxPotionSlots);
	}
}
