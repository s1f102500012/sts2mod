
namespace IntegratedStrategyEvents.Events;

public sealed partial class ForSurvivalEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } = IntegratedStrategyEventDefinition.ForEventPortrait("for_survival.png", CreateLocalization);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"为了生存",
				new EventPageLoc(
					InitialPage,
					"取得当地[aqua]海民[/aqua]的信任后，他们将你带到了某个[aqua]迷宫般的溶洞[/aqua]。\n\n这里深入地下，堆满了你从未见过的[purple]巨型贝类[/purple]。海民们正[sine][purple]紧张地忙碌[/purple][/sine]着，他们撬开巨型贝的壳，里面的[purple]肉簇[/purple]立即吐出大量[aqua]分泌物[/aqua]，这些分泌物被迅速收集起来，过滤、封装......\n\n大家并不交谈，尽量压低着动静，一切都井井有条，俨然是个[orange]秘密工厂[/orange]。",
					new EventOptionLoc("JOIN", "报酬很可观，加入他们", "获得[blue]30[/blue][gold]金币[/gold]。"),
					new EventOptionLoc("LEAVE", "离开", "还是算了。")),
				new EventPageLoc(
					"JOIN",
					"你终于弄清了溶洞里的情况。这些[purple]巨型贝类[/purple]竟然是[red]恐鱼的幼卵[/red]，利用它们的分泌物制作的涂料，可以有效[sine][purple]迷惑恐鱼群[/purple][/sine]，躲避它们的追捕......虽然[red]腥臭难忍[/red]。",
					new EventOptionLoc("WORK_SMALL", "工作很简单", "支付[red]45[/red][gold]金币[/gold]。[blue]70%[/blue]概率获得[blue]120[/blue][gold]金币[/gold]。"),
					new EventOptionLoc("WORK_SMALL_LOCKED", "工作很简单", "需要[blue]45[/blue][gold]金币[/gold]。"),
					new EventOptionLoc("LEAVE", "离开", "还是算了。")),
				new EventPageLoc(
					"WORK_SMALL",
					"采集的操作并不复杂，你已经[gold]相当熟练[/gold]。\n\n可好几次看着那些饱满的[purple]肉簇[/purple]逐渐变得干瘪时，你似乎都听到了一声[sine][aqua]带着哭腔的低号[/aqua][/sine]......[font_size=22][i]异常幽微[/i][/font_size]，或许只是刀刮到硬壳的动静吧，你并不确定。",
					new EventOptionLoc("WORK_BIG", "工作很简单", "支付[red]150[/red][gold]金币[/gold]。[blue]50%[/blue]概率获得[blue]300[/blue][gold]金币[/gold]。"),
					new EventOptionLoc("WORK_BIG_LOCKED", "工作很简单", "需要[blue]150[/blue][gold]金币[/gold]。"),
					new EventOptionLoc("FLEE_AFTER_SIGNAL", "离开", "还是算了。")),
				new EventPageLoc(
					"WORK_BIG",
					"[jitter][red]恐鱼群突然出现[/red][/jitter]，袭击了溶洞。\n\n你猛然反应过来，那些[aqua]隐约的低号[/aqua]，难道是贝类向恐鱼发出的讯息？即使是如此隐秘的溶洞，依然无法阻绝[aqua]信息的传递[/aqua]吗......\n\n你从混乱中逃了出来，希望那些海民们也平安无事，希望。"),
				new EventPageLoc(
					"FISH_ATTACK",
					"[jitter][red]恐鱼群突然出现[/red][/jitter]，袭击了溶洞。\n\n你猛然反应过来，那些[aqua]隐约的低号[/aqua]，难道是贝类向恐鱼发出的讯息？即使是如此隐秘的溶洞，依然无法阻绝[aqua]信息的传递[/aqua]吗......\n\n你从混乱中逃了出来，希望那些海民们也平安无事，希望。"),
				new EventPageLoc(
					"LEAVE",
					"工作的间歇，你听见海民们低声讨论，又有一批负责收集巨型贝的同伴[red]没能回来[/red]。\n\n这是[orange]必须付出的代价[/orange]，大家心知肚明，但不会停止。总有一些人[red]牺牲[/red]，更多的人才能活下去。\n\n可这样的法子还能坚持多久，没有人知道。你离开了，海民们还需要[green]为了生存[/green]而继续躲藏，而你有更重要的事情要做。")),
			new EventLoc(
				"For Survival",
				new EventPageLoc(
					InitialPage,
					"After earning the trust of the local [aqua]seafolk[/aqua], they lead you into an [aqua]labyrinthine cavern[/aqua].\n\nIt runs deep underground and is packed with [purple]giant shellfish[/purple] you have never seen. The seafolk work in [sine][purple]tense silence[/purple][/sine], prying open the shells so the [purple]flesh clusters[/purple] inside spew large amounts of [aqua]secretion[/aqua]. It is quickly collected, filtered, and sealed...\n\nNo one talks. Everyone keeps quiet. Everything is orderly, like an [orange]secret factory[/orange].",
					new EventOptionLoc("JOIN", "The pay is good. Join them", "Gain [blue]30[/blue] [gold]Gold[/gold]."),
					new EventOptionLoc("LEAVE", "Leave", "Forget it.")),
				new EventPageLoc(
					"JOIN",
						"You finally understand what is happening in the cavern. These [purple]giant shellfish[/purple] are actually [red]Seaborn eggs[/red]. Paint made from their secretion can [sine][purple]confuse the Seaborn swarm[/purple][/sine] and help people evade pursuit... though the [red]stench is unbearable[/red].",
					new EventOptionLoc("WORK_SMALL", "The work is simple", "Spend [red]45[/red] [gold]Gold[/gold]. [blue]70%[/blue] chance to gain [blue]120[/blue] [gold]Gold[/gold]."),
					new EventOptionLoc("WORK_SMALL_LOCKED", "The work is simple", "Requires [blue]45[/blue] [gold]Gold[/gold]."),
					new EventOptionLoc("LEAVE", "Leave", "Forget it.")),
				new EventPageLoc(
					"WORK_SMALL",
					"The gathering process is not complicated. You have become [gold]quite practiced[/gold].\n\nYet more than once, while watching those swollen [purple]flesh clusters[/purple] gradually shrivel, you seem to hear a [sine][aqua]low sobbing call[/aqua][/sine]...[font_size=22][i]faint and strange[/i][/font_size]. Perhaps it is only the knife scraping against the hard shell. You are not sure.",
					new EventOptionLoc("WORK_BIG", "The work is simple", "Spend [red]150[/red] [gold]Gold[/gold]. [blue]50%[/blue] chance to gain [blue]300[/blue] [gold]Gold[/gold]."),
					new EventOptionLoc("WORK_BIG_LOCKED", "The work is simple", "Requires [blue]150[/blue] [gold]Gold[/gold]."),
					new EventOptionLoc("FLEE_AFTER_SIGNAL", "Leave", "Forget it.")),
				new EventPageLoc(
					"WORK_BIG",
					"[jitter][red]A Seaborn swarm suddenly appears[/red][/jitter] and attacks the cavern.\n\nYou realize with a start: could those [aqua]faint low calls[/aqua] have been the shellfish sending messages to the Seaborn? Even a cavern this hidden cannot block the [aqua]transmission of information[/aqua]...\n\nYou escape the chaos. You hope the seafolk are safe too. Hope."),
				new EventPageLoc(
					"FISH_ATTACK",
						"[jitter][red]A Seaborn swarm suddenly appears[/red][/jitter] and attacks the cavern.\n\nYou realize with a start: could those [aqua]faint low calls[/aqua] have been the shellfish sending messages to the Seaborn? Even a cavern this hidden cannot block the [aqua]transmission of information[/aqua]...\n\nYou escape the chaos. You hope the seafolk are safe too. Hope."),
				new EventPageLoc(
					"LEAVE",
					"During a break in the work, you hear the seafolk whispering. Another group responsible for collecting giant shellfish [red]did not return[/red].\n\nThis is the [orange]necessary price[/orange]. Everyone knows it, but no one will stop. Some must [red]be sacrificed[/red] so that more can live.\n\nNo one knows how long this method can last. You leave. The seafolk must continue hiding [green]for survival[/green], and you have more important things to do."))
		);
	}
}
