
namespace IntegratedStrategyEvents.Events;

public sealed partial class DesperateChoiceEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"desperate_choice.png",
			CreateLocalization,
			IntegratedStrategyEventLayoutProfile.LeftMediumSlightlyRaisedForFourOptions);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"绝境？抉择？",
				new EventPageLoc(
					InitialPage,
					"[sine][red]黑色行列[/red][/sine]在大地上蠕动，[purple]萨卡兹[/purple]们艰难地寻找着出路。[red]魔族[/red]只剩下最后一支[red]血脉[/red]，究竟向哪里走，才能获得[gold]一线生机[/gold]？\n\n你向智者们提问，于是[green]女妖[/green]取出骨笔，[purple]巫妖[/purple][sine]洞穿空间[/sine]，[red]独眼巨人[/red]睁开[jitter][red]只眼[/red][/jitter]。\n\n半晌之后，你决定——",
					new EventOptionLoc("FOLLOW_BANSHEE", "跟随女妖的笛声", "我们会去向何方？"),
					new EventOptionLoc("FOLLOW_LICH", "跟随巫妖的智慧", "失去[red]10[/red]点生命。"),
					new EventOptionLoc("FOLLOW_LICH_LOCKED", "跟随巫妖的智慧", "需要至少[red]11[/red]点生命。"),
					new EventOptionLoc("FOLLOW_CYCLOPS", "跟随独眼巨人的预示", "回复[green]10[/green]点生命。"),
					new EventOptionLoc("FOLLOW_HEART", "跟随内心的选择", "离开。")),
				new EventPageLoc(
					"BANSHEE",
					"随着[sine][aqua]悠扬笛声[/aqua][/sine]，行列再次向前行进，走过河谷，走进洞窟，深入地底。\n\n如果阳光下的地盘不能让[purple]萨卡兹[/purple]发展，那就让这[red]黑暗之地[/red]，成为萨卡兹的[gold]家园[/gold]。",
					new EventOptionLoc("HOME_GIFT", "来自家园的馈赠", "获得一件随机[blue]罕见[/blue][gold]遗物[/gold]。")),
				new EventPageLoc(
					"LICH",
					"[purple]巫妖[/purple]献出[red][jitter]命结[/jitter][/red]，穿过每一个族人。当族群成为个体，敌人们便很难彻底杀死它。\n\n在这个叫做[purple]萨卡兹[/purple]的个体损耗殆尽前，它遇到了海中的[aqua]朋友[/aqua]，并最终成为了那个[green]大家庭[/green]的一部分。",
					new EventOptionLoc("COLLECTIVE_GIFT", "来自众我的馈赠", "从你的牌组中选择[blue]1[/blue]张牌移除。")),
				new EventPageLoc(
					"CYCLOPS",
					"[purple]萨卡兹[/purple]们跟着[red]独眼巨人[/red]顺利在萨米定居，与当地土著一起抵抗北方的[jitter][red]邪魔灾异[/red][/jitter]。\n\n巨人的眼见到了无数[gold]未来[/gold]与[red]惨剧[/red]。为了让萨卡兹们不再受到邪魔侵扰，巨人们离开人群，直视灾异，用那[sine][purple]腐化的眼[/purple][/sine]，看尽邪魔的终末。",
					new EventOptionLoc("ALLIES_GIFT", "来自盟友的馈赠", "获得一次卡牌奖励。")),
				new EventPageLoc(
					"WANDER",
					"你没能从[aqua]智者们[/aqua]的回答中寻找到答案，既然无人能够指明[gold]未来[/gold]，那就继续前进吧，至少，眼前的路仍清晰可见。\n\n于是[purple]萨卡兹[/purple]们继续在大地上[red]流浪[/red]。一个建起[gold]卡兹戴尔[/gold]的梦，也不知何时能够完满。")),
			new EventLoc(
				"Desperation? Choice?",
				new EventPageLoc(
					InitialPage,
					"A [sine][red]black procession[/red][/sine] crawls across the land. The [purple]Sarkaz[/purple] search painfully for a way out. The [red]demons[/red] have only one final [red]bloodline[/red] left. Where can they go to find a [gold]sliver of life[/gold]?\n\nYou ask the wise ones. A [green]Banshee[/green] takes up a bone pen, a [purple]Lich[/purple] [sine]pierces space[/sine], and a [red]Cyclops[/red] opens her [jitter][red]single eye[/red][/jitter].\n\nAfter a long silence, you decide...",
					new EventOptionLoc("FOLLOW_BANSHEE", "Follow the Banshee's flute", "Where will we go?"),
					new EventOptionLoc("FOLLOW_LICH", "Follow the Lich's wisdom", "Lose [red]10[/red] HP."),
					new EventOptionLoc("FOLLOW_LICH_LOCKED", "Follow the Lich's wisdom", "Requires at least [red]11[/red] HP."),
					new EventOptionLoc("FOLLOW_CYCLOPS", "Follow the Cyclops's omen", "Heal [green]10[/green] HP."),
					new EventOptionLoc("FOLLOW_HEART", "Follow your heart", "Leave.")),
				new EventPageLoc(
					"BANSHEE",
					"With the [sine][aqua]long flute song[/aqua][/sine], the procession moves again, passing through river valleys, into caverns, and deeper underground.\n\nIf the lands beneath the sun cannot let the [purple]Sarkaz[/purple] grow, then this [red]dark place[/red] shall become their [gold]home[/gold].",
					new EventOptionLoc("HOME_GIFT", "A gift from home", "Gain a random [blue]Uncommon[/blue] [gold]Relic[/gold].")),
				new EventPageLoc(
					"LICH",
					"The [purple]Lich[/purple] offers up a [red][jitter]life knot[/jitter][/red], threading it through every clansperson. When a people becomes an individual, its enemies can no longer kill it completely.\n\nBefore the individual called [purple]Sarkaz[/purple] is worn away, it meets [aqua]friends[/aqua] in the sea and finally becomes part of that [green]great family[/green].",
					new EventOptionLoc("COLLECTIVE_GIFT", "A gift from the many selves", "Choose [blue]1[/blue] card from your deck to remove.")),
				new EventPageLoc(
					"CYCLOPS",
					"The [purple]Sarkaz[/purple] follow the [red]Cyclops[/red] and settle in Sami, resisting the northern [jitter][red]evil disasters[/red][/jitter] beside the local people.\n\nThe giant's eye sees countless [gold]futures[/gold] and [red]tragedies[/red]. So that the Sarkaz will no longer be harried by those evils, the giants leave the crowd, stare into calamity, and with that [sine][purple]corrupted eye[/purple][/sine], see the end of evil itself.",
					new EventOptionLoc("ALLIES_GIFT", "A gift from allies", "Gain a card reward.")),
				new EventPageLoc(
					"WANDER",
					"You fail to find an answer in the words of the [aqua]wise ones[/aqua]. If no one can point to the [gold]future[/gold], then you must keep going. At least the road before you is still clear.\n\nAnd so the [purple]Sarkaz[/purple] continue to [red]wander[/red] across the land. As for the dream of building [gold]Kazdel[/gold], who knows when it will ever be fulfilled?"))
		);
	}
}
