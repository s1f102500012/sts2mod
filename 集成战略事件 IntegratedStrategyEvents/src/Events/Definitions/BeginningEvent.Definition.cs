
namespace IntegratedStrategyEvents.Events;

public sealed partial class BeginningEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"beginning.png",
			CreateLocalization,
			Layout: IntegratedStrategyEventLayoutProfile.LeftWide,
			AlignHoverTipsRight: true);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"开端",
				new EventPageLoc(
					InitialPage,
					"世界正在[jitter][red]发生剧变[/red][/jitter]，消息终端不断鸣响，坏消息接踵而至：[aqua]恐鱼潮[/aqua]向着陆地行进，[red]惩戒军舰队覆灭[/red]，[purple]深海教徒[/purple]攻击[gold]伊比利亚颂圣棱堡[/gold]。\n\n但最让你担心的，还是由[b]“[gold]愚人号[/gold]”[/b]进入阿戈尔的[aqua]深海猎人[/aqua]。水月找到的文件上详尽地记载着将深海猎人骗回[blue]海洋[/blue]，以及令[sine][purple]祂[/purple][/sine]苏醒的计划。而另一份名为[sine][blue]“深蓝”[/blue][/sine]的文件，记录着[gold]主教[/gold]对于[blue]海嗣起源[/blue]的毕生研究，以及一个[gold]地点[/gold]。",
					new EventOptionLoc("DIVE_DEEP", "决定潜入深海", "获得[gold]主教的研究[/gold]。"),
					new EventOptionLoc("DEEP_BLUE", "“深蓝”？！", "获得[gold]深蓝回忆[/gold]。"),
					new EventOptionLoc("NIGHTMARE", "噩梦......", "获得[green]8[/green]点最大生命。")),
				new EventPageLoc(
					"DIVE_DEEP",
					"已经没有时间通知[gold]罗德岛[/gold]或[gold]伊比利亚[/gold]的盟友了，你必须立刻与[aqua]水月[/aqua]一起潜入[blue]深海[/blue]，挖掘[gold]主教[/gold]留下的秘密，并以此击退[blue]海嗣[/blue]，拯救[aqua]深海猎人[/aqua]。"),
				new EventPageLoc(
					"DEEP_BLUE",
					"[sine][blue]“深蓝”[/blue][/sine]，这个名字让你产生了一股莫名的熟悉感。当你们找到文件上的[gold]地标[/gold]时，你带着[aqua]水月[/aqua]走向设施深处，甚至在他找到并试图向你示意自己有能力同化[sine][purple]祂[/purple][/sine]的核心器官时也没有停下。\n\n你们一路向下，向下，进入了位于[orange]泰拉地幔[/orange]中的[b][blue]“深蓝之树”[/blue][/b]实验中枢。这里是孵育[sine][purple]祂们[/purple][/sine]的场所，自然也能让[blue]海嗣[/blue]成为[sine][purple]祂们[/purple][/sine]的一员。"),
				new EventPageLoc(
					"NIGHTMARE",
					"还好那只是一个[sine][purple]梦[/purple][/sine]。醒来后，你检查了自己的通讯终端，谢天谢地，什么都没有发生。")),
			new EventLoc(
				"Beginning",
				new EventPageLoc(
					InitialPage,
					"The world is [jitter][red]changing violently[/red][/jitter]. Your message terminal keeps ringing as bad news arrives one after another: [aqua]Seaborn waves[/aqua] are marching inland, the [red]Punitive Navy has been destroyed[/red], and [purple]Church of the Deep cultists[/purple] have attacked Iberia's [gold]Canticum Bastion[/gold].\n\nWhat worries you most, however, are the [aqua]Abyssal Hunters[/aqua] who entered Aegir aboard the [b][gold]Stultifera Navis[/gold][/b]. The files Mizuki found record in detail a plan to lure the Abyssal Hunters back into the [blue]ocean[/blue] and awaken [sine][purple]Them[/purple][/sine]. Another file, titled [sine][blue]\"Deep Blue\"[/blue][/sine], records the [gold]Bishop[/gold]'s lifelong research into the [blue]origin of the Seaborn[/blue], and a [gold]location[/gold].",
					new EventOptionLoc("DIVE_DEEP", "Decide to dive into the deep sea", "Gain [gold]Bishop's Research[/gold]."),
					new EventOptionLoc("DEEP_BLUE", "\"Deep Blue\"?!", "Gain [gold]Deep Blue Memory[/gold]."),
					new EventOptionLoc("NIGHTMARE", "A nightmare...", "Gain [green]8[/green] Max HP.")),
				new EventPageLoc(
					"DIVE_DEEP",
					"There is no time to notify [gold]Rhodes Island[/gold] or your [gold]Iberian[/gold] allies. You must dive into the [blue]deep sea[/blue] with [aqua]Mizuki[/aqua] at once, unearth the [gold]Bishop[/gold]'s secrets, and use them to repel the [blue]Seaborn[/blue] and save the [aqua]Abyssal Hunters[/aqua]."),
				new EventPageLoc(
					"DEEP_BLUE",
					"[sine][blue]\"Deep Blue\"[/blue][/sine]. The name stirs a strange familiarity in you. When you find the [gold]landmark[/gold] from the file, you lead [aqua]Mizuki[/aqua] deeper into the facility, not even stopping when he finds a core organ and tries to signal that he can assimilate [sine][purple]Them[/purple][/sine].\n\nYou keep descending, down and down, into the [b][blue]\"Deep Blue Tree\"[/blue][/b] experimental center within [orange]Terra's mantle[/orange]. This is where [sine][purple]They[/purple][/sine] were incubated. Naturally, it can also make the [blue]Seaborn[/blue] one of [sine][purple]Them[/purple][/sine]."),
				new EventPageLoc(
					"NIGHTMARE",
					"Thankfully, it was only a [sine][purple]dream[/purple][/sine]. After waking, you check your communication terminal. Thank goodness. Nothing has happened."))
		);
	}
}
