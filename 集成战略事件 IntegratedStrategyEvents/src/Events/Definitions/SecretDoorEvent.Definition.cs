
namespace IntegratedStrategyEvents.Events;

public sealed partial class SecretDoorEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"secret_door.png",
			CreateLocalization,
			IntegratedStrategyEventLayoutProfile.StandardNarrowSlightlyShiftedRight);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"暗门",
				new EventPageLoc(
					InitialPage,
					"在一阵[jitter][red]误打误撞[/red][/jitter]中，你冲破了一面[aqua]隐藏的墙壁[/aqua]。墙壁背后巧妙地隐藏了很大的空间，房间的陈设彰显着使用者[sine][orange]独特的艺术品位[/orange][/sine]。\n\n墙面上挂满了各种你没有见过的[gold]古物[/gold]，而屋子正中央一块饱含历史气息的[gold]雕文石板[/gold]引起了你的注意。",
					new EventOptionLoc("TAKE_TABLET", "取下石板", "获得[gold]《坎德之花》[/gold]。"),
					new EventOptionLoc("STUDY_MECHANISM", "还是算了", "获得[green]10[/green]点最大生命。")),
				new EventPageLoc(
					"TAKE_TABLET",
					"你取下了那块[gold]石板[/gold]，发现石板背后写着一行字：\n\n“曾经的我因为[purple]愚盲之辈的恶言中伤[/purple]而[jitter][red]郁郁寡欢[/red][/jitter]，但是[sine][orange]艺术[/orange][/sine]又怎会因为[red]粗莽丑陋的外表[/red]而失去意义？”"),
				new EventPageLoc(
					"STUDY_MECHANISM",
					"这间屋子的[aqua]隐藏墙壁[/aqua]设计[gold]精巧[/gold]，似乎是[aqua]源石技艺[/aqua]与[orange]机械结构[/orange]的精妙结合，[sine][gold]令人称奇[/gold][/sine]。")),
			new EventLoc(
				"Secret Door",
				new EventPageLoc(
					InitialPage,
					"In a bout of [jitter][red]blind stumbling[/red][/jitter], you crash through a [aqua]hidden wall[/aqua]. Behind it lies a cleverly concealed space, its furnishings displaying the user's [sine][orange]distinctive artistic taste[/orange][/sine].\n\nThe walls are covered with [gold]antiquities[/gold] you have never seen, but a [gold]carved stone tablet[/gold] rich with history in the center of the room catches your attention.",
					new EventOptionLoc("TAKE_TABLET", "Take the tablet", "Gain [gold]The Flower of Cande[/gold]."),
					new EventOptionLoc("STUDY_MECHANISM", "Forget it", "Gain [green]10[/green] Max HP.")),
				new EventPageLoc(
					"TAKE_TABLET",
					"You take down the [gold]tablet[/gold] and find a line written on its back:\n\n\"Once, I languished under the [purple]slander of foolish people[/purple]. But how could [sine][orange]art[/orange][/sine] lose its meaning merely because its exterior is [red]rough and ugly[/red]?\""),
				new EventPageLoc(
					"STUDY_MECHANISM",
					"The room's [aqua]hidden wall[/aqua] is designed with [gold]remarkable precision[/gold], seemingly combining [aqua]Originium Arts[/aqua] with [orange]mechanical structure[/orange] in a [sine][gold]marvelous[/gold][/sine] way."))
		);
	}
}
