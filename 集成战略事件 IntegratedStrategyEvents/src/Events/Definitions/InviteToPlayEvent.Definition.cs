
namespace IntegratedStrategyEvents.Events;

public sealed partial class InviteToPlayEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } = IntegratedStrategyEventDefinition.ForEventPortrait("invite_to_play.png", CreateLocalization);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"请君入戏",
				new EventPageLoc(
					InitialPage,
					"[aqua]银白的冰原[/aqua]上搭起了[red]猩红色的帐篷[/red]，不知内情的人们掀开帘子，发现[sine][purple]一幕戏剧[/purple][/sine]正待上演。",
					new EventOptionLoc("WATCH", "看一会吧", "就当是休息了。"),
					new EventOptionLoc("WALLET_LOST", "钱包掉了", "失去[red]60[/red][gold]金币[/gold]。获得一次稀有卡牌奖励。"),
					new EventOptionLoc("WALLET_LOST_LOCKED", "钱包掉了", "需要[blue]60[/blue][gold]金币[/gold]。"),
					new EventOptionLoc("LEAVE", "一看就不对劲", "多一事不如少一事。")),
				new EventPageLoc(
					"WATCH",
					"这里上演的剧目叫做[b]《[purple]失落与遗忘[/purple]》[/b]，讲述尖塔的一对[red]双子怪物[/red]惨死的故事，真是恶趣味……\n\n等等，那对演员怎么把[red]武器[/red]对着观众？……伴舞们怎么也从舞台上跳下来了？……[jitter][red]你们想要干什么？[/red][/jitter]",
					new EventOptionLoc("FIGHT", "进入战斗", "遭遇一场特殊的战斗。")),
				new EventPageLoc(
					"WALLET_LOST",
					"在你准备进去看戏前，你突然发现自己的[red]钱包不见了[/red]。\n\n你在帐篷外找了许久，却只发现了一块[purple]奇怪的木刻[/purple]。当你回头时，不远处的帐篷已经[jitter][red]烧了起来[/red][/jitter]，[red]无人生还[/red]。"),
				new EventPageLoc(
					"LEAVE",
					"哪有什么[orange]剧团[/orange]会跑到人烟稀少的[aqua]高塔[/aqua]来演出戏剧呢？\n\n还是离他们远点吧。")),
			new EventLoc(
				"Enter the Play",
				new EventPageLoc(
					InitialPage,
					"Upon the [aqua]silvery icefield[/aqua] stands a [red]crimson tent[/red]. People who know nothing of it draw back the curtain and find that [sine][purple]a play[/purple][/sine] is about to begin.",
					new EventOptionLoc("WATCH", "Watch for a while", "Treat it as a rest."),
					new EventOptionLoc("WALLET_LOST", "Lost your wallet", "Lose [red]60[/red] [gold]Gold[/gold]. Gain a Rare card reward."),
					new EventOptionLoc("WALLET_LOST_LOCKED", "Lost your wallet", "Requires [blue]60[/blue] [gold]Gold[/gold]."),
					new EventOptionLoc("LEAVE", "This is clearly wrong", "Better safe than sorry.")),
				new EventPageLoc(
					"WATCH",
					"The play performed here is called [b][purple]The Lost and Forgotten[/purple][/b]. It tells the story of a pair of twin monsters in the Spire who die tragically. What poor taste...\n\nWait, why are the actors pointing their [red]weapons[/red] at the audience? Why are the dancers jumping down from the stage too? [jitter][red]What are you trying to do?[/red][/jitter]",
					new EventOptionLoc("FIGHT", "Enter combat", "Encounter a special fight.")),
				new EventPageLoc(
					"WALLET_LOST",
					"Before you enter the tent to watch the play, you suddenly realize your [red]wallet is gone[/red].\n\nYou search outside the tent for a long time, but only find a [purple]strange carving[/purple]. When you turn back, the tent nearby is already [jitter][red]burning[/red][/jitter]. [red]No one survives[/red]."),
				new EventPageLoc(
					"LEAVE",
					"What [orange]troupe[/orange] would come to an unpopulated [aqua]tower[/aqua] to perform a play?\n\nBetter keep away from them."))
		);
	}
}
