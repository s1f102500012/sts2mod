using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Rooms;

namespace AITeammate.Scripts;

internal sealed class ShopSnapshotBuilder
{
    private readonly CardEvaluationContextFactory _cardContextFactory = new();

    public ShopVisitState? Build(Player player)
    {
        if (player.RunState.CurrentRoom is not MerchantRoom merchantRoom)
        {
            return null;
        }

        ResolvedMerchantInventory? resolvedInventory = ShopInventoryResolver.Resolve(player);
        if (resolvedInventory == null)
        {
            return null;
        }

        CardEvaluationContext context = _cardContextFactory.Create(
            player,
            CardChoiceSource.Shop,
            skipAllowed: true,
            debugSource: "shop_snapshot");
        List<ShopDeckCard> deckEntries = BuildDeckEntries(player, context.DeckCards);

        MerchantInventory inventory = resolvedInventory.Inventory;
        bool inventoryIsOpen = resolvedInventory.ExecutionMode == ShopExecutionMode.VirtualAiDirect
            ? true
            : NRun.Instance?.MerchantRoom?.Inventory?.IsOpen == true;
        List<ShopOwnedPotion> ownedPotions = BuildOwnedPotions(player);
        List<ShopOffer> offers = BuildOffers(player, inventory);

        bool hasCourier = player.GetRelic<TheCourier>() != null;
        bool hasMembershipCard = player.GetRelic<MembershipCard>() != null;
        bool hasSozu = player.GetRelic<Sozu>() != null;
        bool hasHoarder = context.ModifierIds.Contains("HOARDER");
        bool hasUsableFoulPotion = ownedPotions.Any(static potion => potion.IsFoulPotion && potion.IsUsableAtMerchant);
        bool removalAvailable = inventory.CardRemovalEntry?.IsStocked == true &&
                                Hook.ShouldAllowMerchantCardRemoval(player.RunState, player);

        List<ShopAction> actions = BuildActions(
            inventoryIsOpen,
            resolvedInventory.ExecutionMode,
            hasUsableFoulPotion,
            removalAvailable,
            offers);

        return new ShopVisitState
        {
            PlayerId = player.NetId,
            Player = player,
            RuntimeInventory = inventory,
            InventoryOwnerPlayerId = inventory.Player?.NetId,
            RoomVisitKey = resolvedInventory.RoomVisitKey,
            ExecutionMode = resolvedInventory.ExecutionMode,
            VisitCompleted = resolvedInventory.VisitCompleted,
            RoomType = merchantRoom.GetType().Name,
            SnapshotFingerprint = BuildFingerprint(player, inventoryIsOpen, hasUsableFoulPotion, offers, actions),
            Gold = player.Gold,
            InventoryIsOpen = inventoryIsOpen,
            MaxPotionSlots = player.MaxPotionCount,
            FilledPotionSlots = player.Potions.Count(),
            HasOpenPotionSlots = player.HasOpenPotionSlots,
            HasUsableFoulPotion = hasUsableFoulPotion,
            HasCourier = hasCourier,
            HasMembershipCard = hasMembershipCard,
            HasSozu = hasSozu,
            HasHoarder = hasHoarder,
            CardRemovalAvailable = removalAvailable,
            DeckSummary = context.DeckSummary,
            DeckCards = context.DeckCards,
            DeckEntries = deckEntries,
            RelicIds = context.RelicIds,
            ModifierIds = context.ModifierIds,
            OwnedPotions = ownedPotions,
            Offers = offers,
            Actions = actions
        };
    }

    private List<ShopDeckCard> BuildDeckEntries(Player player, IReadOnlyList<ResolvedCardView> resolvedDeckCards)
    {
        List<ShopDeckCard> entries = [];
        for (int index = 0; index < player.Deck.Cards.Count; index++)
        {
            CardModel card = player.Deck.Cards[index];
            entries.Add(new ShopDeckCard
            {
                CardId = card.Id.Entry,
                Name = GetDisplayName(card.Title, card.Id.Entry),
                IsRemovable = card.IsRemovable,
                ResolvedCard = resolvedDeckCards[index],
                RuntimeCard = card
            });
        }

        return entries;
    }

