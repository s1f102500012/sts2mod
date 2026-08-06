using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using CoreHook = MegaCrit.Sts2.Core.Hooks.Hook;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

internal static class HextechShopForgeHooks
{
	private const int RandomForgeShopRegularCost = 250;
	private const float CardRemovalRandomForgeOffsetY = 60f;

	private static readonly FieldInfo? MerchantInventoryRelicEntriesField = TryGetField(typeof(MerchantInventory), "_relicEntries");
	private static readonly Dictionary<ulong, Vector2> CardRemovalOriginalPositions = [];

	public static void Install(Harmony harmony)
	{
		bool purchaseHookInstalled = TryPatch(
			harmony,
			() => RequireMethod(typeof(MerchantRelicEntry), "OnTryPurchase", BindingFlags.Instance | BindingFlags.NonPublic, typeof(MerchantInventory), typeof(bool)),
			"random forge purchase",
			prefix: new HarmonyMethod(typeof(HextechShopForgeHooks), nameof(MerchantRelicPurchasePrefix)));
		bool restockHookInstalled = TryPatch(
			harmony,
			() => RequireMethod(typeof(MerchantRelicEntry), "RestockAfterPurchase", BindingFlags.Instance | BindingFlags.NonPublic, typeof(MerchantInventory)),
			"random forge restock",
			prefix: new HarmonyMethod(typeof(HextechShopForgeHooks), nameof(MerchantRelicRestockPrefix)));
		bool priceHookInstalled = TryPatch(
			harmony,
			() => RequireMethod(typeof(CoreHook), nameof(CoreHook.ModifyMerchantPrice), BindingFlags.Static | BindingFlags.Public, typeof(IRunState), typeof(Player), typeof(MerchantEntry), typeof(decimal)),
			"random forge price",
			prefix: new HarmonyMethod(typeof(HextechShopForgeHooks), nameof(ModifyMerchantPricePrefix)));
		bool refillHookInstalled = TryPatch(
			harmony,
			() => RequireMethod(typeof(CoreHook), nameof(CoreHook.ShouldRefillMerchantEntry), BindingFlags.Static | BindingFlags.Public, typeof(IRunState), typeof(MerchantEntry), typeof(Player)),
			"random forge refill",
			prefix: new HarmonyMethod(typeof(HextechShopForgeHooks), nameof(ShouldRefillMerchantEntryPrefix)));

		if (!purchaseHookInstalled || !restockHookInstalled || !priceHookInstalled || !refillHookInstalled)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Random forge shop entry disabled because one or more merchant hooks are unavailable.");
			return;
		}

