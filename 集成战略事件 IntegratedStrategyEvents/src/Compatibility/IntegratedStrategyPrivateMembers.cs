using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.Potions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace IntegratedStrategyEvents;

internal static partial class IntegratedStrategyPrivateMembers
{
	internal sealed record Contract(Type Type, string Name, string Feature, bool IsMethod = false, Type[]? Parameters = null);
	// 兼容契约覆盖 STS2 0.107.1、0.110.1、0.111.0；版本专有字段在分部文件登记。
	internal static IEnumerable<Contract> Contracts => new Contract[]
	{
		new(typeof(NNormalMapPoint), "_icon", "map-ui"),
		new(typeof(NNormalMapPoint), "_outline", "map-ui"),
		new(typeof(NNormalMapPoint), "_questIcon", "map-ui"),
		new(typeof(NNormalMapPoint), "_tween", "map-ui"),
		new(typeof(NMapPoint), "_outlineColor", "map-ui"),
		new(typeof(NMapLegendItem), "_icon", "map-ui"),
		new(typeof(NMapLegendItem), "_hoverTip", "map-ui"),
		new(typeof(NMapLegendItem), "_pointType", "map-ui"),
		new(typeof(NMapScreen), "_legendItems", "map-ui"),
		new(typeof(NMapScreen), "_mapLegend", "map-ui"),
		new(typeof(NMapScreen), "_bossPointNode", "map-ui"),
		new(typeof(NMapScreen), "_startingPointNode", "map-ui"),
		new(typeof(NMapScreen), "_map", "map-ui"),
		new(typeof(NMapScreen), "_runState", "map-ui"),
		new(typeof(NMapScreen), "_paths", "map-ui"),
		new(typeof(NActBanner), "_actNumber", "map-ui"),
		new(typeof(NActBanner), "_actName", "map-ui"),
		new(typeof(NNormalMapPoint), "AnimHover", "map-ui", true, []),
		new(typeof(NNormalMapPoint), "AnimUnhover", "map-ui", true, []),
		new(typeof(NEventLayout), "_event", "event-ui"),
		new(typeof(NEventRoom), "_event", "temporary-map"),
		new(typeof(NPotionContainer), "_holders", "potion-ui"),
		new(typeof(NPotionContainer), "UpdateNavigation", "potion-ui", true, []),
		new(typeof(NRunMusicController), "_proxy", "music"),
		new(typeof(ActModel), "_rooms", "temporary-map"),
		new(typeof(RunState), "_mapPointHistory", "temporary-map"),
		new(typeof(RunState), "_visitedEventIds", "forced-events"),
		new(typeof(RunManager), "ClearScreens", "transition-ui", true, []),
		new(typeof(RunManager), "FadeIn", "transition-ui", true, [typeof(bool)]),
		new(typeof(RunManager), "ExitCurrentRooms", "temporary-map", true, []),
		new(typeof(RunManager), "EnterRoomInternal", "temporary-map", true, [typeof(AbstractRoom), typeof(bool)]),
		new(typeof(RunManager), "WinRun", "temporary-map", true, []),
		// protected 抽象方法，虚分派到候选怪的覆写；缺失时变身池判空，岁兽幼崽不再变身。
		new(typeof(MonsterModel), "GenerateMoveStateMachine", "monster-transform", true, [])
	}.Concat(VersionContracts);

	private static readonly Dictionary<(Type, string), MemberInfo?> Members = [];
	private static readonly HashSet<string> MissingFeatures = new(StringComparer.Ordinal);
	private static bool _validated;

	internal static MemberInfo? Resolve(Contract contract) => contract.IsMethod
		? AccessTools.Method(contract.Type, contract.Name, contract.Parameters)
		: AccessTools.Field(contract.Type, contract.Name);

	internal static void Validate()
	{
		if (_validated) return;
		foreach (Contract contract in Contracts)
		{
			MemberInfo? member = Resolve(contract);
			Members[(contract.Type, contract.Name)] = member;
			if (member != null) continue;
			MissingFeatures.Add(contract.Feature);
			Log.Warn($"{ModInfo.LogPrefix}[PrivateMembers] Missing {contract.Type.FullName}.{contract.Name}; disabled {contract.Feature}.");
		}
		_validated = true;
		Log.Info($"{ModInfo.LogPrefix}[PrivateMembers] Missing={Members.Values.Count(member => member == null)}, disabled={string.Join(",", MissingFeatures)}.");
	}

	internal static bool IsAvailable(string feature)
	{
		Validate();
		return !MissingFeatures.Contains(feature);
	}
	internal static FieldInfo? Field(Type type, string name) => Get(type, name) as FieldInfo;
	internal static MethodInfo? Method(Type type, string name) => Get(type, name) as MethodInfo;
	private static MemberInfo? Get(Type type, string name)
	{
		Validate();
		return Members.TryGetValue((type, name), out MemberInfo? member) ? member
			: throw new InvalidOperationException($"Undeclared private member {type.FullName}.{name}");
	}

	internal static AccessTools.FieldRef<T, TField> FieldRef<T, TField>(string name) where T : class
	{
		FieldInfo? field = Field(typeof(T), name);
		return field == null ? null! : AccessTools.FieldRefAccess<T, TField>(field);
	}
}
