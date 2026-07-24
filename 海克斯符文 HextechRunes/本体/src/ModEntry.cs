using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace HextechRunes;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
	private const string HarmonyId = "Natsuki.HextechRunes";

	private static readonly object InitializeLock = new();
	private static Harmony? _harmony;
	private static bool _initialized;

	public static void Initialize()
	{
		lock (InitializeLock)
		{
			if (_initialized)
			{
				HextechLog.Info($"[{ModInfo.Id}] Initialization already completed; skipping duplicate call.");
				return;
			}

			// 注意:以下安装顺序即契约,勿重排——
			// ① HextechModelBootstrap 必须先于 HextechSavedPropertyNetIdHooks(net-id 规范化的正确性前提:
			//    规范化时名字集合已是最终集合,这是 1014/ModMismatch 修复的一部分);
			// ② 全仓库不用 HarmonyPriority,同一目标方法多处 patch 的执行序 = 此处 Install 调用序。
			HextechModelBootstrap.Install();
			HextechRuneConfiguration.Initialize();
			HextechTelemetry.Initialize();
			Harmony harmony = _harmony ??= new Harmony(HarmonyId);
			TryInstallOptionalHookGroup("model id serialization warning compatibility", () => HextechModelIdSerializationWarningHooks.Install(harmony));
			HextechMultiplayerCompatibilityHooks.Install(harmony);
			TryInstallOptionalHookGroup("saved-property net-id canonicalization", () => HextechSavedPropertyNetIdHooks.Install(harmony));
			HextechMobileModelRegistrationHooks.Install(harmony);
			ThoughtOverwriteKeywordPersistenceHooks.Install(harmony);
			HextechSelfUpgradeCardStore.Install(harmony);
			HextechCustomRunModifierHooks.Install(harmony);
			HextechRunLifecycleHooks.Install(harmony);
			HextechCombatHooks.Install(harmony);
			HextechEnemyPowerScalingHooks.Install(harmony);
			TryInstallOptionalHookGroup("artifact encounter compatibility", () => HextechArtifactCompatibilityHooks.Install(harmony));
			TryInstallOptionalHookGroup("personal hive damage-response safety", () => HextechPersonalHiveSafetyHooks.Install(harmony));
			TryInstallOptionalHookGroup("encounter compatibility", () => HextechEncounterCompatibilityHooks.Install(harmony));
			HextechUpdateChecker.Install(harmony);
			TryInstallOptionalHookGroup("inspect relic screen", () => HextechInspectHooks.Install(harmony));
			AssetHooks.Install(harmony);
			TryInstallOptionalHookGroup("relic collection", () => CollectionHooks.Install(harmony));
			TryInstallOptionalHookGroup("shop random forge", () => HextechShopForgeHooks.Install(harmony));
			TryInstallOptionalHookGroup("forge stacking", () => HextechForgeStackingHooks.Install(harmony));
			TryInstallOptionalHookGroup("form auto-play end-turn suppression", () => HextechFormAutoPlayHooks.Install(harmony));
			TryInstallOptionalHookGroup("enemy tezcataras mercy wax relics", () => HextechEnemyTezcatarasMercyHooks.Install(harmony));
			TryInstallOptionalHookGroup("enemy cutting-edge alchemist potion odds", () => HextechEnemyCuttingEdgeAlchemistHooks.Install(harmony));
			TryInstallOptionalHookGroup("enemy hex top bar hover", () => HextechEnemyUi.Install(harmony));
			TryInstallOptionalHookGroup("relic UI safety", () => HextechUiSafetyHooks.Install(harmony));
			TryInstallOptionalHookGroup("player stats hover", () => HextechPlayerStatsHoverHooks.Install(harmony));
			TryInstallOptionalHookGroup("rune configuration menu", () => HextechRuneConfigMenuHooks.Install(harmony));
			TryInstallOptionalHookGroup("reward serialization safety", () => HextechRewardSafetyHooks.Install(harmony));
			TryInstallOptionalHookGroup("relic visibility toggle", () => HextechRelicVisibilityHooks.Install(harmony));
			TryInstallOptionalHookGroup("hand of baron aura visual", () => HextechBaronAuraHooks.Install(harmony));
			TryInstallOptionalHookGroup("slow cook aura visual", () => HextechSlowCookAuraHooks.Install(harmony));
			TryInstallOptionalHookGroup("near-death feast visual", () => HextechNearDeathFeastVisualHooks.Install(harmony));
			TryInstallOptionalHookGroup("combat vfx dispatch", () => HextechCombatVfxHooks.Install(harmony));
			TryInstallOptionalHookGroup("burn power flames", () => HextechBurnVisualHooks.Install(harmony));
			TryInstallOptionalHookGroup("glass cannon health bar lock", () => HextechGlassCannonHealthBarHooks.Install(harmony));
			TryInstallOptionalHookGroup("burn health bar prediction", () => HextechBurnHealthBarHooks.Install(harmony));
			TryInstallOptionalHookGroup("neurosurge doom redirect", () => HextechNeurosurgeHooks.Install(harmony));
			TryInstallOptionalHookGroup("inkshadow blade of ink guard", () => HextechInkshadowHooks.Install(harmony));
			TryInstallOptionalHookGroup("creature anim trigger safety", () => HextechAnimTriggerSafetyHooks.Install(harmony));
			TryInstallOptionalHookGroup("starter card unlimited upgrade", () => HextechStarterUpgradeHooks.Install(harmony));
			TryInstallOptionalHookGroup("card grid upgrade preview revert", () => HextechCardGridPreviewHooks.Install(harmony));
			TryInstallOptionalHookGroup("retired well-laid plans save compatibility", () => HextechWellLaidPlansHooks.Install(harmony));
			TryInstallOptionalHookGroup("prismatic egg treasure replacement", () => HextechTreasureRuneHooks.Install(harmony));
			TryInstallOptionalHookGroup("nightmare dark orb passive", () => HextechNightmareHooks.Install(harmony));
			TryInstallOptionalHookGroup("game over score line compatibility", () => HextechGameOverCompatibilityHooks.Install(harmony));
			_initialized = true;
			// 加载确认行保持始终输出（headless 验证与用户排障都依赖它），不走 verbose 门控。
			Log.Info(
				$"[{ModInfo.Id}] Loaded implementation variant for " +
				$"Slay the Spire 2 compat target {ModInfo.TargetGameVersion}.");
		}
	}

	internal static HextechMayhemModifier EnsureMayhemModifier(RunState runState)
	{
		return HextechRunLifecycleHooks.EnsureMayhemModifier(runState);
	}

	internal static Task HandleHextechActStarted(HextechMayhemModifier modifier)
	{
		return HextechRunLifecycleHooks.HandleHextechActStarted(modifier);
	}

	private static void TryInstallOptionalHookGroup(string label, Action install)
	{
		try
		{
			install();
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Optional hook group skipped: {label}: {ex.GetType().Name}: {ex.Message}");
		}
	}
}
