using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using MonoMod.RuntimeDetour;

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

public static class OrnnsForgeRegistration
{
	private static Hook? _allSharedEventsHook;

	private delegate IEnumerable<EventModel> OrigGetAllSharedEvents();

	public static void Install()
	{
		PreloadDependencyAssemblies();
		InstallHooks();
		Log.Info("[Heartsteel] Registered Ornn's Forge shared event.");
	}

	private static void PreloadDependencyAssemblies()
	{
		Assembly assembly = Assembly.GetExecutingAssembly();
		string? modDirectory = Path.GetDirectoryName(assembly.Location);
		if (string.IsNullOrEmpty(modDirectory) || !Directory.Exists(modDirectory))
		{
			return;
		}

		string selfPath = assembly.Location;
		AssemblyLoadContext loadContext = AssemblyLoadContext.GetLoadContext(assembly) ?? AssemblyLoadContext.Default;
		foreach (string dllPath in Directory.GetFiles(modDirectory, "*.dll"))
		{
			if (string.Equals(dllPath, selfPath, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			loadContext.LoadFromAssemblyPath(dllPath);
		}
	}

	private static void InstallHooks()
	{
		MethodInfo allSharedEventsGetter = typeof(ModelDb).GetProperty(nameof(ModelDb.AllSharedEvents), BindingFlags.Static | BindingFlags.Public)?.GetMethod
			?? throw new InvalidOperationException("Could not find ModelDb.AllSharedEvents getter.");

		_allSharedEventsHook = new Hook(allSharedEventsGetter, AllSharedEventsDetour);
	}

	private static IEnumerable<EventModel> AllSharedEventsDetour(OrigGetAllSharedEvents orig)
	{
		return orig().Concat([ModelDb.Event<OrnnsForge>()]).Distinct();
	}
}
