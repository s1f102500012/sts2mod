
namespace IntegratedStrategyEvents.Events;

public sealed partial class AfterStoryEndsEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"after_story_ends.png",
			CreateLocalization,
			Layout: IntegratedStrategyEventLayoutProfile.LeftWide,
			AlignHoverTipsRight: true);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"在故事结束之后",
				new EventPageLoc(
					InitialPage,
					"[red]温迪戈战士[/red][jitter][red]奋战至死[/red][/jitter]，带着他的[gold]梦想与希望[/gold]逝去，只留下一具躯壳。\n\n属于他的[sine][purple]故事[/purple][/sine]已经画上句号，而你则有机会见证他的[gold]结局[/gold]。\n\n你看到——",
					new EventOptionLoc("CRUMBLE_TO_ASH", "战士的躯壳崩解成灰", "从你的[gold]牌组[/gold]中选择[blue]2[/blue]张牌[gold]升级[/gold]，获得[purple]腐朽[/purple]。"),
					new EventOptionLoc("CRUMBLE_TO_ASH_LOCKED", "战士的躯壳崩解成灰", "没有可升级的牌。"),
					new EventOptionLoc("ROOT_AND_SPROUT", "战士的长角生根发芽", "获得[green]12[/green]点最大生命，随机获得[blue]2[/blue]张牌。"),
					new EventOptionLoc("ETERNAL_STILLNESS", "时光的永恒在此定格", "这些都不是我要去的地方。")),
				new EventPageLoc(
					"CRUMBLE_TO_ASH",
					"大多数[purple]萨卡兹[/purple]都是[red]感染者[/red]，而感染者的结局自不必说。\n\n清风吹来，带走了[sine][blue]晶尘[/blue][/sine]。\n\n这便是他的[gold]遗愿[/gold]，他想要[orange]回家[/orange]。"),
				new EventPageLoc(
					"ROOT_AND_SPROUT",
					"他的尸骸似乎并不愿意接受[red]死亡[/red]的命运。没过多久，[red]温迪戈[/red]的角便开始[sine][green]生根发芽[/green][/sine]，不断生长。\n\n有一小段[green]嫩枝[/green]甚至缠绕上了你的手指，将他的[gold]遗愿[/gold]传达给了你：他的死亡哺育了一个[gold]新的故事[/gold]，他衷心希望那[green]新生命[/green]能够茁壮成长。"),
				new EventPageLoc(
					"ETERNAL_STILLNESS",
					"羽兽不飞翔，风儿不吹动。你等待了许久，也没能见到这一[sine][purple]故事的后续[/purple][/sine]。\n\n直至你离去，直至[red]大地崩裂[/red]，直至[jitter][red]太阳湮灭[/red][/jitter]，这里都不会再有任何改变。")),
			new EventLoc(
				"After the Story Ends",
				new EventPageLoc(
					InitialPage,
					"The [red]Wendigo warrior[/red] [jitter][red]fought to the death[/red][/jitter], departing with his [gold]dreams and hopes[/gold], leaving only a shell behind.\n\nHis [sine][purple]story[/purple][/sine] has reached its final sentence, and you have the chance to witness his [gold]ending[/gold].\n\nYou see...",
					new EventOptionLoc("CRUMBLE_TO_ASH", "The warrior's shell crumbles to ash", "Choose [blue]2[/blue] cards from your [gold]deck[/gold] to [gold]Upgrade[/gold]. Gain [purple]Decay[/purple]."),
					new EventOptionLoc("CRUMBLE_TO_ASH_LOCKED", "The warrior's shell crumbles to ash", "No upgradable cards."),
					new EventOptionLoc("ROOT_AND_SPROUT", "The warrior's long horns take root", "Gain [green]12[/green] Max HP. Gain [blue]2[/blue] random cards."),
					new EventOptionLoc("ETERNAL_STILLNESS", "Time's eternity freezes here", "None of these are where I should go.")),
				new EventPageLoc(
					"CRUMBLE_TO_ASH",
					"Most [purple]Sarkaz[/purple] are [red]Infected[/red], and the ending of the Infected needs no explanation.\n\nA clear wind blows through, carrying away the [sine][blue]crystal dust[/blue][/sine].\n\nThis was his [gold]last wish[/gold]: he wanted to [orange]go home[/orange]."),
				new EventPageLoc(
					"ROOT_AND_SPROUT",
					"His remains seem unwilling to accept the fate of [red]death[/red]. Before long, the [red]Wendigo[/red]'s horns begin to [sine][green]root and sprout[/green][/sine], growing without pause.\n\nA small [green]young branch[/green] even winds around your finger, conveying his [gold]last wish[/gold]: his death has nourished a [gold]new story[/gold], and he sincerely hopes that [green]new life[/green] will grow strong."),
				new EventPageLoc(
					"ETERNAL_STILLNESS",
					"Birdbeasts do not fly. The wind does not move. You wait for a long time, yet never see the [sine][purple]story continue[/purple][/sine].\n\nUntil you leave, until the [red]land splits apart[/red], until the [jitter][red]sun is extinguished[/red][/jitter], nothing here will ever change."))
		);
	}
}
