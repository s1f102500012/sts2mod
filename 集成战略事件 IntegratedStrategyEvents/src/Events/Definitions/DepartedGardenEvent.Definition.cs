
namespace IntegratedStrategyEvents.Events;

public sealed partial class DepartedGardenEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"departed_garden.png",
			CreateLocalization,
			IntegratedStrategyEventLayoutProfile.StandardRaised);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"离去者的庭院",
				new EventPageLoc(
					InitialPage,
					"战争、[red]病痛[/red]、[red]事故[/red]不断地从[orange]特雷西斯[/orange]和[orange]特蕾西娅[/orange]身边夺走那些追随他们的朋友。为了纪念这些人，[red]两位魔王[/red]在卡兹戴尔的中心建立了一座[pink]纪念公园[/pink]。\n\n在这里，你可以见到那些魔王挚友的纪念碑。你走到一座碑前，仔细看了看这位[pink]亡者的名字[/pink]——",
					new EventOptionLoc("ASCALON", "“阿斯卡纶”", "获得一次罕见卡牌奖励。"),
					new EventOptionLoc("TOUCH", "“触痕”", "回复[green]18[/green]点生命。"),
					new EventOptionLoc("FITZROY", "“菲茨罗伊”", "获得[blue]80[/blue][gold]金币[/gold]。"),
					new EventOptionLoc("ERIKSON", "“埃里克森”", "获得[blue]1[/blue]件随机[gold]普通遗物[/gold]。"),
					new EventOptionLoc("AMIYA", "“阿米娅”", "随机获得一张先古牌。将一张随机[red]诅咒牌[/red]加入你的[gold]牌组[/gold]。")),
				new EventPageLoc(
					"ASCALON",
					"“这里埋葬着[red]阿斯卡纶[/red]，[orange]魔王的侍卫长[/orange]。她在一场针对魔王的阴谋中杀死[red]百名刺客[/red]，以身殉职。”\n\n在她的纪念碑前，放着几束[green]鲜花[/green]。"),
				new EventPageLoc(
					"TOUCH",
					"“这里埋葬着[green]触痕[/green]，巴别塔的[green]外勤医疗专家[/green]。她因[red]辛劳过度[/red]不幸逝世。”\n\n在她的纪念碑前，放着几样[green]医疗器具[/green]。"),
				new EventPageLoc(
					"FITZROY",
					"“这里埋葬着[gold]菲茨罗伊先生[/gold]，一位用商业撬动[orange]哥伦比亚[/orange]使之转变态度的商人朋友。他死于无药可医的[red]家族遗传病[/red]，根据遗嘱，其遗产由女妖[green]娜斯提[/green]继承。”\n\n在他的纪念碑前，放着几块[gold]源石锭[/gold]。"),
				new EventPageLoc(
					"ERIKSON",
					"“这里埋葬着[pink]埃里克森[/pink]，[pink]历史的记录者[/pink]。他[green]寿终正寝[/green]，没有任何遗憾。”\n\n在他的纪念碑前，放着几本[gold]书籍[/gold]。"),
				new EventPageLoc(
					"AMIYA",
					"“这里埋葬着[gold]阿米娅[/gold]，愿她安息。”在她的纪念碑上，雕刻着[gold]十枚戒指[/gold]。\n\n[sine][pink]恍惚间[/pink][/sine]，你似乎看到了[orange]特蕾西娅[/orange]为阿米娅戴上戒指，目送她[aqua]远行[/aqua]的场景。回过神来，这十枚戒指已经戴在了你的手指上。\n\n但不知为何，你感觉到，这些戒指[purple]不全属于你[/purple]面前的这位阿米娅，它们是[red]魔王[/red]的，它们是[aqua]罗德岛[/aqua]的......它们属于你认知里的那个[gold]卡特斯[/gold]。")),
			new EventLoc(
				"Garden of the Departed",
				new EventPageLoc(
					InitialPage,
					"War, [red]illness[/red], and [red]accidents[/red] kept taking the friends who followed [orange]Theresis[/orange] and [orange]Theresa[/orange]. To remember them, the [red]two Demon Kings[/red] built a [pink]memorial park[/pink] at the center of Kazdel.\n\nHere, you can find monuments to the Demon Kings' closest friends. You step before one monument and carefully read the [pink]departed name[/pink]...",
					new EventOptionLoc("ASCALON", "\"Ascalon\"", "Gain an Uncommon card reward."),
					new EventOptionLoc("TOUCH", "\"Touch\"", "Heal [green]18[/green] HP."),
					new EventOptionLoc("FITZROY", "\"Fitzroy\"", "Gain [blue]80[/blue] [gold]Gold[/gold]."),
					new EventOptionLoc("ERIKSON", "\"Erikson\"", "Obtain [blue]1[/blue] random [gold]Common Relic[/gold]."),
					new EventOptionLoc("AMIYA", "\"Amiya\"", "Gain a random Ancient card. Add a random [red]Curse[/red] to your deck.")),
				new EventPageLoc(
					"ASCALON",
					"\"Here lies [red]Ascalon[/red], [orange]captain of the Demon King's guard[/orange]. In a conspiracy against the Demon King, she killed [red]one hundred assassins[/red] and died in the line of duty.\"\n\nSeveral bundles of [green]flowers[/green] rest before her monument."),
				new EventPageLoc(
					"TOUCH",
					"\"Here lies [green]Touch[/green], Babel's [green]field medical specialist[/green]. She passed away after [red]working herself beyond exhaustion[/red].\"\n\nSeveral [green]medical instruments[/green] rest before her monument."),
				new EventPageLoc(
					"FITZROY",
					"\"Here lies [gold]Mr. Fitzroy[/gold], a merchant friend who used commerce to move [orange]Columbia[/orange] toward changing its position. He died of an incurable [red]familial disease[/red], and by his will, his estate was inherited by the banshee [green]Nasti[/green].\"\n\nSeveral [gold]Originium ingots[/gold] rest before his monument."),
				new EventPageLoc(
					"ERIKSON",
					"\"Here lies [pink]Erikson[/pink], [pink]recorder of history[/pink]. He reached the end of his life [green]without regret[/green].\"\n\nSeveral [gold]books[/gold] rest before his monument."),
				new EventPageLoc(
					"AMIYA",
					"\"Here lies [gold]Amiya[/gold]. May she rest in peace.\" Ten [gold]rings[/gold] are carved into her monument.\n\n[sine][pink]In a daze[/pink][/sine], you seem to see [orange]Theresa[/orange] placing rings on Amiya's fingers and watching her [aqua]depart[/aqua]. When you come back to yourself, those ten rings are already on your own fingers.\n\nFor some reason, you feel that these rings do [purple]not all belong to[/purple] the Amiya before you. They belong to the [red]Demon King[/red], to [aqua]Rhodes Island[/aqua]... and to the [gold]Cautus[/gold] in your own understanding."))
		);
	}
}
