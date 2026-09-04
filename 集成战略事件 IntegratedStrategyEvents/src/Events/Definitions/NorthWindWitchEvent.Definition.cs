
namespace IntegratedStrategyEvents.Events;

public sealed partial class NorthWindWitchEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"north_wind_witch.png",
			CreateLocalization,
			IntegratedStrategyEventLayoutProfile.LeftVeryCompact);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"北风女巫",
				new EventPageLoc(
					InitialPage,
					"你无论如何也没想到，半个人影都见不到的[sine][aqua]高塔[/aqua][/sine]里会有一座[gold]小木屋[/gold]，屋子的主人会是一位通晓多国语言、礼貌得体的[purple]萨卡兹女士[/purple]。\n\n她邀请你进屋休息，还为你准备了[green]茶水和甜点[/green]。随后，她就带上篮子，冒着[aqua]风雪[/aqua]出门采摘植物。临走前，她嘱咐你就在屋子里好好休息，不要胡乱走动，也可以休息好后直接离开。\n\n总之，她似乎不希望你探查她藏在屋子里的[purple]秘密[/purple]。",
					new EventOptionLoc("TOUCH", "随意走动触摸", "简单转转不会有事的。"),
					new EventOptionLoc("LEAVE", "那就赶紧走吧", "小命要紧。")),
				new EventPageLoc(
					"TOUCH",
					"房子似乎被你的[jitter][red]无礼[/red][/jitter]激怒了。它分裂开来，变成了四根[red]食腐者枯枝[/red]，火炉旁休息的那些......哦不，那是[jitter][red]逐腐兽[/red][/jitter]？！\n\n这可怕的女人难道是什么[red]食腐者王庭[/red]里的重要人物吗？！不然哪有正常人会用这些东西[purple]建房子[/purple]！",
					new EventOptionLoc("FIGHT", "进入战斗", "遭遇一场艰难的战斗。")),
				new EventPageLoc(
					"LEAVE",
					"你听说过许多和[orange]萨米[/orange]有关的奇闻异事，听说一些[jitter][red]可怕的萨满[/red][/jitter]会把人骗进屋子中，然后让[red]活体房屋[/red]把人吃掉。\n\n你越想越觉得可怕。趁着这房子还没什么动静，女巫也还没对你做些什么，还是[aqua]赶紧离开[/aqua]比较好。")),
			new EventLoc(
				"Witch of the North Wind",
				new EventPageLoc(
					InitialPage,
					"You never expected to find a [gold]small cabin[/gold] on a [sine][aqua]frozen plain[/aqua][/sine] where not a single person should be seen, much less that its owner would be a polite, multilingual [purple]Sarkaz lady[/purple].\n\nShe invites you inside to rest and prepares [green]tea and sweets[/green]. Then she takes a basket and steps into the [aqua]snowstorm[/aqua] to gather plants. Before leaving, she tells you to rest properly, not wander around, and to leave directly once you are ready.\n\nIn short, she seems unwilling for you to uncover the [purple]secret[/purple] hidden in her home.",
					new EventOptionLoc("TOUCH", "Wander and touch things", "A quick look around should be fine."),
					new EventOptionLoc("LEAVE", "Leave right away", "Your life matters.")),
				new EventPageLoc(
					"TOUCH",
					"The house seems angered by your [jitter][red]rudeness[/red][/jitter]. It splits apart into four [red]scavenger branches[/red], and the things resting by the hearth... oh no, those are [jitter][red]corpse-feeders[/red][/jitter]?!\n\nIs this terrifying woman some important figure in the [red]Scavenger Court[/red]?! Otherwise, what normal person would use these things to [purple]build a house[/purple]?",
					new EventOptionLoc("FIGHT", "Enter combat", "Encounter a difficult fight.")),
				new EventPageLoc(
					"LEAVE",
					"You have heard many strange tales about [orange]Sami[/orange]. Some say [jitter][red]terrible shamans[/red][/jitter] lure people into cabins, then let [red]living houses[/red] devour them.\n\nThe more you think about it, the more frightening it becomes. Before the house moves, and before the witch does anything to you, it is better to [aqua]leave quickly[/aqua]."))
		);
	}
}