		TryPatch(
			harmony,
			() => RequireMethod(typeof(MerchantInventory), nameof(MerchantInventory.CreateForNormalMerchant), BindingFlags.Static | BindingFlags.Public, typeof(Player)),
			"random forge merchant entry",
			postfix: new HarmonyMethod(typeof(HextechShopForgeHooks), nameof(CreateForNormalMerchantPostfix)));
		TryPatch(
			harmony,
			() => RequireMethod(typeof(NMerchantInventory), nameof(NMerchantInventory.Initialize), BindingFlags.Instance | BindingFlags.Public, typeof(MerchantInventory), typeof(MerchantDialogueSet)),
			"random forge shop layout",
			prefix: new HarmonyMethod(typeof(HextechShopForgeHooks), nameof(MerchantInventoryInitializePrefix)),
			postfix: new HarmonyMethod(typeof(HextechShopForgeHooks), nameof(MerchantInventoryInitializePostfix)));
		TryPatch(
			harmony,
			() => RequireMethod(typeof(NMerchantRelic), "OnSuccessfulPurchase", BindingFlags.Instance | BindingFlags.NonPublic, typeof(PurchaseStatus), typeof(MerchantEntry)),
			"random forge purchase animation",
			prefix: new HarmonyMethod(typeof(HextechShopForgeHooks), nameof(MerchantRelicSuccessfulPurchasePrefix)));
	}

	private static bool TryPatch(Harmony harmony, Func<MethodInfo> resolveTarget, string label, HarmonyMethod? prefix = null, HarmonyMethod? postfix = null)
	{
		try
		{
			MethodInfo target = resolveTarget();
			harmony.Patch(target, prefix, postfix);
			return true;
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Shop random forge hook skipped: {label}: {ex.GetType().Name}: {ex.Message}");
			return false;
		}
	}

	private static void CreateForNormalMerchantPostfix(Player player, MerchantInventory __result)
	{
		InstallRandomForgeEntry(__result, player);
	}

	private static void ModifyMerchantPricePrefix(MerchantEntry entry, ref decimal result)
	{
		if (TryGetRandomForgeShopRelic(entry, out RandomForgeShopRelic? shopRelic) && shopRelic != null)
		{
			HextechForgeShopPriceHelper.RefreshRandomForgeShopRelic(shopRelic, shopRelic.Owner?.RunState as RunState);
			result = GetRandomForgeShopBaseCost(shopRelic);
		}
	}

	private static bool ShouldRefillMerchantEntryPrefix(MerchantEntry entry, ref bool __result)
	{
		if (!IsRandomForgeEntry(entry))
		{
			return true;
		}

		__result = true;
		return false;
	}

	private static void MerchantInventoryInitializePrefix(NMerchantInventory __instance, MerchantInventory inventory)
	{
		if (IsFakeMerchantInventory(__instance))
		{
			RemoveRandomForgeEntries(inventory);
			return;
		}

		InstallRandomForgeEntry(inventory, inventory.Player);
		EnsureRandomForgeRelicSlot(__instance, inventory);
	}

	private static void MerchantInventoryInitializePostfix(NMerchantInventory __instance, MerchantInventory inventory)
	{
		if (IsFakeMerchantInventory(__instance))
		{
			return;
		}

		MoveCardRemovalBelowRandomForge(__instance, inventory);
	}

	private static bool MerchantRelicPurchasePrefix(MerchantRelicEntry __instance, MerchantInventory inventory, bool ignoreCost, ref Task<(bool, int)> __result)
	{
		if (!IsRandomForgeEntry(__instance))
		{
			return true;
		}

		__result = PurchaseRandomForge(__instance, inventory, ignoreCost);
		return false;
	}

	private static bool MerchantRelicRestockPrefix(MerchantRelicEntry __instance)
	{
		return !IsRandomForgeEntry(__instance);
	}

	private static bool MerchantRelicSuccessfulPurchasePrefix(NMerchantRelic __instance)
	{
		if (!IsRandomForgeEntry(__instance.Entry))
		{
			return true;
		}

		__instance.Entry.OnMerchantInventoryUpdated();
		HextechLog.Info($"[{ModInfo.Id}][Mayhem] Skipped merchant relic inventory animation for random forge placeholder.");
		return false;
	}

	private static void InstallRandomForgeEntry(MerchantInventory inventory, Player player)
	{
		if (!IsModEnabledForRun(player))
		{
			return;
		}

		if (inventory.RelicEntries.Any(IsRandomForgeEntry))
		{
			return;
		}

		RandomForgeShopRelic shopRelic = (RandomForgeShopRelic)ModelDb.Relic<RandomForgeShopRelic>().ToMutable();
		HextechForgeShopPriceHelper.RefreshRandomForgeShopRelic(shopRelic, player.RunState as RunState);
		MerchantRelicEntry entry = new(shopRelic, player);
		entry.PurchaseCompleted += (_, _) => UpdateInventoryEntries(inventory);
		inventory.AddRelicEntry(entry);
	}

	// 模组总开关:商店随机锻造器是无条件注入普通局的少数泄漏点之一,按本局冻结值门控。
	// 无 run/modifier 时:联机局固定 false(实时本地配置在两端可能不同,而这里门控的是
	// MerchantInventory 模型写入,按本地配置各走一边会库存分叉);单机局退回实时配置。
	private static bool IsModEnabledForRun(Player? player)
	{
		HextechMayhemModifier? modifier = (player?.RunState as RunState)?.Modifiers
			.OfType<HextechMayhemModifier>()
			.LastOrDefault();
		if (modifier != null)
		{
			return modifier.IsModActiveForRun;
		}

		return !HextechPlayerContextHelper.IsNetworkMultiplayerRun() && HextechRuneConfiguration.GetModEnabled();
	}

	private static async Task<(bool, int)> PurchaseRandomForge(MerchantRelicEntry entry, MerchantInventory inventory, bool ignoreCost)
	{
		Player player = inventory.Player;
		if (TryGetRandomForgeShopRelic(entry, out RandomForgeShopRelic? activeShopRelic) && activeShopRelic != null)
		{
			HextechForgeShopPriceHelper.RefreshRandomForgeShopRelic(activeShopRelic, player.RunState as RunState);
		}

		int cost = TryGetRandomForgeShopRelic(entry, out RandomForgeShopRelic? shopRelic) && shopRelic != null
			? entry.Cost
			: RandomForgeShopRegularCost;

		int purchaseOrdinal = shopRelic?.PurchaseCount ?? 0;
		if (!HextechForgeGrantHelper.TryCreateStableShopForgeChoice(player, purchaseOrdinal, out List<RelicModel> options))
		{
#if STS2_104_OR_NEWER
			entry.InvokePurchaseFailed(PurchaseStatus.FailureOutOfStock);
#else
			entry.InvokePurchaseFailed(PurchaseStatus.FailureForbidden);
#endif
			return (false, 0);
		}

		RelicModel? forge = await HextechForgeSelectionCoordinator.SelectForge(player, options, "shop", syncMultiplayerChoice: false);
		if (forge == null)
		{
			return (false, 0);
		}

		// 主机权威复核:被配置禁用的锻造器即便因配置同步时序混进了候选,也要在扣钱前挡下,避免客机白花金币又拿到被禁锻造器。
		if (HextechForgeGrantHelper.IsForgeDisabledForPlayer(player, forge))
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Blocked purchasing a config-disabled forge: player={player.NetId} relic={(forge.CanonicalInstance?.Id ?? forge.Id).Entry}");
#if STS2_104_OR_NEWER
			entry.InvokePurchaseFailed(PurchaseStatus.FailureOutOfStock);
#else
			entry.InvokePurchaseFailed(PurchaseStatus.FailureForbidden);
#endif
			return (false, 0);
		}

		if (!CanContinueSynchronizedPurchase())
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Random forge purchase cancelled because multiplayer service is disconnected.");
			return (false, 0);
		}

		if (!ignoreCost)
		{
			await PlayerCmd.LoseGold(cost, player, GoldLossType.Spent);
			if (CanSyncMultiplayerReward())
			{
				RunManager.Instance.RewardSynchronizer.SyncLocalGoldLost(cost);
			}
		}

		player.RunState.CurrentMapPointHistoryEntry?
			.GetEntry(player.NetId)
			.BoughtRelics
			.Add(forge.Id);

		await HextechForgeGrantHelper.ObtainSelectedForge(player, forge, syncObtainedRelic: true);

		// 复视复制商店购买的锻造器:商店购买不走锻造奖励(HextechForgeChoiceReward)那条已支持的复制路径,
		// 且直接 RelicCmd.Obtain 会被复视的「本模组程序集」闸门跳过,故在此显式触发一次(复用锻造奖励同款逻辑)。
		// 已付费购买不应因复制异常而中断,故防御性兜底;复制份自身走 syncObtainedRelic 广播,联机一致。
		try
		{
			await DoubleVisionRune.DuplicatePurchasedForge(player, forge);
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Double Vision failed to duplicate purchased forge: player={player.NetId} relic={(forge.CanonicalInstance?.Id ?? forge.Id).Entry}: {ex.GetType().Name}: {ex.Message}");
		}

		if (shopRelic != null)
		{
			shopRelic.IncrementPurchaseCount();
			entry.OnMerchantInventoryUpdated();
		}
		return (true, ignoreCost ? 0 : cost);
	}

	private static bool CanContinueSynchronizedPurchase()
	{
		INetGameService netService = RunManager.Instance.NetService;
		return netService.Type is not (NetGameType.Host or NetGameType.Client) || netService.IsConnected;
	}

	private static bool CanSyncMultiplayerReward()
	{
		INetGameService netService = RunManager.Instance.NetService;
		return netService.Type is NetGameType.Host or NetGameType.Client && netService.IsConnected;
	}

	private static bool IsRandomForgeEntry(MerchantEntry entry)
	{
		return entry is MerchantRelicEntry relicEntry && HextechCatalog.IsHextechShopRelic(relicEntry.Model);
	}

	private static bool IsFakeMerchantInventory(NMerchantInventory merchantInventory)
	{
		return merchantInventory is NFakeMerchantInventory;
	}

	private static void RemoveRandomForgeEntries(MerchantInventory inventory)
	{
		if (!inventory.RelicEntries.Any(IsRandomForgeEntry))
		{
			return;
		}

		if (MerchantInventoryRelicEntriesField?.GetValue(inventory) is List<MerchantRelicEntry> relicEntries)
		{
			relicEntries.RemoveAll(IsRandomForgeEntry);
		}
	}

	private static bool TryGetRandomForgeShopRelic(MerchantEntry entry, out RandomForgeShopRelic? shopRelic)
	{
		shopRelic = entry is MerchantRelicEntry relicEntry ? relicEntry.Model as RandomForgeShopRelic : null;
		return shopRelic != null;
	}

	private static int GetRandomForgeShopBaseCost(RandomForgeShopRelic shopRelic)
	{
		return HextechForgeShopPriceHelper.GetRandomForgeShopPriceFor(shopRelic.Owner?.RunState as RunState);
	}

	private static void UpdateInventoryEntries(MerchantInventory inventory)
	{
		foreach (MerchantEntry entry in inventory.AllEntries)
		{
			if (TryGetRandomForgeShopRelic(entry, out RandomForgeShopRelic? shopRelic) && shopRelic != null)
			{
				HextechForgeShopPriceHelper.RefreshRandomForgeShopRelic(shopRelic, inventory.Player.RunState as RunState);
			}

			entry.OnMerchantInventoryUpdated();
		}
	}

	private static void EnsureRandomForgeRelicSlot(NMerchantInventory merchantInventory, MerchantInventory inventory)
	{
		if (!inventory.RelicEntries.Any(IsRandomForgeEntry))
		{
			return;
		}

		if (merchantInventory.GetNodeOrNull<Control>("%Relics") is not Control relicContainer)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Random forge shop slot skipped: relic container unavailable.");
			return;
		}

		List<NMerchantRelic> relicSlots = relicContainer.GetChildren().OfType<NMerchantRelic>().ToList();
		while (relicSlots.Count < inventory.RelicEntries.Count)
		{
			NMerchantRelic? template = relicSlots.LastOrDefault();
			if (template == null)
			{
				Log.Warn($"[{ModInfo.Id}][Mayhem] Random forge shop slot skipped: no relic slot template available.");
				return;
			}

			Node duplicatedNode = template.Duplicate();
			if (duplicatedNode is not NMerchantRelic extraSlot)
			{
				duplicatedNode.QueueFree();
				Log.Warn($"[{ModInfo.Id}][Mayhem] Random forge shop slot skipped: duplicated node is not a merchant relic slot.");
				return;
			}

			extraSlot.Name = $"{template.Name}_HextechExtra{relicSlots.Count}";
			extraSlot.Position = template.Position + GetNextSlotOffset(relicSlots);
			relicContainer.AddChild(extraSlot);
			relicSlots.Add(extraSlot);
		}
	}

	private static void MoveCardRemovalBelowRandomForge(NMerchantInventory merchantInventory, MerchantInventory inventory)
	{
		if (!inventory.RelicEntries.Any(IsRandomForgeEntry))
		{
			return;
		}

		object? cardRemovalNode = merchantInventory.GetNodeOrNull<NMerchantCardRemoval>("%MerchantCardRemoval");
		if (!TryMoveCardRemovalNode(cardRemovalNode, new Vector2(0f, CardRemovalRandomForgeOffsetY)))
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Random forge shop card removal offset skipped: card removal node unavailable.");
		}
	}

	private static bool TryMoveCardRemovalNode(object? cardRemovalNode, Vector2 offset)
	{
		switch (cardRemovalNode)
		{
			case Control control:
				control.Position = GetOriginalCardRemovalPosition(control, control.Position) + offset;
				return true;
			case Node2D node:
				node.Position = GetOriginalCardRemovalPosition(node, node.Position) + offset;
				return true;
			default:
				return false;
		}
	}

	private static Vector2 GetOriginalCardRemovalPosition(GodotObject node, Vector2 currentPosition)
	{
		ulong instanceId = node.GetInstanceId();
		if (!CardRemovalOriginalPositions.TryGetValue(instanceId, out Vector2 originalPosition))
		{
			originalPosition = currentPosition;
			CardRemovalOriginalPositions[instanceId] = originalPosition;
		}

		return originalPosition;
	}

	private static Vector2 GetNextSlotOffset(IReadOnlyList<NMerchantRelic> relicSlots)
	{
		if (relicSlots.Count >= 2)
		{
			Vector2 offset = relicSlots[^1].Position - relicSlots[^2].Position;
			if (offset.LengthSquared() > 1f)
			{
				return offset;
			}
		}

		return new Vector2(160f, 0f);
	}

}
