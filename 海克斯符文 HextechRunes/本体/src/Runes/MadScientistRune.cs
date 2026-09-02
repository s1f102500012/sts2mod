using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Orbs;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace HextechRunes;

public sealed class MadScientistRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("OrbSlots", 1m)
	];

	public override bool IsAvailableForPlayer(Player player)
	{
		return IsDefectPlayer(player);
	}

	public override async Task AfterOrbChanneled(PlayerChoiceContext choiceContext, Player player, OrbModel orb)
	{
		if (player != Owner || Owner == null || Owner.Creature.IsDead)
		{
			return;
		}

		int orbSlots = Math.Max(0, DynamicVars["OrbSlots"].IntValue);
		if (orbSlots <= 0)
		{
			return;
		}

		Flash();
		await OrbCmd.AddSlots(Owner, orbSlots);
	}

	[HarmonyPatch(typeof(OrbCmd), nameof(OrbCmd.AddSlots), typeof(Player), typeof(int))]
	[HextechPatch("rune.mad-scientist", "科学狂人", Rune = typeof(MadScientistRune))]
	private static class MadScientistPatch
	{
		[HarmonyPrefix]
		[HarmonyPriority(Priority.Low)]
		private static bool Prefix(Player player, int amount, ref Task __result)
		{
			if (player.GetRelic<MadScientistRune>() == null)
			{
				return true;
			}

			if (CombatManager.Instance.IsOverOrEnding || amount <= 0)
			{
				__result = Task.CompletedTask;
				return false;
			}

			if (player.PlayerCombatState == null)
			{
				return true;
			}

			player.PlayerCombatState.OrbQueue.AddCapacity(amount);
			NCombatRoom.Instance?.GetCreatureNode(player.Creature)?.OrbManager?.AddSlotAnim(amount);
			__result = Task.CompletedTask;
			return false;
		}
	}
}
