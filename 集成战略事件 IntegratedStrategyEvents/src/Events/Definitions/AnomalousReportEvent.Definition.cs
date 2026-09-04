
namespace IntegratedStrategyEvents.Events;

public sealed partial class AnomalousReportEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"anomalous_report.png",
			CreateLocalization,
			IntegratedStrategyEventLayoutProfile.StandardNarrow);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"扣响异音",
				new EventPageLoc(
					InitialPage,
					"[orange]卡兹戴尔魂灵熔炉[/orange]边，躺着一把[gold]古老的守护铳[/gold]。你有一种[sine][aqua]预感[/aqua][/sine]，如果鸣响这支守护铳，便会发生[jitter][red]翻天覆地[/red][/jitter]的变化。",
					new EventOptionLoc("PULL_TRIGGER", "扣动扳机", "获得[gold]时与光[/gold]。"),
					new EventOptionLoc("DESTROY_FIREARM", "销毁铳械", "我不允许......萨科塔......")),
				new EventPageLoc(
					"PULL_TRIGGER",
					"在[jitter][gold]铳响[/gold][/jitter]的一刹那，那种模糊的[sine][aqua]连结感[/aqua][/sine]变得无比清晰，一座[gold]洁白的圣城[/gold]出现在你眼前。\n\n你伸手去取腰间的[orange]先知长角[/orange]，却触到了另外一把[gold]铳[/gold]。当你鸣响它，一切便重归寂静。但你脑中的[sine][purple]“共感”[/purple][/sine]，却再也没有消退......"),
				new EventPageLoc(
					"DESTROY_FIREARM",
					"你将[gold]守护铳[/gold]丢入[orange]熔炉[/orange]，看着[red]萨科塔[/red]的象征消失在[jitter][orange]火焰[/orange][/jitter]中。\n\n[gold]萨卡兹[/gold]的故事，不应当被自己的[red]仇敌[/red]玷污。")),
			new EventLoc(
				"Anomalous Report",
				new EventPageLoc(
					InitialPage,
					"Beside the [orange]Kazdel Soul Furnace[/orange] lies an [gold]ancient guardian gun[/gold]. You have a [sine][aqua]premonition[/aqua][/sine]: if this weapon sounds, everything will [jitter][red]change beyond recognition[/red][/jitter].",
					new EventOptionLoc("PULL_TRIGGER", "Pull the trigger", "Gain [gold]Time and Light[/gold]."),
					new EventOptionLoc("DESTROY_FIREARM", "Destroy the gun", "I will not allow this... Sankta...")),
				new EventPageLoc(
					"PULL_TRIGGER",
					"In the instant of the [jitter][gold]gunshot[/gold][/jitter], that vague [sine][aqua]sense of connection[/aqua][/sine] becomes unmistakably clear. A [gold]white holy city[/gold] appears before your eyes.\n\nYou reach for the [orange]Prophet's horn[/orange] at your waist, but your hand finds another [gold]gun[/gold] instead. When you fire it, all returns to silence. Yet the [sine][purple]\"empathy\"[/purple][/sine] in your mind never fades again..."),
				new EventPageLoc(
					"DESTROY_FIREARM",
					"You cast the [gold]guardian gun[/gold] into the [orange]furnace[/orange] and watch the symbol of the [red]Sankta[/red] vanish in [jitter][orange]flame[/orange][/jitter].\n\nThe story of the [gold]Sarkaz[/gold] should not be stained by their own [red]enemies[/red]."))
		);
	}
}
