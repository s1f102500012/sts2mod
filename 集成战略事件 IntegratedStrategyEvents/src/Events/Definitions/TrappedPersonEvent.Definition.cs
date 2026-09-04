
namespace IntegratedStrategyEvents.Events;

public sealed partial class TrappedPersonEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"trapped_person.png",
			CreateLocalization,
			IntegratedStrategyEventLayoutProfile.StandardMediumNarrowSlightlyShiftedRight);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"被困的人",
				new EventPageLoc(
					InitialPage,
					"一个青年样貌的[orange]菲林[/orange]不知为何一直躲在[aqua]长桌底下[/aqua]，一旦有人靠近，就会发出[purple]惊恐的呢喃[/purple]。\n\n他坚持不愿从桌下出来，但在你的努力下，他表示相信你不是来捉他演戏唱歌的[red]魔鬼[/red]，并愿意拿自己[gold]唯一的宝物[/gold]与你做交换。",
					new EventOptionLoc("TRADE", "交换必须平等公正", "支付[red]所有[/red][gold]金币[/gold]。获得[gold]眼熟的雕像[/gold]。"),
					new EventOptionLoc("COAX_OUT", "先出来再说话", "可不能指望这样的人。")),
				new EventPageLoc(
					"TRADE",
					"或许这[sine][gold]值得[/gold][/sine]，或许……"),
				new EventPageLoc(
					"COAX_OUT",
					"他能有什么[gold]宝物[/gold]？行行好，先从[aqua]桌底下[/aqua]出来吧！")),
			new EventLoc(
				"The Trapped Man",
				new EventPageLoc(
					InitialPage,
					"A youthful [orange]Feline[/orange] is hiding beneath a [aqua]long table[/aqua] for reasons unknown. Whenever anyone approaches, he lets out [purple]terrified murmurs[/purple].\n\nHe refuses to come out, but after your efforts, he says he believes you are not a [red]devil[/red] here to drag him away to act and sing. He is willing to trade his [gold]only treasure[/gold] with you.",
					new EventOptionLoc("TRADE", "Trades must be fair", "Pay [red]all[/red] your [gold]Gold[/gold]. Gain [gold]Familiar Statue[/gold]."),
					new EventOptionLoc("COAX_OUT", "Come out first", "You cannot count on someone like this.")),
				new EventPageLoc(
					"TRADE",
					"Perhaps it is [sine][gold]worth it[/gold][/sine]. Perhaps..."),
				new EventPageLoc(
					"COAX_OUT",
					"What [gold]treasure[/gold] could he have? Please, come out from [aqua]under the table[/aqua] first!"))
		);
	}
}