    private List<ShopOwnedPotion> BuildOwnedPotions(Player player)
    {
        List<ShopOwnedPotion> potions = [];
        for (int slotIndex = 0; slotIndex < player.PotionSlots.Count; slotIndex++)
        {
            PotionModel? potion = player.PotionSlots[slotIndex];
            if (potion == null)
            {
                continue;
            }

            potions.Add(new ShopOwnedPotion
            {
                SlotIndex = slotIndex,
                PotionId = potion.Id.Entry,
                Name = GetDisplayName(potion.Title, potion.Id.Entry),
                IsFoulPotion = potion is FoulPotion,
                IsUsableAtMerchant = potion.PassesCustomUsabilityCheck
            });
        }

        return potions;
    }

    private List<ShopOffer> BuildOffers(Player player, MerchantInventory inventory)
    {
        List<ShopOffer> offers = [];

        for (int index = 0; index < inventory.CharacterCardEntries.Count; index++)
        {
            MerchantCardEntry entry = inventory.CharacterCardEntries[index];
            offers.Add(BuildCardOffer(player, entry, ShopOfferKind.CharacterCard, "character_card", index));
        }

        for (int index = 0; index < inventory.ColorlessCardEntries.Count; index++)
        {
            MerchantCardEntry entry = inventory.ColorlessCardEntries[index];
            offers.Add(BuildCardOffer(player, entry, ShopOfferKind.ColorlessCard, "colorless_card", index));
        }

        for (int index = 0; index < inventory.RelicEntries.Count; index++)
        {
            MerchantRelicEntry entry = inventory.RelicEntries[index];
            offers.Add(BuildRelicOffer(entry, index));
        }

        for (int index = 0; index < inventory.PotionEntries.Count; index++)
        {
            MerchantPotionEntry entry = inventory.PotionEntries[index];
            offers.Add(BuildPotionOffer(player, entry, index));
        }

        if (inventory.CardRemovalEntry != null)
        {
            offers.Add(BuildRemovalOffer(player, inventory.CardRemovalEntry));
        }

        return offers;
    }

    private ShopOffer BuildCardOffer(Player player, MerchantCardEntry entry, ShopOfferKind kind, string slotGroup, int slotIndex)
    {
        CardModel? cardModel = entry.CreationResult?.Card;
        ResolvedCardView? resolved = cardModel != null
            ? _cardContextFactory.ResolveCandidate(cardModel, slotIndex)
            : null;

        return new ShopOffer
        {
            OfferId = $"{slotGroup}_{slotIndex}_{Sanitize(entry.CreationResult?.Card?.Id.Entry ?? "empty")}",
            Kind = kind,
            Name = cardModel != null ? GetDisplayName(cardModel.Title, cardModel.Id.Entry) : "<empty>",
            ModelId = cardModel?.Id.Entry ?? "EMPTY",
            Cost = entry.Cost,
            IsStocked = entry.IsStocked,
            IsAffordable = entry.EnoughGold,
            IsPurchaseLegalNow = entry.IsStocked && entry.EnoughGold,
            RequiresInventoryOpen = true,
            Rarity = cardModel?.Rarity.ToString(),
            Category = resolved?.Type.ToString() ?? cardModel?.Type.ToString(),
            IsOnSale = entry.IsOnSale,
            ResolvedCard = resolved,
            RuntimeCardModel = cardModel,
            RuntimeLocator = new ShopRuntimeLocator
            {
                LocatorId = $"{slotGroup}:{slotIndex}",
                SlotGroup = slotGroup,
                SlotIndex = slotIndex,
                Entry = entry
            }
        };
    }

