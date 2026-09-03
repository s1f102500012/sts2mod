using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace HextechRunesSponsorPack;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
	private const string PrerequisiteAssemblyName = "HextechRunes";
	private const string HarmonyId = "Natsuki.HextechRunesSponsorPack";

	private static readonly object InitializeLock = new();
	private static bool _waitingForPrerequisite;
	private static bool _contentRegistered;
	private static bool _registered;

	public static void Initialize()
	{
		lock (InitializeLock)
		{
			if (_registered)
			{
				Log.Info($"[{ModInfo.Id}] Initialization already completed; skipping duplicate call.");
				return;
			}

			// 前置检测按「程序集名 HextechRunes」而非 manifest 的 mod id —— 本体与二创/synergy 版都打包了同名
			// HextechRunes.dll(含 HextechRunesApi),所以两者都能识别(manifest 里已去掉对具体 mod id 的硬依赖)。
			if (IsHextechRunesAssemblyPresent())
			{
				RegisterAll();
				return;
			}

			// 前置程序集可能晚于本拓展包加载(尤其二创版:mod id 不同、按字母序可能排在本包之后)——
			// 订阅程序集加载事件,待 HextechRunes 程序集载入后再注册,与加载顺序无关。注册发生在任何 run 开始前,内容仍及时入池。
			if (!_waitingForPrerequisite)
			{
				_waitingForPrerequisite = true;
				Log.Info($"[{ModInfo.Id}] HextechRunes assembly not loaded yet; deferring registration until it loads.");
				AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
			}
		}
	}

	private static void OnAssemblyLoad(object? sender, AssemblyLoadEventArgs args)
	{
		if (!string.Equals(args.LoadedAssembly.GetName().Name, PrerequisiteAssemblyName, StringComparison.Ordinal))
		{
			return;
		}

		lock (InitializeLock)
		{
			AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
			_waitingForPrerequisite = false;

			// 延迟路径的失败模式:本体若在模组初始化阶段结束后才载入,模型与 SavedProperty 的注册窗口已经关闭,
			// HextechRunesApi 的注册会抛 InvalidOperationException,并从 AssemblyLoad 事件处理器里冒出去。
			// 符文不入池比崩溃好:这里只警告并退出。
			if (IsModelRegistrationWindowClosed())
			{
				Log.Warn($"[{ModInfo.Id}] HextechRunes 加载过晚(模型注册窗口已关闭),拓展包内容未注册。", 2);
				return;
			}

			RegisterAll();
		}
	}

	// ModManager.State 在全部 mod 的 initializer 跑完之后才置 Initialized / Skipped
	// (public static,0.107.1 第 500/527 行、0.111.0 第 523/550 行),所以"仍是 None"等价于"注册窗口还开着"。
	private static bool IsModelRegistrationWindowClosed()
	{
		return ModManager.State != ModManagerState.None;
	}

	private static void RegisterAll()
	{
		if (_registered)
		{
			return;
		}

		if (!_contentRegistered)
		{
			// 注册按功能组隔离(SponsorCatalog.RegisterAll:先依赖后可获得内容,依赖失败的功能整组不入池),失败条目已各自 Warn。
			// 补丁无条件照装:注册不是事务,失败时前面的内容已经入池,此时跳过补丁反而会留下
			// "符文抽得到、依赖的补丁没装"的半初始化状态;每个补丁都以持有对应符文为前提,内容缺席只是空转。
			int failures = SponsorCatalog.RegisterAll();
			_contentRegistered = true;
			if (failures > 0)
			{
				Log.Warn($"[{ModInfo.Id}] {failures} content registration(s) failed or were skipped; remaining content stays registered and patches are still applied.", 2);
			}
		}

		try
		{
			Harmony harmony = new(HarmonyId);
			SponsorPatcher.ApplyAll(harmony, typeof(ModEntry).Assembly);
			SponsorPatcher.LogSummary();
			SponsorPatcher.DumpIfRequested(harmony);
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}] Patch application failed: {ex.GetType().Name}: {ex.Message}", 2);
		}

		_registered = true;
		Log.Info($"[{ModInfo.Id}] Loaded and registered HextechRunes sponsor-pack content.");
	}

	// 兼容本体与二创(synergy)版:两者都打包了程序集名为 "HextechRunes" 的 dll(暴露同样的 HextechRunesApi)。
	private static bool IsHextechRunesAssemblyPresent()
	{
		return AppDomain.CurrentDomain.GetAssemblies()
			.Any(assembly => string.Equals(assembly.GetName().Name, PrerequisiteAssemblyName, StringComparison.Ordinal));
	}
}
