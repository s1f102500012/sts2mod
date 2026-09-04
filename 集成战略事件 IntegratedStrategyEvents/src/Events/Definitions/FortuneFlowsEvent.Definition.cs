
namespace IntegratedStrategyEvents.Events;

public sealed partial class FortuneFlowsEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"fortune_flows.png",
			CreateLocalization,
			IntegratedStrategyEventLayoutProfile.LeftWide);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"财源广进",
				new EventPageLoc(
					InitialPage,
					"这座[gold]金碧辉煌[/gold]的庙宇里供奉着一座[aqua]玄铁[/aqua]打造的[orange]阿纳萨塑像[/orange]。听过路参拜的先民说，[orange]玄铁三身天王[/orange]是掌管[gold]商运财运[/gold]的大能，只要[b]虔心祈祷[/b]或[gold]随喜结缘[/gold]，就能得到天王的祝福，从而[sine][gold]财源广进[/gold][/sine]。",
					new EventOptionLoc("OFFER_SOME", "奉上一些财物", "支付[red]150[/red][gold]金币[/gold]。获得[blue]2[/blue]件随机[gold]遗物[/gold]。"),
					new EventOptionLoc("OFFER_SOME_LOCKED", "奉上一些财物", "需要[blue]150[/blue][gold]金币[/gold]。"),
					new EventOptionLoc("OFFER_ALL", "奉上所有财物", "支付[red]所有[/red][gold]金币[/gold]。获得[blue]3[/blue]件随机[gold]遗物[/gold]。"),
					new EventOptionLoc("LEAVE", "多一事不如少一事", "离开。")),
				new EventPageLoc(
					"OFFER",
					"你刚[orange]参拜[/orange]完毕，一位[orange]白发库兰塔居士[/orange]就托着裹有[gold]黄绸[/gold]的方盒出现在你面前，将这来自庙宇的谢礼赠予了你。\n\n她返回后院时，你无意间瞥见了院子里全是摆放齐整的[gold]黄绸盒子[/gold]。令[orange]信仰[/orange]与[gold]财富[/gold]能够互相置换，看来，这就是寺庙香火旺盛的真正原因。"),
				new EventPageLoc(
					"LEAVE",
					"你根本不认识这里供奉的[orange]阿纳萨[/orange]，对两位[aqua]蓝发居士[/aqua]的解说也不感兴趣。在寺庙里草草逛了一圈后，你就离开了。")),
			new EventLoc(
				"Fortune Flows In",
				new EventPageLoc(
					InitialPage,
					"This [gold]resplendent temple[/gold] enshrines an [orange]Anasa statue[/orange] forged from [aqua]black iron[/aqua]. According to the pilgrims passing by, the [orange]Three-bodied Black Iron King[/orange] governs [gold]trade and fortune[/gold]. Those who [b]pray sincerely[/b] or [gold]offer alms[/gold] may receive the king's blessing and see [sine][gold]fortune flow in[/gold][/sine].",
					new EventOptionLoc("OFFER_SOME", "Offer some wealth", "Pay [red]150[/red] [gold]Gold[/gold]. Gain [blue]2[/blue] random [gold]Relics[/gold]."),
					new EventOptionLoc("OFFER_SOME_LOCKED", "Offer some wealth", "Requires [blue]150[/blue] [gold]Gold[/gold]."),
					new EventOptionLoc("OFFER_ALL", "Offer all your wealth", "Pay [red]all[/red] your [gold]Gold[/gold]. Gain [blue]3[/blue] random [gold]Relics[/gold]."),
					new EventOptionLoc("LEAVE", "Better safe than sorry", "Leave.")),
				new EventPageLoc(
					"OFFER",
					"Just after you finish [orange]praying[/orange], a [orange]white-haired Kuranta laywoman[/orange] appears before you with a square box wrapped in [gold]yellow silk[/gold], presenting it as the temple's thanks.\n\nAs she returns to the back courtyard, you glimpse rows upon rows of neatly arranged [gold]yellow-silk boxes[/gold]. It seems that allowing [orange]faith[/orange] and [gold]wealth[/gold] to be exchanged is the true reason this temple's incense burns so brightly."),
				new EventPageLoc(
					"LEAVE",
					"You do not know the [orange]Anasa[/orange] worshiped here, and you are not interested in the explanation from the two [aqua]blue-haired laywomen[/aqua]. After a cursory walk around the temple, you leave."))
		);
	}
}
