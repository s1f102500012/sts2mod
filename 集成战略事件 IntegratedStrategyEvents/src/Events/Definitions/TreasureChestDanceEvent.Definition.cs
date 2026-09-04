
namespace IntegratedStrategyEvents.Events;

public sealed partial class TreasureChestDanceEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"treasure_chest_dance.png",
			CreateLocalization,
			IntegratedStrategyEventLayoutProfile.LeftMedium);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"宝箱之舞",
				new EventPageLoc(
					InitialPage,
					"一群[purple]萨卡兹[/purple]正围着各式各样的[gold]宝箱[/gold][sine][orange]唱歌跳舞[/orange][/sine]，其中有几个宝箱正随着萨卡兹们的舞蹈一开一合。\n\n他们似乎没什么敌意，或许你可以和他们一起唱唱跳跳，放松一下心情。其中一位舞者跳着跳着便凑到了你身边，想要拉着你一起去[orange]跳舞[/orange]。",
					new EventOptionLoc("DANCE", "我去！", "我喜欢跳舞！"),
					new EventOptionLoc("LEAVE", "还是算了", "多一事不如少一事。")),
				new EventPageLoc(
					"DANCE",
					"你被舞者拉近[gold]宝箱堆[/gold]，看到宝箱里有一只正向着[gold]金币[/gold]模样拟态的[red]恐鱼[/red]。\n\n你突然意识到，这群萨卡兹极有可能是[jitter][red]深海教徒[/red][/jitter]！当你回头时，萨卡兹们都已取出武器，[sine][purple]不怀好意[/purple][/sine]地盯着你。",
					new EventOptionLoc("FIGHT", "进入战斗", "遭遇一场艰难的战斗。")),
				new EventPageLoc(
					"LEAVE",
					"你躲过舞者伸出的手，只是远远看着萨卡兹们[sine][orange]舞蹈[/orange][/sine]。\n\n你不想去深究为什么[purple]萨卡兹[/purple]会出现在[orange]杀戮尖塔[/orange]这样的蠢问题。你只想看看别人跳舞放松身心，然后继续面对路途上的[b]难题[/b]。")),
			new EventLoc(
				"Treasure Chest Dance",
				new EventPageLoc(
					InitialPage,
					"A group of [purple]Sarkaz[/purple] are [sine][orange]singing and dancing[/orange][/sine] around all kinds of [gold]treasure chests[/gold]. Several of the chests open and close in rhythm with their dance.\n\nThey seem to mean no harm. Perhaps you could sing and dance with them to relax. One dancer drifts toward you mid-step, reaching out to pull you into the [orange]dance[/orange].",
					new EventOptionLoc("DANCE", "I'm in!", "I like dancing!"),
					new EventOptionLoc("LEAVE", "Never mind", "Better to avoid extra trouble.")),
				new EventPageLoc(
					"DANCE",
					"The dancer pulls you close to the [gold]pile of chests[/gold], and inside one chest you spot a [red]seaborn beast[/red] mimicking the shape of [gold]coins[/gold].\n\nYou suddenly realize these Sarkaz may be [jitter][red]cultists of the deep[/red][/jitter]. When you turn back, they have all drawn weapons and are watching you with [sine][purple]malice[/purple][/sine].",
					new EventOptionLoc("FIGHT", "Enter combat", "Encounter a difficult fight.")),
				new EventPageLoc(
					"LEAVE",
					"You dodge the dancer's outstretched hand and simply watch the Sarkaz [sine][orange]dance[/orange][/sine] from a distance.\n\nYou do not want to dwell on the foolish question of why [purple]Sarkaz[/purple] are in [orange]Iberia[/orange]. You only want to relax by watching others dance, then continue facing the [b]problems[/b] on the road."))
		);
	}
}
