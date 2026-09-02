#if STS2_107_1
using HarmonyLib;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

/// <summary>
/// 把 <c>SavedPropertiesTypeCache</c> 的模组 net-id 布局规范化为确定性顺序,消除联机「1014 模组不匹配」的非确定性根因
/// (运行时 SavedProperties net-id 因模组加载顺序在两端错位 → 抛异常 → 被断连兜底转成 ModMismatch)。
/// 仅 0.107.1 需要:0.109 起 ModelIdSerializationCache.Init 由游戏自己做确定性排序与哈希。
///
/// 时序:挂在 <c>OneTimeInitialization.ExecuteEssential</c> 的后缀。该方法是固定启动状态机里唯一一次、且在
/// 所有 SavedProperty 注入完成之后(ModInitializer 阶段的自注入 + LocManager.Initialize 阶段 RitsuLib 的补注入)、
/// 任何联机序列化之前运行的点,因此是规范化的安全收口位置。
///
/// 规范化规则与可单测的纯逻辑见 <see cref="HextechSavedPropertyNetIdCanonicalizer"/>。
/// </summary>
internal static class HextechSavedPropertyNetIdHooks
{
	private const BindingFlags StaticNonPublic = BindingFlags.NonPublic | BindingFlags.Static;

	private static bool _installed;
	private static bool _canonicalized;

	/// <summary>规范化是否已发生。此后再注入 SavedProperty 载体会绕过规范排序(见 HextechSavedPropertyBootstrap.InjectModelType 的告警)。</summary>
	internal static bool IsCanonicalized => _canonicalized;


	private static void CanonicalizeNetIdMapPostfix()
	{
		if (_canonicalized)
		{
			return;
		}

		_canonicalized = true;

		try
		{
			IReadOnlySet<string>? vanillaNames = BuildVanillaPropertyNameSet();
			if (vanillaNames == null || vanillaNames.Count == 0)
			{
				Log.Warn($"[{ModInfo.Id}][MultiplayerCompat] Could not determine vanilla SavedProperty names; skipping net-id canonicalization.");
				return;
			}

			FieldInfo? netIdToNameField = TryGetField(typeof(SavedPropertiesTypeCache), "_netIdToPropertyNameMap", StaticNonPublic);
			FieldInfo? nameToNetIdField = TryGetField(typeof(SavedPropertiesTypeCache), "_propertyNameToNetIdMap", StaticNonPublic);
			if (netIdToNameField?.GetValue(null) is not List<string> netIdToName
				|| nameToNetIdField?.GetValue(null) is not Dictionary<string, int> nameToNetId)
			{
				Log.Warn($"[{ModInfo.Id}][MultiplayerCompat] SavedPropertiesTypeCache maps unavailable; skipping net-id canonicalization.");
				return;
			}

			List<string>? canonical = HextechSavedPropertyNetIdCanonicalizer.Canonicalize(netIdToName, vanillaNames);
			if (canonical == null || canonical.Count != netIdToName.Count)
			{
				Log.Warn($"[{ModInfo.Id}][MultiplayerCompat] Net-id canonicalization produced an invalid result (mapCount={netIdToName.Count}); leaving the map unchanged.");
				return;
			}

			// 原地重建两张表,保持原引用不变(其它代码可能持有同一 List/Dictionary 引用)。
			netIdToName.Clear();
			netIdToName.AddRange(canonical);
			nameToNetId.Clear();
			for (int i = 0; i < canonical.Count; i++)
			{
				nameToNetId[canonical[i]] = i;
			}

			SetNetIdBitSize(HextechSavedPropertyNetIdCanonicalizer.ComputeNetIdBitSize(canonical.Count));
			HextechLog.Info($"[{ModInfo.Id}][MultiplayerCompat] Canonicalized SavedProperty net-id map: vanilla={vanillaNames.Count} total={canonical.Count} bitSize={SavedPropertiesTypeCache.NetIdBitSize}.");

			// 规范化后终检:此刻拓展包/二创包的延迟注册均已完成,扫描所有引用本模组的程序集,
			// 抓"包侧新增 [SavedProperty] 载体却忘了走 API 注册"的漏项(启动期那次自检看不到包外类型)。
			HextechSavedPropertyBootstrap.WarnOnUninjectedSavedPropertyCarriers();
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][MultiplayerCompat] SavedProperty net-id canonicalization failed: {ex.GetType().Name}: {ex.Message}");
		}
	}

	/// <summary>
	/// 重放原版种子:遍历 <c>AbstractModelSubtypes</c> 收集所有带 <c>[SavedProperty]</c> 的属性名。
	/// 只需名字集合(用于把原版前缀与模组后缀区分开),不依赖游戏的排序细节,且随游戏版本自适应。
	/// 必须与游戏 <c>CachePropertiesForType</c> 一致地用 Instance|Public|NonPublic 取属性。
	/// </summary>
	private static IReadOnlySet<string>? BuildVanillaPropertyNameSet()
	{
		const BindingFlags propertyFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		HashSet<string> names = new(StringComparer.Ordinal);
		foreach (Type type in AbstractModelSubtypes.All)
		{
			if (type == null)
			{
				continue;
			}

			foreach (PropertyInfo property in type.GetProperties(propertyFlags))
			{
				if (property.GetCustomAttribute<SavedPropertyAttribute>() != null)
				{
					names.Add(property.Name);
				}
			}
		}

		return names;
	}

	private static void SetNetIdBitSize(int bitSize)
	{
		FieldInfo? backing = TryGetField(typeof(SavedPropertiesTypeCache), "<NetIdBitSize>k__BackingField", StaticNonPublic);
		if (backing == null)
		{
			Log.Warn($"[{ModInfo.Id}][MultiplayerCompat] SavedPropertiesTypeCache NetIdBitSize backing field not found; net-id bit size left unchanged.");
			return;
		}

		backing.SetValue(null, bitSize);
	}

	[HextechPatch("compat.saved-property-net-id", "SavedProperty net-id 规范化")]
	private static class CanonicalizePatch
	{
		public static void Apply(Harmony harmony)
		{
			if (_installed)
			{
				return;
			}

			_installed = true;

			Type? oneTimeInit = AccessTools.TypeByName("MegaCrit.Sts2.Core.Helpers.OneTimeInitialization");
			MethodInfo? essential = oneTimeInit == null ? null : AccessTools.Method(oneTimeInit, "ExecuteEssential");
			if (essential == null)
			{
				Log.Warn($"[{ModInfo.Id}][MultiplayerCompat] Could not patch OneTimeInitialization.ExecuteEssential; SavedProperty net-id canonicalization is disabled (multiplayer may desync with other SavedProperty mods such as RitsuLib).");
				return;
			}

			try
			{
				harmony.Patch(essential, postfix: new HarmonyMethod(typeof(HextechSavedPropertyNetIdHooks), nameof(CanonicalizeNetIdMapPostfix)));
			}
			catch (Exception ex)
			{
				Log.Warn($"[{ModInfo.Id}][MultiplayerCompat] Skipped SavedProperty net-id canonicalization: {ex.GetType().Name}: {ex.Message}");
			}
		}
	}
}
#endif
