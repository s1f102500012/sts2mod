
namespace IntegratedStrategyEvents.Events;

public sealed partial class BusinessEmpireEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"business_empire.png",
			CreateLocalization,
			IntegratedStrategyEventLayoutProfile.StandardMediumNarrowSlightlyShiftedRight);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"商业帝国？",
				new EventPageLoc(
					InitialPage,
					"[orange]鸭爵[/orange]带着他的新跟班出现在了你的面前。他希望你能从这里离开，原因也很简单——他的[orange]“商业伙伴”[/orange]饱受你的[jitter][red]困扰[/red][/jitter]，你[red]妨碍[/red]到他[gold]赚钱[/gold]了。",
					new EventOptionLoc("CHALLENGE", "凭什么？", "你还妨碍到我走路叻。"),
					new EventOptionLoc("LEAVE", "也不是不行", "多一事不如少一事。")),
				new EventPageLoc(
					"CHALLENGE",
					"这个[orange]鸭子[/orange]真是[jitter][red]得寸进尺[/red][/jitter]，给他点教训。",
					new EventOptionLoc("FIGHT", "进入战斗", "遭遇一场特殊的战斗。")),
				new EventPageLoc(
					"LEAVE",
					"见你答应了他的要求，[orange]鸭爵[/orange]带着他的跟班[sine][gold]大摇大摆[/gold][/sine]地离开了。")),
			new EventLoc(
				"Business Empire?",
				new EventPageLoc(
					InitialPage,
					"[orange]Duck Lord[/orange] appears before you with his new followers. He wants you to leave this place, and the reason is simple: his [orange]\"business partners\"[/orange] have suffered from your [jitter][red]interference[/red][/jitter], and you are [red]getting in the way[/red] of his [gold]profits[/gold].",
					new EventOptionLoc("CHALLENGE", "On what grounds?", "You're getting in my way, too."),
					new EventOptionLoc("LEAVE", "Not impossible", "Avoid unnecessary trouble.")),
				new EventPageLoc(
					"CHALLENGE",
					"This [orange]duck[/orange] is getting [jitter][red]far too greedy[/red][/jitter]. Teach him a lesson.",
					new EventOptionLoc("FIGHT", "Enter combat", "Encounter a special fight.")),
				new EventPageLoc(
					"LEAVE",
					"Seeing you agree to his request, [orange]Duck Lord[/orange] leaves with his followers in [sine][gold]grand swagger[/gold][/sine]."))
		);
	}
}
