
namespace IntegratedStrategyEvents.Events;

public sealed partial class LiuerEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"liuer.png",
			CreateLocalization,
			Layout: IntegratedStrategyEventLayoutProfile.LeftNarrow,
			AlignHoverTipsRight: true);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"柳儿",
				new EventPageLoc(
					InitialPage,
					"[gold]柳儿[/gold]是[orange]司岁台[/orange]伥物房的吏员，主职乃是为你们一行人解析、开发各类[aqua]伥物[/aqua]与[aqua]仪具[/aqua]。\n\n她镇抚过许多器伥，随后将之投入对界园的探索与修缮，[gold]卓有成效[/gold]。但对于是否要使用她那些[purple]毁誉参半[/purple]的发明……你持保留意见，毕竟，总是免不了要支付一笔不小的……\n\n哦，她来了……手上捧着个[gold]木疙瘩[/gold]？",
					new EventOptionLoc("ACCEPT", "接过", "支付[red]一半[/red][gold]金币[/gold]。获得[gold]浮木[/gold]。"),
					new EventOptionLoc("DECLINE", "婉拒", "多一事不如少一事。")),
				new EventPageLoc(
					"ACCEPT",
					"你[gold]乖乖付了账[/gold]，她说这[aqua]伥物[/aqua]能在[pink]岁识[/pink]中大显神通。\n\n虽然她自己都说不准有什么效果，但拿都拿了……就[purple]走一步看一步[/purple]吧。"),
				new EventPageLoc(
					"DECLINE",
					"曾有收了柳儿[aqua]伥物[/aqua]的人白日[jitter][red]目盲[/red][/jitter]，还有人一条舌头只能吃出个[green]蒜味[/green]。\n\n总之，为了免生祸端，你们[sine][aqua]婉言拒绝[/aqua][/sine]了她。")),
			new EventLoc(
				"Liuer",
				new EventPageLoc(
					InitialPage,
					"[gold]Liuer[/gold] is a clerk of [orange]Sui's office[/orange], charged with analyzing and developing all manner of [aqua]instruments[/aqua] for your group.\n\nShe has subdued many unruly tools and put them to work exploring and repairing the garden, to [gold]notable effect[/gold]. Still, you have reservations about using her [purple]controversial inventions[/purple]. After all, they always come with a sizable...\n\nOh, here she comes... holding some [gold]wooden lump[/gold]?",
					new EventOptionLoc("ACCEPT", "Take it", "Pay half your Gold. Gain [gold]Driftwood[/gold]."),
					new EventOptionLoc("DECLINE", "Decline", "Better safe than sorry.")),
				new EventPageLoc(
					"ACCEPT",
					"You [gold]obediently pay[/gold]. She says this [aqua]thing[/aqua] will work wonders in [pink]Sui's memory[/pink].\n\nEven she cannot say exactly what it does, but you have it now... so [purple]take things one step at a time[/purple]."),
				new EventPageLoc(
					"DECLINE",
					"Someone who once accepted one of Liuer's [aqua]tools[/aqua] went [jitter][red]blind in daylight[/red][/jitter]. Another could only taste [green]garlic[/green].\n\nIn short, to avoid trouble, you [sine][aqua]politely refuse[/aqua][/sine] her."))
		);
	}
}
