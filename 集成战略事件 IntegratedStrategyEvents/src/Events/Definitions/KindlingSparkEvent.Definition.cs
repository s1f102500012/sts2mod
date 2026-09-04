
namespace IntegratedStrategyEvents.Events;

public sealed partial class KindlingSparkEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"kindling_spark.png",
			CreateLocalization,
			IntegratedStrategyEventLayoutProfile.StandardNarrowSlightlyShiftedRight);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"点滴星火",
				new EventPageLoc(
					InitialPage,
					"[purple]噬尘[/purple]已经覆盖了整座[orange]城市[/orange]，文明产物都将被有序[red]消解[/red]，化作海嗣生育后代与进化的[red]养料[/red]。\n\n你在即将分解的[purple]废墟[/purple]间艰难前行，寻找还能使用的[gold]物资[/gold]。无意之间，你见到一块噬尘下正有什么东西在[sine][green]发出光亮[/green][/sine]。",
					new EventOptionLoc("TAKE_LAMP", "冒险去取", "失去[red]12[/red]点生命。获得[gold]熔岩灯[/gold]。"),
					new EventOptionLoc("TAKE_LAMP_LOCKED", "冒险去取", "需要至少[red]13[/red]点生命。"),
					new EventOptionLoc("TAKE_SUPPLIES", "拾取安全区域的物资", "获得[blue]60[/blue][gold]金币[/gold]。")),
				new EventPageLoc(
					"TAKE_LAMP",
					"你冒着[jitter][red]生命危险[/red][/jitter]拨开[purple]噬尘[/purple]后，找到了一个受腐蚀严重的[orange]提灯[/orange]。\n\n可即使如此，提灯内里的[sine][green]火焰[/green][/sine]仍闪烁着[green]温暖的光亮[/green]。"),
				new EventPageLoc(
					"TAKE_SUPPLIES",
					"在找到些[gold]有价值的物品[/gold]后，你匆匆离去。\n\n几个小时后，此地再无城市。")),
			new EventLoc(
				"Kindling Spark",
				new EventPageLoc(
					InitialPage,
					"[purple]Seaborn dust[/purple] has covered the entire [orange]city[/orange]. The products of civilization will be methodically [red]dissolved[/red], becoming [red]nutrients[/red] for the Seaborn to reproduce and evolve.\n\nYou struggle through the [purple]ruins[/purple] on the verge of disintegration, searching for usable [gold]supplies[/gold]. By chance, you notice something beneath the dust [sine][green]glowing faintly[/green][/sine].",
					new EventOptionLoc("TAKE_LAMP", "Risk taking it", "Lose [red]12[/red] HP. Gain [gold]Lava Lamp[/gold]."),
					new EventOptionLoc("TAKE_LAMP_LOCKED", "Risk taking it", "Requires at least [red]13[/red] HP."),
					new EventOptionLoc("TAKE_SUPPLIES", "Take safer supplies", "Gain [blue]60[/blue] [gold]Gold[/gold].")),
				new EventPageLoc(
					"TAKE_LAMP",
					"You risk your [jitter][red]life[/red][/jitter] to brush aside the [purple]dust[/purple] and find a badly corroded [orange]lantern[/orange].\n\nEven so, the [sine][green]flame[/green][/sine] within it still flickers with [green]gentle warmth[/green]."),
				new EventPageLoc(
					"TAKE_SUPPLIES",
					"After finding a few [gold]valuable items[/gold], you leave in haste.\n\nSeveral hours later, no city remains here."))
		);
	}
}