    private ShopOffer BuildRelicOffer(MerchantRelicEntry entry, int slotIndex)
    {
        RelicModel? relicModel = entry.Model;
        return new ShopOffer
        {
            OfferId = $"relic_{slotIndex}_{Sanitize(relicModel?.Id.Entry ?? "empty")}",
            Kind = ShopOfferKind.Relic,
            Name = relicModel != null ? GetDisplayName(relicModel.Title, relicModel.Id.Entry) : "<empty>",
            ModelId = relicModel?.Id.Entry ?? "EMPTY",
            Cost = entry.Cost,
            IsStocked = entry.IsStocked,
            IsAffordable = entry.EnoughGold,
            IsPurchaseLegalNow = entry.IsStocked && entry.EnoughGold,
            RequiresInventoryOpen = true,
            Rarity = relicModel?.Rarity.ToString(),
            Category = "Relic",
            RuntimeRelicModel = relicModel,
            RuntimeLocator = new ShopRuntimeLocator
            {
                LocatorId = $"relic:{slotIndex}",
                SlotGroup = "relic",
                SlotIndex = slotIndex,
                Entry = entry
            }
        };
    }

    private ShopOffer BuildPotionOffer(Player player, MerchantPotionEntry entry, int slotIndex)
    {
        PotionModel? potionModel = entry.Model;
        bool canProcure = potionModel != null &&
                          Hook.ShouldProcurePotion(player.RunState, player.Creature.CombatState, potionModel, player) &&
                          player.HasOpenPotionSlots;

        return new ShopOffer
        {
            OfferId = $"potion_{slotIndex}_{Sanitize(potionModel?.Id.Entry ?? "empty")}",
            Kind = ShopOfferKind.Potion,
            Name = potionModel != null ? GetDisplayName(potionModel.Title, potionModel.Id.Entry) : "<empty>",
            ModelId = potionModel?.Id.Entry ?? "EMPTY",
            Cost = entry.Cost,
            IsStocked = entry.IsStocked,
            IsAffordable = entry.EnoughGold,
            IsPurchaseLegalNow = entry.IsStocked && entry.EnoughGold && canProcure,
            RequiresInventoryOpen = true,
            Rarity = potionModel?.Rarity.ToString(),
            Category = "Potion",
            RuntimePotionModel = potionModel,
            RuntimeLocator = new ShopRuntimeLocator
            {
                LocatorId = $"potion:{slotIndex}",
                SlotGroup = "potion",
                SlotIndex = slotIndex,
                Entry = entry
            }
        };
    }

    private ShopOffer BuildRemovalOffer(Player player, MerchantCardRemovalEntry entry)
    {
        bool removalAllowed = Hook.ShouldAllowMerchantCardRemoval(player.RunState, player);
        return new ShopOffer
        {
            OfferId = "card_removal_service",
            Kind = ShopOfferKind.CardRemoval,
            Name = "Card Removal",
            ModelId = "CARD_REMOVAL",
            Cost = entry.Cost,
            IsStocked = entry.IsStocked && removalAllowed,
            IsAffordable = entry.EnoughGold,
            IsPurchaseLegalNow = entry.IsStocked && removalAllowed && entry.EnoughGold,
            RequiresInventoryOpen = true,
            Category = "Service",
            RuntimeLocator = new ShopRuntimeLocator
            {
                LocatorId = "card_removal:0",
                SlotGroup = "card_removal",
                SlotIndex = 0,
                Entry = entry
            }
        };
    }

