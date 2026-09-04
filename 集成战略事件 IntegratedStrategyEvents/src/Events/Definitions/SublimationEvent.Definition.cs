
namespace IntegratedStrategyEvents.Events;

public sealed partial class SublimationEvent
{
	private static readonly IntegratedStrategyEventLayoutProfile InitialPageLayout =
		new(false, 0.92f, -60f, 3);

	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"sublimation.png",
			CreateLocalization,
			InitialPageLayout);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"升华",
				new EventPageLoc(
					InitialPage,
					"[blue]族群[/blue]唯求生存，生存即为[jitter][red]苦难[/red][/jitter]，由是，我们演化不息。克服缺陷，面对自然，对抗外敌。[red]灭绝[/red]狡猾且饥渴，[blue]大群[/blue]稍有疏漏，它便要撕扯我们的[red]血肉[/red]。\n\n所以我们纠错，所以我们适应。从爬上海岸开始，从触碰[orange]源石[/orange]开始，扩大族群领地，接触[red]致命威胁[/red]，直到彻底理解，直到化为食粮。\n\n如今，[green]大地[/green]与[blue]海洋[/blue]皆为乐土，然而族群仍在繁衍，生存仍是苦难，我们需要更广阔的[gold]家园[/gold]。在迎接[sine][aqua]进化奇点[/aqua][/sine]之前，引领我们未来的[sine][purple]祂[/purple][/sine]需给自己一个答案：[gold]“我，是谁？”[/gold]",
					new EventOptionLoc("DETERMINATION", "“我是水月，我会完成博士的请求”", "获得[gold]“决心”[/gold]。"),
					new EventOptionLoc("OBSERVATION", "“让时间来解答吧”", "获得[gold]“观望”[/gold]。"),
					new EventOptionLoc("HESITATION", "“大群非我，我即大群”", "获得[gold]“犹疑”[/gold]。")),
				new EventPageLoc(
					"DETERMINATION",
					"我并非完全的我，正如[sine][purple]祂[/purple][/sine]并非完全的祂。我们的相遇虽是[gold]意外[/gold]，但终有分别的时候。\n\n时候到了。"),
				new EventPageLoc(
					"OBSERVATION",
					"没有迭代无法消解的问题，没有[blue]进化[/blue]无法打破的障碍。\n\n当时机成熟，答案自然会显现。"),
				new EventPageLoc(
					"HESITATION",
					"我为何不是[blue]大群[/blue]，还有什么在影响着我？")),
			new EventLoc(
				"Sublimation",
				new EventPageLoc(
					InitialPage,
					"The [blue]collective[/blue] seeks only survival, and survival is [jitter][red]suffering[/red][/jitter]. Thus, we evolve without end. We overcome flaws, face nature, and resist enemies. [red]Extinction[/red] is cunning and hungry. If the [blue]many[/blue] leave even the smallest opening, it will tear into our [red]flesh[/red].\n\nSo we correct. So we adapt. From crawling onto the shore, from touching [orange]Originium[/orange], we expand the collective's territory and make contact with [red]lethal threats[/red], until we understand them completely, until they become nourishment.\n\nNow, both [green]land[/green] and [blue]sea[/blue] are paradises. Yet the collective still multiplies, survival remains suffering, and we need a broader [gold]home[/gold]. Before welcoming the [sine][aqua]singularity of evolution[/aqua][/sine], [sine][purple]They[/purple][/sine], who will lead our future, must give themselves an answer: [gold]\"Who am I?\"[/gold]",
					new EventOptionLoc("DETERMINATION", "\"I am Mizuki. I will fulfill the Doctor's request.\"", "Gain [gold]\"Determination\"[/gold]."),
					new EventOptionLoc("OBSERVATION", "\"Let time answer.\"", "Gain [gold]\"Observation\"[/gold]."),
					new EventOptionLoc("HESITATION", "\"The many are not me. I am the many.\"", "Gain [gold]\"Hesitation\"[/gold].")),
				new EventPageLoc(
					"DETERMINATION",
					"I am not entirely myself, just as [sine][purple]They[/purple][/sine] are not entirely Themselves. Our meeting was an [gold]accident[/gold], but the time to part must eventually come.\n\nThe time has come."),
				new EventPageLoc(
					"OBSERVATION",
					"There is no problem iteration cannot dissolve, no obstacle [blue]evolution[/blue] cannot break.\n\nWhen the time is ripe, the answer will reveal itself."),
				new EventPageLoc(
					"HESITATION",
					"Why am I not the [blue]many[/blue]? What else is still shaping me?"))
		);
	}
}
