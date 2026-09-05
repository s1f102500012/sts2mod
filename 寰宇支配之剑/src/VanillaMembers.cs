using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Screens.InspectScreens;

namespace UniversalDominionSword;

/// <summary>
/// 本模组触碰的全部原版私有成员集中在这里。找不到的成员进 <see cref="MissingMembers"/>,启动摘要统一列出,
/// 依赖它的补丁经 <c>[HarmonyPrepare]</c> 自行降级,而不是各文件自持句柄悄悄失效。
/// </summary>
internal static class VanillaMembers
{
	private static readonly List<string> Missing = [];

	internal static IReadOnlyList<string> MissingMembers => Missing;

	/// <summary>原版 <c>NRelic._model</c>(0.107.1–0.111.0):顶栏/奖励界面遗物节点当前展示的模型。</summary>
	internal static readonly FieldInfo? NRelicModel = Field(typeof(NRelic), "_model");

	/// <summary>原版 <c>NInspectRelicScreen._relics</c>(0.107.1–0.111.0):检视界面翻页的遗物列表。</summary>
	internal static readonly FieldInfo? InspectRelics = Field(typeof(NInspectRelicScreen), "_relics");

	/// <summary>原版 <c>NInspectRelicScreen._index</c>(0.107.1–0.111.0):检视界面当前页索引。</summary>
	internal static readonly FieldInfo? InspectIndex = Field(typeof(NInspectRelicScreen), "_index");

	/// <summary>原版 <c>NInspectRelicScreen._relicImage</c>(0.107.1–0.111.0):检视界面的大图。</summary>
	internal static readonly FieldInfo? InspectImage = Field(typeof(NInspectRelicScreen), "_relicImage");

	/// <summary>
	/// 原版 <c>AncientEventModel.RelicOption(RelicModel, string, string)</c>(0.107.1–0.111.0,protected):
	/// 涅奥自己生成先古遗物选项用的工厂,复用它才能拿到与原版三个选项完全一致的页面与完成文案。
	/// </summary>
	internal static readonly MethodInfo? AncientRelicOption = Method(
		typeof(AncientEventModel),
		"RelicOption",
		[typeof(RelicModel), typeof(string), typeof(string)]);

	private static FieldInfo? Field(Type type, string name)
	{
		FieldInfo? field = AccessTools.Field(type, name);
		if (field == null)
		{
			Missing.Add($"{type.FullName}.{name}");
		}

		return field;
	}

	private static MethodInfo? Method(Type type, string name, Type[] parameters)
	{
		MethodInfo? method = AccessTools.Method(type, name, parameters);
		if (method == null)
		{
			Missing.Add($"{type.FullName}.{name}({string.Join(", ", parameters.Select(parameter => parameter.Name))})");
		}

		return method;
	}
}
