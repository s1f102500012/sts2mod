using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Runs;

namespace EndlessMode;

/// <summary>
/// 供其他模组消费的公共只读 API（建议通过程序集名 "EndlessMode" 反射调用，避免硬依赖）。
/// 所有方法在无进行中 run、异常或未进入无尽模式时返回 0，绝不抛出。
/// </summary>
public static class EndlessModeInterop
{
	/// <summary>已完成的无尽轮次数（未进入无尽=0；第一轮循环中=1，以此类推）。</summary>
	public static int GetCompletedLoopCount()
	{
		try
		{
			if (RunManager.Instance?.DebugOnlyGetState() is not RunState state)
			{
				return 0;
			}

			return ModEntry.GetCompletedLoopCountForInterop(state);
		}
		catch (Exception ex)
		{
			Log.Warn($"[EndlessMode] Interop GetCompletedLoopCount failed: {ex.Message}");
			return 0;
		}
	}

	/// <summary>进入当前轮之前累计走过的总楼层数（普通 run 或第一圈=0）。</summary>
	public static int GetTotalFloorsBeforeCurrentLoop()
	{
		try
		{
			if (RunManager.Instance?.DebugOnlyGetState() is not RunState state)
			{
				return 0;
			}

			return ModEntry.GetTotalFloorsBeforeCurrentLoop(state);
		}
		catch (Exception ex)
		{
			Log.Warn($"[EndlessMode] Interop GetTotalFloorsBeforeCurrentLoop failed: {ex.Message}");
			return 0;
		}
	}

	/// <summary>跨轮连续的累计总楼层 = 往轮累计 + 当前轮 RunState.TotalFloor。</summary>
	public static int GetCumulativeTotalFloor()
	{
		try
		{
			if (RunManager.Instance?.DebugOnlyGetState() is not RunState state)
			{
				return 0;
			}

			return ModEntry.GetTotalFloorsBeforeCurrentLoop(state) + Math.Max(0, state.TotalFloor);
		}
		catch (Exception ex)
		{
			Log.Warn($"[EndlessMode] Interop GetCumulativeTotalFloor failed: {ex.Message}");
			return 0;
		}
	}
}
