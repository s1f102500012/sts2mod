
namespace IntegratedStrategyEvents.Events;

public sealed partial class UnfreezingRiverEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"unfreezing_river.png",
			CreateLocalization,
			IntegratedStrategyEventLayoutProfile.LeftWide,
			AlignHoverTipsRight: true);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"不冻河",
				new EventPageLoc(
					InitialPage,
					"看到那条[aqua][sine]河流[/sine][/aqua]时，你不自觉地后退了一步。那条河流似乎[purple]凭空出现[/purple]，拦住了你的去路，沿着山涧一路向下奔腾。你和同伴利用了携带的各种仪器，但没有收集到任何[blue]异常数据[/blue]，更无从解释自己心头的[jitter][purple]惶恐不安[/purple][/jitter]。",
					new EventOptionLoc("DETOUR", "绕道而行", "回复[green]10[/green]点生命。获得[purple]羞耻[/purple]。"),
					new EventOptionLoc("CROSS_RIVER", "直接渡河", "失去[red]8[/red]点生命。获得[rainbow freq=0.3 sat=0.8 val=1]混沌药水[/rainbow]。"),
					new EventOptionLoc("CROSS_RIVER_LOCKED", "直接渡河", "需要至少[red]9[/red]点生命。"),
					new EventOptionLoc("SEARCH_TOOLS", "寻找工具", "失去[gold]{Relic}[/gold]。获得一件随机[gold]遗物[/gold]。"),
					new EventOptionLoc("SEARCH_TOOLS_LOCKED", "寻找工具", "没有可失去的遗物。")),
				new EventPageLoc(
					"DETOUR",
					"出于[gold]安全考虑[/gold]，你临时改变了科考队行进的路线。走到[orange]温暖的阳光[/orange]下时，你的双手终于不再[jitter]颤抖[/jitter]，但所有人依然[purple]沉默而沮丧[/purple]——你们都知道那条[aqua][sine]河流[/sine][/aqua]正流向附近的[green]萨米聚落[/green]。"),
				new EventPageLoc(
					"CROSS_RIVER",
					"被[red]侵蚀的大地[/red]，消失的飞羽走兽，向内生长的[purple]自我的躯体[/purple]......当你的同伴将你救上岸时，你的脑海中闪现着无数[sine][purple]混沌的预兆[/purple][/sine]。你已经回忆不起自己究竟是先因[jitter][red]恐惧[/red][/jitter]而跳入了河中，还是在河水里被[jitter][red]恐惧[/red][/jitter]掐住了咽喉。"),
				new EventPageLoc(
					"SEARCH_TOOLS",
					"[gold]运气真好[/gold]。你在河道上游发现了一只足够稳固的船，船中甚至还余有少量[gold]物资[/gold]。只是，在行船渡河时，似乎是被落在手背上融化的一粒[aqua]冰晶[/aqua]提醒，你蓦地低头，而后看到了......[sine][aqua]星空[/aqua][/sine]？")),
			new EventLoc(
				"Unfreezing River",
				new EventPageLoc(
					InitialPage,
					"When you see that [aqua][sine]river[/sine][/aqua], you instinctively take a step back. The river seems to have [purple]appeared from nowhere[/purple], blocking your path as it rushes down the mountain ravine. You and your companions use every instrument you carried, but gather no [blue]abnormal readings[/blue], and find no explanation for the [jitter][purple]dread[/purple][/jitter] in your heart.",
					new EventOptionLoc("DETOUR", "Take a detour", "Heal [green]10[/green] HP. Gain [purple]Shame[/purple]."),
					new EventOptionLoc("CROSS_RIVER", "Ford it directly", "Lose [red]8[/red] HP. Gain [rainbow freq=0.3 sat=0.8 val=1]Entropic Brew[/rainbow]."),
					new EventOptionLoc("CROSS_RIVER_LOCKED", "Ford it directly", "Requires at least [red]9[/red] HP."),
					new EventOptionLoc("SEARCH_TOOLS", "Search for tools", "Lose [gold]{Relic}[/gold]. Obtain a random [gold]Relic[/gold]."),
					new EventOptionLoc("SEARCH_TOOLS_LOCKED", "Search for tools", "You have no Relic to lose.")),
				new EventPageLoc(
					"DETOUR",
					"For [gold]safety[/gold], you temporarily change the survey team's route. When you step into the [orange]warm sunlight[/orange], your hands finally stop [jitter]trembling[/jitter], but everyone remains [purple]silent and dejected[/purple]. You all know that [aqua][sine]river[/sine][/aqua] is flowing toward a nearby [green]Sami settlement[/green]."),
				new EventPageLoc(
					"CROSS_RIVER",
					"The [red]corroded earth[/red], vanished birds and beasts, a [purple]self-grown inward body[/purple]... When your companions drag you ashore, countless [sine][purple]omens of chaos[/purple][/sine] flash through your mind. You can no longer remember whether you first jumped into the river from [jitter][red]fear[/red][/jitter], or whether [jitter][red]fear[/red][/jitter] clutched your throat in the water."),
				new EventPageLoc(
					"SEARCH_TOOLS",
					"[gold]What luck[/gold]. Upstream, you find a boat sturdy enough to use, with even a few [gold]supplies[/gold] left inside. Yet as you cross, a single [aqua]ice crystal[/aqua] melting on the back of your hand seems to remind you of something. You suddenly look down, and then you see... [sine][aqua]the stars[/aqua][/sine]?"))
		);
	}
}
