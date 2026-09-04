
namespace IntegratedStrategyEvents.Events;

public sealed partial class AllComersWelcomeEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"all_comers_welcome.png",
			CreateLocalization,
			IntegratedStrategyEventLayoutProfile.StandardNarrowSlightlyShiftedRight);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"来者不拒",
				new EventPageLoc(
					InitialPage,
					"你远远看到了一个[orange]小摊[/orange]，摊前挤满了各种陶石瓷铁的[orange]塑像[/orange]。看摊的小[gold]黎博利[/gold]正在向它们贩卖各种[gold]锦囊[/gold]。\n\n她瞟到了你们，于是也向你们一行人吆喝了起来：“[orange]司岁台[/orange]的大人不常来这逛吧，想来个[gold]锦囊[/gold]吗？里面可都是外头少有的[gold]稀罕物什[/gold]哦！”",
					new EventOptionLoc("SMALL_POUCH", "挑个小的", "支付[red]80[/red][gold]金币[/gold]。获得一件随机[gold]普通遗物[/gold]。"),
					new EventOptionLoc("SMALL_POUCH_LOCKED", "挑个小的", "需要[blue]80[/blue][gold]金币[/gold]。"),
					new EventOptionLoc("LARGE_POUCH", "挑个大的", "支付[red]200[/red][gold]金币[/gold]。获得[blue]3[/blue]件随机[gold]普通遗物[/gold]。"),
					new EventOptionLoc("LARGE_POUCH_LOCKED", "挑个大的", "需要[blue]200[/blue][gold]金币[/gold]。"),
					new EventOptionLoc("LEAVE", "还是算了", "离开。")),
				new EventPageLoc(
					"SMALL_POUCH",
					"你打开[gold]锦囊[/gold]，看了眼里面的珍宝，旋即把它塞进了背包里。\n\n这些[orange]塑像[/orange]似乎并不怕人，竟然还凑过来问你一些[orange]司岁台[/orange]成员的情况。这其中有的已身居要职，更多的则早已过世。如今仍记得他们音容笑貌的，或许只有眼前这些[pink]岁月难侵的造物[/pink]了。"),
				new EventPageLoc(
					"LARGE_POUCH",
					"你打开[gold]大锦囊[/gold]，给塑像们展示了一下里面的珍宝。趁着塑像们[orange]评宝论物[/orange]的时候，小[gold]黎博利[/gold]一边准备新的大锦囊，一边和你们聊了几句。\n\n从前，她是尖塔外的小商贩，现在则进进出出，做着两手生意。尖塔开放时，她向人们贩卖尖塔里的[gold]“纪念品”[/gold]；闭塔之后，她则将人类的物品卖给尖塔的住民们。\n\n至于挣到的钱……你注意到她把[gold]金币[/gold]像[green]零嘴[/green]一样丢入了口中。"),
				new EventPageLoc(
					"LEAVE",
					"你摆了摆手，小[gold]黎博利[/gold]也就不再继续缠着你，回头处理起了她和[orange]塑像们[/orange]的[orange]生意[/orange]。")),
			new EventLoc(
				"All Comers Welcome",
				new EventPageLoc(
					InitialPage,
					"From afar, you spot an [orange]small stall[/orange] crowded with [orange]statues[/orange] of clay, stone, porcelain, and iron. The young [gold]Liberi[/gold] watching the stall is selling them all kinds of [gold]pouches[/gold].\n\nShe notices your group and calls out, \"The officials from [orange]Sui's office[/orange] do not often come by, do they? Want a [gold]pouch[/gold]? They are full of [gold]rare things[/gold] you will barely find outside!\"",
					new EventOptionLoc("SMALL_POUCH", "Pick a small one", "Pay [red]80[/red] [gold]Gold[/gold]. Gain a random [gold]Common Relic[/gold]."),
					new EventOptionLoc("SMALL_POUCH_LOCKED", "Pick a small one", "Requires [blue]80[/blue] [gold]Gold[/gold]."),
					new EventOptionLoc("LARGE_POUCH", "Pick a large one", "Pay [red]200[/red] [gold]Gold[/gold]. Gain [blue]3[/blue] random [gold]Common Relics[/gold]."),
					new EventOptionLoc("LARGE_POUCH_LOCKED", "Pick a large one", "Requires [blue]200[/blue] [gold]Gold[/gold]."),
					new EventOptionLoc("LEAVE", "Never mind", "Leave.")),
				new EventPageLoc(
					"SMALL_POUCH",
					"You open the [gold]pouch[/gold], glance at the treasure inside, and immediately tuck it into your pack.\n\nThe [orange]statues[/orange] do not seem afraid of people. They even come closer to ask you about members of [orange]Sui's office[/orange]. Some have risen to important posts, while many more have long passed away. Perhaps the only ones who still remember their voices and faces are these [pink]creations untouched by time[/pink]."),
				new EventPageLoc(
					"LARGE_POUCH",
					"You open the [gold]large pouch[/gold] and show the statues the treasures inside. While the statues [orange]appraise and discuss them[/orange], the young [gold]Liberi[/gold] prepares another large pouch and chats with your group.\n\nIn the past, she was a small vendor outside the garden. Now she comes and goes, doing business both ways. When the garden opens, she sells its [gold]\"souvenirs\"[/gold] to people. After it closes, she sells human goods to the garden's residents.\n\nAs for the money she earns... you notice her tossing [gold]coins[/gold] into her mouth like [green]snacks[/green]."),
				new EventPageLoc(
					"LEAVE",
					"You wave your hand, and the young [gold]Liberi[/gold] stops pestering you. She turns back to handle her [orange]business[/orange] with the [orange]statues[/orange]."))
		);
	}
}
