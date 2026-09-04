
namespace IntegratedStrategyEvents.Events;

public sealed partial class WastefulRevelryEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } = IntegratedStrategyEventDefinition.ForEventPortrait("wasteful_revelry.png", CreateLocalization);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"暴殄天物",
				new EventPageLoc(
					InitialPage,
					"你在一座[orange]酒窖[/orange]里发现了正在拼酒的[b]杜林人[/b]。尖塔中[gold]千百年来的藏酒[/gold]正在被他们[red]肆意挥霍[/red]，但他们声称自己已经得到了藏酒主人的许可。\n\n见你来了，他们滚了一个装满[gold]珍酿[/gold]的酒桶给你，邀请你一同[orange]狂欢[/orange]。",
					new EventOptionLoc("ONE_THIRD", "喝三分之一桶", "失去[red]6[/red]点生命。获得[blue]80[/blue][gold]金币[/gold]。"),
					new EventOptionLoc("ONE_THIRD_LOCKED", "喝三分之一桶", "需要至少[red]7[/red]点生命。"),
					new EventOptionLoc("HALF", "喝半桶", "失去[red]9[/red]点生命。获得一次稀有卡牌奖励。"),
					new EventOptionLoc("HALF_LOCKED", "喝半桶", "需要至少[red]10[/red]点生命。"),
					new EventOptionLoc("WHOLE", "喝一桶", "失去[red]18[/red]点生命。获得一件随机[gold]遗物[/gold]。"),
					new EventOptionLoc("WHOLE_LOCKED", "喝一桶", "需要至少[red]19[/red]点生命。"),
					new EventOptionLoc("LEAVE", "算了算了", "身体要紧。")),
				new EventPageLoc(
					"ONE_THIRD",
					"在[b]豪饮[/b]这方面，你做得还不错。杜林人一边[orange]喝彩[/orange]，一边丢了些[gold]纸币[/gold]给你。"),
				new EventPageLoc(
					"HALF",
					"看看你，你就像个[b]真正的杜林人[/b]一样擅长喝酒。杜林人很[green]开心[/green]，赠送了你一些[gold]礼物[/gold]。"),
				new EventPageLoc(
					"WHOLE",
					"杜林人看着你喝干了[red]一整桶[/red]珍酿，惊讶得说不出话来：“这可是那位[orange]建筑师[/orange]珍藏了[gold]五百年[/gold]的陈酿！”\n\n他们决定用掌声和[gold]宝物[/gold]来表达对你的尊敬。"),
				new EventPageLoc(
					"LEAVE",
					"在整座[orange]酒窖[/orange]被[red]喝干[/red]前，杜林人不会停下。但你该走了，你还有[b]要做的事情[/b]。")),
			new EventLoc(
				"Wasteful Revelry",
				new EventPageLoc(
					InitialPage,
					"In an [orange]wine cellar[/orange], you find a group of [b]Durins[/b] drinking competitively. The castle's [gold]centuries-old stock[/gold] is being [red]squandered[/red], though they insist they have the owner's permission.\n\nWhen you arrive, they roll over a cask full of [gold]vintage wine[/gold] and invite you to join the [orange]revelry[/orange].",
					new EventOptionLoc("ONE_THIRD", "Drink one third", "Lose [red]6[/red] HP. Gain [blue]80[/blue] [gold]Gold[/gold]."),
					new EventOptionLoc("ONE_THIRD_LOCKED", "Drink one third", "Requires at least [red]7[/red] HP."),
					new EventOptionLoc("HALF", "Drink half", "Lose [red]9[/red] HP. Gain a Rare card reward."),
					new EventOptionLoc("HALF_LOCKED", "Drink half", "Requires at least [red]10[/red] HP."),
					new EventOptionLoc("WHOLE", "Drink the whole cask", "Lose [red]18[/red] HP. Gain a random Relic."),
					new EventOptionLoc("WHOLE_LOCKED", "Drink the whole cask", "Requires at least [red]19[/red] HP."),
					new EventOptionLoc("LEAVE", "Never mind", "Your body comes first.")),
				new EventPageLoc(
					"ONE_THIRD",
					"When it comes to [b]heavy drinking[/b], you do fairly well. The Durins [orange]cheer[/orange] and toss you a handful of [gold]bills[/gold]."),
				new EventPageLoc(
					"HALF",
					"Look at you. You drink like a [b]true Durin[/b]. The Durins are [green]delighted[/green] and give you some [gold]gifts[/gold]."),
				new EventPageLoc(
					"WHOLE",
					"The Durins watch you drain [red]the whole cask[/red] and are left speechless. \"That was the [orange]troupe director's[/orange] [gold]five-hundred-year-old[/gold] vintage!\"\n\nThey decide to express their respect with applause and [gold]treasure[/gold]."),
				new EventPageLoc(
					"LEAVE",
					"The Durins will not stop before the entire [orange]cellar[/orange] is [red]drunk dry[/red]. But you should leave. You still have [b]things to do[/b]."))
		);
	}
}
