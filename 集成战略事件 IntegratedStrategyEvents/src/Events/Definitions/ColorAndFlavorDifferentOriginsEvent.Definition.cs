namespace IntegratedStrategyEvents.Events;

public sealed partial class ColorAndFlavorDifferentOriginsEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } = IntegratedStrategyEventDefinition.ForEventPortrait(
		"color_and_flavor_different_origins.png",
		CreateLocalization,
		IntegratedStrategyEventLayoutProfile.Standard);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"色味不同源",
				new EventPageLoc(
					InitialPage,
					"灌木丛里[jitter][red]一片狼藉[/red][/jitter]，显然此处刚刚结束了一场[orange]动物宴会[/orange]。你闻到一股[sine][purple]奇特的味道[/purple][/sine]。\n\n摊在地面的叶子上摆放着[red]红色的果实[/red]和[aqua]白色的肉类[/aqua]，哪怕只是动物啃食过的残羹冷炙，也足够[gold]不劳而获[/gold]者果腹。",
					new EventOptionLoc("TAKE_APPLE", "拿走看上去像是苹果的食物", "获得[green]6[/green]点最大生命。"),
					new EventOptionLoc("TAKE_FISH", "拿走看上去像是鱼的食物", "回复[green]12[/green]点生命。"),
					new EventOptionLoc("LEAVE", "我不饿", "离开。")),
				new EventPageLoc(
					"TAKE_APPLE",
					"散发着[sine][purple]朽木味道[/purple][/sine]的苹果[jitter][purple]麻痹了你的舌头[/purple][/jitter]，你用牙齿刮了刮舌苔，虽然味道奇怪，但好歹[green]垫了垫肚子[/green]。"),
				new EventPageLoc(
					"TAKE_FISH",
					"[aqua]生鱼肉[/aqua]并不滑嫩，也不再新鲜，你尝到了一股[purple]酸涩的味道[/purple]，但口感不重要，可以[green]补充体能[/green]就已经足够。"),
				new EventPageLoc(
					"LEAVE",
					"叶子上的残羹显然没有勾起你的食欲，[sine][purple]刺鼻的味道[/purple][/sine]让你却步。离开前你又看了一眼，食物正在[jitter][purple]褪色，蠕动着离开[/purple][/jitter]盛放它的叶子。")),
			new EventLoc(
				"Colors and Flavors from Different Sources",
				new EventPageLoc(
					InitialPage,
					"The shrubs are [jitter][red]a complete mess[/red][/jitter]; an [orange]animal feast[/orange] clearly ended here moments ago. You catch a [sine][purple]peculiar smell[/purple][/sine].\n\n[red]Red fruit[/red] and [aqua]white meat[/aqua] lie on leaves spread across the ground. Even if they are only leftovers gnawed by animals, they are enough to fill the stomach of anyone willing to take a [gold]free meal[/gold].",
					new EventOptionLoc("TAKE_APPLE", "Take the food that looks like an apple", "Gain [green]6[/green] Max HP."),
					new EventOptionLoc("TAKE_FISH", "Take the food that looks like fish", "Heal [green]12[/green] HP."),
					new EventOptionLoc("LEAVE", "I'm not hungry", "Leave.")),
				new EventPageLoc(
					"TAKE_APPLE",
					"The apple, carrying a [sine][purple]rotten-wood smell[/purple][/sine], [jitter][purple]numbs your tongue[/purple][/jitter]. You scrape at the coating on your tongue with your teeth. Strange as it tastes, at least it [green]puts something in your stomach[/green]."),
				new EventPageLoc(
					"TAKE_FISH",
					"The [aqua]raw fish[/aqua] is neither tender nor fresh. You taste a [purple]sour astringency[/purple], but texture does not matter. Being able to [green]replenish your strength[/green] is enough."),
				new EventPageLoc(
					"LEAVE",
					"The leftovers on the leaves clearly fail to stir your appetite; the [sine][purple]pungent smell[/purple][/sine] makes you hesitate. Before leaving, you look back once more. The food is [jitter][purple]losing its color and writhing away[/purple][/jitter] from the leaves that held it."))
		);
	}
}
