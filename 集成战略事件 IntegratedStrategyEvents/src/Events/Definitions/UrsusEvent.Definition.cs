
namespace IntegratedStrategyEvents.Events;

public sealed partial class UrsusEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } = IntegratedStrategyEventDefinition.ForEventPortrait("ursus.png", CreateLocalization);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"乌萨斯",
				new EventPageLoc(
					InitialPage,
					"一座大概曾是[red]裂兽巢穴[/red]的洞窟中回荡着[orange]笑声[/orange]、叫声和[jitter][red][i]乌萨斯粗口[/i][/red][/jitter]，一些像是[orange]新居民[/orange]的人显然正在享受生活。\n\n而在角落里，你能看到洞窟那被拳打脚踢后[sine][red]奄奄一息[/red][/sine]的“原主人”。",
					new EventOptionLoc("ASK", "为什么这里满地都是乌萨斯人？", "获得[green]8[/green]点最大生命。"),
					new EventOptionLoc("BRAWL", "显然这是某种占洞为王的竞赛！", "失去[red]8[/red]点生命。获得[blue]120[/blue][gold]金币[/gold]。"),
					new EventOptionLoc("BRAWL_LOCKED", "显然这是某种占洞为王的竞赛！", "需要至少[red]9[/red]点生命。"),
					new EventOptionLoc("LEAVE", "让他们疯吧，我们还有路要赶", "你不想和一大群醉鬼纠缠。")),
				new EventPageLoc(
					"ASK",
					"“[orange]满地都是乌萨斯人的地方，自然已经是乌萨斯啦！[/orange]”\n\n一个[orange]醉醺醺[/orange]的人回应了你，他们似乎打算把洞窟改造成某种[orange]领土管治办公室[/orange]。\n\n这些人随后塞给你许多[gold]伏特加包装纸[/gold]，说凭着这些“[sine][orange]官方殖民许可证[/orange][/sine]”，你能够获得国家的一切援助。"),
				new EventPageLoc(
					"BRAWL",
					"一个新的挑战者——[gold]你！[/gold]\n\n一番激烈的肉搏（主要是他们自己打自己）过后，乌萨斯们相当佩服你[red]干架[/red]的本事，塞给你很多“[gold]礼物[/gold]”，然后横七竖八地倒在地上打起了呼噜。\n\n整件事里最可怜的可能还是那只[red]裂兽[/red]，它又被[jitter][red]打了好多拳[/red][/jitter]。"),
				new EventPageLoc(
					"LEAVE",
					"你和同伴把洞窟抛在了身后。\n\n你十分确认，当地的[orange]萨米部族[/orange]不会对此事[sine][purple]置之不理[/purple][/sine]。")),
			new EventLoc(
				"Ursus",
				new EventPageLoc(
					InitialPage,
					"A cavern that was probably once a [red]beast den[/red] echoes with [orange]laughter[/orange], shouting, and [jitter][red][i]Ursus profanity[/i][/red][/jitter]. The people who seem to be its [orange]new residents[/orange] are clearly enjoying life.\n\nIn the corner, you can see the cavern's original owner, beaten and kicked until it is [sine][red]barely alive[/red][/sine].",
					new EventOptionLoc("ASK", "Why is this place full of Ursus?", "Gain [green]8[/green] Max HP."),
					new EventOptionLoc("BRAWL", "This is clearly a king-of-the-cave contest!", "Lose [red]8[/red] HP. Gain [blue]120[/blue] [gold]Gold[/gold]."),
					new EventOptionLoc("BRAWL_LOCKED", "This is clearly a king-of-the-cave contest!", "Requires at least [red]9[/red] HP."),
					new EventOptionLoc("LEAVE", "Let them go mad. We have a road to travel", "You do not want to get tangled up with a crowd of drunks.")),
				new EventPageLoc(
					"ASK",
					"\"[orange]A place covered in Ursus people is naturally Ursus already![/orange]\"\n\nAn [orange]drunk[/orange] answers you. They seem to plan on turning the cavern into some sort of [orange]territorial administration office[/orange].\n\nThen they stuff your hands with [gold]vodka wrappers[/gold], saying that with these \"[sine][orange]official colonization permits[/orange][/sine],\" you can receive every form of national aid."),
				new EventPageLoc(
					"BRAWL",
					"A new challenger: [gold]you![/gold]\n\nAfter an intense brawl, mostly involving them hitting each other, the Ursus are impressed by your [red]fighting[/red] and hand you plenty of \"[gold]gifts[/gold]\" before collapsing everywhere and snoring.\n\nThe most pitiful one in all of this may still be the [red]beast[/red]. It was [jitter][red]punched many more times[/red][/jitter]."),
				new EventPageLoc(
					"LEAVE",
					"You and your companions leave the cavern behind.\n\nYou are quite certain that the local [orange]Sami tribes[/orange] will not [sine][purple]ignore this[/purple][/sine]."))
		);
	}
}
