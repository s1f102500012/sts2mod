
namespace IntegratedStrategyEvents.Events;

public sealed partial class DustDevouringSpreadEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"dust_devouring_spread.png",
			CreateLocalization,
			IntegratedStrategyEventLayoutProfile.LeftWide);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"噬尘扩散",
				new EventPageLoc(
					InitialPage,
					"你面前的海岸已经变为了一片[sine][blue]灰白[/blue][/sine]。海风吹过，[gold]大地的残骸[/gold]随风消散。\n\n在[blue]尘土[/blue]与[green]土壤[/green]的边界，似乎有什么东西正在缓慢[jitter][red]灼烧[/red][/jitter]。你想要靠近观察，这时一个男人拉住了你，递过来一件防护服。\n\n“不想死的话就穿上。”",
					new EventOptionLoc("DECLINE_SUIT", "谢绝好意", "从你的[gold]牌组[/gold]中选择[blue]1[/blue]张牌[purple]变化[/purple]。"),
					new EventOptionLoc("DECLINE_SUIT_LOCKED", "谢绝好意", "没有可变化的牌。"),
					new EventOptionLoc("WEAR_SUIT", "赶紧穿好防护服", "回复[blue]6[/blue]点生命。")),
				new EventPageLoc(
					"DECLINE_SUIT",
					"你摆摆手，继续向边界靠近。海风卷起细碎的[blue]白灰[/blue]，擦过你的衣袖和随身行囊。\n\n你没有立刻感到痛楚，只看见一张记着旧战术的[gold]纸牌[/gold]边缘逐渐发白，图样在灼痕里[sine][purple]扭曲重组[/purple][/sine]。\n\n男人低声骂了一句，把你从边缘拽回。“连装备都会被[red]吃掉[/red]，你还想用身体试？”"),
				new EventPageLoc(
					"WEAR_SUIT",
					"“那是[jitter][red]噬尘[/red][/jitter]，[purple]海嗣[/purple]的新伎俩。它们会[red]分解[/red]掉一切东西，只剩下这种毫无作用的[blue]白灰[/blue]。它们......在捕食[gold]大地本身[/gold]。”\n\n就在你们谈话期间，一块陆地彻底[sine][blue]灰化[/blue][/sine]，散落进海洋中。\n\n“离边缘的[red]灼痕[/red]远点，被那些[purple]看不见的恐鱼[/purple]沾上你就没命了。快离开吧，陌生人，[orange]审判庭[/orange]会处理这个麻烦。”")),
			new EventLoc(
				"Dust Devouring Spread",
				new EventPageLoc(
					InitialPage,
					"The coast before you has turned [sine][blue]ashen white[/blue][/sine]. Sea wind passes over it, scattering the [gold]remains of the land[/gold].\n\nAt the boundary between [blue]dust[/blue] and [green]soil[/green], something seems to be slowly [jitter][red]burning[/red][/jitter]. You move closer to observe, but a man catches your arm and hands you a protective suit.\n\n\"Put it on if you do not want to die.\"",
					new EventOptionLoc("DECLINE_SUIT", "Decline the offer", "[purple]Transform[/purple] [blue]1[/blue] card from your [gold]deck[/gold]."),
					new EventOptionLoc("DECLINE_SUIT_LOCKED", "Decline the offer", "No transformable cards."),
					new EventOptionLoc("WEAR_SUIT", "Put on the suit", "Heal [blue]6[/blue] HP.")),
				new EventPageLoc(
					"DECLINE_SUIT",
					"You wave him off and keep approaching the edge. The sea wind lifts fine [blue]white ash[/blue], brushing across your sleeve and pack.\n\nYou feel no pain at first. Then the edge of a [gold]card[/gold] recording an old tactic turns pale, its image [sine][purple]twisting and re-forming[/purple][/sine] inside the burn mark.\n\nThe man curses under his breath and pulls you away. \"It can [red]eat[/red] equipment, and you wanted to test it with your body?\""),
				new EventPageLoc(
					"WEAR_SUIT",
					"\"That is [jitter][red]dust-devouring[/red][/jitter], a new trick from the [purple]Seaborn[/purple]. It [red]breaks down[/red] everything, leaving only useless [blue]white ash[/blue]. They are... feeding on the [gold]land itself[/gold].\"\n\nAs you speak, a piece of land [sine][blue]crumbles to ash[/blue][/sine] and scatters into the sea.\n\n\"Stay away from the [red]burn marks[/red] near the edge. If those [purple]invisible Sea Terrors[/purple] touch you, you are dead. Leave quickly, stranger. The [orange]Inquisition[/orange] will handle this trouble.\""))
		);
	}
}
