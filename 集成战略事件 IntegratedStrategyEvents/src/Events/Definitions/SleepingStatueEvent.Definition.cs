
namespace IntegratedStrategyEvents.Events;

public sealed partial class SleepingStatueEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } = IntegratedStrategyEventDefinition.ForEventPortrait("sleeping_statue.png", CreateLocalization);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"沉睡石像",
				new EventPageLoc(
					InitialPage,
					"你误入了一个[aqua]空旷的大厅[/aqua]，这里密密麻麻装饰着大量[orange]戏服造型的石像[/orange]。石像维持着不同的姿势，栩栩如生，令人感到[purple]恐惧[/purple]。\n\n你发现所有石像都紧闭双眼，看起来就像是[sine][aqua]睡着[/aqua][/sine]了一样。",
					new EventOptionLoc("AWAKEN", "唤醒石像", "这样真的值得吗？"),
					new EventOptionLoc("LEAVE", "安静离开", "多一事不如少一事。")),
				new EventPageLoc(
					"AWAKEN",
					"“[sine][gold]醒来吧！[/gold][/sine][jitter][green]沉睡的生命啊！[/green][/jitter]”\n\n能说这句台词的机会不多，即便这可能会需要付出一定[red]代价[/red]，但总有人觉得很[gold]值得[/gold]。",
					new EventOptionLoc("FIGHT", "进入战斗", "遭遇一场困难的战斗。")),
				new EventPageLoc(
					"LEAVE",
					"不要惊扰[sine][aqua]沉睡的石像[/aqua][/sine]，特别是当他们有一大群的时候。\n\n你牢记曾经得到过的[gold]忠告[/gold]，轻轻带上了大厅的大门。")),
			new EventLoc(
				"Sleeping Statues",
				new EventPageLoc(
					InitialPage,
					"You stray into a [aqua]vast empty hall[/aqua], densely decorated with [orange]statues dressed like stage performers[/orange]. They hold different poses, lifelike enough to inspire [purple]fear[/purple].\n\nYou notice that every statue's eyes are closed, as though they were [sine][aqua]asleep[/aqua][/sine].",
					new EventOptionLoc("AWAKEN", "Awaken the statues", "Is this truly worth it?"),
					new EventOptionLoc("LEAVE", "Leave quietly", "Better safe than sorry.")),
				new EventPageLoc(
					"AWAKEN",
					"\"[sine][gold]Awaken![/gold][/sine] [jitter][green]O sleeping life![/green][/jitter]\"\n\nThere are not many chances to say a line like that. Even if it may come at a [red]price[/red], some people will still call it [gold]worthwhile[/gold].",
					new EventOptionLoc("FIGHT", "Enter combat", "Encounter a difficult fight.")),
				new EventPageLoc(
					"LEAVE",
					"Do not disturb [sine][aqua]sleeping statues[/aqua][/sine], especially when there is a great crowd of them.\n\nYou remember the [gold]advice[/gold] you once received, and gently close the hall doors behind you."))
		);
	}
}
