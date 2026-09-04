
namespace IntegratedStrategyEvents.Events;

public sealed partial class PathOfSufferingEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"path_of_suffering.png",
			CreateLocalization,
			IntegratedStrategyEventLayoutProfile.LeftVeryCompact);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"苦路",
				new EventPageLoc(
					InitialPage,
					"在[orange]审判庭[/orange]的授意下，一些[b]苦修仪式[/b]早已与[red]重体力劳作[/red]互相结合。每个完成仪式的人会得到[gold]奖赏[/gold]，但说实话......真的有人能通过这样的[orange]试炼[/orange]吗？",
					new EventOptionLoc("HAUL_FOOD", "从搬运食物开始", "失去[red]6[/red]点生命。获得[blue]1[/blue]瓶随机[gold]药水[/gold]。"),
					new EventOptionLoc("HAUL_FOOD_LOCKED", "从搬运食物开始", "需要至少[red]7[/red]点生命。"),
					new EventOptionLoc("LEAVE", "并没有这个需要", "就此离开。")),
				new EventPageLoc(
					"FOOD",
					"抱着一袋袋[green]食物[/green]来回走动，你觉得心底里扬起一股[green]幸福感[/green]。要继续吗？",
					new EventOptionLoc("FIELD_WORK", "田间农活", "失去[red]6[/red]点生命。获得[green]2[/green]点最大生命。"),
					new EventOptionLoc("FIELD_WORK_LOCKED", "田间农活", "需要至少[red]7[/red]点生命。"),
					new EventOptionLoc("EARLY_LEAVE", "提前离开", "还是算了。")),
				new EventPageLoc(
					"FIELD",
					"耕作是项十分[red]辛苦[/red]的运动，但你觉得自己似乎[green]强壮[/green]了一些。还要继续吗？",
					new EventOptionLoc("SWING_BELL", "摆动巨钟", "失去[red]6[/red]点生命。获得一次无色卡牌奖励。"),
					new EventOptionLoc("SWING_BELL_LOCKED", "摆动巨钟", "需要至少[red]7[/red]点生命。"),
					new EventOptionLoc("EARLY_LEAVE", "提前离开", "还是算了。")),
				new EventPageLoc(
					"BELL",
					"那口巨钟根本[red]无法以人力摇动[/red]。你借助了许多装置才堪堪让它发出声响，好消息是，你在路上捡到了一把[gold]钥匙[/gold]。真的还要继续吗？",
					new EventOptionLoc("WATCHTOWER", "将石料与燃料运上瞭望塔", "失去[red]6[/red]点生命。获得一件随机[gold]遗物[/gold]。"),
					new EventOptionLoc("WATCHTOWER_LOCKED", "将石料与燃料运上瞭望塔", "需要至少[red]7[/red]点生命。"),
					new EventOptionLoc("EARLY_LEAVE", "提前离开", "还是算了。")),
				new EventPageLoc(
					"COMPLETE",
					"审判官们将[gold]酬劳[/gold]交至你手中，教士们为你唱起[gold]赞歌[/gold]，远方的[aqua]瞭望塔[/aqua]照亮黑夜。\n\n虽然累得气喘吁吁，但至少在这一刻，你心中满是[green]希望与憧憬[/green]。"),
				new EventPageLoc(
					"LEAVE",
					"对自己的[green]体力[/green]没有信心，还是算了吧。")),
			new EventLoc(
				"Path of Suffering",
				new EventPageLoc(
					InitialPage,
					"Under the [orange]Inquisition's[/orange] direction, certain [b]penitent rites[/b] have long been joined with [red]heavy labor[/red]. Everyone who completes the rites receives a [gold]reward[/gold], but honestly... can anyone truly pass such an [orange]trial[/orange]?",
					new EventOptionLoc("HAUL_FOOD", "Start by hauling food", "Lose [red]6[/red] HP. Gain [blue]1[/blue] random [gold]Potion[/gold]."),
					new EventOptionLoc("HAUL_FOOD_LOCKED", "Start by hauling food", "Requires at least [red]7[/red] HP."),
					new EventOptionLoc("LEAVE", "No need for this", "Leave.")),
				new EventPageLoc(
					"FOOD",
					"Carrying sacks of [green]food[/green] back and forth, you feel a [green]sense of happiness[/green] rise in your heart. Continue?",
					new EventOptionLoc("FIELD_WORK", "Farm work", "Lose [red]6[/red] HP. Gain [green]2[/green] Max HP."),
					new EventOptionLoc("FIELD_WORK_LOCKED", "Farm work", "Requires at least [red]7[/red] HP."),
					new EventOptionLoc("EARLY_LEAVE", "Leave early", "Never mind.")),
				new EventPageLoc(
					"FIELD",
					"Farming is [red]exhausting[/red] labor, but you feel somewhat [green]stronger[/green]. Continue?",
					new EventOptionLoc("SWING_BELL", "Swing the great bell", "Lose [red]6[/red] HP. Gain a Colorless card reward."),
					new EventOptionLoc("SWING_BELL_LOCKED", "Swing the great bell", "Requires at least [red]7[/red] HP."),
					new EventOptionLoc("EARLY_LEAVE", "Leave early", "Never mind.")),
				new EventPageLoc(
					"BELL",
					"The great bell is [red]impossible to move by human strength alone[/red]. With many mechanisms, you barely manage to make it sound. The good news is that you found a [gold]key[/gold] on the way. Do you really continue?",
					new EventOptionLoc("WATCHTOWER", "Carry stone and fuel to the watchtower", "Lose [red]6[/red] HP. Gain a random [gold]Relic[/gold]."),
					new EventOptionLoc("WATCHTOWER_LOCKED", "Carry stone and fuel to the watchtower", "Requires at least [red]7[/red] HP."),
					new EventOptionLoc("EARLY_LEAVE", "Leave early", "Never mind.")),
				new EventPageLoc(
					"COMPLETE",
					"The inquisitors place your [gold]pay[/gold] in your hands, the clerics sing [gold]hymns[/gold] for you, and the distant [aqua]watchtower[/aqua] lights the night.\n\nThough you are panting from exhaustion, in this moment your heart is filled with [green]hope and longing[/green]."),
				new EventPageLoc(
					"LEAVE",
					"You do not trust your [green]stamina[/green]. Better to leave it be."))
		);
	}
}
