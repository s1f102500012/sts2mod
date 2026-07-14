using System.Reflection;
using MegaCrit.Sts2.Core.Logging;

namespace CustomDifficulty;

/// <summary>
/// 软联动无尽模式（EndlessMode ≥ 0.4.0）：递进模式的房间计数在进入新一轮后
/// 叠加往轮累计楼层，使难度曲线跨轮连续。未安装 EndlessMode 时恒返回 0。
/// 按程序集名反射解析，不产生硬依赖。
/// </summary>
internal static class EndlessModeCompat
{
	private const string InteropTypeName = "EndlessMode.EndlessModeInterop";
	private const string FloorsMethodName = "GetTotalFloorsBeforeCurrentLoop";

	private static bool _resolveAttempted;
	private static MethodInfo? _getFloorsBeforeCurrentLoop;

	public static int GetFloorsBeforeCurrentLoop()
	{
		MethodInfo? method = ResolveMethod();
		if (method == null)
		{
			return 0;
		}

		try
		{
			return method.Invoke(null, null) is int floors ? Math.Max(0, floors) : 0;
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}] EndlessMode interop call failed: {ex.Message}");
			return 0;
		}
	}

	private static MethodInfo? ResolveMethod()
	{
		if (_resolveAttempted)
		{
			return _getFloorsBeforeCurrentLoop;
		}

		_resolveAttempted = true;
		try
		{
			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				if (!string.Equals(assembly.GetName().Name, "EndlessMode", StringComparison.Ordinal))
				{
					continue;
				}

				MethodInfo? method = assembly.GetType(InteropTypeName)
					?.GetMethod(FloorsMethodName, BindingFlags.Public | BindingFlags.Static, binder: null, Type.EmptyTypes, modifiers: null);
				if (method != null)
				{
					_getFloorsBeforeCurrentLoop = method;
					Log.Info($"[{ModInfo.Id}] EndlessMode interop resolved; progressive floors will continue across endless loops.");
				}
				else
				{
					Log.Info($"[{ModInfo.Id}] EndlessMode found but interop API missing (version < 0.4.0); progressive floors reset each endless loop.");
				}

				return _getFloorsBeforeCurrentLoop;
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}] EndlessMode interop resolve failed: {ex.Message}");
		}

		return _getFloorsBeforeCurrentLoop;
	}
}
