using MegaCrit.Sts2.Core.Models.Relics;

namespace HextechRunes;

public sealed class ShoulderVakuRune : HextechRelicBase
{
	private static readonly ModelId WhisperingEarringId = ModelDb.GetId<WhisperingEarring>();

	private int _lastControlledRound;
	private bool _controllingTurn;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new EnergyVar(2),
		new CardsVar(2),
		new DynamicVar("HealPercent", 5m)
	];

	// 额外回合不推进 RoundNumber 且回合开始 hook 会重入,奇数回合回血按 RoundNumber 防重。
	private int _lastHealRound = -1;

	public override Task BeforeCombatStart()
	{
		_lastControlledRound = 0;
		_controllingTurn = false;
		_lastHealRound = -1;
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		_lastControlledRound = 0;
		_controllingTurn = false;
		_lastHealRound = -1;
		return Task.CompletedTask;
	}

	public override decimal ModifyHandDraw(Player player, decimal count)
	{
		return player == Owner ? count + DynamicVars.Cards.BaseValue : count;
	}

	public override decimal ModifyMaxEnergy(Player player, decimal amount)
	{
		return player == Owner ? amount + DynamicVars.Energy.BaseValue : amount;
	}

	public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (!IsOddOwnerTurn(player, out int round) || _lastHealRound == round)
		{
			return;
		}

		_lastHealRound = round;
		int heal = Math.Max(1, FloorToInt(Owner!.Creature.MaxHp * DynamicVars["HealPercent"].BaseValue / 100m));
		Flash();
		await CreatureCmd.Heal(Owner.Creature, heal);
	}

	public override Task AfterAutoPrePlayPhaseEnteredLate(PlayerChoiceContext choiceContext, Player player)
	{
		return ControlOddTurnWithVakuu(choiceContext, player);
	}

	private async Task ControlOddTurnWithVakuu(PlayerChoiceContext choiceContext, Player player)
	{
		if (!IsOddOwnerTurn(player, out int round) || _lastControlledRound == round || _controllingTurn)
		{
			return;
		}

		if (round == 1 && OwnerHasWhisperingEarring())
		{
			return;
		}

		_lastControlledRound = round;
		_controllingTurn = true;
		try
		{
			Flash();
			int cardsPlayed = await VakuuTurnController.AutoPlayPlayableHand(Owner!);
			VakuuTurnController.PlayLineIfCardsPlayed(Owner!, cardsPlayed);
		}
		finally
		{
			_controllingTurn = false;
		}
	}

	private bool IsOddOwnerTurn(Player player, out int round)
	{
		round = 0;
		if (player != Owner || Owner == null || Owner.Creature.IsDead || Owner.Creature.CombatState == null)
		{
			return false;
		}

		round = Owner.Creature.CombatState.RoundNumber;
		return round > 0 && round % 2 == 1;
	}

	private bool OwnerHasWhisperingEarring()
	{
		return Owner?.Relics.Any(static relic =>
			(relic.CanonicalInstance?.Id ?? relic.Id) == WhisperingEarringId) == true;
	}
}
