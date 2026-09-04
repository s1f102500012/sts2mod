# Modding Decisions

## AI teammate character setup UI

- Keep the existing custom Godot control layout because it already owns the local AI teammate slot flow and lobby wiring.
- Reuse original UI nodes/resources instead of approximating them with custom colors: `CharSelectButtons/ButtonContainer` provides the character-button placement, `char_select_button.tscn` provides the portrait frame/outline/mask, `confirm_button.tscn` provides the ready/start visual, and `RemotePlayerContainer` provides the multiplayer player list.
- Keep transparent Godot `Button` nodes around those stock visuals so the custom AI teammate flow can still own click routing without relying on the original character select screen's model state.
- Load UI strings through `AiTeammateLocalization`, with JSON-formatted `.loc` override files under `config/localization/{eng,zhs}/ai_teammate_ui.loc`; the non-`.json` extension avoids STS2's recursive mod manifest scanner.
- Avoid replacing the setup screen with the original character select scene for now because the AI teammate screen needs custom slot and session controls.
- In the stock AI multiplayer screen, clicking a lobby player slot also syncs the original character select preview to that player's current character. This keeps repeated same-character assignment working without requiring the user to bounce through a different character first.
- AI teammate display names are derived from their selected character, with deterministic duplicate-name pools per character. The host keeps the platform player name instead of being renamed to a generic host label.
- When an AI slot changes character in the stock character select screen, refresh the original `NRemoteLobbyPlayer` nameplate after `PlayerChanged` so the left-side player list immediately follows the new selected character.
- Character-based AI display names now use a stable random pick from the per-character name pool, with duplicate avoidance inside the current lobby/session, instead of always taking names in list order.

## AI teammate merchant execution

- Do not execute virtual AI merchant card removal through `MerchantCardRemovalEntry.OnTryPurchaseWrapper`, because the stock removal path calls `OneOffSynchronizer.DoLocalMerchantCardRemoval(LocalPlayer)` and opens the host player's deck-removal selector.
- For virtual AI inventories, remove the chosen AI-owned deck card through the stock command layer (`PlayerCmd.LoseGold`, `CardPileCmd.RemoveFromDeck`), then mark that virtual removal entry used and fire the merchant purchase hook/completion event. This keeps the operation scoped to the AI player without showing removal UI to a non-autopiloted host.

## Combat AI potion and scaling strategy

- Treat `AbstractRoom.RoomType` as the source of truth for Boss/Elite combat, because real boss fights can still use the `CombatRoom` runtime class.
- In elite/boss or otherwise long fights, spend high-leverage setup potions more readily instead of waiting until the hand is empty: focus/capacity/cultist potions count as scaling, liquid bronze counts as attack-punish defense, and attack/skill/power/gambler/entropic style potions count as hand-fixing.
- Keep fairy/revive-style conservation potions excluded from forced slot-dumping bonuses.
- For Defect-like decks, raise the reward value of focus and orb-slot scaling once the deck already has orb density, especially from Act 2 onward.
