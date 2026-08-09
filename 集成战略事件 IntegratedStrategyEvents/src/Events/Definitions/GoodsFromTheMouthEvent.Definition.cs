namespace IntegratedStrategyEvents.Events;

public sealed partial class GoodsFromTheMouthEvent
{
	private static readonly IntegratedStrategyEventLayoutProfile PortraitLayout = new(
		LeftAligned: true,
		ContentWidthScale: 0.78f,
		VerticalOffset: -70f,
		VerticalOffsetOptionCount: 4);

	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"goods_from_the_mouth.png",
			CreateLocalization,
			PortraitLayout);

	private static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"货从口出",
				new EventPageLoc(
					InitialPage,
					"[sine]“我见过您。”[/sine]一匹提着大口袋的马叫住了你，“过去您总是光顾我的[orange]生意[/orange]，今天有什么需要的吗？”\n\n它向你打开袋口，露出里面满满当当的[gold]源石锭[/gold]，证明它的[orange]生意[/orange]确实火爆。虽然你没找到它的货放在哪里，但一个头脑精明的[orange]商人[/orange]能从任何地方把货掏出来。",
					new EventOptionLoc("LABOR_SAVING", "想要一个能节省力气的", "支付[red]50[/red][gold]金币[/gold]。获得[blue]1[/blue]瓶随机[gold]稀有药水[/gold]。"),
					new EventOptionLoc("LABOR_SAVING_LOCKED", "想要一个能节省力气的", "需要[blue]50[/blue][gold]金币[/gold]。"),
					new EventOptionLoc("VALUE_PRESERVING", "想要一个可以保值的", "支付[red]100[/red][gold]金币[/gold]。获得一次[gold]稀有卡牌奖励[/gold]。"),
					new EventOptionLoc("VALUE_PRESERVING_LOCKED", "想要一个可以保值的", "需要[blue]100[/blue][gold]金币[/gold]。"),
					new EventOptionLoc("TREE_SEA_SOUVENIR", "想要一个树海特色纪念品", "支付[red]150[/red][gold]金币[/gold]。获得一件随机[gold]遗物[/gold]。"),
					new EventOptionLoc("TREE_SEA_SOUVENIR_LOCKED", "想要一个树海特色纪念品", "需要[blue]150[/blue][gold]金币[/gold]。"),
					new EventOptionLoc("LEAVE", "没什么想买的", "离开。")),
				new EventPageLoc(
					"LABOR_SAVING",
					"[jitter]马嘴抖动着[/jitter]，吐出一样东西：“我曾经卖出过[gold]四只羊蹄[/gold]给想要学会攀岩的主顾——虽然羊蹄只有长在羊腿上才听使唤。[sine][orange]商人总是能敏锐地捕捉到需求[/orange][/sine]，不是吗？”"),
				new EventPageLoc(
					"VALUE_PRESERVING",
					"[jitter]马嘴抖动着[/jitter]，吐出一样东西：“您想不到有收藏爱好的主顾都在买什么。重要的不是商品真正的价值和买进的价格——而是如何把这些货包装得[gold]价值连城[/gold]。[sine][orange]商人靠的就是巧舌如簧[/orange][/sine]，不是吗？”"),
				new EventPageLoc(
					"TREE_SEA_SOUVENIR",
					"[jitter]马嘴抖动着[/jitter]，吐出一样东西：“我曾遇到过和您一样有眼光的主顾，每到一个新地方就想带回一些[orange]特色商品[/orange]——在某地随处可见，但在另一处却是[gold]稀罕珍品[/gold]。[sine][orange]商人就是要利用信息差赚钱[/orange][/sine]，不是吗？”"),
				new EventPageLoc(
					"LEAVE",
					"你决定先不消费，至少[b]不是在此处[/b]。")),
			new EventLoc(
				"Goods from the Horse's Mouth",
				new EventPageLoc(
					InitialPage,
					"[sine]“I've seen you before.”[/sine] A horse carrying a large sack calls out to you. “You used to frequent my [orange]business[/orange]. What can I get for you today?”\n\nIt opens the sack and reveals it is stuffed with [gold]Originium Ingots[/gold], proof that its [orange]business[/orange] is booming. You cannot tell where it keeps its merchandise, but a shrewd [orange]merchant[/orange] can produce goods from anywhere.",
					new EventOptionLoc("LABOR_SAVING", "Something that saves effort", "Pay [red]50[/red] [gold]Gold[/gold]. Gain [blue]1[/blue] random [gold]Rare Potion[/gold]."),
					new EventOptionLoc("LABOR_SAVING_LOCKED", "Something that saves effort", "Requires [blue]50[/blue] [gold]Gold[/gold]."),
					new EventOptionLoc("VALUE_PRESERVING", "Something that holds its value", "Pay [red]100[/red] [gold]Gold[/gold]. Gain a [gold]Rare card reward[/gold]."),
					new EventOptionLoc("VALUE_PRESERVING_LOCKED", "Something that holds its value", "Requires [blue]100[/blue] [gold]Gold[/gold]."),
					new EventOptionLoc("TREE_SEA_SOUVENIR", "A Tree Sea specialty souvenir", "Pay [red]150[/red] [gold]Gold[/gold]. Gain a random [gold]Relic[/gold]."),
					new EventOptionLoc("TREE_SEA_SOUVENIR_LOCKED", "A Tree Sea specialty souvenir", "Requires [blue]150[/blue] [gold]Gold[/gold]."),
					new EventOptionLoc("LEAVE", "Nothing I want to buy", "Leave.")),
				new EventPageLoc(
					"LABOR_SAVING",
					"[jitter]The horse's mouth quivers[/jitter] before it spits something out. “I once sold [gold]four sheep hooves[/gold] to a customer who wanted to learn rock climbing—though hooves only obey when attached to a sheep's legs. [sine][orange]A merchant always spots demand quickly[/orange][/sine], wouldn't you agree?”"),
				new EventPageLoc(
					"VALUE_PRESERVING",
					"[jitter]The horse's mouth quivers[/jitter] before it spits something out. “You would never guess what collectors buy. What matters is not an item's true value or purchase price, but how you package it to look [gold]priceless[/gold]. [sine][orange]A merchant lives by a silver tongue[/orange][/sine], wouldn't you agree?”"),
				new EventPageLoc(
					"TREE_SEA_SOUVENIR",
					"[jitter]The horse's mouth quivers[/jitter] before it spits something out. “I once met another discerning customer like you, someone who wanted to bring home a [orange]local specialty[/orange] from every new place—common in one land, yet a [gold]rare treasure[/gold] in another. [sine][orange]A merchant profits from an information gap[/orange][/sine], wouldn't you agree?”"),
				new EventPageLoc(
					"LEAVE",
					"You decide not to spend anything for now—at least [b]not here[/b]."))
		);
	}
}
