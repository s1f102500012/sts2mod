
namespace IntegratedStrategyEvents.Events;

public sealed partial class GlimpseEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"glimpse.png",
			CreateLocalization,
			IntegratedStrategyEventLayoutProfile.LeftWide,
			AlignHoverTipsRight: true);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"瞥视",
				new EventPageLoc(
					InitialPage,
					"碑林的碎片被[aqua]溟痕[/aqua]同化，沉落在[red]消亡巨物[/red]旁，碑文[sine][aqua]不断变化[/aqua][/sine]，昭示着[gold]万千可能性[/gold]：你与相识的女性结为伴侣，抑或成为她的[red]死敌[/red]；你引领人类对抗[purple]海嗣[/purple]，或身为[purple]海嗣[/purple]带领族群；你站在泰拉上远望[sine][aqua]星空[/aqua][/sine]，或站在[jitter]■■■[/jitter]上眺望泰拉。\n\n你还想探查更多，水月却示意你先停下，[aqua]溟痕[/aqua]正不断解析着信息，你所阅览的，[jitter][purple]大群[/purple][/jitter]绝不会错过。",
					new EventOptionLoc("CLEAR_SEABORN", "试图清除溟痕", "失去[gold]{Potion}[/gold]及其对应药水栏位。"),
					new EventOptionLoc("CLEAR_SEABORN_LOCKED", "试图清除溟痕", "没有可失去的药水。"),
					new EventOptionLoc("LISTEN_TO_MIZUKI", "听听水月的建议", "获得一件随机[gold]遗物[/gold]。")),
				new EventPageLoc(
					"CLEAR_SEABORN",
					"你的尝试很快就获得了回报，[aqua]溟痕石碑[/aqua]将你的意识带向了[purple]海嗣族群[/purple]或将经历的某个[sine][aqua]时间片段[/aqua][/sine]。\n\n在存活到这个片段的终点前，你将代替[purple]海嗣们[/purple]做出[gold]抉择[/gold]。",
					new EventOptionLoc("ENTER_TIME_SPACE", "或许你能为“族群”解决些许困难", "进入[sine][aqua]神秘的时空[/aqua][/sine]。")),
				new EventPageLoc(
					"LISTEN_TO_MIZUKI",
					"水月让[aqua]溟痕[/aqua]改变了[gold]行为准则[/gold]，它们依托于信息开始[jitter][aqua]剧烈耗能[/aqua][/jitter]并转换物质形态。\n\n最终，溟痕石碑消失了，一个你所熟悉的[gold]物品[/gold]出现在水月手中。他并没有多说什么，而是笑了笑把它放在了你的手心里。")),
			new EventLoc(
				"Glimpse",
				new EventPageLoc(
					InitialPage,
					"Fragments of a stela forest have been assimilated by [aqua]Seaborn marks[/aqua] and sunk beside a [red]vanished colossus[/red]. The inscriptions [sine][aqua]shift endlessly[/aqua][/sine], revealing [gold]countless possibilities[/gold]: you marry a woman you know, or become her [red]deadliest enemy[/red]; you lead humanity against the [purple]Seaborn[/purple], or lead the brood as [purple]Seaborn[/purple] yourself; you stand on Terra and gaze at the [sine][aqua]stars[/aqua][/sine], or stand upon [jitter]■■■[/jitter] and gaze back at Terra.\n\nYou want to look deeper, but Mizuki motions for you to stop. The [aqua]marks[/aqua] are still parsing the information, and whatever you read, the [jitter][purple]Seaborn collective[/purple][/jitter] will not miss.",
					new EventOptionLoc("CLEAR_SEABORN", "Try to clear the marks", "Lose [gold]{Potion}[/gold] and its potion slot."),
					new EventOptionLoc("CLEAR_SEABORN_LOCKED", "Try to clear the marks", "You have no Potion to lose."),
					new EventOptionLoc("LISTEN_TO_MIZUKI", "Listen to Mizuki", "Gain a random [gold]Relic[/gold].")),
				new EventPageLoc(
					"CLEAR_SEABORN",
					"Your attempt is quickly rewarded. The [aqua]marked stela[/aqua] draws your awareness into a [sine][aqua]time fragment[/aqua][/sine] that the [purple]Seaborn brood[/purple] may yet experience.\n\nUntil you survive to the end of that fragment, you will make [gold]choices[/gold] in place of the [purple]Seaborn[/purple].",
					new EventOptionLoc("ENTER_TIME_SPACE", "Perhaps you can solve a few problems for the \"brood\"", "Enter a [sine][aqua]mysterious spacetime[/aqua][/sine].")),
				new EventPageLoc(
					"LISTEN_TO_MIZUKI",
					"Mizuki changes the [gold]behavioral directives[/gold] of the [aqua]marks[/aqua]. They begin [jitter][aqua]burning through energy[/aqua][/jitter] on the information itself, converting their material form.\n\nAt last, the marked stela disappears, and a [gold]familiar object[/gold] rests in Mizuki's hand. He says nothing more, only smiles and places it in your palm."))
		);
	}
}
