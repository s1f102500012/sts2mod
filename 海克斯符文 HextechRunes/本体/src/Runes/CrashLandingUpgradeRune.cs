namespace HextechRunes;

public sealed class CrashLandingUpgradeRune : CardUpgradeRuneBase<CrashLanding>
{
	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromCard<CrashLanding>(),
		HoverTipFactory.FromCard<CollisionCourse>()
	];

	protected override bool IsAvailableForCharacter(Player player)
	{
		return IsRegentPlayer(player);
	}

	internal static bool ShouldUseUpgradedPlay(CardModel card)
	{
		return card is CrashLanding && card.Owner?.GetRelic<CrashLandingUpgradeRune>() != null;
	}

	internal static async Task PlayUpgraded(PlayerChoiceContext choiceContext, CrashLanding card)
	{
		var combatState = card.CombatState;
		if (combatState == null)
		{
			return;
		}

		card.Owner.GetRelic<CrashLandingUpgradeRune>()?.Flash();
		HextechLog.Info($"[{ModInfo.Id}][CrashLanding] Upgraded play for {card.Owner.NetId}: hand={CardPile.GetCards(card.Owner, PileType.Hand).Count()}/{CardPile.MaxCardsInHand}");
		await DamageCmd.Attack(card.DynamicVars.Damage.BaseValue)
			.FromCardCompat(card)
			.TargetingAllOpponents(combatState)
			.WithHitFx("vfx/vfx_heavy_blunt", null, "heavy_attack.mp3")
			.WithHitVfxSpawnedAtBase()
			.Execute(choiceContext);

		int cardsToAdd = CardPile.MaxCardsInHand - CardPile.GetCards(card.Owner, PileType.Hand).Count();
		if (cardsToAdd <= 0)
		{
			return;
		}

		List<CardModel> collisionCourses = new();
		for (int i = 0; i < cardsToAdd; i++)
		{
			collisionCourses.Add(combatState.CreateCard<CollisionCourse>(card.Owner));
		}

		await CardPileCmd.AddGeneratedCardsToCombat(collisionCourses, PileType.Hand, card.Owner);
	}

	[HarmonyPatch(typeof(CrashLanding), "OnPlay", typeof(PlayerChoiceContext), typeof(CardPlay))]
	[HextechPatch("rune.crash-landing", "升级迫降", Rune = typeof(CrashLandingUpgradeRune))]
	private static class CrashLandingPatch
	{
		[HarmonyPrefix]
		[HarmonyPriority(Priority.Low)]
		private static bool Prefix(CrashLanding __instance, PlayerChoiceContext choiceContext, ref Task __result)
		{
			if (!CrashLandingUpgradeRune.ShouldUseUpgradedPlay(__instance))
			{
				return true;
			}

			__result = CrashLandingUpgradeRune.PlayUpgraded(choiceContext, __instance);
			return false;
		}
	}
}
