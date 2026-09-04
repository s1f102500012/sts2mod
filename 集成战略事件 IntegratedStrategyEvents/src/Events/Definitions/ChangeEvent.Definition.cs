
namespace IntegratedStrategyEvents.Events;

public sealed partial class ChangeEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"change.png",
			CreateLocalization,
			IntegratedStrategyEventLayoutProfile.StandardNarrow);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"变化",
				new EventPageLoc(
					InitialPage,
					"距离第一次发现[aqua]冰原尽头[/aqua]的[gold]巨大设施[/gold]已经过去了许多年，在这期间发生了诸多大事：[b]《[gold]泰拉冰原巨构研究及修复公约[/gold]》[/b]——[jitter][red]黑洞协议[/red][/jitter]的诞生，[sine][green]巨兽萨米[/green][/sine]的离去，[purple]坍缩体[/purple]研究带来的又一次科技爆发。\n\n前往北方，去到那个[gold]改变一切[/gold]的地方无疑是探险家们的夙愿，可是，邀请函上所说的[jitter][red]考验[/red][/jitter]......到底是什么呢？就在这时，你发现营地里多了个[sine][purple]陌生的人影[/purple][/sine]，而其他队员似乎都没看到他。",
					new EventOptionLoc("ACCEPT_INVITATION", "接受邀约", "让探索开启不同的方向。"),
					new EventOptionLoc("REACH_FOR_WEAPON", "伸手去够武器", "有入侵者！")),
				new EventPageLoc(
					"ACCEPT_INVITATION",
					"你下意识地掏出了[gold]邀约[/gold]交给来人，他看了一眼，点点头，随后就转身消失在你眼前。在他消失的那一刻，你似乎看到了[aqua]罗德岛[/aqua]的标志与一支[gold]骨笔[/gold]。\n\n然而，下一秒，你就置身于[orange]萨尔贡的黄沙[/orange]之中，队员们在这[jitter][purple]突变[/purple][/jitter]下陷入恐慌，而你却很清楚，这就是对你的[red]考验[/red]，你得证明自己。",
					new EventOptionLoc("FIGHT", "进入战斗", "遭遇一场特殊的战斗。")),
				new EventPageLoc(
					"REACH_FOR_WEAPON",
					"就在你想要[red]攻击[/red]他的时候，他消失在了[sine][aqua]雾气[/aqua][/sine]之中，其他科考队员都带着[jitter]惊讶[/jitter]的目光看着你。\n\n还好，这场闹剧很快就过去了，你们再次踏上了前往[aqua]冰原[/aqua]的道路。")),
			new EventLoc(
				"Change",
				new EventPageLoc(
					InitialPage,
					"Many years have passed since the [gold]vast facility[/gold] at the [aqua]end of the icefield[/aqua] was first found. Much has happened since then: the birth of [b]the [gold]Terra Icefield Megastructure Research and Restoration Convention[/gold][/b], the [jitter][red]Black Hole Protocol[/red][/jitter]; the departure of [sine][green]Sami the Beast[/green][/sine]; and another technological leap driven by [purple]collapsed entity[/purple] research.\n\nGoing north to that place which [gold]changed everything[/gold] is surely every explorer's wish. Yet what exactly is the [jitter][red]trial[/red][/jitter] mentioned in the invitation? Just then, you notice a [sine][purple]strange figure[/purple][/sine] in the camp, though the rest of the team seems unable to see him.",
					new EventOptionLoc("ACCEPT_INVITATION", "Accept the invitation", "Let exploration open onto another direction."),
					new EventOptionLoc("REACH_FOR_WEAPON", "Reach for your weapon", "An intruder!")),
				new EventPageLoc(
					"ACCEPT_INVITATION",
					"Almost on instinct, you hand the [gold]invitation[/gold] to the visitor. He glances at it, nods, then turns and vanishes before your eyes. For a moment, you think you see the mark of [aqua]Rhodes Island[/aqua] and a [gold]bone pen[/gold].\n\nThe next instant, you stand amid the [orange]yellow sands of Sargon[/orange]. Your companions panic at this [jitter][purple]sudden shift[/purple][/jitter], but you understand clearly: this is the [red]trial[/red]. You must prove yourself.",
					new EventOptionLoc("FIGHT", "Enter combat", "Encounter a special fight.")),
				new EventPageLoc(
					"REACH_FOR_WEAPON",
					"As you move to [red]attack[/red] him, he disappears into the [sine][aqua]mist[/aqua][/sine]. The other members of the expedition stare at you in [jitter]shock[/jitter].\n\nFortunately, the commotion soon passes, and you set out once more toward the [aqua]icefield[/aqua]."))
		);
	}
}
