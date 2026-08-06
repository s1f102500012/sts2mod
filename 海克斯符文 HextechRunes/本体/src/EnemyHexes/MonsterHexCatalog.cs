using Godot;
using MegaCrit.Sts2.Core.Localization;

namespace HextechRunes;

internal static class MonsterHexCatalog
{
	private static readonly IReadOnlyList<MonsterHexKind> SilverMonsterHexes = HextechContentRegistry.SilverMonsterHexes;

	private static readonly IReadOnlyList<MonsterHexKind> GoldMonsterHexes = HextechContentRegistry.GoldMonsterHexes;

	private static readonly IReadOnlyList<MonsterHexKind> PrismaticMonsterHexes = HextechContentRegistry.PrismaticMonsterHexes;

	private static readonly IReadOnlyDictionary<MonsterHexKind, Type> MonsterHexIconRelicTypes = HextechContentRegistry.MonsterHexIconRelicTypes;

	private static readonly IReadOnlySet<MonsterHexKind> EnemyHexesWithBurnHoverTip =
		HextechContentRegistry.MonsterHexesWithBurnHoverTip;

	private static readonly IReadOnlyDictionary<MonsterHexKind, Type[]> EnemyHexPowerHoverTipTypes =
		new Dictionary<MonsterHexKind, Type[]>
		{
			[MonsterHexKind.Slap] = [typeof(StrengthPower)],
			[MonsterHexKind.Corrosion] = [typeof(FrailPower)],
			[MonsterHexKind.Brutality] = [typeof(VigorPower)],
			[MonsterHexKind.EscapePlan] = [typeof(ShrinkPower)],
			[MonsterHexKind.ProtectiveVeil] = [typeof(ArtifactPower)],
			[MonsterHexKind.Repulsor] = [typeof(SlipperyPower)],
			[MonsterHexKind.Thornmail] = [typeof(ThornsPower)],
			[MonsterHexKind.FrostWraith] = [typeof(HextechPlayerSlowPower)],
			[MonsterHexKind.DawnbringersResolve] = [typeof(RegenPower)],
			[MonsterHexKind.ShrinkRay] = [typeof(ShrinkPower)],
			[MonsterHexKind.SuperBrain] = [typeof(PlatingPower)],
			[MonsterHexKind.Nightstalking] = [typeof(StrengthPower), typeof(PaperCutsPower)],
			[MonsterHexKind.ShrinkEngine] = [typeof(SlipperyPower)],
			[MonsterHexKind.GetExcited] = [typeof(StrengthPower), typeof(PainfulStabsPower)],
			[MonsterHexKind.ServantMaster] = [typeof(IllusionPower)],
			[MonsterHexKind.DivineIntervention] = [typeof(IntangiblePower)],
			[MonsterHexKind.CourageOfColossus] = [typeof(PlatingPower)],
			[MonsterHexKind.HandOfBaron] = [typeof(ShrinkPower)],
			[MonsterHexKind.CantTouchThis] = [typeof(BufferPower)],
			[MonsterHexKind.MasterOfDuality] = [typeof(StrengthPower), typeof(DexterityPower)],
			[MonsterHexKind.FeelTheBurn] = [typeof(WeakPower), typeof(VulnerablePower)],
			[MonsterHexKind.FeyMagic] = [typeof(ShrinkPower), typeof(NoDrawPower)],
			[MonsterHexKind.UnmovableMountain] = [typeof(BarricadePower)],
			[MonsterHexKind.BloodPact] = [typeof(StrengthPower)],
			[MonsterHexKind.BrutalForce] = [typeof(StrengthPower)],
			[MonsterHexKind.Zealot] = [typeof(VigorPower)],
			[MonsterHexKind.BloodArmor] = [typeof(PlatingPower)],
			[MonsterHexKind.Doomsday] = [typeof(DisintegrationPower)],
			[MonsterHexKind.WarmogsSpirit] = [typeof(PlatingPower)],
			[MonsterHexKind.ClownCollege] = [typeof(SlipperyPower)],
			[MonsterHexKind.HailToTheKing] = [typeof(ArtifactPower), typeof(PlatingPower), typeof(RegenPower)],
			[MonsterHexKind.NearDeathFeast] = [typeof(StrengthPower)],
			[MonsterHexKind.SerpentsFang] = [typeof(PoisonPower)],
			[MonsterHexKind.Porcupine] = [typeof(ThornsPower)],
			[MonsterHexKind.MonarchsGaze] = [typeof(StrengthPower)],
			[MonsterHexKind.SwiftAndSafe] = [typeof(ArtifactPower)],
			[MonsterHexKind.ArcanePunch] = [typeof(TaintedPower)],
			[MonsterHexKind.Omega] = [typeof(DisintegrationPower)],
			[MonsterHexKind.OminousPact] = [typeof(DoomPower)],
			[MonsterHexKind.Cerberus] = [typeof(VigorPower)],
			[MonsterHexKind.OmniDragonSoul] = [typeof(WeakPower), typeof(FrailPower), typeof(VulnerablePower)],
			[MonsterHexKind.SkulkingColony] = [typeof(HardenedShellPower)],
			[MonsterHexKind.PhantasmalGardener] = [typeof(SkittishPower)],
			[MonsterHexKind.Queen] = [typeof(ChainsOfBindingPower)],
			[MonsterHexKind.LagavulinMatriarch] = [typeof(StrengthPower), typeof(DexterityPower)],
			[MonsterHexKind.Exoskeleton] = [typeof(HardToKillPower)],
			[MonsterHexKind.TestSubject] = [typeof(EnragePower), typeof(PainfulStabsPower)],
			[MonsterHexKind.ShrinkerBeetle] = [typeof(ShrinkPower)],
			[MonsterHexKind.Inklet] = [typeof(SlipperyPower)],
			[MonsterHexKind.Vantom] = [typeof(SlipperyPower)],
			[MonsterHexKind.TheLost] = [typeof(StrengthPower)],
			[MonsterHexKind.TheForgotten] = [typeof(DexterityPower)],
			[MonsterHexKind.Byrdonis] = [typeof(TerritorialPower)],
		};

