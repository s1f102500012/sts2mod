using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HextechRunes.Tests;

// 队友额外回合（佩尔之眼等）只带那名玩家重入回合钩子：side 照样是玩家侧、回合号也不推进，
// 只有 participants 能区分"持有者这回合没动"。原版玩家侧回合结束的 participants 不含宠物。
internal static partial class Program
{
	private static void TurnHooksSkipOwnersAbsentFromParticipants()
	{
		var (_, first, second) = CreatePrismaticEnemyFixture();
		CombatState combat = (CombatState)first.Creature.CombatState!;
		Creature enemy = CreatePrismaticTestCreature(CombatSide.Enemy, combat);
		IReadOnlyList<Creature> both = [first.Creature, second.Creature];
		IReadOnlyList<Creature> teammateOnly = [second.Creature];

		ParticipantProbeRelic relic = CreateMutableTestModel<ParticipantProbeRelic>();
		relic.Owner = first;
		RunTurnHooks(relic, CombatSide.Player, teammateOnly, combat);
		Equal(0, relic.Calls, "teammate's extra turn does not reach the absent owner's relic");
		RunTurnHooks(relic, CombatSide.Player, both, combat);
		Equal(4, relic.Calls, "normal player turn still reaches every relic hook");
		RunTurnHooks(relic, CombatSide.Enemy, [enemy], combat);
		Equal(8, relic.Calls, "enemy-side turns are left to the relic itself");

		Creature pet = CreatePrismaticTestCreature(CombatSide.Player, combat);
		AccessTools.Field(typeof(Creature), "_petOwner").SetValue(pet, first);
		ParticipantProbePower power = CreateMutableTestModel<ParticipantProbePower>();
		AccessTools.Property(typeof(PowerModel), nameof(PowerModel.Owner)).SetValue(power, pet);
		RunTurnHooks(power, CombatSide.Player, [first.Creature], combat);
		Equal(4, power.Calls, "a pet's power follows its owner, who is the only listed creature at player turn end");
		RunTurnHooks(power, CombatSide.Player, teammateOnly, combat);
		Equal(4, power.Calls, "a pet's power sits out the teammate's extra turn");

		Expect(HextechTurnParticipants.Includes(both, pet), "pets count as taking part when their owner does");
		SequenceEqual(
			[second.Creature],
			HextechEnemyHexContext.FilterTakingTurn([first.Creature, pet, second.Creature], teammateOnly),
			"per-player enemy hexes only touch the players taking this turn");
		SequenceEqual(
			[first.Creature, pet, second.Creature],
			HextechEnemyHexContext.FilterTakingTurn([first.Creature, pet, second.Creature], null),
			"outside turn hooks every player is included");
	}

	private static void FeyMagicKeepsPendingNoDrawForPlayersNotTakingTheTurn()
	{
		var (context, first, second) = CreatePrismaticEnemyFixture();
		CombatState combat = (CombatState)first.Creature.CombatState!;
		AccessTools.Field(typeof(Creature), "<CombatId>k__BackingField").SetValue(first.Creature, (uint?)11);
		((List<Creature>)AccessTools.Field(typeof(CombatState), "_allies").GetValue(combat)!).Add(first.Creature);
		context.Tracking.FeyMagicPendingNoDrawPlayers[11] = 99;

		new FeyMagicEnemyHex().BeforePlayerSideTurnStart(context, combat, [second.Creature]).GetAwaiter().GetResult();
		Expect(context.Tracking.FeyMagicPendingNoDrawPlayers.ContainsKey(11), "the hit player's no-draw waits for their own next turn");
	}

	private static void CombatStartForgesUseTheOwnersOwnTurnNumber()
	{
		var (_, first, _) = CreatePrismaticEnemyFixture();
		PreparedForge forge = CreateMutableTestModel<PreparedForge>();
		forge.Owner = first;
		SetTurnNumber(first, 1);
		Expect(forge.ModifyHandDraw(first, 5m) > 5m, "first turn of the combat draws extra");
		SetTurnNumber(first, 2);
		Equal(5m, forge.ModifyHandDraw(first, 5m), "an extra turn in round 1 is the owner's second turn and draws normally");
	}

	private static void SetTurnNumber(Player player, int turnNumber)
	{
		AccessTools.Field(typeof(PlayerCombatState), "<TurnNumber>k__BackingField").SetValue(player.PlayerCombatState, turnNumber);
	}

	private static void RunTurnHooks(AbstractModel model, CombatSide side, IReadOnlyList<Creature> participants, CombatState combat)
	{
		model.BeforeSideTurnStart(null!, side, participants, combat).GetAwaiter().GetResult();
		model.AfterSideTurnStart(side, participants, combat).GetAwaiter().GetResult();
		model.BeforeSideTurnEnd(null!, side, participants).GetAwaiter().GetResult();
		model.AfterSideTurnEnd(null!, side, participants).GetAwaiter().GetResult();
	}

	private sealed class ParticipantProbeRelic : HextechRelicBase
	{
		public int Calls { get; private set; }

		public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, ICombatState combatState) => Count();
		public override Task AfterSideTurnStart(CombatSide side, ICombatState combatState) => Count();
		public override Task BeforeTurnEnd(PlayerChoiceContext choiceContext, CombatSide side) => Count();
		public override Task AfterTurnEnd(PlayerChoiceContext choiceContext, CombatSide side) => Count();

		private Task Count()
		{
			Calls++;
			return Task.CompletedTask;
		}
	}

	private sealed class ParticipantProbePower : HextechPowerBase
	{
		public int Calls { get; private set; }

		public override PowerType Type => PowerType.Buff;
		public override PowerStackType StackType => PowerStackType.Counter;

		public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, ICombatState combatState) => Count();
		public override Task AfterSideTurnStart(CombatSide side, ICombatState combatState) => Count();
		public override Task BeforeTurnEnd(PlayerChoiceContext choiceContext, CombatSide side) => Count();
		public override Task AfterTurnEnd(PlayerChoiceContext choiceContext, CombatSide side) => Count();

		private Task Count()
		{
			Calls++;
			return Task.CompletedTask;
		}
	}
}
