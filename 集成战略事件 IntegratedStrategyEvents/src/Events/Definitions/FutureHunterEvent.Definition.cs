
namespace IntegratedStrategyEvents.Events;

public sealed partial class FutureHunterEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"future_hunter.png",
			CreateLocalization,
			IntegratedStrategyEventLayoutProfile.StandardSlightlyRaisedForFourOptions);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"未来猎手",
				new EventPageLoc(
					InitialPage,
					"一位[red]独眼巨人[/red]撕开[purple]裂隙[/purple]，来到了你的面前。\n\n“你好。”她很有礼貌，言辞中却布满[red]荆棘[/red]。“你带着它，对吗？那把[gold]改变未来的钥匙[/gold]。”她虽没有看着你，但她的视线早已将你[jitter][purple]洞穿[/purple][/jitter]。\n\n“请交给我吧，我会带去改变，我会抹消我所见的[pink]悲惨未来[/pink]，请不要拒绝我。”",
					new EventOptionLoc("REFUSE", "拒绝她", "我还无法信任你。"),
					new EventOptionLoc("OFFER_HOPE", "拿出希望的画作。", "从你的牌组中选择[blue]2[/blue]张技能牌移除。"),
					new EventOptionLoc("OFFER_HOPE_LOCKED", "拿出希望的画作。", "需要至少[blue]2[/blue]张可移除的技能牌。"),
					new EventOptionLoc("OFFER_HATRED", "拿出仇恨的矛头", "从你的牌组中选择[blue]2[/blue]张攻击牌移除。"),
					new EventOptionLoc("OFFER_HATRED_LOCKED", "拿出仇恨的矛头", "需要至少[blue]2[/blue]张可移除的攻击牌。"),
					new EventOptionLoc("LEAVE", "转头就跑", "离开。")),
				new EventPageLoc(
					"REFUSE",
					"“抱歉，即使诉诸[red]暴力[/red]，我也一定要得到它。”\n\n[red]独眼巨人[/red]挥动法杖，消失在了[purple]迷雾[/purple]中。随后迷雾消散，一场[red]战争[/red]被映射到此处，将你们卷入其中。",
					new EventOptionLoc("FIGHT", "进入战斗", "遭遇一场艰难的战斗。")),
				new EventPageLoc(
					"OFFER_HOPE",
					"“[green]和平[/green]，[green]互助[/green]，多么美妙的幻象，内里蕴含的[gold]色彩[/gold]足够倾覆任何晦暗。\n\n“有了它，我就能将预见的[pink]黑暗未来[/pink]抹消在[purple]时光[/purple]中，谢谢你。”\n\n她为你留下了一段[green]生命的奥秘[/green]，随后就消失在了[purple]裂隙[/purple]中。"),
				new EventPageLoc(
					"OFFER_HATRED",
					"“无止境的[red]仇恨[/red]，[red]暴力[/red]的结晶。我可以用它屠戮所有预见的[pink]黑暗未来[/pink]，让独眼巨人所能预见的一切[red]苦难[/red]成为谎言。谢谢你。”\n\n她为你留下了一些已逝[pink]预言[/pink]的片段，随后就消失在了[purple]裂隙[/purple]中。"),
				new EventPageLoc(
					"LEAVE",
					"")),
			new EventLoc(
				"Future Hunter",
				new EventPageLoc(
					InitialPage,
					"A [red]Cyclops[/red] tears open a [purple]rift[/purple] and steps before you.\n\n\"Hello.\" She is very polite, yet every word is lined with [red]thorns[/red]. \"You carry it, yes? The [gold]key that changes the future[/gold].\" Though she does not look at you, her gaze has already [jitter][purple]pierced[/purple][/jitter] you through.\n\n\"Please give it to me. I will bring change. I will erase the [pink]tragic future[/pink] I have seen. Please do not refuse me.\"",
					new EventOptionLoc("REFUSE", "Refuse her", "I cannot trust you yet."),
					new EventOptionLoc("OFFER_HOPE", "Offer the painting of hope.", "Choose [blue]2[/blue] Skills from your deck to remove."),
					new EventOptionLoc("OFFER_HOPE_LOCKED", "Offer the painting of hope.", "Requires at least [blue]2[/blue] removable Skills."),
					new EventOptionLoc("OFFER_HATRED", "Offer the spearhead of hatred", "Choose [blue]2[/blue] Attacks from your deck to remove."),
					new EventOptionLoc("OFFER_HATRED_LOCKED", "Offer the spearhead of hatred", "Requires at least [blue]2[/blue] removable Attacks."),
					new EventOptionLoc("LEAVE", "Turn and run", "Leave.")),
				new EventPageLoc(
					"REFUSE",
					"\"I am sorry. Even if I must resort to [red]violence[/red], I will obtain it.\"\n\nThe [red]Cyclops[/red] waves her staff and vanishes into [purple]mist[/purple]. Then the mist scatters, and a [red]war[/red] is reflected into this place, pulling you in.",
					new EventOptionLoc("FIGHT", "Enter combat", "Encounter a difficult fight.")),
				new EventPageLoc(
					"OFFER_HOPE",
					"\"[green]Peace[/green]. [green]Mutual aid[/green]. What a beautiful illusion. The [gold]colors[/gold] within are enough to overturn any gloom.\n\n\"With this, I can erase the [pink]dark future[/pink] I have foreseen from [purple]time[/purple] itself. Thank you.\"\n\nShe leaves you a secret of [green]life[/green], then disappears into the [purple]rift[/purple]."),
				new EventPageLoc(
					"OFFER_HATRED",
					"\"Endless [red]hatred[/red], a crystallization of [red]violence[/red]. I can use it to slaughter every [pink]dark future[/pink] I have foreseen, turning all [red]suffering[/red] a Cyclops can see into lies. Thank you.\"\n\nShe leaves you fragments of departed [pink]prophecies[/pink], then disappears into the [purple]rift[/purple]."),
				new EventPageLoc(
					"LEAVE",
					""))
		);
	}
}
