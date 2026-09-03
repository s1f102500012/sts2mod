using System.Reflection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;

namespace HextechRunesSponsorPack;

internal static class IntegratedStrategyEventsBridge
{
	private const string AssemblyName = "IntegratedStrategyEvents";
	private const string ProphecyProjectionRelicTypeName = "IntegratedStrategyEvents.Relics.ProphecyProjectionRelic";
	private const string EndlessKeyRelicTypeName = "IntegratedStrategyEvents.Relics.EndlessKeyRelic";
	private const string FinalChoraleTypeName = "IntegratedStrategyEvents.Encounters.FinalChorale";
	private const int FinalChoraleRandomRelicRewardCount = 2;

	private static Type? ProphecyProjectionRelicType;
	private static Type? EndlessKeyRelicType;
	private static Type? FinalChoraleType;

	internal static bool IsAvailable => TryResolveTypes(out _, out _);

	internal static bool IsFinalChorale(Creature creature)
	{
		if (creature.Monster?.GetType().FullName == FinalChoraleTypeName)
		{
			return true;
		}

		return TryResolveTypes(out _, out Type? finalChoraleType)
			&& creature.Monster != null
			&& finalChoraleType.IsInstanceOfType(creature.Monster);
	}

	internal static async Task<bool> ObtainProphecyProjection(Player owner, int? choraleHp = null)
	{
		if (!TryCreateProphecyProjection(out RelicModel? projection))
		{
			return false;
		}

		if (choraleHp.HasValue)
		{
			ConfigureProjectionChoraleHp(projection, choraleHp.Value);
			await RemoveExistingProphecyProjections(owner);
		}

		await RelicCmd.Obtain(projection, owner);
		return true;
	}

	internal static void AddFinalChoraleRewardsIfMissing(CombatRoom room)
	{
		foreach (Player player in room.CombatState.Players)
		{
			if (HasEndlessKeyReward(room, player))
			{
				continue;
			}

			if (!TryCreateEndlessKey(out RelicModel? endlessKey))
			{
				Log.Warn($"[{ModInfo.Id}] Failed to add Final Chorale rewards: EndlessKeyRelic is unavailable.", 2);
				return;
			}

			for (int i = 0; i < FinalChoraleRandomRelicRewardCount; i++)
			{
				room.AddExtraReward(player, new RelicReward(player));
			}

			room.AddExtraReward(player, new RelicReward(endlessKey, player));
			Log.Info($"[{ModInfo.Id}] Added missing Final Chorale completion rewards for player {player.NetId}.");
		}
	}

