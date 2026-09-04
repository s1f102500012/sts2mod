
namespace IntegratedStrategyEvents.Events;

public sealed partial class SamiLanguageEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"sami_language.png",
			CreateLocalization,
			IntegratedStrategyEventLayoutProfile.LeftWide);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"萨米之语",
				new EventPageLoc(
					InitialPage,
					"“从[gold]语言学[/gold]角度破译当地人手中的[purple]芙尔妲密文板[/purple]并不是难事，难题在于，它们从何而来，是不是人为雕刻......等等，那是什么[sine][aqua]声音[/aqua][/sine]？”\n\n科考队中的专业人士正向你提议设置[aqua]摄像机[/aqua]以监控当地[orange]萨满[/orange]的行动，因此你们共同目睹了这一刻：眼前[green]古木[/green]的表皮在[jitter][red]巨响[/red][/jitter]中开裂剥落，错综复杂的[sine][purple]纹路[/purple][/sine]浮现其上。",
					new EventOptionLoc("PEEL_OFF", "揭下它", "随机移除[blue]2[/blue]张牌。"),
					new EventOptionLoc("PEEL_OFF_LOCKED", "揭下它", "需要至少[blue]2[/blue]张可移除的牌。"),
					new EventOptionLoc("DECLARE", "宣示它", "失去[red]8[/red]点最大生命。从你的[gold]牌组[/gold]中选择[blue]2[/blue]张牌[purple]变化[/purple]。"),
					new EventOptionLoc("DECLARE_LOCKED", "宣示它", "需要至少[red]9[/red]点最大生命，且至少[blue]2[/blue]张可变化牌。"),
					new EventOptionLoc("LEAVE", "离开", "多一事不如少一事。")),
				new EventPageLoc(
					"PEEL_OFF",
					"部族的[orange]萨满[/orange]为你解读了[sine][green]自然的密文[/green][/sine]，同时你还获知了一些传说：\n\n“神灵藏身万物，最初的密文是遍布山野的[sine][aqua]哀鸣[/aqua][/sine]”；“[orange]萨米[/orange]曾经预兆自身，显现被[red]火焰熔化[/red]的山脉与[aqua]冰原[/aqua]，祂们的战争远在万物醒来之前，且还将[jitter][red]再来[/red][/jitter]”。\n\n你很庆幸。只凭破译[gold]语言[/gold]或[purple]符号[/purple]，你们是无法得到这些信息的。"),
				new EventPageLoc(
					"DECLARE",
					"[red]史尔特尔[/red]来到[green]古木[/green]边，为它带去了无可抗拒的[jitter][red]火焰[/red][/jitter]。\n\n在噼啪声中，不止一块[purple]密文板[/purple]带着灼痕落向地面。“火焰、战争、我的记忆......”她喃喃自语，看着古木上的火焰在[aqua]风雪[/aqua]中熄灭。\n\n随后，她拿出笔记，记录了些内容，又划去了一条备注。"),
				new EventPageLoc(
					"LEAVE",
					"你们没有揭下那块[green]树皮[/green]，也没有在古木前宣示任何结论。\n\n[aqua]摄像机[/aqua]仍在风雪中运转，记录那些[sine][purple]纹路[/purple][/sine]在树身上逐渐隐没。或许有些语言只属于山林与[orange]萨满[/orange]，在被翻译之前，它们已经完成了[gold]告诫[/gold]。")),
			new EventLoc(
				"Language of Sami",
				new EventPageLoc(
					InitialPage,
					"\"From a [gold]linguistic[/gold] standpoint, deciphering the [purple]Fulda cipher boards[/purple] held by the locals is not difficult. The hard question is where they came from, and whether they were carved by human hands... wait, what is that [sine][aqua]sound[/aqua][/sine]?\"\n\nA specialist in the expedition team is proposing that you set up [aqua]cameras[/aqua] to monitor the local [orange]shamans[/orange]. Because of that, you witness the moment together: the bark of the [green]ancient tree[/green] before you cracks and peels away in a [jitter][red]thunderous noise[/red][/jitter], revealing [sine][purple]intricate patterns[/purple][/sine] beneath.",
					new EventOptionLoc("PEEL_OFF", "Peel it off", "Randomly remove [blue]2[/blue] cards."),
					new EventOptionLoc("PEEL_OFF_LOCKED", "Peel it off", "Requires at least [blue]2[/blue] removable cards."),
					new EventOptionLoc("DECLARE", "Declare it", "Lose [red]8[/red] Max HP. Choose [blue]2[/blue] cards from your [gold]deck[/gold] to [purple]Transform[/purple]."),
					new EventOptionLoc("DECLARE_LOCKED", "Declare it", "Requires at least [red]9[/red] Max HP and at least [blue]2[/blue] transformable cards."),
					new EventOptionLoc("LEAVE", "Leave", "Better not make trouble.")),
				new EventPageLoc(
					"PEEL_OFF",
					"The tribe's [orange]shaman[/orange] interprets the [sine][green]cipher of nature[/green][/sine] for you, and you learn several legends as well:\n\n\"Gods hide within all things. The first ciphers were [sine][aqua]wails[/aqua][/sine] spread across the mountains and wilds.\" \"[orange]Sami[/orange] once foretold itself, showing mountains melted by [red]flame[/red] and [aqua]ice fields[/aqua]. Their war began before all things awoke, and it will [jitter][red]come again[/red][/jitter].\"\n\nYou are relieved. If you had relied only on deciphering [gold]language[/gold] or [purple]symbols[/purple], you would never have obtained this information."),
				new EventPageLoc(
					"DECLARE",
					"[red]Surtr[/red] approaches the [green]ancient tree[/green] and brings it irresistible [jitter][red]flame[/red][/jitter].\n\nAmid the crackling, more than one [purple]cipher board[/purple] falls to the ground, scorched. \"Fire, war, my memories...\" she murmurs, watching the flames on the ancient tree die out in the [aqua]wind and snow[/aqua].\n\nThen she takes out her notes, writes down a few lines, and crosses out a remark."),
				new EventPageLoc(
					"LEAVE",
					"You do not peel away that piece of [green]bark[/green], nor do you declare any conclusion before the ancient tree.\n\nThe [aqua]camera[/aqua] keeps running in the wind and snow, recording the [sine][purple]patterns[/purple][/sine] as they gradually sink back into the trunk. Perhaps some languages belong only to the forests and [orange]shamans[/orange]. Before they can be translated, they have already delivered their [gold]warning[/gold]."))
		);
	}
}
