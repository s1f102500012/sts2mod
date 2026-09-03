namespace HextechRunesSponsorPack;

/// <summary>
/// 补丁类元数据。挂在带 <c>[HarmonyPatch]</c> 的类(或声明了 <c>static void Apply(Harmony)</c> 的动态目标类)上,
/// 由 <see cref="SponsorPatcher"/> 统一应用并逐条汇报。
/// </summary>
/// <remarks>
/// 约定:
/// <list type="bullet">
/// <item><see cref="Id"/> 稳定且唯一,形如 <c>abyssal.regent-forge</c>,日志与补丁表 dump 都按它定位。</item>
/// <item><see cref="Feature"/> 是玩家可感知的功能名(符文名 / 事件名),失败时日志按功能归因。</item>
/// <item><see cref="Optional"/> 表示目标在某些游戏版本或某些模组组合下可能不存在;它只决定失败日志级别,不改变是否安装。</item>
/// </list>
/// 本体 <c>HextechPatchAttribute</c> 的 <c>Rune</c>/<c>Runes</c> 在这里没有对应物:拓展包没有"运行时符文可用性登记"的口子。
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal sealed class SponsorPatchAttribute : Attribute
{
	public SponsorPatchAttribute(string id, string feature)
	{
		Id = id;
		Feature = feature;
	}

	public string Id { get; }

	public string Feature { get; }

	public bool Optional { get; init; }
}
