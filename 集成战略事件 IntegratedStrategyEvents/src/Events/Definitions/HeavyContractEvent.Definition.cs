namespace IntegratedStrategyEvents.Events;

public sealed partial class HeavyContractEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"heavy_contract.png",
			CreateLocalization,
			IntegratedStrategyEventLayoutProfile.StandardNarrowShiftedRight);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"沉重的契约",
				new EventPageLoc(
					InitialPage,
					"“[sine]一百三十一，一百三十二，一百三十三......[/sine]”一只兔子站在[gold]石磨[/gold]旁，而推动磨盘的，是一只浑身长满[green]青苔[/green]的树懒。\n\n“我们有[gold]契约[/gold]在先，这偷懒的家伙要给我转[blue]五百圈[/blue]。”兔子说，“在转完之前我会一直监督它。”\n\n你看了眼空空如也的磨盘，显然它们被一个[sine][purple]荒诞的约定[/purple][/sine]困在了这里。",
					new EventOptionLoc("HELP", "上去帮个小忙", "从你的[gold]牌组[/gold]中选择[blue]1[/blue]张牌移除。"),
					new EventOptionLoc("HELP_LOCKED", "上去帮个小忙", "需要至少[blue]1[/blue]张可移除的牌。"),
					new EventOptionLoc("OVERTURN", "推翻石磨", "失去[red]12[/red]点生命。从你的[gold]牌组[/gold]中选择[blue]2[/blue]张牌移除。"),
					new EventOptionLoc("OVERTURN_LOCKED_CARDS", "推翻石磨", "需要至少[blue]2[/blue]张可移除的牌。"),
					new EventOptionLoc("OVERTURN_LOCKED_HP", "推翻石磨", "需要至少[red]13[/red]点生命。"),
					new EventOptionLoc("LEAVE", "不关我的事", "多一事不如少一事。")),
				new EventPageLoc(
					"HELP",
					"你接过了树懒手里的推杆，它[sine]慢腾腾[/sine]地趴到了地上。石磨比你想象中还要[jitter][red]沉重[/red][/jitter]，推动它费了你不少力气，即便如此你也比树懒快上不少。\n\n[gold]契约完成了[/gold]，你看到树懒抬起手，长长的指甲指向了一个方向。"),
				new EventPageLoc(
					"OVERTURN",
					"你决定终止这个[sine][purple]荒谬的契约[/purple][/sine]，使出全身的力气[jitter][red]推翻了石磨[/red][/jitter]。兔子和树懒摔倒在地，磨盘滚向了树林深处。\n\n你沿着磨盘碾出的痕迹，竟然找到了一条[gold]捷径[/gold]。"),
				new EventPageLoc(
					"LEAVE",
					"你没有再理会[jitter][red]跳脚的兔子[/red][/jitter]和可怜的树懒，转身离开了。")),
			new EventLoc(
				"A Heavy Contract",
				new EventPageLoc(
					InitialPage,
					"“[sine]One hundred thirty-one, one hundred thirty-two, one hundred thirty-three...[/sine]” A rabbit stands beside a [gold]stone mill[/gold], while the one pushing its wheel is a sloth covered head to toe in [green]moss[/green].\n\n“We had a [gold]contract[/gold]. This lazy fellow owes me [blue]five hundred turns[/blue],” says the rabbit. “I will keep watch until every turn is finished.”\n\nYou glance at the utterly empty mill. An [sine][purple]absurd agreement[/purple][/sine] has clearly trapped them here.",
					new EventOptionLoc("HELP", "Lend a hand", "Choose [blue]1[/blue] card from your [gold]deck[/gold] to remove."),
					new EventOptionLoc("HELP_LOCKED", "Lend a hand", "Requires at least [blue]1[/blue] removable card."),
					new EventOptionLoc("OVERTURN", "Overturn the stone mill", "Lose [red]12[/red] HP. Choose [blue]2[/blue] cards from your [gold]deck[/gold] to remove."),
					new EventOptionLoc("OVERTURN_LOCKED_CARDS", "Overturn the stone mill", "Requires at least [blue]2[/blue] removable cards."),
					new EventOptionLoc("OVERTURN_LOCKED_HP", "Overturn the stone mill", "Requires at least [red]13[/red] HP."),
					new EventOptionLoc("LEAVE", "None of my business", "Better not to invite trouble.")),
				new EventPageLoc(
					"HELP",
					"You take the push bar from the sloth's hands, and it [sine]slowly[/sine] lowers itself to the ground. The stone mill is [jitter][red]heavier[/red][/jitter] than you imagined, and moving it takes considerable effort. Even so, you are much faster than the sloth.\n\nThe [gold]contract is fulfilled[/gold]. You see the sloth raise one hand, its long claws pointing in a direction."),
				new EventPageLoc(
					"OVERTURN",
					"You decide to end this [sine][purple]absurd contract[/purple][/sine] and use all your strength to [jitter][red]overturn the stone mill[/red][/jitter]. The rabbit and the sloth tumble to the ground, and the millstone rolls deep into the woods.\n\nFollowing the trail crushed by the millstone, you unexpectedly find a [gold]shortcut[/gold]."),
				new EventPageLoc(
					"LEAVE",
					"You pay no more attention to the [jitter][red]furious rabbit[/red][/jitter] or the pitiful sloth, and turn away."))
		);
	}
}
