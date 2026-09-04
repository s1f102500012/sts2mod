
namespace IntegratedStrategyEvents.Events;

public sealed partial class PopularAttractionEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"popular_attraction.png",
			CreateLocalization,
			IntegratedStrategyEventLayoutProfile.LeftWide);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"热门景点",
				new EventPageLoc(
					InitialPage,
					"一个造型奇特的[orange]拱门[/orange]出现在你的视野内。锈迹斑斑的金属架上焊着[b]“失落之城卡兹戴尔”[/b]几个大字。\n\n简陋的售票窗口无人排队，里面坐着一个[sine][aqua]睡眼惺忪[/aqua][/sine]的孩童。他瞥了你一眼，说：“买门票送[gold]魔王私藏纪念品[/gold]，三[purple]‘嘎嘣’[/purple]一个，概不退换。”\n\n可是你把口袋翻了个遍，也没找到叫[purple]“嘎嘣”[/purple]的玩意。",
					new EventOptionLoc("BUY_TICKET", "这可以代替门票吗？", "失去[gold]{Relic}[/gold]。获得一件随机[gold]遗物[/gold]。"),
					new EventOptionLoc("BUY_TICKET_LOCKED", "这可以代替门票吗？", "你没有可失去的遗物。"),
					new EventOptionLoc("SNEAK_IN", "从旁边的小路溜进去", "没票就不能看了吗？"),
					new EventOptionLoc("LEAVE", "无奈离开", "我怎么一个“嘎嘣”都没有？")),
				new EventPageLoc(
					"PLATFORM",
					"拱门背后是一道[sine][aqua]蜿蜒向下的阶梯[/aqua][/sine]，两边潦草的壁画讲述着[purple]卡兹戴尔[/purple]在一夜之间陷落地下的故事。越往深处，壁画的内容就愈发[purple]荒诞不经[/purple]。\n\n不知多久后，你抵达了一处观景平台。借着火把的光亮，你看见了断崖上用茅草和树枝搭成的[b]“宫殿”[/b]，还有旁边的一个歪斜告示牌，写着[orange]“魔王戴冠仪式处，同款王冠售票处有售”[/orange]。",
					new EventOptionLoc("COMPLAIN", "去找售票员要个说法", "这是什么坑人的假景区！"),
					new EventOptionLoc("WALK_AWAY", "憋着怒气离开", "吃一堑长一智，再也不来了！")),
				new EventPageLoc(
					"CONFUSED_LEAVE",
					"你走出几步，越想越纳闷，[purple]“嘎嘣”[/purple]到底是什么？你怎么从来没听说过？你决定折回去问个清楚。\n\n售票处的小孩听完了你的问题，却哈哈大笑起来：“[purple]‘嘎嘣’[/purple]就是[purple]‘嘎嘣’[/purple]，连[purple]‘嘎嘣’[/purple]都没有，那你肩膀上面是什么？”\n\n听完这话，你只觉得[jitter][red]头晕脑胀[/red][/jitter]，脖子上似乎有几百公斤重。你赶紧离开了，不敢继续打听。"),
				new EventPageLoc(
					"COMPLAINT",
					"你怒气冲冲地回到售票处，里面的人换成了一个身上沾满泥土的[purple]萨卡兹[/purple]。他一边吹口哨一边抠着指甲，根本没把你的投诉听进去。\n\n你要求他赔偿你[gold]精神损失费[/gold]，他却掏出一把刀[jitter][red]插在桌子上[/red][/jitter]：“该看的都看了，想退款？那就别怪老子把你[red]眼珠子[/red]挖出来做成石头！”",
					new EventOptionLoc("FIGHT", "进入战斗", "遭遇一场艰难的战斗。")),
				new EventPageLoc(
					"WALK_AWAY",
					"路过售票处时，你发现里面的人换成了一个身上沾满泥土的[purple]萨卡兹[/purple]。你觉得他有些眼熟，但你不想再跟这里的人纠缠，只当他不存在。\n\n走出一段距离后，你忽然听到背后传来一阵[aqua]闷响[/aqua]。回头看去，拱门已消失，只剩地上一个[purple]深坑[/purple]。")),
			new EventLoc(
				"Popular Attraction",
				new EventPageLoc(
					InitialPage,
					"A strange [orange]archway[/orange] appears before you. Welded onto its rusted metal frame are the words [b]\"Lost City of Kazdel\"[/b].\n\nNo one is waiting at the crude ticket window. A [sine][aqua]sleepy-eyed[/aqua][/sine] child sits inside, glances at you, and says, \"Buy a ticket and get a [gold]Demon King's private souvenir[/gold]. Three [purple]kabongs[/purple] each. No refunds.\"\n\nYou turn out your pockets, but find nothing called a [purple]kabong[/purple].",
					new EventOptionLoc("BUY_TICKET", "Can this replace a ticket?", "Lose [gold]{Relic}[/gold]. Gain a random [gold]Relic[/gold]."),
					new EventOptionLoc("BUY_TICKET_LOCKED", "Can this replace a ticket?", "You have no Relic to lose."),
					new EventOptionLoc("SNEAK_IN", "Sneak in from the side path", "No ticket means no sightseeing?"),
					new EventOptionLoc("LEAVE", "Leave helplessly", "How do I not have a single kabong?")),
				new EventPageLoc(
					"PLATFORM",
					"Beyond the archway is a [sine][aqua]winding staircase[/aqua][/sine] descending into the earth. Rough murals on either side tell the story of [purple]Kazdel[/purple] sinking underground in a single night. The deeper you go, the more [purple]absurd[/purple] the murals become.\n\nAfter an unknown time, you reach a viewing platform. By torchlight, you see a [b]\"palace\"[/b] made of straw and branches on the cliff, and a crooked sign reading, [orange]\"Demon King's coronation site. Matching crowns sold at the ticket window.\"[/orange]",
					new EventOptionLoc("COMPLAIN", "Demand an explanation", "What kind of scam attraction is this?"),
					new EventOptionLoc("WALK_AWAY", "Leave while holding back anger", "Lesson learned. Never coming again!")),
				new EventPageLoc(
					"CONFUSED_LEAVE",
					"You walk a few steps and grow more puzzled. What exactly is a [purple]kabong[/purple]? Why have you never heard of it? You decide to turn back and ask clearly.\n\nAfter hearing your question, the child at the ticket window bursts into laughter. \"A [purple]kabong[/purple] is a [purple]kabong[/purple]. If you do not even have a [purple]kabong[/purple], then what is above your shoulders?\"\n\nThose words leave your head [jitter][red]dizzy and swollen[/red][/jitter], and your neck feels hundreds of kilograms heavier. You hurry away, no longer daring to ask."),
				new EventPageLoc(
					"COMPLAINT",
					"You storm back to the ticket window. The person inside has changed into a [purple]Sarkaz[/purple] covered in dirt. He whistles and picks at his nails, ignoring your complaint entirely.\n\nYou demand compensation for [gold]emotional damages[/gold], but he pulls out a knife and [jitter][red]slams it into the table[/red][/jitter]. \"You saw what there was to see. Want a refund? Then don't blame me for digging out your [red]eyeballs[/red] and making stones out of them!\"",
					new EventOptionLoc("FIGHT", "Enter combat", "Encounter a difficult fight.")),
				new EventPageLoc(
					"WALK_AWAY",
					"As you pass the ticket window, you notice that the person inside has changed into a [purple]Sarkaz[/purple] covered in dirt. He looks somewhat familiar, but you do not want to tangle with anyone here, so you pretend he does not exist.\n\nAfter you walk some distance, a [aqua]muffled crash[/aqua] sounds behind you. You turn back and find the archway gone, leaving only a [purple]deep pit[/purple] in the ground."))
		);
	}
}
