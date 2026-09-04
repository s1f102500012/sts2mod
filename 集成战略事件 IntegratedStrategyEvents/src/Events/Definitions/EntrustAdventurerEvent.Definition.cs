
namespace IntegratedStrategyEvents.Events;

public sealed partial class EntrustAdventurerEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } = IntegratedStrategyEventDefinition.ForEventPortrait("entrust_adventurer.png", CreateLocalization);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"委托冒险者",
				new EventPageLoc(
					InitialPage,
					"你遇到了一个[b]徘徊的冒险者[/b]。对方向你吐露最近遇到的[purple]怪事[/purple]：听说这里深夜会有一场[sine][purple]盛大的幽灵剧场[/purple][/sine]，鬼魂们在舞台上[jitter][orange]彻夜狂欢[/orange][/jitter]。可他却一无所获，还险些被一群大喊着[red][jitter]古怪台词[/jitter][/red]的疯子袭击。\n\n现在他准备离开了。或许能够托他给在外等待的同伴带信，寻求支援。",
					new EventOptionLoc("MEAGER_TIP", "意思意思", "支付[red]20[/red][gold]金币[/gold]。获得一次二选一卡牌奖励。"),
					new EventOptionLoc("MEAGER_TIP_LOCKED", "意思意思", "需要[blue]20[/blue][gold]金币[/gold]。"),
					new EventOptionLoc("HEAVY_REWARD", "重金酬谢", "支付[red]50[/red][gold]金币[/gold]。获得一次五选一卡牌奖励。"),
					new EventOptionLoc("HEAVY_REWARD_LOCKED", "重金酬谢", "需要[blue]50[/blue][gold]金币[/gold]。"),
					new EventOptionLoc("SINCERE_REQUEST", "真诚拜托", "相信诚意能够打动他。")),
				new EventPageLoc(
					"MEAGER_TIP",
					"你抓出了几枚探索所得的[gold]古钱币[/gold]放在冒险者手中，对方露出笑容，并承诺就将话带到。"),
				new EventPageLoc(
					"HEAVY_REWARD",
					"[b]真诚的态度[/b]能打动许多人。你太[sine][gold]真诚[/gold][/sine]了，冒险者非常感动，十分乐意为你效劳，甚至追问你的联系方式，期待下一次合作。"),
				new EventPageLoc(
					"SINCERE_REQUEST",
					"对方满口答应，但你等了很久，[jitter][purple]没有任何人[/purple][/jitter]前来[sine][green]支援[/green][/sine]。")),
			new EventLoc(
				"Entrust the Adventurer",
				new EventPageLoc(
					InitialPage,
					"You meet a [b]wandering adventurer[/b]. He tells you of [purple]strange rumors[/purple]: at midnight, a [sine][purple]grand ghost theater[/purple][/sine] opens nearby, and spirits [jitter][orange]revel until dawn[/orange][/jitter]. He found nothing there, and was nearly attacked by zealots shouting [red][jitter]bizarre lines[/jitter][/red].\n\nHe is leaving now. Perhaps he can carry a message to your companions outside and ask for support.",
					new EventOptionLoc("MEAGER_TIP", "A modest tip", "Pay [red]20[/red] [gold]Gold[/gold]. Gain a card reward with two choices."),
					new EventOptionLoc("MEAGER_TIP_LOCKED", "A modest tip", "Requires [blue]20[/blue] [gold]Gold[/gold]."),
					new EventOptionLoc("HEAVY_REWARD", "Reward him well", "Pay [red]50[/red] [gold]Gold[/gold]. Gain a card reward with five choices."),
					new EventOptionLoc("HEAVY_REWARD_LOCKED", "Reward him well", "Requires [blue]50[/blue] [gold]Gold[/gold]."),
					new EventOptionLoc("SINCERE_REQUEST", "Ask sincerely", "Trust that sincerity can move him.")),
				new EventPageLoc(
					"MEAGER_TIP",
					"You press a few [gold]ancient coins[/gold] from your expedition into the adventurer's hand. He smiles and promises to carry the message."),
				new EventPageLoc(
					"HEAVY_REWARD",
					"[b]Sincerity[/b] can move many people. You are so [sine][gold]sincere[/gold][/sine] that the adventurer is deeply touched, gladly agrees to help, and even asks how to contact you for future work."),
				new EventPageLoc(
					"SINCERE_REQUEST",
					"He readily agrees. You wait for a long time, but [jitter][purple]no one[/purple][/jitter] comes to offer [sine][green]support[/green][/sine]."))
		);
	}
}
