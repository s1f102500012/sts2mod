
namespace IntegratedStrategyEvents.Events;

public sealed partial class VoidPortentEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"void_portent.png",
			CreateLocalization,
			Layout: IntegratedStrategyEventLayoutProfile.LeftWide,
			AlignHoverTipsRight: true);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"空无前兆",
				new EventPageLoc(
					InitialPage,
					"[gold]妮芙[/gold]漫步在[purple]想象的边界[/purple]，寻找那可遇而不可求的[gold]灵感[/gold]。\n\n讲述者向[purple]死魂灵[/purple]们分享了许多[gold]故事[/gold]。然而，当现实的、改编的、臆想的已全部讲完，还能讲述些什么呢？\n\n[sine][purple]死魂灵[/purple][/sine]在妮芙身边游荡，不断翻检并改造着那些讲述者不愿谈及的想法，比如，一个关于[jitter][red]魔王[/red][/jitter]的[gold]预言[/gold]......",
					new EventOptionLoc("ACCEPT_IT", "试着接受它", "获得[gold]预言显像[/gold]。"),
					new EventOptionLoc("THINK_AGAIN", "还是再想想吧！", "获得[blue]1[/blue]件随机[gold]遗物[/gold]。")),
				new EventPageLoc(
					"ACCEPT_IT",
					"那是个关于[jitter][red]魔王[/red][/jitter]，关于[red]阿米娅[/red]的[gold]故事[/gold]，而且绝对不存在理想的结局。\n\n可是，可是，在[purple]死魂灵[/purple]的催促下，[gold]妮芙[/gold]还是退让了。她说服自己去接受，去讲述这个能令先祖们“[sine][purple]心满意足[/purple][/sine]”的故事。\n\n可她还没有意识到，当自己让渡出[gold]故事的主导权[/gold]时，而后发生的一切，就不再受她[jitter][red]掌控[/red][/jitter]了。"),
				new EventPageLoc(
					"THINK_AGAIN",
					"[gold]妮芙[/gold]不愿去尝试讲述那[jitter][red]黑暗又绝望[/red][/jitter]的故事。\n\n她决定再独自思考一会。\n\n或许，当她想通些什么的时候，新的[gold]故事[/gold]便会就此成形吧。")),
			new EventLoc(
				"Void Portent",
				new EventPageLoc(
					InitialPage,
					"[gold]Nymph[/gold] strolls along the [purple]border of imagination[/purple], searching for that rare and elusive [gold]inspiration[/gold].\n\nThe storyteller has shared many [gold]stories[/gold] with the [purple]dead souls[/purple]. Yet once the real, the adapted, and the imagined have all been told, what remains to be told?\n\nThe [sine][purple]dead souls[/purple][/sine] drift around Nymph, rummaging through and reshaping the thoughts the storyteller refuses to speak of. For example, a [gold]prophecy[/gold] about a [jitter][red]Demon King[/red][/jitter]...",
					new EventOptionLoc("ACCEPT_IT", "Try to accept it", "Gain [gold]Prophecy Projection[/gold]."),
					new EventOptionLoc("THINK_AGAIN", "Think about it again!", "Gain [blue]1[/blue] random [gold]Relic[/gold].")),
				new EventPageLoc(
					"ACCEPT_IT",
					"It is a [gold]story[/gold] about the [jitter][red]Demon King[/red][/jitter], about [red]Amiya[/red], and it absolutely has no ideal ending.\n\nBut, but, urged on by the [purple]dead souls[/purple], [gold]Nymph[/gold] gives in. She persuades herself to accept it, to tell this story that will leave the ancestors \"[sine][purple]satisfied[/purple][/sine].\"\n\nShe has yet to realize that once she yields the [gold]authority of the story[/gold], everything that follows will no longer be within her [jitter][red]control[/red][/jitter]."),
				new EventPageLoc(
					"THINK_AGAIN",
					"[gold]Nymph[/gold] refuses to try telling that [jitter][red]dark and hopeless[/red][/jitter] story.\n\nShe decides to think alone for a while longer.\n\nPerhaps, when she understands something, a new [gold]story[/gold] will take shape."))
		);
	}
}
