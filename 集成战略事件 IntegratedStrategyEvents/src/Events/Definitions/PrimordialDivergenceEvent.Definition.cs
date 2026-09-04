
namespace IntegratedStrategyEvents.Events;

public sealed partial class PrimordialDivergenceEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"primordial_divergence.png",
			CreateLocalization,
			IntegratedStrategyEventLayoutProfile.LeftMediumSlightlyRaisedForFourOptions,
			AlignHoverTipsRight: true);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"原初异途",
				new EventPageLoc(
					InitialPage,
					"[purple]遭逐的猎手[/purple]蹒跚游荡，[aqua]银色山脉[/aqua]危机四伏，他似要[jitter][red]死在这绝地[/red][/jitter]了。\n\n然在那[red]金属杀阵[/red]间，终是有着什么，冥冥中要引他出这[red]死路[/red]。\n\n那是——",
					new EventOptionLoc("ODDITY", "一件奇物", "你愿为故事舍弃什么？"),
					new EventOptionLoc("BLACK_BOX", "一个方盒", "你愿为故事创造什么？"),
					new EventOptionLoc("LOTUS_PETAL", "一片莲瓣", "你愿为故事追寻什么？"),
					new EventOptionLoc("METAL", "一堆金属", "你愿为故事遗失什么？")),
				new EventPageLoc(
					"ODDITY",
					"猎手取过[gold]奇物[/gold]，继续向[aqua]山脉深处[/aqua]进发......",
					new EventOptionLoc("FATE_IS_WONDROUS", "命运真奇妙", "获得[blue]1[/blue]件随机[gold]遗物[/gold]。")),
				new EventPageLoc(
					"FATE_IS_WONDROUS",
					"你放下的[gold]遗物[/gold]在一瞬间成为了另外一件，而整件事的原委却跨越了千百年。\n\n那为你完成置换的[jitter][purple]萨……提卡兹[/purple][/jitter]不是别人，正是被称为[red]远逐者[/red]的第一位[red]魔王[/red]。"),
				new EventPageLoc(
					"BLACK_BOX",
					"[purple]黑色方盒[/purple]显现[red]深红纹路[/red]，洁白光辉展于背脊头颅。漆黑猎手启迪之初，[gold]秩序[/gold]显现，乐园将成。\n\n于是他带走方盒折返回部落，点亮同胞，[sine][aqua]共感共荣[/aqua][/sine]......最终，[gold]卡兹戴尔圣城[/gold]屹立于大地，猎手曾经领受启示的地方，安放着他的[orange]圣角[/orange]。",
					new EventOptionLoc("HORN_OR_GUN", "那是一只角，还是一把铳？", "获得[gold]先知长角[/gold]。")),
				new EventPageLoc(
					"HORN_OR_GUN",
					"那只是一只再普通不过的[purple]萨卡兹角[/purple]，但你很清楚，它不属于萨卡兹，上面有[red]世仇[/red]的气味，你绝不会弄错。\n\n可萨卡兹怎会是[orange]萨科塔[/orange]？带着疑惑，你暂且把这只角留在了身边。"),
				new EventPageLoc(
					"LOTUS_PETAL",
					"浮于水面的[pink]花瓣[/pink]指向东方，隐隐昭示着[purple]提卡兹[/purple]解决困境的方向。然而猎手所能见到的，只有石钵中的清水。\n\n他饮尽清水，继续向[aqua]山脉深处[/aqua]进发......雨过天晴，石钵已重新盈满清水，花瓣悬浮，等待着下一位访客。",
					new EventOptionLoc("TEEKAZ_ANASA", "一念提卡兹，一念阿纳萨", "获得[gold]片瓣[/gold]。")),
				new EventPageLoc(
					"TEEKAZ_ANASA",
					"你对那些东方的[purple]萨卡兹同族[/purple]并不了解，你所知晓的，只有“[jitter][aqua]青色怒火[/aqua][/jitter]”奎隆东行后殒身异地，一部分萨卡兹就此在东方安了家。\n\n为了进一步探索这段[gold]故事[/gold]的可能性，你托起石钵，走向东方。"),
				new EventPageLoc(
					"METAL",
					"破损的[red]金属[/red]无法射出火焰，于是猎手便得以安然深入山脉。\n\n往后的日子，他将戴冠为王。",
					new EventOptionLoc("LEAVE", "离开", ""))),
			new EventLoc(
				"Primordial Divergence",
				new EventPageLoc(
					InitialPage,
					"The [purple]exiled hunter[/purple] wanders unsteadily through the perilous [aqua]silver mountains[/aqua]. It seems he will [jitter][red]die in this dead land[/red][/jitter].\n\nYet amid that [red]metal killing field[/red], something still waits, fated to guide him away from this [red]dead end[/red].\n\nIt is...",
					new EventOptionLoc("ODDITY", "A strange object", "What will you abandon for the story?"),
					new EventOptionLoc("BLACK_BOX", "A black box", "What will you create for the story?"),
					new EventOptionLoc("LOTUS_PETAL", "A lotus petal", "What will you pursue for the story?"),
					new EventOptionLoc("METAL", "A heap of metal", "What will you lose for the story?")),
				new EventPageLoc(
					"ODDITY",
					"The hunter takes the [gold]strange object[/gold] and continues deeper into the [aqua]mountains[/aqua]...",
					new EventOptionLoc("FATE_IS_WONDROUS", "Fate is wondrous", "Gain [blue]1[/blue] random [gold]Relic[/gold].")),
				new EventPageLoc(
					"FATE_IS_WONDROUS",
					"The [gold]relic[/gold] you set down becomes another in an instant, while the reason behind the exchange spans centuries.\n\nThe [jitter][purple]Sar... Teekaz[/purple][/jitter] who completes the replacement for you is none other than the first [red]Demon King[/red], known as the [red]Far-Exiled[/red]."),
				new EventPageLoc(
					"BLACK_BOX",
					"[purple]Crimson lines[/purple] appear on the black box, and white radiance unfolds over spine and skull. At the first enlightenment of the dark hunter, [gold]order[/gold] appears, and paradise begins to take shape.\n\nSo he takes the box and returns to his tribe, illuminating his kin in [sine][aqua]shared feeling and shared glory[/aqua][/sine]... At last, the holy city of [gold]Kazdel[/gold] stands upon the land. In the place where the hunter once received revelation rests his [orange]holy horn[/orange].",
					new EventOptionLoc("HORN_OR_GUN", "Is it a horn, or a gun?", "Gain [gold]Prophet's Horn[/gold].")),
				new EventPageLoc(
					"HORN_OR_GUN",
					"It is only an ordinary [purple]Sarkaz horn[/purple], but you know clearly that it does not belong to the Sarkaz. It carries the scent of a [red]blood feud[/red], and you would never mistake it.\n\nBut how could a Sarkaz be [orange]Sankta[/orange]? With that question in mind, you keep the horn for now."),
				new EventPageLoc(
					"LOTUS_PETAL",
					"The [pink]petal[/pink] floating on the water points east, faintly showing the [purple]Teekaz[/purple] a way out. Yet all the hunter can see is the clear water in the stone bowl.\n\nHe drains the water and continues deeper into the [aqua]mountains[/aqua]... After the rain clears, the bowl is full once more, the petal floating, waiting for the next visitor.",
					new EventOptionLoc("TEEKAZ_ANASA", "One thought Teekaz, one thought Anasa", "Gain [gold]Petal[/gold].")),
				new EventPageLoc(
					"TEEKAZ_ANASA",
					"You know little of those eastern [purple]Sarkaz kin[/purple]. What you know is only that \"[jitter][aqua]Cyan Wrath[/aqua][/jitter]\" Qiu Long went east and died in a foreign land, and that some Sarkaz made their home there.\n\nTo explore the possibility of this [gold]story[/gold] further, you lift the stone bowl and walk east."),
				new EventPageLoc(
					"METAL",
					"The damaged [red]metal[/red] can no longer shoot flame, allowing the hunter to pass safely deeper into the mountains.\n\nIn the days to come, he will wear a crown and become king.",
					new EventOptionLoc("LEAVE", "Leave", "")))
		);
	}
}
