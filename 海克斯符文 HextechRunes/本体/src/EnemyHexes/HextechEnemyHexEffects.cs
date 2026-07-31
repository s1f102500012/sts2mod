namespace HextechRunes;

internal static class HextechEnemyHexEffects
{
	private static readonly IReadOnlyList<HextechEnemyHexEffect> OrderedEffects = CreateOrderedEffects(
	[
		new SlapEnemyHex(),
		new EscapePlanEnemyHex(),
		new HeavyHitterEnemyHex(),
		new BigStrengthEnemyHex(),
		new TormentorEnemyHex(),
		new ProtectiveVeilEnemyHex(),
		new RepulsorEnemyHex(),
		new ThornmailEnemyHex(),
		new LightEmUpEnemyHex(),
		new MountainSoulEnemyHex(),
		new FirstAidKitEnemyHex(),
		new SpeedDemonEnemyHex(),
		new FrostWraithEnemyHex(),
		new BloodPactEnemyHex(),
		new StartupRoutineEnemyHex(),
		new DizzySpinningEnemyHex(),
		new BrutalForceEnemyHex(),
		new ZealotEnemyHex(),
		new SwiftAndSafeEnemyHex(),
		new BloodIdolEnemyHex(),
		new SturdyEnemyHex(),
		new DawnbringersResolveEnemyHex(),
		new ShrinkRayEnemyHex(),
		new FirebrandEnemyHex(),
		new SuperBrainEnemyHex(),
		new NightstalkingEnemyHex(),
		new AstralBodyEnemyHex(),
		new TankEngineEnemyHex(),
		new ShrinkEngineEnemyHex(),
		new GetExcitedEnemyHex(),
		new TwiceThriceEnemyHex(),
		new LoopEnemyHex(),
		new ServantMasterEnemyHex(),
		new CuttingEdgeAlchemistEnemyHex(),
		new DivineInterventionEnemyHex(),
		new SonataEnemyHex(),
		new DevilsDanceEnemyHex(),
		new SkulkingColonyEnemyHex(),
		new DoomsdayEnemyHex(),
		new WarmogsSpiritEnemyHex(),
		new BloodArmorEnemyHex(),
		new JinlianBoxEnemyHex(),
		new MirrorReflectionEnemyHex(),
		new BlueCandleMedkitEnemyHex(),
		new TanksShieldEnemyHex(),
		new PorcupineEnemyHex(),
		new MonarchsGazeEnemyHex(),
		new OmegaEnemyHex(),
		new ManipulateRealityEnemyHex(),
		new NatureIsHealingEnemyHex(),
		new ArchmageEnemyHex(),
		new PhantasmalGardenerEnemyHex(),
		new AncientWineEnemyHex(),
		new CourageOfColossusEnemyHex(),
		new GlassCannonEnemyHex(),
		new GoliathEnemyHex(),
		new QueenEnemyHex(),
		new HandOfBaronEnemyHex(),
		new CantTouchThisEnemyHex(),
		new MasterOfDualityEnemyHex(),
		new GoldrendEnemyHex(),
		new FeelTheBurnEnemyHex(),
		new BackToBasicsEnemyHex(),
		new MadScientistEnemyHex(),
		new FeyMagicEnemyHex(),
		new FinalFormEnemyHex(),
		new UnmovableMountainEnemyHex(),
		new MikaelsBlessingEnemyHex(),
		new ClownCollegeEnemyHex(),
		new SingularityAIEnemyHex(),
		new ProteinShakeEnemyHex(),
		new GoldenSpatulaEnemyHex(),
		new HailToTheKingEnemyHex(),
		new EightPennyGateEnemyHex(),
		new DuffsVintageEnemyHex(),
		new HastyScribbleEnemyHex(),
		new LagavulinMatriarchEnemyHex(),
		new ShoulderVakuEnemyHex(),
		new UpgradeEnemyHex(),
		new NearDeathFeastEnemyHex(),
		new ExoskeletonEnemyHex(),
		new SerpentsFangEnemyHex(),
		new PandorasBoxEnemyHex(),
		new ForbiddenGrimoireEnemyHex(),
		new TezcatarasMercyEnemyHex(),
		new ArcanePunchEnemyHex(),
		new TestSubjectEnemyHex(),
		new MysteryEnemyHex(),
		new MindOverMatterEnemyHex(),
		new CompensationEnemyHex(),
		new OminousPactEnemyHex(),
		new SolidTimeEnemyHex(),
		new ForgottenSoulEnemyHex(),
		new CerberusEnemyHex(),
		new OmniDragonSoulEnemyHex(),
		new BlankCheckEnemyHex(),
		new CorrosionEnemyHex(),
		new BrutalityEnemyHex(),
		new JudicatorEnemyHex(),
		new SoulEaterEnemyHex(),
		new DeathHarvestEnemyHex(),
		new GiantSlayerEnemyHex(),
		new DualWieldEnemyHex(),
		new JeweledGauntletEnemyHex(),
		new LeafSlimeEnemyHex(),
		new ShrinkerBeetleEnemyHex(),
		new InkletEnemyHex(),
		new PhrogParasiteEnemyHex(),
		new VantomEnemyHex(),
		new AeonglassEnemyHex(),
		new TheLostEnemyHex(),
		new TheForgottenEnemyHex(),
		new SlimedBerserkerEnemyHex(),
		new GlobeHeadEnemyHex(),
		new MyteEnemyHex(),
		new FossilStalkerEnemyHex(),
		new ByrdonisEnemyHex(),
		new TungstenRodEnemyHex(),
		new HundredRefinementsEnemyHex(),
		new VitalitySurgeEnemyHex(),
		new AncientStatueEnemyHex(),
		new IInspectEnemyHex(),
		new IGripEnemyHex()
	]);

