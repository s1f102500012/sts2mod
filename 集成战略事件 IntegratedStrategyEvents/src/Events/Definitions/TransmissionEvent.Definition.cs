
namespace IntegratedStrategyEvents.Events;

public sealed partial class TransmissionEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"transmission.png",
			CreateLocalization,
			IntegratedStrategyEventLayoutProfile.StandardNarrowSlightlyShiftedRight);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"传讯",
				new EventPageLoc(
					InitialPage,
					"无论何时，[aqua]声音[/aqua]都是最即时的传讯手段。当新手还在取腰间的对讲机时，富有经验的老师傅早已一声[sine][aqua]长啸[/aqua][/sine]，声调[aqua]蜿蜒[/aqua]，将讯息传了出去。\n\n听得山那头传来[sine][aqua]声响[/aqua][/sine]，便知道事已办成。不多时，一套[gold]装备[/gold]便由驮兽送至眼前，待人取用。",
					new EventOptionLoc("TAKE_ARMOR", "拿些护甲", "将你牌组中的[blue]1[/blue]张[gold]防御[/gold][purple]变化[/purple]为[gold]妙计[/gold]。"),
					new EventOptionLoc("TAKE_ARMOR_LOCKED", "拿些护甲", "没有可[purple]变化[/purple]的牌。"),
					new EventOptionLoc("TAKE_FLAG", "取面令旗", "将你牌组中的[blue]1[/blue]张[gold]打击[/gold][purple]变化[/purple]为[gold]亮剑[/gold]。"),
					new EventOptionLoc("TAKE_FLAG_LOCKED", "取面令旗", "没有可[purple]变化[/purple]的牌。"),
					new EventOptionLoc("LEAVE", "没有需求", "离开。")),
				new EventPageLoc(
					"TAKE_ARMOR",
					"这些看似[aqua]简易[/aqua]、在小摊上大量出现的工艺护甲，其实经过了严格的[gold]赐福开光[/gold]手续，有着[sine][green]辟邪消灾[/green][/sine]的功用。"),
				new EventPageLoc(
					"TAKE_FLAG",
					"这面[orange]令旗[/orange]能够供秉烛人[aqua]快速调动[/aqua]后备人员，在尖塔中及时获得[green]人力支持[/green]。"),
				new EventPageLoc(
					"LEAVE",
					"临行时，见你们要往界园深处走，老师傅抓着你的手，麻烦你们给里头回讯的[orange]天师府后人[/orange]传个信。\n\n虽不知那究竟是[purple]人是伥[/purple]，但你们还是[b]应承[/b]了下来。")),
			new EventLoc(
				"Transmission",
				new EventPageLoc(
					InitialPage,
					"At any time, [aqua]sound[/aqua] is the quickest way to send a message. While a novice is still reaching for the radio at their waist, an experienced master has already released a [sine][aqua]long cry[/aqua][/sine], its tone [aqua]winding[/aqua] through the air to carry the message onward.\n\nWhen an [sine][aqua]answer[/aqua][/sine] comes from the far side of the mountain, you know the matter is done. Before long, a set of [gold]equipment[/gold] is delivered by pack beast and laid before you.",
					new EventOptionLoc("TAKE_ARMOR", "Take some armor", "[purple]Transform[/purple] [blue]1[/blue] [gold]Defend[/gold] in your deck into [gold]Finesse[/gold]."),
					new EventOptionLoc("TAKE_ARMOR_LOCKED", "Take some armor", "No transformable card."),
					new EventOptionLoc("TAKE_FLAG", "Take a command flag", "[purple]Transform[/purple] [blue]1[/blue] [gold]Strike[/gold] in your deck into [gold]Flash of Steel[/gold]."),
					new EventOptionLoc("TAKE_FLAG_LOCKED", "Take a command flag", "No transformable card."),
					new EventOptionLoc("LEAVE", "No need", "Leave.")),
				new EventPageLoc(
					"TAKE_ARMOR",
					"These craft armors may look [aqua]simple[/aqua] and common at market stalls, but each has passed through strict [gold]blessing rites[/gold], giving it the power to [sine][green]ward off misfortune[/green][/sine]."),
				new EventPageLoc(
					"TAKE_FLAG",
					"This [orange]command flag[/orange] lets candlebearers [aqua]quickly mobilize[/aqua] reserves and receive timely [green]support[/green] within the garden."),
				new EventPageLoc(
					"LEAVE",
					"As you prepare to leave for the depths of the garden, the old master grabs your hand and asks you to carry a message to the [orange]descendants of the Celestial Master's estate[/orange] who answered from within.\n\nYou do not know whether they are truly [purple]human or something else[/purple], but you still [b]agree[/b]."))
		);
	}
}
