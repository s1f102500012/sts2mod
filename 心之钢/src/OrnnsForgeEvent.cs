using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace Heartsteel;

public sealed class OrnnsForge : EventModel
{
	private const int GreetingGold = 60;

	private const int TradeGoldCost = 250;

	private const int TradeMaxHpGain = 6;

	private const int StealHpLoss = 28;

	protected override IReadOnlyList<EventOption> GenerateInitialOptions()
	{
		RelicModel fairTradeRelic = ModelDb.Relic<HeartsteelRelic>().ToMutable();
		RelicModel stealRelic = ModelDb.Relic<HeartsteelRelic>().ToMutable();

		List<EventOption> options =
		[
			new EventOption(this, Greet, "ORNNS_FORGE.pages.INITIAL.options.GREET"),
			(base.Owner.Gold >= TradeGoldCost
				? CreateRelicOptionWithHoverTips(fairTradeRelic, FairTrade, "ORNNS_FORGE.pages.INITIAL.options.FAIR_TRADE")
				: new EventOption(this, null, "ORNNS_FORGE.pages.INITIAL.options.FAIR_TRADE_LOCKED")),
			(base.Owner.Creature.CurrentHp >= StealHpLoss + 1
				? CreateRelicOptionWithHoverTips(stealRelic, GrabAndRun, "ORNNS_FORGE.pages.INITIAL.options.GRAB_AND_RUN").ThatDoesDamage(StealHpLoss)
				: new EventOption(this, null, "ORNNS_FORGE.pages.INITIAL.options.GRAB_AND_RUN_LOCKED"))
		];

		return options;
	}

	public override bool IsAllowed(IRunState runState)
	{
		return runState.Players.All(static player => player.Gold >= TradeGoldCost || player.Creature.CurrentHp >= StealHpLoss + 1);
	}

	private EventOption CreateRelicOptionWithHoverTips(RelicModel relic, Func<Task> onChosen, string textKey)
	{
		return new EventOption(this, onChosen, textKey, relic.HoverTips).WithRelic(relic);
	}

	private async Task Greet()
	{
		await PlayerCmd.GainGold(GreetingGold, Owner);
		SetEventFinished(L10NLookup("ORNNS_FORGE.pages.GREET.description"));
	}

	private async Task FairTrade()
	{
		await PlayerCmd.LoseGold(TradeGoldCost, Owner, GoldLossType.Spent);
		await RelicCmd.Obtain<HeartsteelRelic>(Owner);
		await CreatureCmd.GainMaxHp(Owner.Creature, TradeMaxHpGain);
		SetEventFinished(L10NLookup("ORNNS_FORGE.pages.FAIR_TRADE.description"));
	}

	private async Task GrabAndRun()
	{
		await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), Owner.Creature, StealHpLoss, ValueProp.Unblockable | ValueProp.Unpowered, null, null);
		await RelicCmd.Obtain<HeartsteelRelic>(Owner);
		SetEventFinished(L10NLookup("ORNNS_FORGE.pages.GRAB_AND_RUN.description"));
	}
}
