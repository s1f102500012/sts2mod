using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace UniversalDominionSword;

public sealed class UniversalDominionSwordCard : CardModel
{
	private const int MaximumEnergyCost = 999_999_999;

	private int _permanentCostIncrease;

	public override CardPoolModel Pool => IsMutable && Owner != null
		? Owner.Character.CardPool
		: ModelDb.CardPool<TokenCardPool>();

	public override CardPoolModel VisualCardPool => Pool;

	public override string PortraitPath => ModInfo.CardPortraitPath;

	public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

	[SavedProperty]
	public int PermanentCostIncrease
	{
		get => _permanentCostIncrease;
		set
		{
			AssertMutable();
			_permanentCostIncrease = Math.Clamp(value, 0, MaximumEnergyCost);
			EnergyCost.SetCustomBaseCost(Math.Min(
				EnergyCost.Canonical + _permanentCostIncrease,
				MaximumEnergyCost));
		}
	}

	public UniversalDominionSwordCard()
		: base(0, CardType.Attack, CardRarity.Token, TargetType.AnyEnemy, shouldShowInCardLibrary: true)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		Creature target = cardPlay.Target
			?? throw new InvalidOperationException("Erasure requires a single enemy target.");
		ICombatState combatState = CombatState
			?? target.CombatState
			?? throw new InvalidOperationException("Erasure requires an active combat.");

		await CreatureCmd.TriggerAnim(
			Owner.Creature,
			"Attack",
			Owner.Character.AttackAnimDelay);
		using ErasurePersistenceLease persistence =
			ErasureKill.BeginPersistenceLease(combatState);

		IncreasePermanentCost();
		if (DeckVersion is UniversalDominionSwordCard deckVersion)
		{
			deckVersion.IncreasePermanentCost();
		}
		persistence.Commit();
		await ErasureKill.Execute(target, combatState);
	}

	protected override void OnUpgrade()
	{
	}

	private void IncreasePermanentCost()
	{
		PermanentCostIncrease = Math.Min(
			PermanentCostIncrease + 1,
			MaximumEnergyCost);
	}
}
