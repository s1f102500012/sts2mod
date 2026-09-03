using HextechRunes;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HextechRunesSponsorPack;

public sealed class OtterAndFriendsRune : HextechRelicBase
{
	private int _cycleIndex;
	private HextechCombatState? _cycleCombatState;
	private int _cycleRoundNumber = -1;
	private int _activeCycle = -1;
	private bool _flashedThisRound;
	private bool _addedBelieveInYouThisRound;
	private readonly HashSet<ulong> _buffedPlayersThisRound = [];

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedCycleIndex
	{
		get => _cycleIndex;
		set => _cycleIndex = Math.Max(0, value);
	}

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new CardsVar(2),
		new EnergyVar(1),
		new PowerVar<StrengthPower>(2m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromCard<BelieveInYou>(),
		HoverTipFactory.FromPower<StrengthPower>()
	];

	public override bool IsAvailableForPlayer(Player player)
	{
		return IsNetworkMultiplayer();
	}

	public override async Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
	{
		if (Owner == null
			|| Owner.Creature.IsDead
			|| player.Creature.IsDead
			|| player.Creature.CombatState is not HextechCombatState combatState
			|| !ReferenceEquals(Owner.Creature.CombatState, combatState))
		{
			return;
		}

		int cycle = GetCycleForRound(combatState);
		if (!_flashedThisRound)
		{
			FlashDeferred(combatState.Players
				.Where(static combatPlayer => combatPlayer.Creature.IsAlive)
				.Select(static combatPlayer => combatPlayer.Creature));
			_flashedThisRound = true;
		}

		if (player == Owner && !_addedBelieveInYouThisRound)
		{
			await AddCardCopiesToCombatHand<BelieveInYou>(1);
			_addedBelieveInYouThisRound = true;
		}

		if (!_buffedPlayersThisRound.Add(player.NetId))
		{
			return;
		}

		switch (cycle)
		{
			case 0:
				await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, player, fromHandDraw: false);
				break;
			case 1:
				await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, player);
				break;
			default:
				await PowerCmd.Apply<StrengthPower>(player.Creature, DynamicVars.Strength.BaseValue, Owner.Creature, null);
				break;
		}
	}

	private int GetCycleForRound(HextechCombatState combatState)
	{
		if (!ReferenceEquals(_cycleCombatState, combatState) || _cycleRoundNumber != combatState.RoundNumber)
		{
			_cycleCombatState = combatState;
			_cycleRoundNumber = combatState.RoundNumber;
			_activeCycle = _cycleIndex % 3;
			SavedCycleIndex = _cycleIndex + 1;
			_flashedThisRound = false;
			_addedBelieveInYouThisRound = false;
			_buffedPlayersThisRound.Clear();
		}

		return _activeCycle;
	}
}
