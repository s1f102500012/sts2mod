
namespace IntegratedStrategyEvents.Events;

public sealed partial class TruthToBeToldEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"truth_to_be_told.png",
			CreateLocalization,
			IntegratedStrategyEventLayoutProfile.LeftWide);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"待诉说的真相",
				new EventPageLoc(
					InitialPage,
					"你注意到了卡兹戴尔角落里的一条[sine][purple]裂隙[/purple][/sine]，一个不成熟的[gold]想法[/gold]催生了它，却又[jitter][purple]没能使之完满[/purple][/jitter]。\n\n看着裂隙中展现出的景象，你决定——",
					new EventOptionLoc("COMPLETE_FLAW", "用构想补足缺陷", "随机获得[blue]2[/blue]张牌。"),
					new EventOptionLoc("ABSORB_THOUGHT", "将它吸纳入思维", "希望它能滋润其他[gold]故事[/gold]。")),
				new EventPageLoc(
					"COMPLETE_FLAW",
					"[sine][purple]裂隙[/purple][/sine]扩张，覆盖了周围的景色，与此同时，一种模糊的[gold]连结感[/gold]在你的思维中扩散开来......\n\n无论如何，一段[gold]故事[/gold]开始了。",
					new EventOptionLoc("ENTER_FRAGMENT", "进入其中", "进入[sine][purple]诡谲断章[/purple][/sine]。")),
				new EventPageLoc(
					"ABSORB_THOUGHT",
					"裂隙很快就消失了，在你的[gold]思维[/gold]中留下了一些[sine][purple]零碎的认知[/purple][/sine]。",
					new EventOptionLoc("COLLECT_THOUGHTS", "收集散落的思绪", "获得[blue]1[/blue]瓶随机[green]药水[/green]。")),
				new EventPageLoc(
					"COLLECT_THOUGHTS",
					"假以时日，这些[gold]想法[/gold]或许能被运用在合适的地方。")),
			new EventLoc(
				"A Truth Yet to Be Told",
				new EventPageLoc(
					InitialPage,
					"You notice a [sine][purple]rift[/purple][/sine] tucked away in a corner of Kazdel. An immature [gold]idea[/gold] gave birth to it, but [jitter][purple]failed to make it whole[/purple][/jitter].\n\nWatching the scenery revealed within the rift, you decide...",
					new EventOptionLoc("COMPLETE_FLAW", "Complete the flaw with conception", "Gain [blue]2[/blue] random cards."),
					new EventOptionLoc("ABSORB_THOUGHT", "Absorb it into your thoughts", "May it nourish other [gold]stories[/gold].")),
				new EventPageLoc(
					"COMPLETE_FLAW",
					"The [sine][purple]rift[/purple][/sine] expands, covering the surrounding scenery. At the same time, a vague sense of [gold]connection[/gold] spreads through your thoughts...\n\nOne way or another, a [gold]story[/gold] begins.",
					new EventOptionLoc("ENTER_FRAGMENT", "Enter it", "Enter the [sine][purple]Eerie Fragment[/purple][/sine].")),
				new EventPageLoc(
					"ABSORB_THOUGHT",
					"The rift soon vanishes, leaving fragments of [sine][purple]scattered cognition[/purple][/sine] within your [gold]thoughts[/gold].",
					new EventOptionLoc("COLLECT_THOUGHTS", "Collect the scattered thoughts", "Gain [blue]1[/blue] random [green]Potion[/green].")),
				new EventPageLoc(
					"COLLECT_THOUGHTS",
					"Given time, these [gold]ideas[/gold] may find a suitable use."))
		);
	}
}
