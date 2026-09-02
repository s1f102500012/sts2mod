#if STS2_107_1
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

// 0.107.1:游戏不自动收录模组载体,启动期手动逐个注入 SavedPropertiesTypeCache,
// 再由 HextechSavedPropertyNetIdHooks 在 ExecuteEssential 后把 net-id 表规范化;规范化之后窗口即关闭。
internal static partial class HextechSavedPropertyBootstrap
{
	private const string RegistrationFreezePointName = "SavedProperty net-id 规范化";

	private static bool IsRegistrationWindowClosed()
	{
		return HextechSavedPropertyNetIdHooks.IsCanonicalized;
	}

	private static void InjectModelTypeCore(Type type)
	{
		if (HextechSavedPropertyNetIdHooks.IsCanonicalized)
		{
			return;
		}

		SavedPropertiesTypeCache.InjectTypeIntoCache(type);
	}

	private static void InjectCachesCore()
	{
		foreach (Type type in HextechModelTypeIdentity.Distinct(HextechCatalog.GetAllCustomRelicTypes()))
		{
			SavedPropertiesTypeCache.InjectTypeIntoCache(type);
		}

		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechMayhemModifier));
		foreach (Type type in HextechCustomModelRegistry.AllCustomModifierTypes)
		{
			SavedPropertiesTypeCache.InjectTypeIntoCache(type);
		}

		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechBurnPower));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechNextTurnDamagePower));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechGalvanicPower));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechTemporaryStrengthPower));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechTemporaryDexterityPower));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechTemporaryStrengthLossPower));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechTemporaryDexterityLossPower));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechLethalTempoTemporaryStrengthPower));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechBloodPactTemporaryStrengthPower));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechPowerShieldTemporaryStrengthPower));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechAttackReplayPower));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechPlayerSlowPower));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechTemporarySlowPower));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechOceanDragonSoulPower));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechInfernalDragonSoulPower));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechDragonSoulPower));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechMountainDragonSoulPower));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechChemtechDragonSoulPower));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HextechCloudDragonSoulPower));
		WarnOnUninjectedSavedPropertyCarriers();
		EnsureSavedPropertyNetIdBitSize();
	}

	private static void EnsureSavedPropertyNetIdBitSize()
	{
		// 兜底:按与游戏 / RitsuLib 一致的公式 CeilToInt(Log2(count)) 把位宽抬到能容纳当前属性数。
		// 联机一致性的权威设置由 HextechSavedPropertyNetIdHooks 在规范化后统一完成;此处仅保证即便该
		// 后缀钩子未能安装,本模组单独联机时位宽也够用。不再使用旧的固定下限 16——它与原版 / RitsuLib 的
		// 公式不一致,会让一端是 16、另一端是 CeilToInt(Log2(count)),造成 net-id 位宽错位而断连。
		const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;

		FieldInfo? mapField = TryGetField(typeof(SavedPropertiesTypeCache), "_netIdToPropertyNameMap", flags);
		int propertyNameCount = (mapField?.GetValue(null) as System.Collections.ICollection)?.Count ?? 0;
		int targetBitSize = HextechSavedPropertyNetIdCanonicalizer.ComputeNetIdBitSize(propertyNameCount);
		int currentBitSize = SavedPropertiesTypeCache.NetIdBitSize;
		if (currentBitSize >= targetBitSize)
		{
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] SavedPropertiesTypeCache NetIdBitSize unchanged: bitSize={currentBitSize} propertyNames={propertyNameCount}");
			return;
		}

		FieldInfo? backingField = TryGetField(typeof(SavedPropertiesTypeCache), "<NetIdBitSize>k__BackingField", flags);
		if (backingField == null)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] SavedPropertiesTypeCache NetIdBitSize backing field not found; custom saved properties may desync in multiplayer.");
			return;
		}

		backingField.SetValue(null, targetBitSize);
		HextechLog.Info($"[{ModInfo.Id}][Mayhem] SavedPropertiesTypeCache NetIdBitSize updated: old={currentBitSize} new={targetBitSize} propertyNames={propertyNameCount}");
	}
}
#endif
