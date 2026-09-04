
namespace IntegratedStrategyEvents.Events;

public sealed partial class SuspicionChainEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"suspicion_chain.png",
			CreateLocalization,
			IntegratedStrategyEventLayoutProfile.StandardLowered);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"猜疑链",
				new EventPageLoc(
					InitialPage,
					"一个[orange]萨米部族[/orange]接纳了大量[orange]外乡人[/orange]。然而种族与国别的差异让他们相互之间猜忌重重，抱怨与[sine][purple]窃窃私语[/purple][/sine]汇成了一条暗流在部族里流淌。\n\n这里并没有[red]坍缩体[/red]出现，但最终，[purple]谎言的影子[/purple]击碎了人们的理智，整个部族里[jitter][red]乱作一团[/red][/jitter]。",
					new EventOptionLoc("MAINTAIN_ORDER", "维持秩序", "我们不能袖手旁观。"),
					new EventOptionLoc("AVOID_CONFLICT", "避免纷争", "多一事不如少一事。")),
				new EventPageLoc(
					"MAINTAIN_ORDER",
					"你和萨米部族的[orange]萨满[/orange]一起为尚有理智的人开辟了[green]避难区[/green]，然后开始着手应对[red]暴徒[/red]。",
					new EventOptionLoc("FIGHT", "进入战斗", "遭遇一场特殊的战斗。")),
				new EventPageLoc(
					"AVOID_CONFLICT",
					"你发现自己心头也涌上了一股[red]宣泄暴力[/red]的冲动，这绝对不合常理。于是你赶紧带着自己的队员离开了。")),
			new EventLoc(
				"Chain of Suspicion",
				new EventPageLoc(
					InitialPage,
					"An [orange]Sami tribe[/orange] has taken in many [orange]outsiders[/orange]. Yet differences of race and nation leave everyone deeply suspicious of one another. Complaints and [sine][purple]whispers[/purple][/sine] gather into an undercurrent flowing through the tribe.\n\nNo [red]collapsed entity[/red] appears here. In the end, however, the [purple]shadow of lies[/purple] shatters people's reason, and the whole tribe falls into [jitter][red]chaos[/red][/jitter].",
					new EventOptionLoc("MAINTAIN_ORDER", "Maintain order", "You cannot stand by."),
					new EventOptionLoc("AVOID_CONFLICT", "Avoid the conflict", "Better safe than sorry.")),
				new EventPageLoc(
					"MAINTAIN_ORDER",
					"You and the tribe's [orange]shaman[/orange] establish a [green]refuge[/green] for those who still have their senses. Then you begin dealing with the [red]rioters[/red].",
					new EventOptionLoc("FIGHT", "Enter combat", "Encounter a special fight.")),
				new EventPageLoc(
					"AVOID_CONFLICT",
					"You feel an urge to [red]unleash violence[/red] welling up in your own heart. That cannot be natural. You hurry away with your squad."))
		);
	}
}
