
namespace IntegratedStrategyEvents.Events;

public sealed partial class StoryToBeToldEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"story_to_be_told.png",
			CreateLocalization,
			IntegratedStrategyEventLayoutProfile.LeftWide);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"待诉说的故事",
				new EventPageLoc(
					InitialPage,
					"你注意到了卡兹戴尔角落里的一条[sine][purple]裂隙[/purple][/sine]，一个不成熟的[gold]想法[/gold]催生了它，却又[jitter][purple]没能使之完满[/purple][/jitter]。\n\n看着裂隙中展现出的景象，你决定——",
					new EventOptionLoc("COMPLETE_FLAW", "用构想补足缺陷", "随机获得[blue]2[/blue]张牌。"),
					new EventOptionLoc("ABSORB_THOUGHT", "将它吸纳入思维", "无法结果的[purple]斜枝[/purple]，只好成为其他[gold]故事[/gold]的养料。")),
				new EventPageLoc(
					"COMPLETE_FLAW",
					"[sine][purple]裂隙[/purple][/sine]扩张，覆盖了周围的景色。\n\n一段[gold]故事[/gold]开始了。",
					new EventOptionLoc("ENTER_FRAGMENT", "进入其中", "进入[sine][purple]诡谲断章[/purple][/sine]。")),
				new EventPageLoc(
					"ABSORB_THOUGHT",
					"裂隙很快就消失了，在你的[gold]思维[/gold]中留下了一些[sine][purple]零碎的认知[/purple][/sine]。",
					new EventOptionLoc("COLLECT_THOUGHTS", "收集散落的思绪", "获得[blue]1[/blue]瓶随机[green]药水[/green]。")),
				new EventPageLoc(
					"COLLECT_THOUGHTS",
					"假以时日，这些[gold]想法[/gold]或许能被运用在合适的地方。")),
			new EventLoc(
				"A Story Yet to Be Told",
				new EventPageLoc(
					InitialPage,
					"You notice a [sine][purple]rift[/purple][/sine] tucked away in a corner of Kazdel. An immature [gold]idea[/gold] gave birth to it, but [jitter][purple]failed to make it whole[/purple][/jitter].\n\nWatching the scenery revealed within the rift, you decide...",
					new EventOptionLoc("COMPLETE_FLAW", "Complete the flaw with conception", "Gain [blue]2[/blue] random cards."),
					new EventOptionLoc("ABSORB_THOUGHT", "Absorb it into your thoughts", "A [purple]barren branch[/purple] can only become nourishment for other [gold]stories[/gold].")),
				new EventPageLoc(
					"COMPLETE_FLAW",
					"The [sine][purple]rift[/purple][/sine] expands, covering the surrounding scenery.\n\nA [gold]story[/gold] begins.",
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