    private static List<ShopAction> BuildActions(
        bool inventoryIsOpen,
        ShopExecutionMode executionMode,
        bool hasUsableFoulPotion,
        bool removalAvailable,
        IReadOnlyList<ShopOffer> offers)
    {
        List<ShopAction> actions = [];

        if (executionMode == ShopExecutionMode.LocalSharedUi)
        {
            actions.Add(new ShopAction
            {
                ActionId = "shop_open_inventory",
                Kind = ShopActionKind.OpenInventory,
                Description = "Open merchant inventory.",
                IsCurrentlyLegal = !inventoryIsOpen,
                RequiresInventoryOpen = false
            });
        }

        foreach (ShopOffer offer in offers.Where(static offer => offer.Kind != ShopOfferKind.CardRemoval))
        {
            actions.Add(new ShopAction
            {
                ActionId = $"shop_buy_{offer.OfferId}",
                Kind = ShopActionKind.BuyOffer,
                Description = $"Buy {offer.Name}.",
                IsCurrentlyLegal = inventoryIsOpen && offer.IsPurchaseLegalNow,
                GoldCost = offer.Cost,
                OfferId = offer.OfferId,
                ModelId = offer.ModelId,
                RequiresInventoryOpen = true,
                RuntimeLocator = offer.RuntimeLocator
            });
        }

        ShopOffer? removalOffer = offers.FirstOrDefault(static offer => offer.Kind == ShopOfferKind.CardRemoval);
        if (removalOffer != null)
        {
            actions.Add(new ShopAction
            {
                ActionId = "shop_remove_card",
                Kind = ShopActionKind.RemoveCard,
                Description = "Use merchant card removal service. Card target selection is deferred.",
                IsCurrentlyLegal = inventoryIsOpen && removalAvailable && removalOffer.IsAffordable,
                GoldCost = removalOffer.Cost,
                OfferId = removalOffer.OfferId,
                ModelId = removalOffer.ModelId,
                RequiresInventoryOpen = true,
                RuntimeLocator = removalOffer.RuntimeLocator
            });
        }

        if (hasUsableFoulPotion)
        {
            actions.Add(new ShopAction
            {
                ActionId = "shop_use_foul_potion",
                Kind = ShopActionKind.UseFoulPotionAtMerchant,
                Description = "Use owned Foul Potion at the normal merchant.",
                IsCurrentlyLegal = true,
                ModelId = nameof(FoulPotion),
                RequiresInventoryOpen = false
            });
        }

        if (executionMode == ShopExecutionMode.LocalSharedUi)
        {
            actions.Add(new ShopAction
            {
                ActionId = "shop_close_inventory",
                Kind = ShopActionKind.CloseInventory,
                Description = "Close merchant inventory.",
                IsCurrentlyLegal = inventoryIsOpen,
                RequiresInventoryOpen = true
            });
        }

        actions.Add(new ShopAction
        {
            ActionId = "shop_leave",
            Kind = ShopActionKind.LeaveShop,
            Description = "Leave the merchant room via proceed.",
            IsCurrentlyLegal = executionMode == ShopExecutionMode.VirtualAiDirect || !inventoryIsOpen,
            RequiresInventoryOpen = false,
            EndsVisit = true
        });

        return actions;
    }

    private static string BuildFingerprint(
        Player player,
        bool inventoryIsOpen,
        bool hasUsableFoulPotion,
        IEnumerable<ShopOffer> offers,
        IEnumerable<ShopAction> actions)
    {
        string offerState = string.Join(
            "|",
            offers.Select(static offer =>
                $"{offer.OfferId}:{offer.Cost}:{offer.IsStocked}:{offer.IsAffordable}:{offer.IsPurchaseLegalNow}"));
        string actionState = string.Join(
            "|",
            actions.Select(static action =>
                $"{action.ActionId}:{action.IsCurrentlyLegal}:{action.GoldCost?.ToString() ?? "n"}"));
        string potionState = string.Join(
            "|",
            player.PotionSlots.Select(static potion => potion?.Id.Entry ?? "empty"));
        return $"gold={player.Gold};inventoryOpen={inventoryIsOpen};foul={hasUsableFoulPotion};potions={potionState};offers={offerState};actions={actionState}";
    }

    private static string Sanitize(string value)
    {
        return value.Replace(':', '_').Replace('/', '_').Replace(' ', '_');
    }

    private static string GetDisplayName(object? title, string fallback)
    {
        if (title is LocString locString)
        {
            string raw = GetRawLocText(locString);
            return string.IsNullOrWhiteSpace(raw) ? fallback : raw;
        }

        string? text = title?.ToString();
        return string.IsNullOrWhiteSpace(text) ? fallback : text;
    }

    private static string GetRawLocText(LocString locString)
    {
        if (locString.IsEmpty)
        {
            return string.Empty;
        }

        try
        {
            string raw = locString.GetRawText();
            if (!string.IsNullOrWhiteSpace(raw))
            {
                return raw;
            }
        }
        catch (LocException)
        {
        }

        return locString.LocEntryKey ?? string.Empty;
    }
}
