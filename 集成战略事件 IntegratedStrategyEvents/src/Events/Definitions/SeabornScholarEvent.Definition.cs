
namespace IntegratedStrategyEvents.Events;

public sealed partial class SeabornScholarEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } = IntegratedStrategyEventDefinition.ForEventPortrait("seaborn_scholar.png", CreateLocalization);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"海嗣学者",
				new EventPageLoc(
					InitialPage,
					"一位老[aqua]阿戈尔人[/aqua]正在向你讲述对付[purple]海嗣[/purple]的要诀。据他所说，他曾隶属于一个专门研究海嗣行为的[aqua]科学院[/aqua]，然而科学院被海嗣[red]毁灭[/red]，他也被洋流带离了家乡。\n\n幸好，这里也有人在抵抗[purple]海嗣[/purple]，这样他的研究就还能派上点用场。现在，他收集了些[red]恐鱼尸体[/red]并以此制作装备。他希望你可以使用它，为这件[sine][orange]实验品[/orange][/sine]收集更多数据。",
					new EventOptionLoc("TRY_PROTOTYPE", "要不试试", "获得一件随机[gold]遗物[/gold]。[blue]50%[/blue]概率将一张随机[red]诅咒牌[/red]加入你的[gold]牌组[/gold]。"),
					new EventOptionLoc("LEAVE", "还是算了", "多一事不如少一事。")),
				new EventPageLoc(
					"TRY_PROTOTYPE",
					"见你接受了他的[sine][orange]实验品[/orange][/sine]，他欣慰地笑了。他向你讲解了这个东西的使用要点，并在最后提了一句：\n\n“如果这东西[jitter][red]活过来[/red][/jitter]，一定要立刻[red]破坏掉[/red]，明白吗？”"),
				new EventPageLoc(
					"LEAVE",
					"如果他的[aqua]研究[/aqua]真的能派上用场，他的城市就不会被[red]摧毁[/red]。\n\n所以你拒绝成为他的[orange]实验者[/orange]。")),
			new EventLoc(
				"Seaborn Scholar",
				new EventPageLoc(
					InitialPage,
					"An old [aqua]Aegir[/aqua] explains the essentials of fighting the [purple]Seaborn[/purple]. He says he once belonged to an [aqua]academy[/aqua] dedicated to studying Seaborn behavior, but the academy was [red]destroyed[/red], and the currents carried him far from home.\n\nFortunately, people here are also resisting the [purple]Seaborn[/purple], so his research may still be useful. He has gathered [red]Sea Terror corpses[/red] and fashioned equipment from them. He hopes you will use this [sine][orange]prototype[/orange][/sine] and collect more data for it.",
					new EventOptionLoc("TRY_PROTOTYPE", "Try it", "Gain a random [gold]Relic[/gold]. [blue]50%[/blue] chance to add a random [red]Curse[/red] to your deck."),
					new EventOptionLoc("LEAVE", "Forget it", "Better safe than sorry.")),
				new EventPageLoc(
					"TRY_PROTOTYPE",
					"Seeing you accept his [sine][orange]prototype[/orange][/sine], he smiles with relief. He explains how to use it, then adds one last thing:\n\n\"If this thing [jitter][red]comes alive[/red][/jitter], destroy it [red]immediately[/red]. Understood?\""),
				new EventPageLoc(
					"LEAVE",
					"If his [aqua]research[/aqua] were truly useful, his city would not have been [red]destroyed[/red].\n\nSo you refuse to become his [orange]test subject[/orange]."))
		);
	}
}
