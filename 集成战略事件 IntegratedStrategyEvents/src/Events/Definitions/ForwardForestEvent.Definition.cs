
namespace IntegratedStrategyEvents.Events;

public sealed partial class ForwardForestEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"forward_forest.png",
			CreateLocalization,
			IntegratedStrategyEventLayoutProfile.LeftWide);

	internal static List<(string, string)>? CreateLocalization()
	{
		const string ChineseChopText = "你终于砍开了杂乱的林木和藤条，阳光照入林间，什么东西掉在了地上。\n\n你产生了一种[jitter][purple]奇怪的直觉[/purple][/jitter]——或许你刚刚破坏了什么[sine][gold]精妙的人造物[/gold][/sine]。但造成的损坏已经无法复原，你只好捡起掉落的物件离开。";
		const string EnglishChopText = "At last, you hack through the tangled [green]trees and vines[/green]. Sunlight spills into the woodland, and something falls to the ground.\n\nA [jitter][purple]strange intuition[/purple][/jitter] comes to you: perhaps you have just damaged some [sine][gold]delicate artifice[/gold][/sine]. The harm cannot be undone, so you pick up what fell and leave.";

		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"前行的林地",
				new EventPageLoc(
					InitialPage,
					"虬结的[green]树木与藤蔓[/green]彻底拦住了你的去路，萨米人口中无数生灵的[sine][green]命运[/green][/sine]顺枝叶延伸，在你眼前[jitter]交错相连[/jitter]。\n\n你反复确认过，当地人给你的[gold]路线图[/gold]确实指向这个方向。",
					new EventOptionLoc("TREE_TOP", "树上就不能走吗？", "失去[red]8[/red]点生命上限。进入[jitter][aqua]深埋迷境[/aqua][/jitter]。"),
					new EventOptionLoc("TREE_TOP_LOCKED", "树上就不能走吗？", "需要至少[red]9[/red]点生命上限。"),
					new EventOptionLoc("CHOP_FOREST", "取出伐木斧", "清除林木，开出道路。")),
				new EventPageLoc(
					"CHOP_GOLD",
					ChineseChopText,
					new EventOptionLoc("CLAIM_GOLD", "离开", "获得[blue]30[/blue][gold]金币[/gold]。")),
				new EventPageLoc(
					"CHOP_MAX_HP",
					ChineseChopText,
					new EventOptionLoc("CLAIM_MAX_HP", "离开", "获得[green]2[/green]点最大生命。")),
				new EventPageLoc(
					"CHOP_HEAL",
					ChineseChopText,
					new EventOptionLoc("CLAIM_HEAL", "离开", "回复[green]4[/green]点生命。")),
				new EventPageLoc(
					"CHOP_RELIC",
					ChineseChopText,
					new EventOptionLoc("CLAIM_RELIC", "离开", "获得一件随机[gold]遗物[/gold]。"))),
			new EventLoc(
				"Forward Forest",
				new EventPageLoc(
					InitialPage,
					"Gnarled [green]trees and vines[/green] completely block your path. In the mouths of the Sami, countless lives' [sine][green]fates[/green][/sine] extend along branch and leaf, [jitter]crossing before your eyes[/jitter].\n\nYou check the [gold]route map[/gold] the locals gave you again and again. It truly points this way.",
					new EventOptionLoc("TREE_TOP", "Can we not walk on the trees?", "Lose [red]8[/red] Max HP. Enter the [jitter][aqua]Buried Labyrinth[/aqua][/jitter]."),
					new EventOptionLoc("TREE_TOP_LOCKED", "Can we not walk on the trees?", "Requires at least [red]9[/red] Max HP."),
					new EventOptionLoc("CHOP_FOREST", "Take out the logging axe", "Clear the woods and open a road.")),
				new EventPageLoc(
					"CHOP_GOLD",
					EnglishChopText,
					new EventOptionLoc("CLAIM_GOLD", "Leave", "Gain [blue]30[/blue] [gold]Gold[/gold].")),
				new EventPageLoc(
					"CHOP_MAX_HP",
					EnglishChopText,
					new EventOptionLoc("CLAIM_MAX_HP", "Leave", "Gain [green]2[/green] Max HP.")),
				new EventPageLoc(
					"CHOP_HEAL",
					EnglishChopText,
					new EventOptionLoc("CLAIM_HEAL", "Leave", "Heal [green]4[/green] HP.")),
				new EventPageLoc(
					"CHOP_RELIC",
					EnglishChopText,
					new EventOptionLoc("CLAIM_RELIC", "Leave", "Gain a random [gold]Relic[/gold].")))
		);
	}
}
