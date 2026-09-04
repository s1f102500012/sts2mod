
namespace IntegratedStrategyEvents.Events;

public sealed partial class TimidThievesEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"timid_thieves.png",
			CreateLocalization,
			IntegratedStrategyEventLayoutProfile.StandardNarrowSlightlyShiftedRight);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"鼠胆神偷",
				SearchPageLocZhs(
					InitialPage,
					"你漫无目地游荡在[aqua]古堡[/aqua]中，不经意间踩破了一块[jitter][red]腐朽的地板[/red][/jitter]，一路滚到了古堡的[aqua]地下室[/aqua]里。\n\n在这里你看到了堆积如山的[purple]废物与垃圾[/purple]，以及三个[sine][orange]愁眉苦脸的札拉克[/orange][/sine]。札拉克向你坦白，他们其实是三个[gold]寻宝者[/gold]，费尽心机找到了城堡的藏宝室，但是似乎有人捷足先登，藏宝室里只剩下一地[purple]垃圾[/purple]。"),
				SearchPageLocZhs(
					"REPEAT",
					"“[gold]居然还有剩下的东西？[/gold]”\n\n三个[orange]札拉克[/orange]一脸茫然地看着你。"),
				new EventPageLoc(
					"BIG_FOOD",
					"你挖到了一大筐[green]食物[/green]，三个札拉克人[sine][green]流下了口水[/green][/sine]。",
					new EventOptionLoc("TAKE_FOOD", "收起食物", "回复[green]8[/green]点生命。")),
				new EventPageLoc(
					"RELIC",
					"你翻到了一件[gold]稀罕的古物[/gold]，三个札拉克[aqua]瞪大了眼睛[/aqua]。",
					new EventOptionLoc("TAKE_RELIC", "收起古物", "获得一件随机[gold]遗物[/gold]。")),
				new EventPageLoc(
					"BIG_GOLD",
					"你挖到了一箱[gold]金币[/gold]，三个札拉克人用[purple]羡慕的眼神[/purple]看着你。",
					new EventOptionLoc("TAKE_GOLD", "收起金币", "获得[blue]100[/blue][gold]金币[/gold]。")),
				new EventPageLoc(
					"SMALL_GOLD",
					"你挖到了几枚[gold]金币[/gold]，三个札拉克人眼睛[sine][gold]闪闪发亮[/gold][/sine]。",
					new EventOptionLoc("TAKE_GOLD", "收起金币", "获得[blue]30[/blue][gold]金币[/gold]。")),
				new EventPageLoc(
					"SMALL_FOOD",
					"你挖到了一包[green]食物[/green]，三个札拉克人[green]咽了口口水[/green]。",
					new EventOptionLoc("TAKE_FOOD", "收起食物", "回复[green]4[/green]点生命。")),
				new EventPageLoc(
					"NOTHING",
					"你把自己弄得[purple]灰头土脸[/purple]，但是什么都没有找到。",
					new EventOptionLoc("CONTINUE", "继续", "")),
				new EventPageLoc(
					"LEAVE",
					"还是不要在这里[purple]浪费时间[/purple]了。")),
			new EventLoc(
				"Timid Thieves",
				SearchPageLocEng(
					InitialPage,
					"You wander aimlessly through the [aqua]castle[/aqua], accidentally break through a [jitter][red]rotten floor[/red][/jitter], and tumble all the way into the castle [aqua]basement[/aqua].\n\nHere you see mountains of [purple]waste and trash[/purple], along with three [sine][orange]dejected Zalak[/orange][/sine]. They confess that they are treasure hunters who worked tirelessly to find the castle treasure room, but someone seems to have beaten them to it. Only [purple]trash[/purple] remains."),
				SearchPageLocEng(
					"REPEAT",
					"\"[gold]There is still something left?[/gold]\"\n\nThe three [orange]Zalak[/orange] stare at you in confusion."),
				new EventPageLoc(
					"BIG_FOOD",
					"You dig up a large basket of [green]food[/green]. The three Zalak [sine][green]begin to drool[/green][/sine].",
					new EventOptionLoc("TAKE_FOOD", "Take the food", "Heal [green]8[/green] HP.")),
				new EventPageLoc(
					"RELIC",
					"You find a [gold]rare antique[/gold]. The three Zalak [aqua]stare wide-eyed[/aqua].",
					new EventOptionLoc("TAKE_RELIC", "Take the antique", "Gain a random [gold]Relic[/gold].")),
				new EventPageLoc(
					"BIG_GOLD",
					"You dig up a chest of [gold]Gold[/gold]. The three Zalak watch you with [purple]envy[/purple].",
					new EventOptionLoc("TAKE_GOLD", "Take the Gold", "Gain [blue]100[/blue] [gold]Gold[/gold].")),
				new EventPageLoc(
					"SMALL_GOLD",
					"You dig up a few [gold]Gold[/gold] pieces. The three Zalak's eyes [sine][gold]sparkle[/gold][/sine].",
					new EventOptionLoc("TAKE_GOLD", "Take the Gold", "Gain [blue]30[/blue] [gold]Gold[/gold].")),
				new EventPageLoc(
					"SMALL_FOOD",
					"You dig up a small pack of [green]food[/green]. The three Zalak [green]swallow hard[/green].",
					new EventOptionLoc("TAKE_FOOD", "Take the food", "Heal [green]4[/green] HP.")),
				new EventPageLoc(
					"NOTHING",
					"You cover yourself in [purple]dust and grime[/purple], but find nothing at all.",
					new EventOptionLoc("CONTINUE", "Continue", "")),
				new EventPageLoc(
					"LEAVE",
					"Better not [purple]waste time[/purple] here."))
		);
	}

	private static EventPageLoc SearchPageLocZhs(string pageKey, string description)
	{
		return new EventPageLoc(
			pageKey,
			description,
			new EventOptionLoc("DEEP_SEARCH", "兴奋地在垃圾堆里翻翻看", "失去[red]4[/red]点最大生命。"),
			new EventOptionLoc("DEEP_SEARCH_LOCKED", "兴奋地在垃圾堆里翻翻看", "需要至少[red]5[/red]点最大生命。"),
			new EventOptionLoc("CASUAL_SEARCH", "在垃圾堆里随便翻翻", "失去[red]2[/red]点最大生命。"),
			new EventOptionLoc("CASUAL_SEARCH_LOCKED", "在垃圾堆里随便翻翻", "需要至少[red]3[/red]点最大生命。"),
			new EventOptionLoc("LEAVE", "就这样吧", "就此离开。"));
	}

	private static EventPageLoc SearchPageLocEng(string pageKey, string description)
	{
		return new EventPageLoc(
			pageKey,
			description,
			new EventOptionLoc("DEEP_SEARCH", "Search excitedly", "Lose [red]4[/red] Max HP."),
			new EventOptionLoc("DEEP_SEARCH_LOCKED", "Search excitedly", "Requires at least [red]5[/red] Max HP."),
			new EventOptionLoc("CASUAL_SEARCH", "Search casually", "Lose [red]2[/red] Max HP."),
			new EventOptionLoc("CASUAL_SEARCH_LOCKED", "Search casually", "Requires at least [red]3[/red] Max HP."),
			new EventOptionLoc("LEAVE", "Leave it be", "Leave."));
	}
}
