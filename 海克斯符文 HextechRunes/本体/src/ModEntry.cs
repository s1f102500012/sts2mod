using MegaCrit.Sts2.Core.Modding;

namespace HextechRunes;

/// <summary>
/// 模组入口,只做编排:模型注册 → 配置/遥测 → 补丁应用 → 诊断输出。
/// 所有 Harmony 补丁以 <c>[HarmonyPatch]</c> + <c>[HextechPatch]</c> 补丁类的形式分布在各功能文件里,
/// 由 <see cref="HextechPatcher"/> 统一应用;同一目标上的执行序只由 <c>[HarmonyPriority]</c> 决定。
/// </summary>
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

			// 模型注册必须先于任何补丁:0.107.1 的 SavedProperty net-id 规范化以此刻的名字集合为最终集合。
			HextechModelBootstrap.Install();
			HextechRuneConfiguration.Initialize();
			HextechTelemetry.Initialize();
			HextechIntegratedStrategyEventsCompat.Install();

			Harmony harmony = _harmony ??= new Harmony(HarmonyId);
			HextechPatcher.ApplyAll(harmony, typeof(ModEntry).Assembly);
			HextechPatcher.LogSummary();
			HextechPatcher.LogSharedPatchTargets(harmony);
			HextechVanillaCopyGuard.Verify(harmony.Id);
			HextechPatcher.DumpIfRequested(harmony);
			_initialized = true;
			HextechMultiplayerDiagnostics.LogNetworkSignature();
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
}
