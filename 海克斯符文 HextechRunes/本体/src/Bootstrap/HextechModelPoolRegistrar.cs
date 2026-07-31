using System.Collections;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.RelicPools;

namespace HextechRunes;

internal static class HextechModelPoolRegistrar
{
	private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

	private static readonly FieldInfo? ModdedContentForPoolsField =
		typeof(ModHelper).GetField("_moddedContentForPools", BindingFlags.NonPublic | BindingFlags.Static);

	private static readonly List<(Type PoolType, Type ModelType)> MobileDuplicateRegistrations = new();

	internal static void RegisterModels()
	{
		IReadOnlyList<Type> customRelicTypes = HextechModelTypeIdentity.Distinct(HextechCatalog.GetAllCustomRelicTypes());
		IReadOnlyList<Type> eventRelicTypes = HextechModelTypeIdentity.Distinct(HextechContentRegistry.EventRelicTypes);
		IReadOnlyList<Type> customCardTypes = HextechModelTypeIdentity.Distinct(HextechCatalog.GetAllCustomCardTypes());

		RegisterModelsInPool(typeof(SharedRelicPool), customRelicTypes);
		RegisterModelsInPool(typeof(EventRelicPool), eventRelicTypes);
		RegisterModelsInPool(typeof(TokenCardPool), customCardTypes);
	}

	internal static void RegisterPlayerRuneModels(IEnumerable<Type> runeTypes)
	{
		RegisterModelsInPool(typeof(SharedRelicPool), HextechModelTypeIdentity.Distinct(runeTypes));
	}

	internal static void RegisterForgeModels(IEnumerable<Type> forgeTypes)
	{
		RegisterModelsInPool(typeof(SharedRelicPool), HextechModelTypeIdentity.Distinct(forgeTypes));
	}

	internal static void RegisterEventRelicModels(IEnumerable<Type> relicTypes)
	{
		RegisterModelsInPool(typeof(EventRelicPool), HextechModelTypeIdentity.Distinct(relicTypes));
	}

	internal static void CleanupMobileFirstModelRegistrationWorkaround()
	{
		if (MobileDuplicateRegistrations.Count == 0)
		{
			return;
		}

		foreach ((Type poolType, Type modelType) in MobileDuplicateRegistrations)
		{
			RemoveDuplicatePoolRegistration(poolType, modelType);
		}

		MobileDuplicateRegistrations.Clear();
	}

	private static void TryAddModelToPool(Type poolType, Type modelType)
	{
		if (IsModelAlreadyQueuedForPool(poolType, modelType) && !IsMobileFirstModelWorkaroundDuplicate(poolType, modelType))
		{
			HextechLog.Info($"[{ModInfo.Id}] Skipping duplicate pool registration for {modelType.FullName} in {poolType.FullName}.");
			return;
		}

		ModHelper.AddModelToPool(poolType, modelType);
	}

	private static void RegisterModelsInPool(Type poolType, IReadOnlyList<Type> modelTypes)
	{
		if (ShouldUseMobileFirstModelRegistrationWorkaround())
		{
			QueueMobileFirstModelRegistrationWorkaround(poolType, modelTypes);
		}

		foreach (Type modelType in modelTypes)
		{
			TryAddModelToPool(poolType, modelType);
		}

		CleanupDuplicatePoolRegistrations(poolType, modelTypes);
	}

	internal static bool IsModelAlreadyQueuedForPool(Type poolType, Type modelType)
	{
		try
		{
			if (ModdedContentForPoolsField?.GetValue(null) is not IDictionary pools)
			{
				return false;
			}

			foreach (DictionaryEntry entry in pools)
			{
				if (entry.Key is Type existingPoolType
					&& HextechModelTypeIdentity.IsSame(existingPoolType, poolType)
					&& ContentContainsModelType(entry.Value, modelType))
				{
					return true;
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}] Could not inspect existing mod pool registrations: {ex.GetType().Name}: {ex.Message}");
		}

		return false;
	}

	private static bool ShouldUseMobileFirstModelRegistrationWorkaround()
	{
		return HextechRuntimeRuneCompatibility.IsAndroidRuntime;
	}

	private static bool IsMobileFirstModelWorkaroundDuplicate(Type poolType, Type modelType)
	{
		return MobileDuplicateRegistrations.Any(entry =>
			HextechModelTypeIdentity.IsSame(entry.PoolType, poolType)
			&& HextechModelTypeIdentity.IsSame(entry.ModelType, modelType));
	}

	private static void QueueMobileFirstModelRegistrationWorkaround(Type poolType, IReadOnlyList<Type> modelTypes)
	{
		if (modelTypes.Count == 0)
		{
			return;
		}

		Type modelType = modelTypes[0];
		try
		{
			ModHelper.AddModelToPool(poolType, modelType);
			MobileDuplicateRegistrations.Add((poolType, modelType));
			Log.Warn($"[{ModInfo.Id}] Android model registration workaround queued first-model sentinel: pool={poolType.Name} model={modelType.Name}.");
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}] Android model registration workaround failed for {modelType.FullName}: {ex.GetType().Name}: {ex.Message}");
		}
	}

	private static void RemoveDuplicatePoolRegistration(Type poolType, Type modelType)
	{
		try
		{
			if (ModdedContentForPoolsField?.GetValue(null) is not IDictionary pools)
			{
				return;
			}

			foreach (DictionaryEntry entry in pools)
			{
				if (entry.Key is not Type existingPoolType || !HextechModelTypeIdentity.IsSame(existingPoolType, poolType))
				{
					continue;
				}

				FieldInfo? modelsField = entry.Value?.GetType().GetField("modelsToAdd", InstanceFields);
				if (modelsField?.GetValue(entry.Value) is not IList models)
				{
					continue;
				}

				int seen = 0;
				int removed = 0;
				for (int index = models.Count - 1; index >= 0; index--)
				{
					if (models[index] is not Type existingModelType || !HextechModelTypeIdentity.IsSame(existingModelType, modelType))
					{
						continue;
					}

					seen++;
					if (seen > 1)
					{
						models.RemoveAt(index);
						removed++;
					}
				}

				if (removed > 0)
				{
					HextechLog.Info($"[{ModInfo.Id}] Android model registration workaround cleaned duplicate entries: pool={poolType.Name} model={modelType.Name} removed={removed}.");
				}

				return;
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}] Android model registration workaround cleanup failed for {modelType.FullName}: {ex.GetType().Name}: {ex.Message}");
		}
	}

	private static void CleanupDuplicatePoolRegistrations(Type poolType, IReadOnlyList<Type> modelTypes)
	{
		foreach (Type modelType in modelTypes)
		{
			RemoveDuplicatePoolRegistration(poolType, modelType);
		}
	}

	private static bool ContentContainsModelType(object? content, Type modelType)
	{
		if (content == null)
		{
			return false;
		}

		FieldInfo? modelsField = content.GetType().GetField("modelsToAdd", InstanceFields);
		if (modelsField?.GetValue(content) is not IEnumerable models)
		{
			return false;
		}

		foreach (object? model in models)
		{
			if (model is Type existingModelType && HextechModelTypeIdentity.IsSame(existingModelType, modelType))
			{
				return true;
			}
		}

		return false;
	}
}
