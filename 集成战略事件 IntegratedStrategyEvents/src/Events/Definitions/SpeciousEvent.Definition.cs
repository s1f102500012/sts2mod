
namespace IntegratedStrategyEvents.Events;

public sealed partial class SpeciousEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"specious.png",
			CreateLocalization,
			Layout: IntegratedStrategyEventLayoutProfile.LeftWide,
			AlignHoverTipsRight: true);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"似是而非",
				new EventPageLoc(
					InitialPage,
					"你跨过了一道[sine][aqua]“门”[/aqua][/sine]，落在了一艘船的甲板上。这个地方似乎是[green]罗德岛[/green]，但船上那独属于[purple]卡兹戴尔[/purple]的[orange]熔炉[/orange]却又让你觉得可能是艘[purple]萨卡兹移民船[/purple]。\n\n就在这时，你看到了两个人向你走来，一个是你所熟悉的[gold]特蕾西娅[/gold]，而另一个，则是当初进攻[purple]卡兹戴尔[/purple]的[aqua]菲林将军[/aqua][jitter][purple]......？[/purple][/jitter]\n\n这是怎么回事？",
					new EventOptionLoc("LEARN_STORIES", "了解她们的故事", "获得[gold]罗德之门[/gold]。"),
					new EventOptionLoc("REQUEST_AID", "向她们求援", "从你的[gold]牌组[/gold]中选择[blue]1[/blue]张牌[gold]升级[/gold]。"),
					new EventOptionLoc("REQUEST_AID_LOCKED", "向她们求援", "没有可升级的牌。"),
					new EventOptionLoc("LEAVE", "远远躲开", "多一事不如少一事。")),
				new EventPageLoc(
					"LEARN_STORIES",
					"她们并没有过多谈论[red]魔王特雷西斯[/red]的死亡以及[aqua]凯尔希[/aqua]与[purple]黑冠[/purple]的对话，只讲述了[green]罗德戴尔[/green]是[orange]感染者[/orange]的港湾，也将成为反抗[purple]源石[/purple]的前线。\n\n共同的[gold]愿景[/gold]让本是[red]死敌[/red]的两人走到了一起。在听闻了你的旅程后，她们帮助你寻找并固定了[sine][aqua]“门”[/aqua][/sine]，这样你们就能随时支援对方了。"),
				new EventPageLoc(
					"REQUEST_AID",
					"在得知了你被[sine][aqua]“门”[/aqua][/sine]带来这里的境遇后，[gold]特蕾西娅[/gold]与[aqua]凯尔希[/aqua]为你安排了一位[green]干员[/green]。这位干员将与你一同旅行，也会去到你的[gold]“大地”[/gold]中继续探索。\n\n“不用担心。”[aqua]凯尔希[/aqua]说道，“故事间的联系比你想象的更加[sine][aqua]紧密[/aqua][/sine]，如果有意愿，回到这里并不是件难事。”"),
				new EventPageLoc(
					"LEAVE",
					"由于[sine][aqua]“门”[/aqua][/sine]内并不是你所讲述的故事，在看到[aqua]菲林[/aqua]的那一刻，你开始担心起这里是某座[jitter][red]萨卡兹移动监狱[/red][/jitter]。\n\n因此，你躲过了这两个人，在舰船上寻找[sine][aqua]“门”[/aqua][/sine]为你留下的出口，最终回到了你的[gold]故事[/gold]中。")),
			new EventLoc(
				"Specious",
				new EventPageLoc(
					InitialPage,
					"You step through a [sine][aqua]\"door\"[/aqua][/sine] and land on the deck of a ship. This place seems to be [green]Rhodes Island[/green], yet the [orange]furnace[/orange] aboard it belongs unmistakably to [purple]Kazdel[/purple], making you wonder whether this is a [purple]Sarkaz immigrant ship[/purple].\n\nThen two people walk toward you. One is the [gold]Theresa[/gold] you know. The other is the [aqua]Feline general[/aqua] who once attacked [purple]Kazdel[/purple][jitter][purple]...?[/purple][/jitter]\n\nWhat is going on?",
					new EventOptionLoc("LEARN_STORIES", "Hear their story", "Gain [gold]Rhodes Door[/gold]."),
					new EventOptionLoc("REQUEST_AID", "Ask for aid", "Choose [blue]1[/blue] card from your [gold]deck[/gold] to [gold]Upgrade[/gold]."),
					new EventOptionLoc("REQUEST_AID_LOCKED", "Ask for aid", "No upgradable cards."),
					new EventOptionLoc("LEAVE", "Keep away", "Avoid unnecessary trouble.")),
				new EventPageLoc(
					"LEARN_STORIES",
					"They do not say much about the death of [red]Theresis[/red], nor about the conversation between [aqua]Kal'tsit[/aqua] and the [purple]Black Crown[/purple]. They only tell you that [green]Rhodesdale[/green] is a harbor for the [orange]Infected[/orange], and will become the frontline against [purple]Originium[/purple].\n\nA shared [gold]vision[/gold] has brought two former [red]enemies[/red] together. After hearing of your journey, they help you find and anchor the [sine][aqua]\"door\"[/aqua][/sine], so that you may support each other at any time."),
				new EventPageLoc(
					"REQUEST_AID",
					"After learning that the [sine][aqua]\"door\"[/aqua][/sine] brought you here, [gold]Theresa[/gold] and [aqua]Kal'tsit[/aqua] assign an [green]operator[/green] to you. This operator will travel with you and continue exploring within your [gold]\"land\"[/gold].\n\n\"Do not worry,\" [aqua]Kal'tsit[/aqua] says. \"The links between stories are [sine][aqua]closer[/aqua][/sine] than you think. If there is a will, returning here is no difficult matter.\""),
				new EventPageLoc(
					"LEAVE",
					"Since what lies beyond the [sine][aqua]\"door\"[/aqua][/sine] is not the story you know, the sight of that [aqua]Feline[/aqua] makes you worry this place may be a [jitter][red]Sarkaz mobile prison[/red][/jitter].\n\nSo you avoid the two of them and search the ship for the exit left by the [sine][aqua]\"door\"[/aqua][/sine], eventually returning to your own [gold]story[/gold]."))
		);
	}
}
