
namespace IntegratedStrategyEvents.Events;

public sealed partial class SecretRoomEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"secret_room.png",
			CreateLocalization,
			Layout: IntegratedStrategyEventLayoutProfile.LeftWide,
			AlignHoverTipsRight: true);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"密室",
				new EventPageLoc(
					InitialPage,
					"你拖着疲惫的步伐游荡在高塔中，不慎从楼梯上[jitter][red]滚落[/red][/jitter]，没想到反而打开了一扇[sine][aqua]隐藏的机关房门[/aqua][/sine]。\n\n你看到了一间装饰别致的卧室。与高塔的其他区域不同，卧室干净整洁，似乎有人经常打理。\n\n你注意到卧室的书桌上摆着一尊造型精美的[orange]雕塑[/orange]。",
					new EventOptionLoc("TAKE_SCULPTURE", "拿走雕塑", "获得[gold]《麻木与庸俗》[/gold]。"),
					new EventOptionLoc("REST", "尝试休息", "回复[blue]15[/blue]点生命。")),
				new EventPageLoc(
					"TAKE_SCULPTURE",
					"[orange]雕塑[/orange]的底座上写着几行文字：\n\n“曾经的我沉迷于[jitter][purple]过激的艺术表达[/purple][/jitter]，但是这依然是追求[sine][aqua]美学之湖[/aqua][/sine]的一叶扁舟，这一切本不应受到[red]庸俗之辈[/red]的指责。\n\n倘若艺术只浮于表面，[sine][gold]美[/gold][/sine]与[jitter][red]丑[/red][/jitter]又如何区别？”"),
				new EventPageLoc(
					"REST",
					"为什么这座古老的高塔里会有如此[sine][aqua]先进的隐藏机关[/aqua][/sine]？高塔的主人到底是什么人？\n\n无论背后有什么答案，这里至少能成为一个[green]安全的憩所[/green]，还是先[gold]休息[/gold]吧。")),
			new EventLoc(
				"Secret Room",
				new EventPageLoc(
					InitialPage,
					"You wander through the Spire with exhausted steps, then accidentally [jitter][red]tumble[/red][/jitter] down a staircase. Somehow, the fall opens a [sine][aqua]hidden mechanical door[/aqua][/sine].\n\nBeyond it is a tastefully decorated bedroom. Unlike the rest of the Spire, the room is clean and orderly, as though someone tends to it often.\n\nOn the desk, you notice an exquisitely shaped [orange]sculpture[/orange].",
					new EventOptionLoc("TAKE_SCULPTURE", "Take the sculpture", "Gain [gold]Numbness and Vulgarity[/gold]."),
					new EventOptionLoc("REST", "Try to rest", "Heal [blue]15[/blue] HP.")),
				new EventPageLoc(
					"TAKE_SCULPTURE",
					"Several lines are written on the base of the [orange]sculpture[/orange]:\n\n\"I once lost myself in [jitter][purple]extreme artistic expression[/purple][/jitter], but even that was a small boat crossing the [sine][aqua]lake of aesthetics[/aqua][/sine]. None of it should have been condemned by [red]vulgar souls[/red].\n\nIf art remains only on the surface, how can [sine][gold]beauty[/gold][/sine] be distinguished from [jitter][red]ugliness[/red][/jitter]?\""),
				new EventPageLoc(
					"REST",
					"Why would this ancient Spire contain such a [sine][aqua]advanced hidden mechanism[/aqua][/sine]? Who is the owner of this tower?\n\nWhatever answer lies behind it, this place can at least serve as a [green]safe refuge[/green]. You decide to [gold]rest[/gold] first."))
		);
	}
}
