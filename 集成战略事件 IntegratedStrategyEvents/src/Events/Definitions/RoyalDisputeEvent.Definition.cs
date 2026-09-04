
namespace IntegratedStrategyEvents.Events;

public sealed partial class RoyalDisputeEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } = IntegratedStrategyEventDefinition.ForEventPortrait("royal_dispute.png", CreateLocalization);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"“皇家争执”",
				new EventPageLoc(
					InitialPage,
					"在一个装饰夸张的奇怪房间里，两个衣着华贵、头戴[gold]皇冠[/gold]的[orange]黎博利[/orange]正在[jitter][red]争执[/red][/jitter]。\n\n他们自称[orange]高卢[/orange]的第六位皇帝和第八位皇帝，两人正在讨论高卢的最后一位皇帝[orange]科西嘉一世[/orange]的功过。他们吵得不可开交，差一点要打起来了。",
					new EventOptionLoc("DENOUNCE", "怒斥科西嘉一世", "获得[blue]1[/blue]张随机其他颜色卡牌。"),
					new EventOptionLoc("PRAISE", "赞美科西嘉一世", "支付[red]40[/red][gold]金币[/gold]。获得一次其他颜色卡牌奖励。"),
					new EventOptionLoc("PRAISE_LOCKED", "赞美科西嘉一世", "需要[blue]40[/blue][gold]金币[/gold]。")),
				new EventPageLoc(
					"DENOUNCE",
					"你怒斥[orange]科西嘉一世[/orange]是导致高卢亡国的元凶，嘲笑他的固执与鲁莽。\n\n其中一位皇帝赞同你的观点，并且赏赐给你一些[gold]贵重的东西[/gold]。随后两人的身影就消散在[purple]迷雾[/purple]中。"),
				new EventPageLoc(
					"PRAISE",
					"你赞美[orange]科西嘉一世[/orange]，宣称他是高卢最伟大的皇帝之一，虽然兵败亡国，但是人非圣贤，孰能无过。\n\n两位皇帝都觉得你说的有道理，于是向你赠送了大量[gold]宝贝[/gold]。随后两人的身影就消散在[purple]迷雾[/purple]中。")),
			new EventLoc(
				"\"Royal Dispute\"",
				new EventPageLoc(
					InitialPage,
					"In a strangely overdecorated room, two finely dressed [orange]Liberi[/orange] wearing [gold]crowns[/gold] are [jitter][red]arguing[/red][/jitter].\n\nThey call themselves the sixth and eighth emperors of [orange]Gaul[/orange], and they are debating the merits and faults of [orange]Corsica I[/orange], the last emperor of Gaul. Their quarrel grows so heated that they nearly come to blows.",
					new EventOptionLoc("DENOUNCE", "Denounce Corsica I", "Gain [blue]1[/blue] random off-color card."),
					new EventOptionLoc("PRAISE", "Praise Corsica I", "Pay [red]40[/red] [gold]Gold[/gold]. Gain an off-color card reward."),
					new EventOptionLoc("PRAISE_LOCKED", "Praise Corsica I", "Requires [blue]40[/blue] [gold]Gold[/gold].")),
				new EventPageLoc(
					"DENOUNCE",
					"You denounce [orange]Corsica I[/orange] as the culprit behind Gaul's fall, mocking his stubbornness and recklessness.\n\nOne emperor agrees with your view and rewards you with something [gold]valuable[/gold]. Then both figures dissolve into [purple]mist[/purple]."),
				new EventPageLoc(
					"PRAISE",
					"You praise [orange]Corsica I[/orange], declaring him one of Gaul's greatest emperors. Though he lost the war and the nation, no saint is without fault.\n\nBoth emperors find your words reasonable, so they present you with many [gold]treasures[/gold]. Then both figures dissolve into [purple]mist[/purple]."))
		);
	}
}
