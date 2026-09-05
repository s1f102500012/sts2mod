namespace UniversalDominionSword;

/// <summary>
/// 补丁类元数据。挂在带 <c>[HarmonyPatch]</c> 的类上,由 <see cref="SwordPatcher"/> 统一应用并逐条汇报。
/// <see cref="Id"/> 稳定且唯一,日志与补丁清单快照都按它定位;<see cref="Feature"/> 是玩家可感知的功能名;
/// <see cref="Optional"/> 表示目标或所需私有成员在某些游戏版本上可能不存在,补丁类应配合 <c>[HarmonyPrepare]</c> 自行跳过,
/// 这里只决定失败日志级别。
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal sealed class SwordPatchAttribute(string id, string feature) : Attribute
{
	public string Id { get; } = id;

	public string Feature { get; } = feature;

	public bool Optional { get; init; }
}
