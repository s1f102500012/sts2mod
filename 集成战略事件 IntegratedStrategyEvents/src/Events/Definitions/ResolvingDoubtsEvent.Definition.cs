
namespace IntegratedStrategyEvents.Events;

public sealed partial class ResolvingDoubtsEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"resolving_doubts.png",
			CreateLocalization,
			IntegratedStrategyEventLayoutProfile.StandardNarrowSlightlyShiftedRight);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"解惑",
				new EventPageLoc(
					InitialPage,
					"年轻的[purple]巫妖[/purple]听见你走近，显得[gold]非常激动[/gold]。\n\n“终于找到一个[b]活人[/b]了！”他急切地询问，“你认识字吗？帮我看看这本[gold]书[/gold]的结局到底是什么。”\n\n你接过书翻到最后一页：一名[red]萨卡兹猎人[/red]终于追上了他的猎物，一名[orange]萨科塔[/orange]。他们的结局是——",
					new EventOptionLoc("SANKTA_WINS", "萨科塔杀死了萨卡兹", "获得[blue]60[/blue][gold]金币[/gold]。"),
					new EventOptionLoc("SARKAZ_WINS", "萨卡兹埋葬了萨科塔", "获得[blue]1[/blue]瓶随机[gold]稀有药水[/gold]。"),
					new EventOptionLoc("HALOS", "他们的头顶都生出了光环", "获得一次四选一卡牌奖励。")),
				new EventPageLoc(
					"SANKTA_WINS",
					"“[sine][green]符合常理[/green][/sine]......”\n\n你这才发现，巫妖的双眼一片[aqua]灰白[/aqua]。他不好意思地解释道，为了解决老师布置的问题，他只好一直躲在这里查找资料。\n\n现在，他终于可以[sine][green]回去[/green][/sine]了。"),
				new EventPageLoc(
					"SARKAZ_WINS",
					"“[sine][purple]原来如此[/purple][/sine]......”\n\n你这才发现，巫妖的双眼一片[aqua]灰白[/aqua]。他不好意思地解释道，为了解决老师布置的问题，他只好一直躲在这里查找资料。\n\n现在，他终于可以[sine][green]回去[/green][/sine]了。"),
				new EventPageLoc(
					"HALOS",
					"“[jitter][red]怎么可能是这样？[/red][/jitter]”巫妖惊叫出声，但他随即又陷入沉思。也许这正是老师想让他探寻的答案？\n\n你发现巫妖的双眼[aqua]灰白[/aqua]，他的脑后轻微地闪烁着[sine][gold]奇异的光芒[/gold][/sine]。难道他将故事[purple]当真[/purple]了？")),
			new EventLoc(
				"Resolving Doubts",
				new EventPageLoc(
					InitialPage,
					"A young [purple]lich[/purple] grows [gold]excited[/gold] when he hears you approach.\n\n\"At last, a [b]living person[/b]!\" he asks urgently. \"Can you read? Help me find out how this [gold]book[/gold] ends.\"\n\nYou take the book and turn to the final page. A [red]Sarkaz hunter[/red] has finally caught up to his prey, an [orange]Sankta[/orange]. Their ending is...",
					new EventOptionLoc("SANKTA_WINS", "The Sankta kills the Sarkaz", "Gain [blue]60[/blue] [gold]Gold[/gold]."),
					new EventOptionLoc("SARKAZ_WINS", "The Sarkaz buries the Sankta", "Gain [blue]1[/blue] random [gold]Rare Potion[/gold]."),
					new EventOptionLoc("HALOS", "Halos grow above both their heads", "Gain a card reward with four choices.")),
				new EventPageLoc(
					"SANKTA_WINS",
					"\"[sine][green]That is reasonable[/green][/sine]...\"\n\nOnly now do you notice that the lich's eyes are [aqua]ashen white[/aqua]. Embarrassed, he explains that he hid here searching through materials to solve a question assigned by his teacher.\n\nNow, he can finally [sine][green]go back[/green][/sine]."),
				new EventPageLoc(
					"SARKAZ_WINS",
					"\"[sine][purple]I see[/purple][/sine]...\"\n\nOnly now do you notice that the lich's eyes are [aqua]ashen white[/aqua]. Embarrassed, he explains that he hid here searching through materials to solve a question assigned by his teacher.\n\nNow, he can finally [sine][green]go back[/green][/sine]."),
				new EventPageLoc(
					"HALOS",
					"\"[jitter][red]How could that be possible?[/red][/jitter]\" the lich cries out, but immediately falls into thought. Perhaps this is exactly the answer his teacher wanted him to seek?\n\nYou notice that his eyes are [aqua]ashen white[/aqua], and that a [sine][gold]strange light[/gold][/sine] flickers faintly behind his head. Did he take the story [purple]as truth[/purple]?"))
		);
	}
}
