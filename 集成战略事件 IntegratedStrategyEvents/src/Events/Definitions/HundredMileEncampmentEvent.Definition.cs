
namespace IntegratedStrategyEvents.Events;

public sealed partial class HundredMileEncampmentEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"hundred_mile_encampment.png",
			CreateLocalization,
			IntegratedStrategyEventLayoutProfile.StandardLowered);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"百里连营",
				new EventPageLoc(
					InitialPage,
					"似乎有一支规模庞大的[red]军队[/red]驻扎在了[aqua]冰原[/aqua]上，营寨一眼望不到头。是[orange]乌萨斯[/orange]吗？还是东国？抑或是大炎？\n\n也有可能都不是，这些营寨的样式完全与之不符。或许你应该近距离观察一下。",
					new EventOptionLoc("APPROACH", "走近一些", "失去[red]8[/red]点生命。"),
					new EventOptionLoc("APPROACH_LOCKED", "走近一些", "需要至少[red]9[/red]点生命。"),
					new EventOptionLoc("LEAVE", "还是离开吧", "太危险了。")),
				new EventPageLoc(
					"APPROACH",
					"营寨中射出的[red]箭矢[/red]对你的队伍造成了一定的伤害。可当你走到营寨中时，里面却什么都没有。\n\n就像是一个[purple]幻影[/purple]，一场骗局。正当你准备离开时，嘹亮的[sine][gold]号角声[/gold][/sine]凭空响起，原本空无一物的幻影帐篷中走出一位位身着异服的[orange]库兰塔[/orange]。\n\n他们迅速排好阵列，将一位[red]库兰塔将军[/red]拱卫在其间。他将武器指向了你。",
					new EventOptionLoc("FIGHT", "进入战斗", "遭遇一场特殊的战斗。"),
					new EventOptionLoc("FLEE", "快逃！", "太危险了。")),
				new EventPageLoc(
					"LEAVE",
					"这座营寨太过庞大，你花费了好长时间才到达它的边界。\n\n然而，你惊讶地发现，还有数个规模相当的营寨首尾相连，向着[aqua]北方[/aqua]不断延伸，正如一位[orange]独行的骑士[/orange]，在大地上留下一个个脚印。")),
			new EventLoc(
				"Endless Encampments",
				new EventPageLoc(
					InitialPage,
					"A vast [red]army[/red] seems to be stationed on the [aqua]icefield[/aqua], its encampments stretching beyond sight. Is it [orange]Ursus[/orange]? Higashi? Or Yan?\n\nPerhaps none of them. These camps match none of their styles. You may want to observe from closer range.",
					new EventOptionLoc("APPROACH", "Move closer", "Lose [red]8[/red] HP."),
					new EventOptionLoc("APPROACH_LOCKED", "Move closer", "Requires at least [red]9[/red] HP."),
					new EventOptionLoc("LEAVE", "Leave instead", "Too dangerous.")),
				new EventPageLoc(
					"APPROACH",
					"[red]Arrows[/red] fired from the encampment wound your group. Yet when you reach the camp, there is nothing inside.\n\nIt is like a [purple]phantom[/purple], a deception. Just as you prepare to leave, a clear [sine][gold]horn call[/gold][/sine] rings out from nowhere. From the empty phantom tents emerge [orange]Kuranta[/orange] in strange dress.\n\nThey quickly form ranks around a [red]Kuranta general[/red]. He points his weapon at you.",
					new EventOptionLoc("FIGHT", "Enter combat", "Encounter a special fight."),
					new EventOptionLoc("FLEE", "Run!", "Too dangerous.")),
				new EventPageLoc(
					"LEAVE",
					"The encampment is enormous. It takes you a long time to reach its edge.\n\nThen, to your surprise, you find several more camps of similar scale joined end to end, extending ever farther [aqua]north[/aqua], like an [orange]lone knight[/orange] leaving footprints across the land."))
		);
	}
}
