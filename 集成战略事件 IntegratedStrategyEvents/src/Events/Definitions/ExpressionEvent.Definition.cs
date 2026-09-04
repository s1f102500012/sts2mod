
namespace IntegratedStrategyEvents.Events;

public sealed partial class ExpressionEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"expression.png",
			CreateLocalization,
			IntegratedStrategyEventLayoutProfile.StandardNarrowSlightlyShiftedRight,
			AlignHoverTipsRight: true);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"表达欲",
				new EventPageLoc(
					InitialPage,
					"死魂灵突然就出现在了你的[gold]故事[/gold]里，他们[jitter][red]横冲直撞[/red][/jitter]，把[aqua]天空[/aqua]撕下一片贴在大地上，又原地建起一座大[gold]“门”[/gold]，无数[sine][gold]“门”[/gold][/sine]喷涌而出，将生物传去各个[pink]时间[/pink]。\n\n再这样下去，不等[orange]先祖们[/orange]闹完，你就得先被他们[jitter][red]逼疯[/red][/jitter]。",
					new EventOptionLoc("ENDURE", "再忍一忍", "获得一件随机[gold]遗物[/gold]。"),
					new EventOptionLoc("RESIST", "奋起反抗", "从你的[gold]牌组[/gold]中选择[blue]2[/blue]张牌移除。获得[gold]滚动先祖[/gold]。"),
					new EventOptionLoc("RESIST_LOCKED", "奋起反抗", "没有足够可移除的牌。"),
					new EventOptionLoc("NEGOTIATE", "让锡人交涉", "离开。")),
				new EventPageLoc(
					"ENDURE",
					"在你的退让下，这个[gold]故事[/gold]很快被撕扯成了[purple]虚无[/purple]，再也没有东西可以毁坏的时候，死魂灵们才心满意足地消失了，只给你留下一块[orange]故事的残骸[/orange]。\n\n你叹了口气，捡起这块残骸，开始想象新的[gold]故事[/gold]。"),
				new EventPageLoc(
					"RESIST",
					"在[orange]锡人[/orange]的帮助下，你耗费了一些构想将这个[gold]故事[/gold]化作囚笼。死魂灵们胡闹完正准备离开，才发现自己被困在了[sine][purple]死循环[/purple][/sine]中。\n\n你将这个满载[orange]先祖[/orange]的圆球放入了行囊，自认为解决了他们惹的麻烦，而他们也在不断的滚动中等待时机，为下一次肆意[jitter][red]“表达”[/red][/jitter]做准备。"),
				new EventPageLoc(
					"NEGOTIATE",
					"在[orange]锡人[/orange]的交涉下，死魂灵们很快就离开了。")),
			new EventLoc(
				"Urge to Express",
				new EventPageLoc(
					InitialPage,
					"Restless souls suddenly appear in your [gold]story[/gold]. They [jitter][red]rampage[/red][/jitter], tear a piece of [aqua]sky[/aqua] down onto the earth, then build a great [gold]\"door\"[/gold] where they stand. Countless [sine][gold]\"doors\"[/gold][/sine] gush outward, sending living things to every [pink]time[/pink].\n\nIf this continues, you will lose your mind before the [orange]ancestors[/orange] finish their riot.",
					new EventOptionLoc("ENDURE", "Endure a little longer", "Gain a random [gold]Relic[/gold]."),
					new EventOptionLoc("RESIST", "Fight back", "Choose [blue]2[/blue] cards from your deck to remove. Gain [gold]Rolling Ancestors[/gold]."),
					new EventOptionLoc("RESIST_LOCKED", "Fight back", "Not enough removable cards."),
					new EventOptionLoc("NEGOTIATE", "Let the Tin Man negotiate", "Leave.")),
				new EventPageLoc(
					"ENDURE",
					"With your concession, this [gold]story[/gold] is soon torn into [purple]nothingness[/purple]. When nothing remains to destroy, the restless souls vanish in satisfaction, leaving only a [orange]shard of story[/orange] behind.\n\nYou sigh, pick up the shard, and begin imagining a new [gold]story[/gold]."),
				new EventPageLoc(
					"RESIST",
					"With the [orange]Tin Man[/orange]'s help, you spend some ideas and turn this [gold]story[/gold] into a cage. The restless souls finish their mischief and prepare to leave, only to find themselves trapped in a [sine][purple]dead loop[/purple][/sine].\n\nYou place the sphere full of [orange]ancestors[/orange] into your pack, believing their trouble solved, while they roll on and wait for another chance to [jitter][red]\"express\"[/red][/jitter] themselves."),
				new EventPageLoc(
					"NEGOTIATE",
					"After negotiating with the [orange]Tin Man[/orange], the restless souls soon leave."))
		);
	}
}