	private static readonly Lazy<IReadOnlyDictionary<MonsterHexKind, HextechRarityTier>> RarityByMonsterHex = new(BuildRarityByMonsterHex);

	private static readonly Lazy<IReadOnlyDictionary<ModelId, MonsterHexKind>> MonsterHexByIconRelicId = new(BuildMonsterHexByIconRelicId);

	public static IReadOnlyList<MonsterHexKind> GetMonsterHexesForRarity(HextechRarityTier rarity)
	{
		return rarity switch
		{
			HextechRarityTier.Silver => SilverMonsterHexes,
			HextechRarityTier.Gold => GoldMonsterHexes,
			HextechRarityTier.Prismatic => PrismaticMonsterHexes,
			_ => Array.Empty<MonsterHexKind>()
		};
	}

	public static HextechRarityTier GetMonsterHexRarity(MonsterHexKind hex)
	{
		if (RarityByMonsterHex.Value.TryGetValue(hex, out HextechRarityTier rarity))
		{
			return rarity;
		}

		throw new ArgumentOutOfRangeException(nameof(hex), hex, "Unknown monster hex rarity.");
	}

	public static RelicModel GetIconRelicForMonsterHex(MonsterHexKind hex)
	{
		if (!MonsterHexIconRelicTypes.TryGetValue(hex, out Type? relicType))
		{
			throw new ArgumentOutOfRangeException(nameof(hex), hex, "Unknown monster hex icon relic.");
		}

		return ModelDb.GetById<RelicModel>(ModelDb.GetId(relicType));
	}

	public static bool TryGetMonsterHexKind(RelicModel relic, out MonsterHexKind hex)
	{
		ModelId id = relic.CanonicalInstance?.Id ?? relic.Id;
		return MonsterHexByIconRelicId.Value.TryGetValue(id, out hex);
	}

