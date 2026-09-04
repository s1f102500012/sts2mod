
namespace IntegratedStrategyEvents.Events;

public sealed partial class SwordInStoneEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"sword_in_stone.png",
			CreateLocalization,
			Layout: IntegratedStrategyEventLayoutProfile.LeftWide,
			AlignHoverTipsRight: true);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"“石中剑”",
				new EventPageLoc(
					InitialPage,
					"你遇到了一个穿着[orange]古旧盔甲[/orange]的[gold]骑士[/gold]，正愁眉苦脸地看着一把插在[purple]巨石[/purple]中的[sine][aqua]宝剑[/aqua][/sine]。\n\n他自称是外出寻找[gold]圣剑[/gold]的勇士，需要把这把宝剑带回自己的[orange]王国[/orange]才能成为[gold]国王[/gold]。但是现在，他甚至没办法把剑从石头里[jitter][red]拔出来[/red][/jitter]。",
					new EventOptionLoc("BREAK_SWORD", "干脆把剑折断", "获得[gold]“断剑”[/gold]。"),
					new EventOptionLoc("LIFT_STONE", "石头能当锤子用", "获得[gold]“剑锤”[/gold]。"),
					new EventOptionLoc("QUESTION_NECESSITY", "似乎没有必要", "剑与王位存在必要联系吗？")),
				new EventPageLoc(
					"BREAK_SWORD",
					"你想尽一切办法，最终把[sine][aqua]宝剑[/aqua][/sine][jitter][red]敲成两节[/red][/jitter]。\n\n看着残留在[purple]石头[/purple]中的半截宝剑，[gold]骑士[/gold]长叹一口气，扭头离开了。"),
				new EventPageLoc(
					"LIFT_STONE",
					"听到你的建议，[gold]骑士[/gold]尝试连带着[purple]巨石[/purple]一起举起[sine][aqua]宝剑[/aqua][/sine]，却因为[jitter][red]重心不稳[/red][/jitter]而跌倒。\n\n头部着地的瞬间，他就晕了过去。"),
				new EventPageLoc(
					"QUESTION_NECESSITY",
					"“一把插在[purple]奇怪石头[/purple]里的[aqua]破剑[/aqua]绝不是组成国家和政府的基础。一位[gold]国王[/gold]应该是依靠[orange]群众的支持[/orange]获得王位。\n\n靠着一把来路不明的[sine][aqua]宝剑[/aqua][/sine]就能当国王也太[jitter][red]离谱[/red][/jitter]了。”\n\n[gold]骑士[/gold]觉得你说的话很有道理，心满意足地离开了。")),
			new EventLoc(
				"\"Sword in the Stone\"",
				new EventPageLoc(
					InitialPage,
					"You meet a [gold]knight[/gold] in [orange]ancient armor[/orange], staring glumly at a [sine][aqua]sword[/aqua][/sine] embedded in a [purple]great stone[/purple].\n\nHe claims to be a brave soul searching for a [gold]holy sword[/gold]. He must bring this blade back to his [orange]kingdom[/orange] before he can become [gold]king[/gold]. But at the moment, he cannot even [jitter][red]pull it out[/red][/jitter] of the stone.",
					new EventOptionLoc("BREAK_SWORD", "Just break the sword", "Gain [gold]\"Broken Sword\"[/gold]."),
					new EventOptionLoc("LIFT_STONE", "Stone works as a hammer", "Gain [gold]\"Sword Hammer\"[/gold]."),
					new EventOptionLoc("QUESTION_NECESSITY", "Seems unnecessary", "Must sword and throne be connected?")),
				new EventPageLoc(
					"BREAK_SWORD",
					"You try every method you can think of, and finally [jitter][red]snap[/red][/jitter] the [sine][aqua]sword[/aqua][/sine] in two.\n\nLooking at the half blade left in the [purple]stone[/purple], the [gold]knight[/gold] sighs deeply, turns, and leaves."),
				new EventPageLoc(
					"LIFT_STONE",
					"After hearing your suggestion, the [gold]knight[/gold] attempts to lift the [sine][aqua]sword[/aqua][/sine] together with the [purple]great stone[/purple]. The [jitter][red]balance[/red][/jitter] is impossible, and he falls.\n\nThe instant his head hits the ground, he passes out."),
				new EventPageLoc(
					"QUESTION_NECESSITY",
					"\"A [aqua]broken sword[/aqua] stuck in a [purple]strange stone[/purple] is no basis for a system of government. A [gold]king[/gold] should receive his throne through [orange]the support of the people[/orange].\n\nBecoming king just because of some unknown [sine][aqua]sword[/aqua][/sine] is [jitter][red]absurd[/red][/jitter].\"\n\nThe [gold]knight[/gold] finds your words quite reasonable, and leaves satisfied."))
		);
	}
}