	internal static IEnumerable<HextechEnemyHexEffect> GetActive(HextechMayhemModifier modifier)
	{
		foreach (HextechEnemyHexEffect effect in OrderedEffects)
		{
			if (modifier.HasActiveMonsterHex(effect.Kind))
			{
				yield return effect;
			}
		}
	}

	internal static bool HasActiveAttackCostPreviewEffect(HextechMayhemModifier modifier)
	{
		return GetActive(modifier).Any(static effect => effect.AffectsPlayerAttackCostPreview);
	}

	internal static IReadOnlySet<MonsterHexKind> RegisteredKinds => OrderedEffects
		.Select(static effect => effect.Kind)
		.ToHashSet();

	internal static void ResetAllRunScopedState()
	{
		foreach (HextechEnemyHexEffect effect in OrderedEffects)
		{
			effect.ResetRunScopedState();
		}
	}

	private static IReadOnlyList<HextechEnemyHexEffect> CreateOrderedEffects(IReadOnlyList<HextechEnemyHexEffect> effects)
	{
		MonsterHexKind[] duplicateKinds = effects
			.GroupBy(static effect => effect.Kind)
			.Where(static group => group.Count() > 1)
			.Select(static group => group.Key)
			.ToArray();
		if (duplicateKinds.Length > 0)
		{
			throw new InvalidOperationException($"Duplicate enemy hex effects: {string.Join(", ", duplicateKinds)}");
		}

		IReadOnlySet<MonsterHexKind> registeredKinds = HextechContentRegistry.AllMonsterHexKinds;
		MonsterHexKind[] missingEffects = registeredKinds
			.Except(effects.Select(static effect => effect.Kind))
			.ToArray();
		if (missingEffects.Length > 0)
		{
			throw new InvalidOperationException($"Missing enemy hex effects: {string.Join(", ", missingEffects)}");
		}

		MonsterHexKind[] unknownEffects = effects
			.Select(static effect => effect.Kind)
			.Except(registeredKinds)
			.ToArray();
		if (unknownEffects.Length > 0)
		{
			throw new InvalidOperationException($"Enemy hex effects without content registrations: {string.Join(", ", unknownEffects)}");
		}

		return effects;
	}
}
