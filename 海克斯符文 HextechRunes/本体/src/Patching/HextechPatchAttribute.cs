namespace HextechRunes;

/// <summary>
/// 补丁类元数据。挂在带 <c>[HarmonyPatch]</c> 的类上,由 <see cref="HextechPatcher"/> 统一应用并逐条汇报。
/// </summary>
/// <remarks>
/// 约定:
/// <list type="bullet">
/// <item><see cref="Id"/> 稳定且唯一,形如 <c>combat.heal</c>,日志与补丁清单快照都按它定位。</item>
/// <item><see cref="Feature"/> 是玩家可感知的功能名(符文名 / 界面名),失败时日志按功能归因。</item>
/// <item><see cref="Rune"/> 指定后,补丁失败会把该符文标记为"本运行时不可用"(<see cref="HextechRuntimeRuneCompatibility"/>),
/// 与旧 <c>TryInstallRuneHook</c> 语义一致。</item>
/// <item><see cref="Optional"/> 表示目标在某些游戏版本上可能不存在,补丁类应配合 <c>[HarmonyPrepare]</c> 自行跳过;
/// 这里只决定失败日志级别。</item>
/// </list>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal sealed class HextechPatchAttribute : Attribute
{
	public HextechPatchAttribute(string id, string feature)
	{
		Id = id;
		Feature = feature;
	}

	public string Id { get; }

	public string Feature { get; }

	public Type? Rune { get; init; }

	/// <summary>一个补丁同时服务多个符文时(如卡牌标签),失败时全部标记。</summary>
	public Type[]? Runes { get; init; }

	public bool Optional { get; init; }

	internal IEnumerable<Type> AffectedRunes
	{
		get
		{
			if (Rune != null)
			{
				yield return Rune;
			}

			foreach (Type rune in Runes ?? [])
			{
				yield return rune;
			}
		}
	}
}
