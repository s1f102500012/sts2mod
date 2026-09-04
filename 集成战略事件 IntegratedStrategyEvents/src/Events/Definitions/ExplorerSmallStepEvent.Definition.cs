
namespace IntegratedStrategyEvents.Events;

public sealed partial class ExplorerSmallStepEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"explorer_small_step.png",
			CreateLocalization,
			IntegratedStrategyEventLayoutProfile.StandardNarrow);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"探索者的一小步",
				new EventPageLoc(
					InitialPage,
					"前一秒，你仍身在[aqua]冰原深处[/aqua]，下一秒，你就进入了[orange]炽热的荒漠[/orange]。在你背后，是一个与冰原的[gold]巨构[/gold]相同，但几乎已经被[orange]风沙[/orange]侵蚀殆尽的残破巨构。考虑到前方环境十分恶劣，你立刻向门的另一端发出[sine][aqua]求援讯息[/aqua][/sine]。",
					new EventOptionLoc("WAIT_RESPONSE", "等待回应", "小心为上。")),
				new EventPageLoc(
					ResponsePage,
					"等待了一段时间后，一支小队带着[gold]构建阵地[/gold]的材料从门内出现。",
					new EventOptionLoc("ADVANCE", "好了，让我们前进吧", "回复所有[green]生命值[/green]。")),
				new EventPageLoc(
					SurvivalPage,
					"似乎是因为年久失修，支援到来后[gold]巨构[/gold]便停止了工作。你和队员们商议决定先就地探索一下周边，在巨构[jitter][gold]重启[/gold][/jitter]后立刻返回。[orange]高温[/orange]让刚从[aqua]极地[/aqua]来的你们感到极度不适，但这不是最糟糕的。前方的黄沙泛起[gold]金光[/gold]，金属反射出的光线不断闪烁，后方响起[sine][orange]若有若无的号角声[/orange][/sine]，[red]恐惧[/red]与[gold]臣服[/gold]的威压如浪潮般袭来。你突然明白过来，现在的当务之急不是探索，而是在一场[jitter][red]绝无胜算的战争[/red][/jitter]中幸存下来。")),
			new EventLoc(
				"A Small Step for an Explorer",
				new EventPageLoc(
					InitialPage,
					"One moment, you are still deep within the [aqua]icefield[/aqua]. The next, you have stepped into a [orange]scorching desert[/orange]. Behind you stands a ruined [gold]megastructure[/gold], identical to the one on the icefield but nearly consumed by [orange]wind and sand[/orange]. With the environment ahead so hostile, you immediately send a [sine][aqua]request for aid[/aqua][/sine] through the other side of the gate.",
					new EventOptionLoc("WAIT_RESPONSE", "Wait for a response", "Caution comes first.")),
				new EventPageLoc(
					ResponsePage,
					"After some time, a squad emerges from the gate carrying materials to [gold]establish a position[/gold].",
					new EventOptionLoc("ADVANCE", "All right. Let's move out", "Heal to full [green]HP[/green].")),
				new EventPageLoc(
					SurvivalPage,
					"Perhaps from its long years of disrepair, the [gold]megastructure[/gold] stops functioning as soon as support arrives. You and your team agree to explore the surroundings for now and return the instant the megastructure [jitter][gold]restarts[/gold][/jitter]. The [orange]heat[/orange] is brutal for people who just came from the [aqua]polar wastes[/aqua], but that is not the worst of it. Ahead, the yellow sand glimmers with [gold]golden light[/gold], metal reflections flash again and again, and behind you comes the [sine][orange]faint call of horns[/orange][/sine]. A tide of [red]fear[/red] and [gold]submission[/gold] rolls over you. You suddenly understand that the priority now is not exploration, but surviving a [jitter][red]war that cannot be won[/red][/jitter]."))
		);
	}
}
