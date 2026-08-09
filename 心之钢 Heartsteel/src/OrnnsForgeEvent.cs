using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
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
using STS2RitsuLib.Scaffolding.Content;

namespace Heartsteel;

public sealed class OrnnsForge : ModEventTemplate
{
	private const int GreetingGold = 60;

	private const int TradeGoldCost = 250;

	private const int TradeMaxHpGain = 6;

	private const int StealHpLoss = 28;

	public override EventAssetProfile AssetProfile { get; } = new(
		InitialPortraitPath: ModInfo.OrnnsForgePortraitPath);

	protected override IReadOnlyList<EventOption> GenerateInitialOptions()
	{
		Player owner = GetOwnerOrThrow();
		RelicModel fairTradeRelic = ModelDb.Relic<HeartsteelRelic>().ToMutable();
		RelicModel stealRelic = ModelDb.Relic<HeartsteelRelic>().ToMutable();

		List<EventOption> options =
		[
			new EventOption(this, Greet, InitialOptionKey("GREET")),
			(owner.Gold >= TradeGoldCost
				? CreateRelicOptionWithHoverTips(fairTradeRelic, FairTrade, InitialOptionKey("FAIR_TRADE"))
				: new EventOption(this, null, InitialOptionKey("FAIR_TRADE_LOCKED"))),
			(owner.Creature.CurrentHp >= StealHpLoss + 1
				? CreateRelicOptionWithHoverTips(stealRelic, GrabAndRun, InitialOptionKey("GRAB_AND_RUN")).ThatDoesDamage(StealHpLoss)
				: new EventOption(this, null, InitialOptionKey("GRAB_AND_RUN_LOCKED")))
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
		Player owner = GetOwnerOrThrow();
		await PlayerCmd.GainGold(GreetingGold, owner);
		SetEventFinished(PageDescription("GREET"));
	}

	private async Task FairTrade()
	{
		Player owner = GetOwnerOrThrow();
		await PlayerCmd.LoseGold(TradeGoldCost, owner, GoldLossType.Spent);
		await RelicCmd.Obtain<HeartsteelRelic>(owner);
		await CreatureCmd.GainMaxHp(owner.Creature, TradeMaxHpGain);
		SetEventFinished(PageDescription("FAIR_TRADE"));
	}

	private async Task GrabAndRun()
	{
		Player owner = GetOwnerOrThrow();
		await CreatureCmd.Damage(
			new ThrowingPlayerChoiceContext(),
			owner.Creature,
			StealHpLoss,
			ValueProp.Unblockable | ValueProp.Unpowered,
			dealer: null!);
		await RelicCmd.Obtain<HeartsteelRelic>(owner);
		SetEventFinished(PageDescription("GRAB_AND_RUN"));
	}

	private Player GetOwnerOrThrow()
	{
		return Owner ?? throw new InvalidOperationException("Ornn's Forge event has no owner.");
	}
}

public static class OrnnsForgeRegistration
{
	private static bool _installed;

	public static void Install()
	{
		if (_installed)
		{
			return;
		}

		MethodInfo getter = typeof(ModelDb)
			.GetProperty(nameof(ModelDb.AllSharedEvents), BindingFlags.Static | BindingFlags.Public)?.GetMethod
			?? throw new InvalidOperationException("Could not find ModelDb.AllSharedEvents getter.");

		new Harmony("Natsuki.Heartsteel.SharedEvent")
			.Patch(getter, postfix: new HarmonyMethod(typeof(OrnnsForgeRegistration), nameof(AppendSharedEvent)));
		_installed = true;
	}

	private static void AppendSharedEvent(ref IEnumerable<EventModel> __result)
	{
		__result = __result.Concat([ModelDb.Event<OrnnsForge>()]).Distinct();
	}
}
