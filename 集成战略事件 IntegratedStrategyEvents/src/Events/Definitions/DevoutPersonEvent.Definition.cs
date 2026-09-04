
namespace IntegratedStrategyEvents.Events;

public sealed partial class DevoutPersonEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"devout_person.png",
			CreateLocalization,
			IntegratedStrategyEventLayoutProfile.LeftNarrow);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"敬虔之人",
				new EventPageLoc(
					InitialPage,
					"[gold]虔诚[/gold]的[orange]萨科塔修女[/orange]跪在[gold]圣像[/gold]前[sine][gold]轻声祈祷[/gold][/sine]，丝毫没有注意到你们的到来。",
					new EventOptionLoc("APPROACH", "接近修女", "有些人不应当在这里。"),
					new EventOptionLoc("OBSERVE", "谨慎观察", "多一事不如少一事。")),
				new EventPageLoc(
					"APPROACH",
					"你走到修女背后，还在犹豫应当怎么处理这个[red]敌人[/red]，然而一声[jitter][red]铳响[/red][/jitter]打破了你的疑虑。那位修女的身形[sine][aqua]消散在眼前[/aqua][/sine]，[orange]萨科塔[/orange]将你团团包围。\n\n好吧......原来这是个[jitter][red]陷阱[/red][/jitter]。",
					new EventOptionLoc("FIGHT", "那就来吧！", "遭遇一场特殊的战斗。")),
				new EventPageLoc(
					"OBSERVE",
					"出现在[purple]卡兹戴尔[/purple]的[orange]萨科塔[/orange]，带有[red]长角[/red]的[gold]圣像[/gold]，这[purple]诡异[/purple]的场景让你没有急于行动。\n\n很快，修女结束了[sine][gold]祈祷[/gold][/sine]向你走来。你看清楚了，那是个纯粹的[orange]萨科塔[/orange]，但她放在你手心的，却是个[purple]萨卡兹泥人[/purple]。\n\n她是在为两族的[sine][green]和解[/green][/sine]祈祷？这有可能吗？")),
			new EventLoc(
				"The Devout",
				new EventPageLoc(
					InitialPage,
					"A [gold]devout[/gold] [orange]Sankta sister[/orange] kneels before a [gold]holy icon[/gold], [sine][gold]praying softly[/gold][/sine]. She does not notice your arrival.",
					new EventOptionLoc("APPROACH", "Approach the sister", "Some people should not be here."),
					new EventOptionLoc("OBSERVE", "Observe carefully", "Better safe than sorry.")),
				new EventPageLoc(
					"APPROACH",
					"You step behind the sister, still hesitating over how to deal with this [red]enemy[/red]. Then a [jitter][red]gunshot[/red][/jitter] dispels your doubts. The sister's form [sine][aqua]vanishes before your eyes[/aqua][/sine], and [orange]Sankta[/orange] surround you.\n\nFine... so this was a [jitter][red]trap[/red][/jitter].",
					new EventOptionLoc("FIGHT", "Then come on!", "Encounter a special fight.")),
				new EventPageLoc(
					"OBSERVE",
					"An [orange]Sankta[/orange] in [purple]Kazdel[/purple], and a [gold]holy icon[/gold] with [red]horns[/red]. This [purple]uncanny[/purple] scene keeps you from acting rashly.\n\nSoon, the sister finishes her [sine][gold]prayer[/gold][/sine] and walks toward you. You can see clearly now: she is purely [orange]Sankta[/orange]. Yet what she places in your palm is a [purple]Sarkaz clay figure[/purple].\n\nWas she praying for [sine][green]reconciliation[/green][/sine] between the two races? Is that possible?"))
		);
	}
}
