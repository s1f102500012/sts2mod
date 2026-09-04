
namespace IntegratedStrategyEvents.Events;

public sealed partial class FatefulMeetingEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } = IntegratedStrategyEventDefinition.ForEventPortrait("fateful_meeting.png", CreateLocalization);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"相逢",
				new EventPageLoc(
					InitialPage,
					"为了躲避突如其来的[aqua]风暴[/aqua]，你意外闯入了某个[aqua]溶洞[/aqua]。里面竟是一只重伤的[red]恐鱼[/red]。\n\n不对，那应该是......[jitter][purple]半个人和半只恐鱼[/purple][/jitter]不完美的拼接体，身上还挂着[gold]教士服[/gold]的残片。它在看见你的瞬间，濒死的眼神中迸出了一丝[green]光亮[/green]。",
					new EventOptionLoc("EXAMINE", "查看它的情况", "失去[red]8[/red]点生命。获得[blue]120[/blue][gold]金币[/gold]。"),
					new EventOptionLoc("EXAMINE_LOCKED", "查看它的情况", "需要至少[red]9[/red]点生命。"),
					new EventOptionLoc("AID", "尝试救助它", "失去[red]16[/red]点生命。获得[green]8[/green]点最大生命。"),
					new EventOptionLoc("AID_LOCKED", "尝试救助它", "需要至少[red]17[/red]点生命。"),
					new EventOptionLoc("LEAVE", "默默离开", "说不定是深海教徒的陷阱。")),
				new EventPageLoc(
					"EXAMINE",
					"眼前的异类散发着[red]刺激性气体[/red]，它正在死亡。而那只仅存的人类眼眸中，此时也只剩下了[purple]空洞的黑[/purple]。\n\n在你的记忆里，似乎有位[b]虔信的教士[/b]满怀信心回到[orange]伊比利亚[/orange]。你希望他不要像面前这个教士一般，[aqua]孤独[/aqua]且毫无尊严地死去。"),
				new EventPageLoc(
					"AID",
					"你顶着[red]呛人的气雾[/red]为他施救。然而，他没有肢体上的反应，只是以那只人类的眼睛怔怔地望着你。\n\n里面没有[green]希望[/green]，也没有[purple]绝望[/purple]，只剩下平静，[sine][aqua]死一般的平静[/aqua][/sine]。在撑过了数分钟后，他眼里的光芒终究是[jitter][red]消失了[/red][/jitter]。\n\n不知为何，你决定将他的遗体送到[aqua]塔外[/aqua]，而非留在这漆黑的洞穴里，最终成为[red]恐鱼[/red]的口粮。"),
				new EventPageLoc(
					"LEAVE",
					"你对[red]非人非恐鱼[/red]的怪物不感兴趣。它会引来其他[red]恐鱼[/red]的，还是赶快离开这里吧。")),
			new EventLoc(
				"Fateful Meeting",
				new EventPageLoc(
					InitialPage,
					"Seeking shelter from a sudden [aqua]storm[/aqua], you stumble into a [aqua]cavern[/aqua]. Inside is a badly wounded [red]seaborn beast[/red].\n\nNo, it is more like a [jitter][purple]failed joining of half a person and half a beast[/purple][/jitter], still carrying scraps of a [gold]cleric's robe[/gold]. The moment it sees you, a faint [green]light[/green] returns to its dying eye.",
					new EventOptionLoc("EXAMINE", "Check its condition", "Lose [red]8[/red] HP. Gain [blue]120[/blue] [gold]Gold[/gold]."),
					new EventOptionLoc("EXAMINE_LOCKED", "Check its condition", "Requires at least [red]9[/red] HP."),
					new EventOptionLoc("AID", "Try to aid it", "Lose [red]16[/red] HP. Gain [green]8[/green] Max HP."),
					new EventOptionLoc("AID_LOCKED", "Try to aid it", "Requires at least [red]17[/red] HP."),
					new EventOptionLoc("LEAVE", "Leave quietly", "It may be a cultist trap.")),
				new EventPageLoc(
					"EXAMINE",
					"The thing before you gives off [red]caustic fumes[/red]. It is dying, and its one remaining human eye now holds only [purple]empty blackness[/purple].\n\nYou remember a [b]devout cleric[/b] who once returned to [orange]Iberia[/orange] full of faith. You hope he does not die like this one, [aqua]alone[/aqua] and without dignity."),
				new EventPageLoc(
					"AID",
					"You push through the [red]choking mist[/red] and try to help him. His body does not respond. He only stares at you with that human eye.\n\nThere is no [green]hope[/green] in it, and no [purple]despair[/purple], only calm, [sine][aqua]deathly calm[/aqua][/sine]. After several minutes, the light in his eye [jitter][red]vanishes[/red][/jitter].\n\nFor reasons you cannot explain, you decide to carry his remains to the [aqua]sea[/aqua] rather than leave them in this black cavern to become food for [red]beasts[/red]."),
				new EventPageLoc(
					"LEAVE",
					"You have no interest in a [red]thing that is neither human nor beast[/red]. It will draw more [red]beasts[/red] here. Better to leave quickly."))
		);
	}
}
