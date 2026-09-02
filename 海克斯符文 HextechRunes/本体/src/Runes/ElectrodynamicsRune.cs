using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Orbs;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace HextechRunes;

public sealed class ElectrodynamicsRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("OrbCount", 1m)
	];

	public override bool IsAvailableForPlayer(Player player)
	{
		return IsDefectPlayer(player);
	}

	public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (player != Owner || Owner == null || Owner.Creature.IsDead || Owner.Creature.CombatState == null)
		{
			return;
		}

		Flash();
		for (int i = 0; i < DynamicVars["OrbCount"].IntValue; i++)
		{
			OrbModel orb = ModelDb.Orb<LightningOrb>().ToMutable();
			await OrbCmd.Channel(choiceContext, orb, Owner);
		}
	}

	// 0.108 起 ApplyLightningDamage 追加 isEvoke 参数。
#if STS2_108_OR_NEWER
	[HarmonyPatch(typeof(LightningOrb), "ApplyLightningDamage", typeof(decimal), typeof(Creature), typeof(PlayerChoiceContext), typeof(bool))]
#else
	[HarmonyPatch(typeof(LightningOrb), "ApplyLightningDamage", typeof(decimal), typeof(Creature), typeof(PlayerChoiceContext))]
#endif
	[HextechPatch("rune.electrodynamics", "电动力学", Rune = typeof(ElectrodynamicsRune))]
	private static class ElectrodynamicsPatch
	{
		[HarmonyPrefix]
		[HarmonyPriority(Priority.Low)]
		private static bool Prefix(LightningOrb __instance, decimal value, Creature? target, PlayerChoiceContext choiceContext, ref Task<IEnumerable<Creature>> __result)
		{
			if (__instance.Owner?.GetRelic<ElectrodynamicsRune>() == null)
			{
				return true;
			}

			__result = HextechPlayerRuneHooks.ApplyElectrodynamicsLightningDamage(__instance, value, choiceContext);
			return false;
		}
	}
}
