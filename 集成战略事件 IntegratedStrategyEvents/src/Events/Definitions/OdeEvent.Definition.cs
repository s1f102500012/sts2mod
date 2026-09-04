
namespace IntegratedStrategyEvents.Events;

public sealed partial class OdeEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"ode.png",
			CreateLocalization,
			Layout: IntegratedStrategyEventLayoutProfile.LeftMedium);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"赞歌",
				new EventPageLoc(
					InitialPage,
					"你在[sine][purple]祂[/purple][/sine]的栖所旁发现了一片没有被[blue]海嗣[/blue]染指的区域。里面除了[aqua]海砂[/aqua]、一座破损的[gold]人像[/gold]与一对紧握武器的[sine][blue]睡美人[/blue][/sine]之外，什么都没有。",
					new EventOptionLoc("TAKE_RELICS", "亲吻猎人们的额头并取走遗物", "获得[blue]3[/blue]件随机[gold]遗物[/gold]。"),
					new EventOptionLoc("LEAVE", "离开", "多一事不如少一事。")),
				new EventPageLoc(
					"AFTERMATH",
					"直至最后一刻，[aqua]深海猎人[/aqua]仍是以[gold]人[/gold]，而非[blue]海嗣[/blue]的身份睡去。在你离开之后，[red]恐鱼[/red]也没有侵占这里，一道刻入[blue]族群[/blue]的[jitter][red]伤痕[/red][/jitter]就此形成。\n\n它们不会记住猎人们的样貌，它们只知道，那里不可以进入，[sine][gold]永远不可[/gold][/sine]。")),
			new EventLoc(
				"Ode",
				new EventPageLoc(
					InitialPage,
					"Beside the lair of [sine][purple]Them[/purple][/sine], you find a place untouched by the [blue]Seaborn[/blue]. There is nothing here but [aqua]sea sand[/aqua], a broken [gold]statue[/gold], and a pair of [sine][blue]sleeping beauties[/blue][/sine] holding their weapons tight.",
					new EventOptionLoc("TAKE_RELICS", "Kiss the hunters' foreheads and take the relics", "Gain [blue]3[/blue] random [gold]Relics[/gold]."),
					new EventOptionLoc("LEAVE", "Leave", "Better safe than sorry.")),
				new EventPageLoc(
					"AFTERMATH",
					"Until the very last moment, the [aqua]Abyssal Hunters[/aqua] fell asleep as [gold]humans[/gold], not as [blue]Seaborn[/blue]. After you leave, the [red]Sea Terrors[/red] do not occupy this place either. A [jitter][red]scar[/red][/jitter] is carved into the [blue]cluster[/blue].\n\nThey will not remember the hunters' faces. They only know that this place must not be entered, [sine][gold]never again[/gold][/sine]."))
		);
	}
}
