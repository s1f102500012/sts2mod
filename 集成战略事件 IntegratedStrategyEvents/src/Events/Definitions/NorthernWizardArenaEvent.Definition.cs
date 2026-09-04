
namespace IntegratedStrategyEvents.Events;

public sealed partial class NorthernWizardArenaEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"northern_wizard_arena.png",
			CreateLocalization,
			IntegratedStrategyEventLayoutProfile.LeftWide);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"北地巫师竞技",
				new EventPageLoc(
					InitialPage,
					"各个[green]部族[/green]正在聚会！连误闯的你们也受到了邀请。在分享了不少[gold]故事[/gold]、笑话和[orange]树汁饮料[/orange]后，萨米人突然[purple]安静[/purple]了下来，几个穿着打扮明显是[aqua]雪祀[/aqua]的人步态庄严地走进了树林。\n\n聚会的萨米主持人走了过来，用一板一眼的腔调询问你愿不愿意[jitter]“选择一位斗士”[/jitter]。在他身后，一群奇形怪状的[sine][green]“巨人”[/green][/sine]在雪祀们的驱使下走出了树林。",
					new EventOptionLoc("CHOOSE_FIGHTER", "接过名册木板，是该选择了！", "支付[red]40[/red][gold]金币[/gold]。"),
					new EventOptionLoc("CHOOSE_FIGHTER_LOCKED", "接过名册木板，是该选择了！", "需要[blue]40[/blue][gold]金币[/gold]。"),
					new EventOptionLoc("LEAVE_EARLY", "这好像要挺久的，还是赶路要紧", "他向你道别，并祝你吃饱穿暖。")),
				new EventPageLoc(
					"HORN_ROCK",
					"[b]“兽角岩”[/b]大获全胜，一拳将[green]“莫西巨草”[/green]打得[jitter][red]翻了个四脚朝天[/red][/jitter]，后者身体上附着的苔藓洒得到处都是！\n\n你选择的[gold]战争傀儡[/gold]赢了！主持人哈哈大笑着送来了你的[gold]奖品[/gold]。",
					new EventOptionLoc("CLAIM_HORN_ROCK", "哈！几根柳条怎能胜过磐石！", "获得[blue]60[/blue][gold]金币[/gold]。")),
				new EventPageLoc(
					"TREE_EYEBROWS",
					"[green]“树眉毛”[/green]一甩身就闪过了[aqua]“大雪暴”[/aqua]投来的巨大雪块，然后跑步接近对方，还接了一个[sine]敏捷的空翻[/sine]，用腿部的重量将对手[jitter][red]轰倒在地[/red][/jitter]——你也没想到它还有这一招！\n\n你选择的[gold]战争傀儡[/gold]赢了！主持人哈哈大笑着送来了你的奖品，这次是更多的[green]食用聚块和凝胶[/green]。",
					new EventOptionLoc("CLAIM_TREE_EYEBROWS", "不要光靠体格评判对手的能耐！", "回复[green]4[/green]点生命。")),
				new EventPageLoc(
					"HOT_STAN",
					"[orange]“烫手斯丹”[/orange]泥土的双掌上竟显现出[aqua]密文[/aqua]！它们显然散发出了[jitter][red]高温[/red][/jitter]！[orange]“烫手斯丹”[/orange]火热的连续掌击打得[purple]“异铁皮阿萨格”[/purple]浑身冒[jitter][orange]火花[/orange][/jitter]！\n\n你选择的[gold]战争傀儡[/gold]赢了！主持人哈哈大笑着送来了你的奖品，你惊奇地发现其中竟有一架[gold]微型作业平台[/gold]。",
					new EventOptionLoc("CLAIM_HOT_STAN", "“烫手斯丹”掌法显然是练过！", "获得[blue]1[/blue]瓶随机[gold]药水[/gold]。")),
				new EventPageLoc(
					"OLD_BJORN",
					"像一堆枯树杈搭起的羽兽窝一样、摇摇欲坠、步履蹒跚的[green]“老比约恩”[/green]突然[gold]大显神威[/gold]，一把将[purple]“走路石墙”[/purple]推倒并[jitter][red]狂殴[/red][/jitter]！你选择的[gold]战争傀儡[/gold]赢了！这个结果除了你没多少人料到！\n\n操控它的年轻姑娘显然长出了一口气。“她刚接班，干得不错！”主持人一边把[gold]大奖[/gold]递给你一边喊，声音几乎要被观众的叫好声掩盖。",
					new EventOptionLoc("CLAIM_OLD_BJORN", "花草根须令顽石也不得不让道！", "获得一件随机[gold]遗物[/gold]。")),
				new EventPageLoc(
					"HELMA",
					"有那么一会儿，你觉得你选中的战争傀儡[gold]肯定要赢了[/gold]，但[aqua]“门卫海尔玛”[/aqua]使出了扬起地上[jitter]积雪和泥土[/jitter]的技能，最终[purple]更胜一筹[/purple]。\n\n主持人朝你耸了耸肩，然后便笑眯眯地凑近，等着你开口说话。",
					new EventOptionLoc("ACCEPT_LOSS", "可惜！", "还以为这次可以赢的。")),
				new EventPageLoc(
					"REPEAT",
					"巨大的[gold]战争傀儡[/gold]完全不知疲倦，操控它取胜的[aqua]雪祀[/aqua]正得意地挥舞着木杖。“[gold]下一场！再打啊！[/gold]”你不禁觉得这些一反常态大吼着、吹着口哨的萨米人[green]亲切[/green]了不少。\n\n主持人笑呵呵地把名册又朝你递来——整个聚会场上，[orange]严肃的气氛[/orange]已经一点都没剩下了。",
					new EventOptionLoc("RETRY", "没来过萨米的人一定没见过！", "支付[red]40[/red][gold]金币[/gold]。"),
					new EventOptionLoc("RETRY_LOCKED", "没来过萨米的人一定没见过！", "需要[blue]40[/blue][gold]金币[/gold]。"),
					new EventOptionLoc("LEAVE_REPEAT", "劲爆！刺激！但是该离开了", "这种几率控制的游戏应该适度。")),
				new EventPageLoc(
					"LEAVE",
					"你离开了[green]部族的聚会场[/green]，身后传来了[aqua]树干[/aqua]与泥土碰撞的声音和人们[green]欢乐尽兴的大笑[/green]。")),
			new EventLoc(
				"Northern Wizard Arena",
				new EventPageLoc(
					InitialPage,
					"The [green]tribes[/green] are holding a gathering, and even your lost group is invited. After sharing plenty of [gold]stories[/gold], jokes, and [orange]tree sap drinks[/orange], the Sami suddenly fall [purple]quiet[/purple]. Several people dressed unmistakably as [aqua]snowpriests[/aqua] stride solemnly into the woods.\n\nThe Sami host approaches and asks, in a stiff formal tone, whether you would like to [jitter]\"choose a fighter\"[/jitter]. Behind him, a crowd of strange [sine][green]\"giants\"[/green][/sine] emerges from the trees under the snowpriests' command.",
					new EventOptionLoc("CHOOSE_FIGHTER", "Take the roster board", "Pay [red]40[/red] [gold]Gold[/gold]."),
					new EventOptionLoc("CHOOSE_FIGHTER_LOCKED", "Take the roster board", "Requires [blue]40[/blue] [gold]Gold[/gold]."),
					new EventOptionLoc("LEAVE_EARLY", "This may take a while", "He bids you farewell and wishes you full bellies and warm clothes.")),
				new EventPageLoc(
					"HORN_ROCK",
					"[b]\"Horn Rock\"[/b] wins a sweeping victory, punching [green]\"Mossy Grass\"[/green] [jitter][red]belly-up[/red][/jitter] and scattering moss everywhere.\n\nYour [gold]war puppet[/gold] has won. Laughing, the host brings over your [gold]prize[/gold].",
					new EventOptionLoc("CLAIM_HORN_ROCK", "Willows cannot beat stone!", "Gain [blue]60[/blue] [gold]Gold[/gold].")),
				new EventPageLoc(
					"TREE_EYEBROWS",
					"[green]\"Tree Brows\"[/green] sways aside from the huge snowball thrown by [aqua]\"Big Blizzard\"[/aqua], runs in, then performs a [sine]nimble flip[/sine] and uses the weight of its legs to [jitter][red]slam[/red][/jitter] the opponent down. You did not expect it to know that move.\n\nYour [gold]war puppet[/gold] has won. Laughing, the host brings over your prize: more [green]edible lumps and gel[/green].",
					new EventOptionLoc("CLAIM_TREE_EYEBROWS", "Do not judge by size!", "Heal [green]4[/green] HP.")),
				new EventPageLoc(
					"HOT_STAN",
					"[orange]\"Hot-Handed Stan\"[/orange] reveals [aqua]cipher marks[/aqua] on its earthen palms. They are clearly giving off [jitter][red]heat[/red][/jitter]. Its scorching palm strikes make [purple]\"Asag of Alien Iron\"[/purple] shower [jitter][orange]sparks[/orange][/jitter].\n\nYour [gold]war puppet[/gold] has won. Laughing, the host brings over your prize, and you are surprised to find a [gold]miniature work platform[/gold] among it.",
					new EventOptionLoc("CLAIM_HOT_STAN", "Stan has clearly trained!", "Gain [blue]1[/blue] random [gold]Potion[/gold].")),
				new EventPageLoc(
					"OLD_BJORN",
					"[green]\"Old Bjorn\"[/green], wobbling along like a nest of dry branches, suddenly shows [gold]astonishing strength[/gold], shoves [purple]\"Walking Stone Wall\"[/purple] over, and [jitter][red]pummels[/red][/jitter] it. Your [gold]war puppet[/gold] has won. Almost no one expected this result except you.\n\nThe young girl controlling it visibly sighs in relief. \"She just took over, and she did well!\" the host shouts while handing you the [gold]grand prize[/gold], his voice nearly drowned by the cheering crowd.",
					new EventOptionLoc("CLAIM_OLD_BJORN", "Roots make stone yield!", "Gain a random [gold]Relic[/gold].")),
				new EventPageLoc(
					"HELMA",
					"For a moment, you are sure your war puppet is [gold]going to win[/gold], but [aqua]\"Gatekeeper Helma\"[/aqua] uses a technique that kicks up [jitter]snow and mud[/jitter], ultimately proving [purple]stronger[/purple].\n\nThe host shrugs, then leans in with a smile, waiting for you to speak.",
					new EventOptionLoc("ACCEPT_LOSS", "A shame!", "You thought this one could win.")),
				new EventPageLoc(
					"REPEAT",
					"The huge [gold]war puppets[/gold] show no sign of tiring, and the victorious [aqua]snowpriest[/aqua] proudly waves a wooden staff. \"[gold]Next round! Fight again![/gold]\" You find these Sami, shouting and whistling so uncharacteristically, much more [green]approachable[/green].\n\nThe host cheerfully offers you the roster again. Across the gathering, not a trace of the [orange]solemn mood[/orange] remains.",
					new EventOptionLoc("RETRY", "Only in Sami country!", "Pay [red]40[/red] [gold]Gold[/gold]."),
					new EventOptionLoc("RETRY_LOCKED", "Only in Sami country!", "Requires [blue]40[/blue] [gold]Gold[/gold]."),
					new EventOptionLoc("LEAVE_REPEAT", "Thrilling, but time to go", "Games of chance should be enjoyed in moderation.")),
				new EventPageLoc(
					"LEAVE",
					"You leave the [green]tribal gathering[/green]. Behind you come the sounds of [aqua]tree trunks[/aqua] colliding with mud and people laughing in [green]merry satisfaction[/green]."))
		);
	}
}