	private static bool TryCreateProphecyProjection(out RelicModel projection)
	{
		projection = null!;
		if (!TryResolveTypes(out Type? projectionType, out _))
		{
			return false;
		}

		try
		{
			if (!ModelDb.Contains(projectionType))
			{
				Log.Warn($"[{ModInfo.Id}] Failed to create ProphecyProjectionRelic: model type is not registered in ModelDb.", 2);
				return false;
			}

			projection = ModelDb.GetById<RelicModel>(ModelDb.GetId(projectionType)).ToMutable();
			return true;
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}] Failed to create ProphecyProjectionRelic: {ex.Message}", 2);
			return false;
		}
	}

	private static bool TryCreateEndlessKey(out RelicModel relic)
	{
		relic = null!;
		if (!TryResolveEndlessKeyType(out Type? endlessKeyType))
		{
			return false;
		}

		try
		{
			if (!ModelDb.Contains(endlessKeyType))
			{
				Log.Warn($"[{ModInfo.Id}] Failed to create EndlessKeyRelic: model type is not registered in ModelDb.", 2);
				return false;
			}

			relic = ModelDb.GetById<RelicModel>(ModelDb.GetId(endlessKeyType)).ToMutable();
			return true;
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}] Failed to create EndlessKeyRelic: {ex.Message}", 2);
			return false;
		}
	}

	private static void ConfigureProjectionChoraleHp(RelicModel projection, int choraleHp)
	{
		int hp = Math.Max(1, choraleHp);
		bool ok = true;
		ok &= SetNonPublicProperty(projection, "SavedProphecyProjectionChoraleDefeated", false);
		ok &= SetNonPublicProperty(projection, "SavedProphecyProjectionChoraleRewardsGranted", false);
		ok &= SetNonPublicProperty(projection, "SavedProphecyProjectionUsesScaledChoraleHp", true);
		ok &= SetNonPublicProperty(projection, "SavedProphecyProjectionChoraleHp", hp);
		if (!ok)
		{
			Log.Warn($"[{ModInfo.Id}] ProphecyProjectionRelic HP bonus was only partially configured.", 2);
		}
	}

	private static async Task RemoveExistingProphecyProjections(Player owner)
	{
		if (!TryResolveTypes(out Type? projectionType, out _))
		{
			return;
		}

		List<RelicModel> existingProjections = [];
		foreach (Player player in owner.RunState.Players)
		{
			foreach (RelicModel relic in player.Relics)
			{
				if (!projectionType.IsInstanceOfType(relic)
					|| relic.IsMelted
					|| relic.HasBeenRemovedFromState)
				{
					continue;
				}

				existingProjections.Add(relic);
			}
		}

		foreach (RelicModel relic in existingProjections)
		{
			await RelicCmd.Remove(relic);
		}

		if (existingProjections.Count > 0)
		{
			Log.Info($"[{ModInfo.Id}] Removed {existingProjections.Count} old ProphecyProjectionRelic instance(s) before granting the empowered projection.");
		}
	}

	private static bool SetNonPublicProperty(object instance, string propertyName, object value)
	{
		PropertyInfo? property = instance.GetType().GetProperty(
			propertyName,
			BindingFlags.Instance | BindingFlags.NonPublic);
		MethodInfo? setter = property?.GetSetMethod(nonPublic: true);
		if (setter == null)
		{
			return false;
		}

		try
		{
			setter.Invoke(instance, [value]);
			return true;
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}] Failed to set {propertyName} on ProphecyProjectionRelic: {ex.Message}", 2);
			return false;
		}
	}

	private static bool HasEndlessKeyReward(CombatRoom room, Player player)
	{
		if (!room.ExtraRewards.TryGetValue(player, out List<Reward>? rewards))
		{
			return false;
		}

		return rewards.OfType<RelicReward>().Any(reward => IsEndlessKey(reward.Relic));
	}

	private static bool IsEndlessKey(RelicModel? relic)
	{
		return relic != null
			&& TryResolveEndlessKeyType(out Type? endlessKeyType)
			&& endlessKeyType.IsInstanceOfType(relic);
	}

	private static bool TryResolveTypes(out Type prophecyProjectionRelicType, out Type finalChoraleType)
	{
		if (ProphecyProjectionRelicType != null && FinalChoraleType != null)
		{
			prophecyProjectionRelicType = ProphecyProjectionRelicType;
			finalChoraleType = FinalChoraleType;
			return true;
		}

		if (!TryResolveAssembly(out Assembly? assembly))
		{
			prophecyProjectionRelicType = null!;
			finalChoraleType = null!;
			return false;
		}

		Type? projectionType = assembly.GetType(ProphecyProjectionRelicTypeName, throwOnError: false);
		Type? choraleType = assembly.GetType(FinalChoraleTypeName, throwOnError: false);
		if (projectionType == null
			|| choraleType == null
			|| !typeof(RelicModel).IsAssignableFrom(projectionType))
		{
			prophecyProjectionRelicType = null!;
			finalChoraleType = null!;
			return false;
		}

		ProphecyProjectionRelicType = projectionType;
		FinalChoraleType = choraleType;
		prophecyProjectionRelicType = projectionType;
		finalChoraleType = choraleType;
		return true;
	}

	private static bool TryResolveEndlessKeyType(out Type endlessKeyRelicType)
	{
		if (EndlessKeyRelicType != null)
		{
			endlessKeyRelicType = EndlessKeyRelicType;
			return true;
		}

		if (!TryResolveAssembly(out Assembly? assembly))
		{
			endlessKeyRelicType = null!;
			return false;
		}

		Type? relicType = assembly.GetType(EndlessKeyRelicTypeName, throwOnError: false);
		if (relicType == null || !typeof(RelicModel).IsAssignableFrom(relicType))
		{
			endlessKeyRelicType = null!;
			return false;
		}

		EndlessKeyRelicType = relicType;
		endlessKeyRelicType = relicType;
		return true;
	}

	private static bool TryResolveAssembly(out Assembly assembly)
	{
		assembly = AppDomain.CurrentDomain.GetAssemblies()
			.FirstOrDefault(static candidate => string.Equals(
				candidate.GetName().Name,
				AssemblyName,
				StringComparison.Ordinal))!;
		return assembly != null;
	}
}