	/// <summary>
	/// 联机时会按玩家数（×N）放大的敌方 hex 层数：仅 mod 强制 ×玩家数 的 Slippery/Artifact。
	/// 每项是描述里的变量占位名 + 单人基数；联机时填 base×玩家数、单人填 base，让选择/hover 描述显示实际层数。
	/// 注意：SwiftAndSafe 的人工制品走裸 PowerCmd.Apply、不被缩放，故不在此列。
	/// </summary>
	private static readonly IReadOnlyDictionary<MonsterHexKind, (string Var, int Base)[]> PlayerCountScaledStacks =
		new Dictionary<MonsterHexKind, (string, int)[]>
		{
			[MonsterHexKind.Repulsor] = new[] { ("Stacks1", 1), ("Stacks2", 2), ("Stacks3", 3) },
			[MonsterHexKind.ClownCollege] = new[] { ("Stacks", 1) },
			[MonsterHexKind.CantTouchThis] = new[] { ("Stacks", 1) },
			[MonsterHexKind.ShrinkEngine] = new[] { ("Stacks", 1) },
			[MonsterHexKind.ProtectiveVeil] = new[] { ("Stacks1", 1), ("Stacks2", 2), ("Stacks3", 3) },
			[MonsterHexKind.HailToTheKing] = new[] { ("Stacks", 3) },
			[MonsterHexKind.Inklet] = new[] { ("Stacks1", 1), ("Stacks2", 2), ("Stacks3", 3) },
		};

	private static readonly IReadOnlyDictionary<MonsterHexKind, (string Var, int Base)> PlayerCountScaledThresholds =
		new Dictionary<MonsterHexKind, (string, int)>
		{
			[MonsterHexKind.HeavyHitter] = ("HpPerPercent", 15),
			[MonsterHexKind.VitalitySurge] = ("HpPerPercent", 20),
			[MonsterHexKind.ProteinShake] = ("HpPerPercent", 5),
		};

	public static string GetEnemyHexDescriptionFormatted(MonsterHexKind hex)
	{
		RelicModel relic = GetIconRelicForMonsterHex(hex);
		string localizationKey = GetEnemyHexDescriptionKey(relic);
		try
		{
			LocString locString = new("relics", localizationKey);
			if (PlayerCountScaledStacks.TryGetValue(hex, out (string Var, int Base)[]? scaledStacks))
			{
				int playerCount = GetScalingPlayerCount();
				foreach ((string varName, int baseValue) in scaledStacks)
				{
					locString.Add(varName, baseValue * playerCount);
				}
			}
			if (PlayerCountScaledThresholds.TryGetValue(hex, out (string Var, int Base) scaledThreshold))
			{
				locString.Add(scaledThreshold.Var, scaledThreshold.Base * GetScalingPlayerCount());
			}

			return locString.GetFormattedText();
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Enemy hex description fallback: hex={hex} key={localizationKey} error={ex.Message}");
			try
			{
				return relic.DynamicDescription.GetFormattedText();
			}
			catch (Exception fallbackEx)
			{
				Log.Warn($"[{ModInfo.Id}][Mayhem] Enemy hex description fallback failed: hex={hex} relic={(relic.CanonicalInstance?.Id ?? relic.Id).Entry} error={fallbackEx.Message}");
				return relic.Title.GetFormattedText();
			}
		}
	}

