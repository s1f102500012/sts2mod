
namespace IntegratedStrategyEvents.Events;

public sealed partial class CompletionCeremonyEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } = IntegratedStrategyEventDefinition.ForEventPortrait("completion_ceremony.png", CreateLocalization);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"竣工仪式",
				new EventPageLoc(
					InitialPage,
					"[aqua]夜色[/aqua]呼啸而过，燥热的风卷起[red]灰烬[/red]与残页。这是[orange]卡兹戴尔[/orange]浮上天空后的第一个夜晚，裸露的泥土上再也没有城市的踪影，只剩下数百个[red]燃烧的铁桶[/red]。\n\n你看见有人正将成堆的[gold]图纸[/gold]扔进火中。这些工程师打扮的[purple]萨卡兹[/purple]警惕地看向你的背后，你回过头去，那里站着一群面色不善的[orange]异族[/orange]。\n\n留守地面的工程师出声质问，你答道——",
					new EventOptionLoc("DEMAND_BLUEPRINTS", "交出浮空城的图纸！", "获得一次卡牌奖励。"),
					new EventOptionLoc("ATTEND_SEMINAR", "我们是来参加研讨会的", "获得[gold]《旧高卢地名源流考》[/gold]。")),
				new EventPageLoc(
					"DEMAND_BLUEPRINTS",
					"萨卡兹工程师们冷哼一声，抱起地上的[gold]图纸[/gold]全数投进[red]火焰[/red]中，接着便转身跑进[aqua]夜色[/aqua]里。\n\n你没有追上去的打算，而是从火焰中抢出几张还未烧尽的[gold]残页[/gold]。萨卡兹与其他种族之间的矛盾注定无法调和，否则他们也不至于逃到[sine][aqua]天上[/aqua][/sine]去。"),
				new EventPageLoc(
					"ATTEND_SEMINAR",
					"萨卡兹工程师们听说你们是来参加[orange]研讨会[/orange]的，面色缓和了不少，并为你们指明了去会场的路。\n\n“[orange]卡兹戴尔能飞起来[/orange]，离不开各族朋友的帮衬。这次设计研讨会就是为了将我们的经验和教训分享给所有人，算是一份小小的谢礼。”\n\n你跟着那群同样迷路的[orange]异族[/orange]抵达了会场，做了不少笔记。你觉得这些知识将来肯定能[sine][gold]派上用场[/gold][/sine]。")),
			new EventLoc(
				"Completion Ceremony",
				new EventPageLoc(
					InitialPage,
					"[aqua]Night[/aqua] howls past, and the scorching wind lifts [red]ash[/red] and torn pages. This is the first night after [orange]Kazdel[/orange] rose into the sky. No trace of the city remains on the exposed earth, only hundreds of [red]burning iron barrels[/red].\n\nYou see people throwing stacks of [gold]blueprints[/gold] into the fire. The [purple]Sarkaz[/purple] dressed as engineers look warily behind you. When you turn back, a group of hostile-looking [orange]outsiders[/orange] stands there.\n\nOne of the engineers left on the ground questions you. You answer...",
					new EventOptionLoc("DEMAND_BLUEPRINTS", "Hand over the flying city's blueprints!", "Gain a card reward."),
					new EventOptionLoc("ATTEND_SEMINAR", "We are here for the seminar", "Gain [gold]Old Gaulish Place Names[/gold].")),
				new EventPageLoc(
					"DEMAND_BLUEPRINTS",
					"The Sarkaz engineers snort coldly, gather every [gold]blueprint[/gold] on the ground, and throw them all into the [red]flames[/red]. Then they turn and run into the [aqua]night[/aqua].\n\nYou have no intention of chasing them. Instead, you snatch a few half-burned [gold]pages[/gold] from the fire. The contradiction between the Sarkaz and other races may be impossible to reconcile. Otherwise, they would not have fled [sine][aqua]into the sky[/aqua][/sine]."),
				new EventPageLoc(
					"ATTEND_SEMINAR",
					"When the Sarkaz engineers hear that you are here for the [orange]seminar[/orange], their expressions soften, and they point you toward the venue.\n\n\"[orange]Kazdel could not have flown[/orange] without help from friends of many races. This design seminar exists to share our experience and lessons with everyone, as a small token of thanks.\"\n\nYou follow the group of equally lost [orange]outsiders[/orange] to the venue and take plenty of notes. You feel certain this knowledge will [sine][gold]come in handy[/gold][/sine] someday."))
		);
	}
}
