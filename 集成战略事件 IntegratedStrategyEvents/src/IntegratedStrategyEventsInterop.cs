using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Runs;

namespace IntegratedStrategyEvents;

/// <summary>
/// 供其他模组使用的公共兼容 API（通过反射或直接引用均可）。
/// 保持方法签名稳定；新增能力只加不改。
/// </summary>
public static class IntegratedStrategyEventsInterop
{
	private static readonly List<Func<ActMap, bool>> SecretNodeSkipPredicates = [];

	/// <summary>
	/// 注册秘境节点跳过谓词：返回 true 时，本模组不会在该地图上生成/识别秘境节点。
	/// 供拥有脚本化/临时地图的其他模组声明自己的地图不参与秘境注入。
	/// </summary>
	public static void RegisterSecretNodeSkipPredicate(Func<ActMap, bool> predicate)
	{
		ArgumentNullException.ThrowIfNull(predicate);
		SecretNodeSkipPredicates.Add(predicate);
	}

	/// <summary>当前跑局是否处于本模组的临时层（树洞 / 终局 / 断章）。</summary>
	public static bool IsTemporaryMapActive()
	{
		return TreeHoles.IntegratedStrategyTreeHoleController.IsActiveCurrentRun();
	}

	/// <summary>该地图是否属于本模组的临时层（含存档往返后的会话拓扑判定）。</summary>
	public static bool IsTemporaryMap(IRunState? runState, ActMap map)
	{
		return TreeHoles.IntegratedStrategyTreeHoleController.IsTemporaryMap(runState, map);
	}

	/// <summary>
	/// 返回当前应被其他模组视为额外幕的稳定 ID。普通树洞与先知号角断章不属于额外幕。
	/// </summary>
	public static string? GetCurrentExtraActId(IRunState runState)
	{
		if (runState is not RunState state ||
			!TreeHoles.TreeHoleSessionManager.TryGetFinaleSession(state, out TreeHoles.EndlessFinaleSession session))
		{
			return null;
		}

		return ClassifyExtraAct(
			session.Kind,
			TreeHoles.TreeHoleSessionManager.IsCurrentFinaleSessionMap(state, session));
	}

	internal static string? ClassifyExtraAct(TreeHoles.SpecialFinaleKind kind, bool isCurrentFinaleMap)
	{
		return isCurrentFinaleMap && kind != TreeHoles.SpecialFinaleKind.ProphetHornFragment
			? $"IntegratedStrategyEvents:Finale:{kind}"
			: null;
	}

	internal static bool ShouldSkipSecretNodes(ActMap map)
	{
		foreach (Func<ActMap, bool> predicate in SecretNodeSkipPredicates)
		{
			try
			{
				if (predicate(map))
				{
					return true;
				}
			}
			catch (Exception ex)
			{
				MegaCrit.Sts2.Core.Logging.Log.Warn(
					$"{ModInfo.LogPrefix} Secret-node skip predicate threw and will be ignored for this map: {ex}");
			}
		}

		return false;
	}
}