	public static IEnumerable<IHoverTip> GetEnemyHexHoverTips(MonsterHexKind hex)
	{
		RelicModel relic = GetIconRelicForMonsterHex(hex);
		HoverTip mainTip = new(relic.Title, GetEnemyHexDescriptionFormatted(hex), GetEnemyHexHoverIcon(relic) ?? relic.Icon);
		List<IHoverTip> tips = [mainTip];

		foreach (Type powerType in GetEnemyHexPowerHoverTipTypes(hex))
		{
			PowerModel power = ModelDb.GetById<PowerModel>(ModelDb.GetId(powerType));
			tips.Add(HoverTipFactory.FromPower(power));
		}

		if (EnemyHexesWithBurnHoverTip.Contains(hex))
		{
			tips.Add(HoverTipFactory.FromPower<HextechBurnPower>());
		}

		if (hex == MonsterHexKind.Compensation)
		{
			tips.Add(HoverTipFactory.FromPower<HextechNextTurnDamagePower>());
		}

		if (hex == MonsterHexKind.SolidTime)
		{
			tips.Add(HoverTipFactory.FromPower<HextechGalvanicPower>());
		}

		if (hex == MonsterHexKind.FossilStalker)
		{
			tips.Add(HoverTipFactory.FromPower<SuckPower>());
		}

		if (hex is MonsterHexKind.AncientStatue or MonsterHexKind.HundredRefinements)
		{
			tips.Add(HoverTipFactory.FromPower<HextechPlayerSlowPower>());
		}

		return tips;
	}

	internal static IReadOnlyList<Type> GetEnemyHexPowerHoverTipTypes(MonsterHexKind hex)
	{
		return EnemyHexPowerHoverTipTypes.TryGetValue(hex, out Type[]? powerTypes)
			? powerTypes
			: Array.Empty<Type>();
	}

	private static Texture2D? GetEnemyHexHoverIcon(RelicModel relic)
	{
		string? path = HextechAssets.TryGetCustomRelicIconPath(relic);
		return path == null ? null : HextechAssetHooks.LoadUiTexture(path);
	}

	private static string GetEnemyHexDescriptionKey(RelicModel relic)
	{
		ModelId id = relic.CanonicalInstance?.Id ?? relic.Id;
		return HextechAssets.ToImageFileStem(id.Entry) + ".enemyDescription";
	}

	/// <summary>
	/// 用于描述显示的玩家数：单人（或非联机/取不到状态）为 1，联机时取本局玩家数并夹到 [1,16]，
	/// 与 <c>HextechEnemyPowerScalingHooks.MultiplyByPlayerCount</c> 的实际缩放保持一致。
	/// </summary>
	private static int GetScalingPlayerCount()
	{
		try
		{
			if (!HextechPlayerContextHelper.IsNetworkMultiplayerRun())
			{
				return 1;
			}

			int count = RunManager.Instance.DebugOnlyGetState() is RunState runState ? runState.Players.Count : 1;
			return Math.Clamp(count, 1, 16);
		}
		catch
		{
			return 1;
		}
	}

	private static IReadOnlyDictionary<MonsterHexKind, HextechRarityTier> BuildRarityByMonsterHex()
	{
		Dictionary<MonsterHexKind, HextechRarityTier> byHex = new();
		AddRarityEntries(byHex, SilverMonsterHexes, HextechRarityTier.Silver);
		AddRarityEntries(byHex, GoldMonsterHexes, HextechRarityTier.Gold);
		AddRarityEntries(byHex, PrismaticMonsterHexes, HextechRarityTier.Prismatic);
		return byHex;
	}

	private static void AddRarityEntries(
		Dictionary<MonsterHexKind, HextechRarityTier> byHex,
		IEnumerable<MonsterHexKind> hexes,
		HextechRarityTier rarity)
	{
		foreach (MonsterHexKind hex in hexes)
		{
			byHex[hex] = rarity;
		}
	}

	private static IReadOnlyDictionary<ModelId, MonsterHexKind> BuildMonsterHexByIconRelicId()
	{
		Dictionary<ModelId, MonsterHexKind> byId = new();
		foreach (KeyValuePair<MonsterHexKind, Type> pair in MonsterHexIconRelicTypes)
		{
			RelicModel iconRelic = ModelDb.GetById<RelicModel>(ModelDb.GetId(pair.Value));
			ModelId id = iconRelic.CanonicalInstance?.Id ?? iconRelic.Id;
			byId[id] = pair.Key;
		}

		return byId;
	}
}
