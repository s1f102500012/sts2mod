using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Daily;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Multiplayer.Messages.Lobby;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.RunHistoryScreen;
using MegaCrit.Sts2.Core.Odds;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.sts2.Core.Nodes.TopBar;
using DetourHook = MonoMod.RuntimeDetour.Hook;
using GameHook = MegaCrit.Sts2.Core.Hooks.Hook;

namespace MoreAscensionChallenge;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
	private const int BaseGameMaxAscension = 10;
	private const int MinExtraAscension = 11;
	private const int MaxExtraAscension = 15;
	private const decimal MissingHealthHealFactor = 0.3m;
	private static readonly HashSet<MapPointType> LowerMapPointRestrictions = new()
	{
		MapPointType.RestSite,
		MapPointType.Elite
	};
	private static readonly HashSet<MapPointType> UpperMapPointRestrictions = new()
	{
		MapPointType.RestSite
	};
	private static readonly HashSet<MapPointType> ParentMapPointRestrictions = new()
	{
		MapPointType.Elite,
		MapPointType.RestSite,
		MapPointType.Treasure,
		MapPointType.Shop
	};
	private static readonly HashSet<MapPointType> ChildMapPointRestrictions = new()
	{
		MapPointType.Elite,
		MapPointType.RestSite,
		MapPointType.Treasure,
		MapPointType.Shop
	};
	private static readonly HashSet<MapPointType> SiblingPointTypeRestrictions = new()
	{
		MapPointType.RestSite,
		MapPointType.Monster,
		MapPointType.Unknown,
		MapPointType.Elite,
		MapPointType.Shop
	};

	private static readonly string[] ExtraTitlesZh =
	{
		string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
		string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
		"补给短缺",
		"路途匆匆",
		"浅眠难安",
		"进阶之灾+",
		"降级"
	};

	private static readonly string[] ExtraTitlesEn =
	{
		string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
		string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
		"Supply Shortage",
		"Shorter Journey",
		"Restless Sleep",
		"Ascender's Bane+",
		"Downgraded"
	};

	private static readonly string[] ExtraDescriptionsZh =
	{
		string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
		string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
		"[gold]药水[/gold]掉落更加稀少。",
		"每阶段地图长度更短。",
		"休息处的生命回复量降低。",
		"进阶之灾升级为进阶之灾+。",
		"不再掉落已升级卡牌。"
	};

	private static readonly string[] ExtraDescriptionsEn =
	{
		string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
		string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
		"[gold]Potion[/gold] drops become rarer.",
		"Each act map becomes shorter.",
		"Rest site healing is reduced.",
		"Ascender's Bane is upgraded to Ascender's Bane+.",
		"Upgraded cards no longer appear."
	};

	private static readonly FieldInfo AscensionPanelMaxAscensionField = RequireField(typeof(NAscensionPanel), "_maxAscension");
	private static readonly FieldInfo AscensionPanelInfoField = RequireField(typeof(NAscensionPanel), "_info");
	private static readonly FieldInfo AscensionPanelLevelLabelField = RequireField(typeof(NAscensionPanel), "_ascensionLevel");
	private static readonly FieldInfo AscensionPanelModeField = RequireField(typeof(NAscensionPanel), "_mode");

	private static readonly FieldInfo CharacterSelectLobbyField = RequireField(typeof(NCharacterSelectScreen), "_lobby");
	private static readonly FieldInfo CharacterSelectPanelField = RequireField(typeof(NCharacterSelectScreen), "_ascensionPanel");

	private static readonly FieldInfo PortraitTipShowTipField = RequireField(typeof(NTopBarPortraitTip), "_showTip");
	private static readonly FieldInfo PortraitTipHoverTipField = RequireField(typeof(NTopBarPortraitTip), "_hoverTip");
	private static readonly FieldInfo RunHistoryPlayerIconHoverTipsField = RequireField(typeof(NRunHistoryPlayerIcon), "_hoverTips");
	private static readonly FieldInfo RunHistoryPlayerIconAscensionIconField = RequireField(typeof(NRunHistoryPlayerIcon), "_ascensionIcon");
	private static readonly FieldInfo RunHistoryPlayerIconAscensionLabelField = RequireField(typeof(NRunHistoryPlayerIcon), "_ascensionLabel");
	private static readonly FieldInfo RunHistoryPlayerIconSelectionReticleField = RequireField(typeof(NRunHistoryPlayerIcon), "_selectionReticle");
	private static readonly FieldInfo RunHistoryPlayerIconCurrentIconField = RequireField(typeof(NRunHistoryPlayerIcon), "_currentIcon");
	private static readonly MethodInfo RunHistoryPlayerIconPlayerSetter = RequirePropertySetter(typeof(NRunHistoryPlayerIcon), nameof(NRunHistoryPlayerIcon.Player), BindingFlags.Instance | BindingFlags.Public);

	private static readonly FieldInfo AbstractOddsRngField = RequireField(typeof(AbstractOdds), "_rng");
	private static readonly FieldInfo StartRunLobbyMaxAscensionField = RequireField(typeof(StartRunLobby), "<MaxAscension>k__BackingField");
	private static readonly FieldInfo RestSiteRoomRunStateField = RequireField(typeof(NRestSiteRoom), "_runState");
	private static readonly PropertyInfo RunManagerStateProperty = RequireProperty(typeof(RunManager), "State", BindingFlags.Instance | BindingFlags.NonPublic);

	private const string PreferenceFileName = "more_ascension_prefs.json";
	private static Dictionary<string, int> ExtraAscensionPrefs = new(StringComparer.Ordinal);
	private static string? ExtraAscensionPrefsPath;
	private static string? PendingRestoreCharacterEntry;
	private static int? PendingRestoreAscension;
	private static int? ActiveMapGenerationAscensionLevel;

	private static DetourHook? _setMaxAscensionHook;
	private static DetourHook? _refreshAscensionTextHook;
	private static DetourHook? _portraitTipInitializeHook;
	private static DetourHook? _updatePreferredAscensionHook;
	private static DetourHook? _setLocalCharacterHook;
	private static DetourHook? _setSingleplayerAscensionAfterCharacterChangedHook;
	private static DetourHook? _syncAscensionChangeHook;
	private static DetourHook? _modifyCardRewardUpgradeOddsHook;
	private static DetourHook? _restSiteBaseHealHook;
	private static DetourHook? _restSiteRoomSetTextHook;
	private static DetourHook? _runHistoryPlayerIconLoadRunHook;
	private static DetourHook? _clientLobbyJoinResponseSerializeHook;
	private static DetourHook? _clientLobbyJoinResponseDeserializeHook;
	private static DetourHook? _tryAddPlayerInFirstAvailableSlotHook;
	private static DetourHook? _createMapHook;
	private static DetourHook? _potionRewardRollHook;
	private static DetourHook? _getNumberOfRoomsHook;
	private static DetourHook? _cardKeywordsHook;
	private static DetourHook? _cardTitleHook;

	private delegate void OrigSetMaxAscension(NAscensionPanel self, int maxAscension);
	private delegate void OrigRefreshAscensionText(NAscensionPanel self);
	private delegate void OrigPortraitTipInitialize(NTopBarPortraitTip self, IRunState runState);
	private delegate void OrigUpdatePreferredAscension(StartRunLobby self);
	private delegate void OrigSetLocalCharacter(StartRunLobby self, CharacterModel character);
	private delegate void OrigSetSingleplayerAscensionAfterCharacterChanged(StartRunLobby self, ModelId characterId);
	private delegate void OrigSyncAscensionChange(StartRunLobby self, int ascension);
	private delegate decimal OrigModifyCardRewardUpgradeOdds(IRunState runState, Player player, CardModel card, decimal originalOdds);
	private delegate decimal OrigGetBaseRestSiteHealAmount(Creature creature);
	private delegate void OrigRestSiteRoomSetText(NRestSiteRoom self, string formattedText);
	private delegate void OrigRunHistoryPlayerIconLoadRun(NRunHistoryPlayerIcon self, RunHistoryPlayer player, RunHistory history);
	private delegate void OrigClientLobbyJoinResponseSerialize(ref ClientLobbyJoinResponseMessage self, PacketWriter writer);
	private delegate void OrigClientLobbyJoinResponseDeserialize(ref ClientLobbyJoinResponseMessage self, PacketReader reader);
	private delegate LobbyPlayer? OrigTryAddPlayerInFirstAvailableSlot(StartRunLobby self, SerializableUnlockState unlockState, int maxAscensionUnlocked, ulong playerId);
	private delegate ActMap OrigCreateMap(ActModel self, RunState runState, bool replaceTreasureWithElites);
	private delegate bool OrigPotionRewardRoll(PotionRewardOdds self, Player player, AscensionManager ascensionManager, RoomType roomType);
	private delegate int OrigGetNumberOfRooms(ActModel self, bool isMultiplayer);
	private delegate IReadOnlySet<CardKeyword> OrigGetCardKeywords(CardModel self);
	private delegate string OrigGetCardTitle(CardModel self);

	public static void Initialize()
	{
		PreloadDependencyAssemblies();
		EnsureAllAscensionsUnlocked();
		InstallHooks();
		Log.Info("[MoreAscensionChallenge] Loaded.");
	}

	private static void PreloadDependencyAssemblies()
	{
		var assembly = Assembly.GetExecutingAssembly();
		var modDirectory = Path.GetDirectoryName(assembly.Location);
		if (string.IsNullOrEmpty(modDirectory) || !Directory.Exists(modDirectory))
		{
			return;
		}

		var selfPath = assembly.Location;
		var loadContext = AssemblyLoadContext.GetLoadContext(assembly) ?? AssemblyLoadContext.Default;
		foreach (var dllPath in Directory.GetFiles(modDirectory, "*.dll"))
		{
			if (string.Equals(dllPath, selfPath, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			loadContext.LoadFromAssemblyPath(dllPath);
		}
	}

	private static void InstallHooks()
	{
		_setMaxAscensionHook = new DetourHook(
			RequireMethod(typeof(NAscensionPanel), nameof(NAscensionPanel.SetMaxAscension), BindingFlags.Instance | BindingFlags.Public, typeof(int)),
			SetMaxAscensionDetour);
		_refreshAscensionTextHook = new DetourHook(
			RequireMethod(typeof(NAscensionPanel), "RefreshAscensionText", BindingFlags.Instance | BindingFlags.NonPublic),
			RefreshAscensionTextDetour);
		_portraitTipInitializeHook = new DetourHook(
			RequireMethod(typeof(NTopBarPortraitTip), nameof(NTopBarPortraitTip.Initialize), BindingFlags.Instance | BindingFlags.Public, typeof(IRunState)),
			PortraitTipInitializeDetour);
		_updatePreferredAscensionHook = new DetourHook(
			RequireMethod(typeof(StartRunLobby), "UpdatePreferredAscension", BindingFlags.Instance | BindingFlags.NonPublic),
			UpdatePreferredAscensionDetour);
		_setLocalCharacterHook = new DetourHook(
			RequireMethod(typeof(StartRunLobby), nameof(StartRunLobby.SetLocalCharacter), BindingFlags.Instance | BindingFlags.Public, typeof(CharacterModel)),
			SetLocalCharacterDetour);
		_setSingleplayerAscensionAfterCharacterChangedHook = new DetourHook(
			RequireMethod(typeof(StartRunLobby), "SetSingleplayerAscensionAfterCharacterChanged", BindingFlags.Instance | BindingFlags.NonPublic, typeof(ModelId)),
			SetSingleplayerAscensionAfterCharacterChangedDetour);
		_syncAscensionChangeHook = new DetourHook(
			RequireMethod(typeof(StartRunLobby), nameof(StartRunLobby.SyncAscensionChange), BindingFlags.Instance | BindingFlags.Public, typeof(int)),
			SyncAscensionChangeDetour);
		_modifyCardRewardUpgradeOddsHook = new DetourHook(
			RequireMethod(typeof(GameHook), nameof(GameHook.ModifyCardRewardUpgradeOdds), BindingFlags.Static | BindingFlags.Public, typeof(IRunState), typeof(Player), typeof(CardModel), typeof(decimal)),
			ModifyCardRewardUpgradeOddsDetour);
		_restSiteBaseHealHook = new DetourHook(
			RequireMethod(typeof(HealRestSiteOption), nameof(HealRestSiteOption.GetBaseHealAmount), BindingFlags.Static | BindingFlags.Public, typeof(Creature)),
			GetBaseRestSiteHealAmountDetour);
		_restSiteRoomSetTextHook = new DetourHook(
			RequireMethod(typeof(NRestSiteRoom), nameof(NRestSiteRoom.SetText), BindingFlags.Instance | BindingFlags.Public, typeof(string)),
			RestSiteRoomSetTextDetour);
		_runHistoryPlayerIconLoadRunHook = new DetourHook(
			RequireMethod(typeof(NRunHistoryPlayerIcon), nameof(NRunHistoryPlayerIcon.LoadRun), BindingFlags.Instance | BindingFlags.Public, typeof(RunHistoryPlayer), typeof(RunHistory)),
			RunHistoryPlayerIconLoadRunDetour);
		_clientLobbyJoinResponseSerializeHook = new DetourHook(
			RequireMethod(typeof(ClientLobbyJoinResponseMessage), nameof(ClientLobbyJoinResponseMessage.Serialize), BindingFlags.Instance | BindingFlags.Public, typeof(PacketWriter)),
			ClientLobbyJoinResponseSerializeDetour);
		_clientLobbyJoinResponseDeserializeHook = new DetourHook(
			RequireMethod(typeof(ClientLobbyJoinResponseMessage), nameof(ClientLobbyJoinResponseMessage.Deserialize), BindingFlags.Instance | BindingFlags.Public, typeof(PacketReader)),
			ClientLobbyJoinResponseDeserializeDetour);
		_tryAddPlayerInFirstAvailableSlotHook = new DetourHook(
			RequireMethod(typeof(StartRunLobby), "TryAddPlayerInFirstAvailableSlot", BindingFlags.Instance | BindingFlags.NonPublic, typeof(SerializableUnlockState), typeof(int), typeof(ulong)),
			TryAddPlayerInFirstAvailableSlotDetour);
		_createMapHook = new DetourHook(
			RequireMethod(typeof(ActModel), nameof(ActModel.CreateMap), BindingFlags.Instance | BindingFlags.Public, typeof(RunState), typeof(bool)),
			CreateMapDetour);
		_potionRewardRollHook = new DetourHook(
			RequireMethod(typeof(PotionRewardOdds), nameof(PotionRewardOdds.Roll), BindingFlags.Instance | BindingFlags.Public, typeof(Player), typeof(AscensionManager), typeof(RoomType)),
			PotionRewardRollDetour);
		_getNumberOfRoomsHook = new DetourHook(
			RequireMethod(typeof(ActModel), nameof(ActModel.GetNumberOfRooms), BindingFlags.Instance | BindingFlags.Public, typeof(bool)),
			GetNumberOfRoomsDetour);
		_cardKeywordsHook = new DetourHook(
			RequirePropertyGetter(typeof(CardModel), nameof(CardModel.Keywords), BindingFlags.Instance | BindingFlags.Public),
			CardKeywordsDetour);
		_cardTitleHook = new DetourHook(
			RequirePropertyGetter(typeof(CardModel), nameof(CardModel.Title), BindingFlags.Instance | BindingFlags.Public),
			CardTitleDetour);
	}

	private static void SetMaxAscensionDetour(OrigSetMaxAscension orig, NAscensionPanel self, int maxAscension)
	{
		EnsureAllAscensionsUnlocked();
		if (AllowsExtraAscensionSelection(self))
		{
			maxAscension = MaxExtraAscension;
		}

		orig(self, maxAscension);
	}

	private static void RefreshAscensionTextDetour(OrigRefreshAscensionText orig, NAscensionPanel self)
	{
		if (self.Ascension <= BaseGameMaxAscension)
		{
			orig(self);
			return;
		}

		var levelLabel = GetFieldValue<MegaLabel>(AscensionPanelLevelLabelField, self);
		var infoLabel = GetFieldValue<MegaRichTextLabel>(AscensionPanelInfoField, self);
		levelLabel.SetTextAutoSize(self.Ascension.ToString());
		infoLabel.Text = $"[b][gold]{GetAscensionTitle(self.Ascension)}[/gold][/b]\n{GetAscensionDescription(self.Ascension)}";
	}

	private static void PortraitTipInitializeDetour(OrigPortraitTipInitialize orig, NTopBarPortraitTip self, IRunState runState)
	{
		if (runState.AscensionLevel <= BaseGameMaxAscension)
		{
			orig(self, runState);
			return;
		}

		SetFieldValue(PortraitTipShowTipField, self, true);
		SetFieldValue(PortraitTipHoverTipField, self, (IHoverTip)BuildExtraAscensionHoverTip(runState));
	}

	private static void UpdatePreferredAscensionDetour(OrigUpdatePreferredAscension orig, StartRunLobby self)
	{
		EnsureAllAscensionsUnlocked();
		if (self.NetService.Type == NetGameType.Singleplayer && self.Players.Count != 0)
		{
			var character = self.LocalPlayer.character;
			if (character.Id != ModelDb.GetId<RandomCharacter>())
			{
				if (self.Ascension > BaseGameMaxAscension)
				{
					SetSavedExtraAscension(character.Id, self.Ascension);
					return;
				}
			}
		}

		orig(self);
	}

	private static void SetLocalCharacterDetour(OrigSetLocalCharacter orig, StartRunLobby self, CharacterModel character)
	{
		EnsureAllAscensionsUnlocked();
		if (self.NetService.Type == NetGameType.Singleplayer && self.Players.Count != 0)
		{
			var currentCharacter = self.LocalPlayer.character;
			if (currentCharacter.Id != ModelDb.GetId<RandomCharacter>())
			{
				if (self.Ascension > BaseGameMaxAscension)
				{
					SetSavedExtraAscension(currentCharacter.Id, self.Ascension);
				}
			}
		}

		orig(self, character);
	}

	private static void SetSingleplayerAscensionAfterCharacterChangedDetour(OrigSetSingleplayerAscensionAfterCharacterChanged orig, StartRunLobby self, ModelId characterId)
	{
		EnsureAllAscensionsUnlocked();
		var savedExtra = GetSavedExtraAscension(characterId);
		PendingRestoreCharacterEntry = characterId.Entry;
		PendingRestoreAscension = savedExtra;
		try
		{
			orig(self, characterId);
		}
		finally
		{
			PendingRestoreCharacterEntry = null;
			PendingRestoreAscension = null;
		}

		if (self.NetService.Type != NetGameType.Singleplayer || characterId == ModelDb.GetId<RandomCharacter>())
		{
			return;
		}

		SetFieldValue(StartRunLobbyMaxAscensionField, self, MaxExtraAscension);
		if (savedExtra.HasValue && self.Ascension == BaseGameMaxAscension)
		{
			self.SyncAscensionChange(savedExtra.Value);
		}
	}

	private static void SyncAscensionChangeDetour(OrigSyncAscensionChange orig, StartRunLobby self, int ascension)
	{
		EnsureAllAscensionsUnlocked();
		var before = self.Ascension;
		var characterBefore = self.Players.Count > 0 ? self.LocalPlayer.character.Id.Entry : null;
		orig(self, ascension);
		if (self.NetService.Type != NetGameType.Singleplayer || self.Players.Count == 0)
		{
			return;
		}

		var character = self.LocalPlayer.character;
		var characterAfter = character.Id.Entry;
		if (character.Id == ModelDb.GetId<RandomCharacter>())
		{
			return;
		}

		if (ascension > BaseGameMaxAscension)
		{
			SetSavedExtraAscension(character.Id, ascension);
		}
		else if (string.Equals(characterBefore, characterAfter, StringComparison.Ordinal)
			&& before > BaseGameMaxAscension
			&& ascension <= BaseGameMaxAscension
			&& !(PendingRestoreAscension.HasValue && string.Equals(PendingRestoreCharacterEntry, character.Id.Entry, StringComparison.Ordinal)))
		{
			ClearSavedExtraAscension(character.Id);
		}
	}

	private static decimal ModifyCardRewardUpgradeOddsDetour(OrigModifyCardRewardUpgradeOdds orig, IRunState runState, Player player, CardModel card, decimal originalOdds)
	{
		if (HasExtraAscension(runState, 15))
		{
			return decimal.MinValue;
		}

		return orig(runState, player, card, originalOdds);
	}

	private static decimal GetBaseRestSiteHealAmountDetour(OrigGetBaseRestSiteHealAmount orig, Creature creature)
	{
		var amount = orig(creature);
		var runState = creature.Player?.RunState;
		if (!HasExtraAscension(runState, 13) || runState?.CurrentRoom is not RestSiteRoom)
		{
			return amount;
		}

		var missingHealth = Math.Max(0, creature.MaxHp - creature.CurrentHp);
		return missingHealth * MissingHealthHealFactor;
	}

	private static void RestSiteRoomSetTextDetour(OrigRestSiteRoomSetText orig, NRestSiteRoom self, string formattedText)
	{
		var runState = GetFieldValue<IRunState>(RestSiteRoomRunStateField, self);
		if (HasExtraAscension(runState, 13))
		{
			formattedText = formattedText
				.Replace("回复最大生命值的30%", "恢复已损失生命值的30%")
				.Replace("回复其他角色最大生命值的30%", "恢复其他角色已损失生命值的30%")
				.Replace("Heal for 30% of your Max HP", "Recover 30% of your missing HP")
				.Replace("for 30% of their Max HP", "for 30% of their missing HP");
		}

		orig(self, formattedText);
	}

	private static void RunHistoryPlayerIconLoadRunDetour(OrigRunHistoryPlayerIconLoadRun orig, NRunHistoryPlayerIcon self, RunHistoryPlayer player, RunHistory history)
	{
		if (history.Ascension <= BaseGameMaxAscension)
		{
			orig(self, player, history);
			return;
		}

		RunHistoryPlayerIconPlayerSetter.Invoke(self, new object[] { player });
		var character = ModelDb.GetById<CharacterModel>(player.Character);
		var currentIcon = GetFieldValue<Control?>(RunHistoryPlayerIconCurrentIconField, self);
		currentIcon?.QueueFreeSafely();
		currentIcon = character.Icon;
		SetFieldValue(RunHistoryPlayerIconCurrentIconField, self, currentIcon);
		self.AddChildSafely(currentIcon);
		self.MoveChild(currentIcon, 0);

		var selectionReticle = GetFieldValue<NSelectionReticle>(RunHistoryPlayerIconSelectionReticleField, self);
		var ascensionIcon = GetFieldValue<Control>(RunHistoryPlayerIconAscensionIconField, self);
		var ascensionLabel = GetFieldValue<MegaLabel>(RunHistoryPlayerIconAscensionLabelField, self);
		var hoverTips = GetFieldValue<List<IHoverTip>>(RunHistoryPlayerIconHoverTipsField, self);
		hoverTips.Clear();

		selectionReticle.Visible = history.Players.Count > 1;
		ascensionIcon.Visible = false;
		ascensionLabel.SetTextAutoSize(history.Ascension.ToString());

		var playerHover = new LocString("run_history", "PLAYER_HOVER");
		if (history.Players.Count > 1)
		{
			playerHover.Add("PlayerName", PlatformUtil.GetPlayerName(history.PlatformType, player.Id));
			playerHover.Add("CharacterName", character.Title.GetFormattedText());
		}
		else
		{
			playerHover.Add("PlayerName", character.Title.GetFormattedText());
			playerHover.Add("CharacterName", string.Empty);
		}

		hoverTips.Add(new HoverTip(playerHover));
		hoverTips.Add(BuildAscensionHoverTip(character, history.Ascension));
	}

	private static void ClientLobbyJoinResponseSerializeDetour(OrigClientLobbyJoinResponseSerialize orig, ref ClientLobbyJoinResponseMessage self, PacketWriter writer)
	{
		if (self.playersInLobby == null)
		{
			throw new InvalidOperationException("Tried to serialize ClientLobbyJoinResponseMessage with null list!");
		}

		writer.WriteList(self.playersInLobby, 3);
		writer.WriteBool(self.dailyTime.HasValue);
		if (self.dailyTime.HasValue)
		{
			writer.Write(self.dailyTime.Value);
		}

		writer.WriteBool(self.seed != null);
		if (self.seed != null)
		{
			writer.WriteString(self.seed);
		}

		writer.WriteInt(self.ascension);
		writer.WriteList(self.modifiers);
	}

	private static void ClientLobbyJoinResponseDeserializeDetour(OrigClientLobbyJoinResponseDeserialize orig, ref ClientLobbyJoinResponseMessage self, PacketReader reader)
	{
		self.playersInLobby = reader.ReadList<LobbyPlayer>(3);
		self.dailyTime = reader.ReadBool() ? reader.Read<TimeServerResult>() : null;
		self.seed = reader.ReadBool() ? reader.ReadString() : null;
		self.ascension = reader.ReadInt();
		self.modifiers = reader.ReadList<SerializableModifier>();
	}

	private static LobbyPlayer? TryAddPlayerInFirstAvailableSlotDetour(OrigTryAddPlayerInFirstAvailableSlot orig, StartRunLobby self, SerializableUnlockState unlockState, int maxAscensionUnlocked, ulong playerId)
	{
		EnsureAllAscensionsUnlocked();
		maxAscensionUnlocked = Math.Max(maxAscensionUnlocked, MaxExtraAscension);
		return orig(self, unlockState, maxAscensionUnlocked, playerId);
	}

	private static ActMap CreateMapDetour(OrigCreateMap orig, ActModel self, RunState runState, bool replaceTreasureWithElites)
	{
		var previousAscension = ActiveMapGenerationAscensionLevel;
		ActiveMapGenerationAscensionLevel = runState.AscensionLevel;
		try
		{
			return orig(self, runState, replaceTreasureWithElites);
		}
		finally
		{
			ActiveMapGenerationAscensionLevel = previousAscension;
		}
	}

	private static bool PotionRewardRollDetour(OrigPotionRewardRoll orig, PotionRewardOdds self, Player player, AscensionManager ascensionManager, RoomType roomType)
	{
		if (!HasExtraAscension(player.RunState, 11))
		{
			return orig(self, player, ascensionManager, roomType);
		}

		var currentValue = self.CurrentValue;
		var targetBaseOdds = GetPotionBaseOddsForAct(player.RunState.CurrentActIndex);
		var effectiveValue = Math.Max(0f, currentValue + (targetBaseOdds - 0.4f));
		var forceReward = GameHook.ShouldForcePotionReward(player.RunState, player, roomType);
		var rng = GetFieldValue<Rng>(AbstractOddsRngField, self);
		var roll = rng.NextFloat();

		if (roll < effectiveValue || forceReward)
		{
			self.OverrideCurrentValue(currentValue - 0.1f);
		}
		else
		{
			self.OverrideCurrentValue(currentValue + 0.1f);
		}

		var eliteBonus = roomType == RoomType.Elite ? PotionRewardOdds.eliteBonus : 0f;
		return forceReward || roll < effectiveValue + eliteBonus * 0.5f;
	}

	private static int GetNumberOfRoomsDetour(OrigGetNumberOfRooms orig, ActModel self, bool isMultiplayer)
	{
		var roomCount = orig(self, isMultiplayer);
		var activeAscension = ActiveMapGenerationAscensionLevel;
		if ((activeAscension.HasValue && activeAscension.Value >= 12) || HasExtraAscension(GetCurrentRunState(), 12))
		{
			roomCount = Math.Max(1, roomCount - 1);
		}

		return roomCount;
	}

	private static IReadOnlySet<CardKeyword> CardKeywordsDetour(OrigGetCardKeywords orig, CardModel self)
	{
		var keywords = orig(self);
		if (!IsAscendersBanePlus(self))
		{
			return keywords;
		}

		return keywords.Where(static keyword => keyword != CardKeyword.Ethereal).ToHashSet();
	}

	private static string CardTitleDetour(OrigGetCardTitle orig, CardModel self)
	{
		var title = orig(self);
		if (!IsAscendersBanePlus(self))
		{
			return title;
		}

		if (!title.EndsWith("+", StringComparison.Ordinal))
		{
			title += "+";
		}

		return title;
	}

	private static bool AllowsExtraAscensionSelection(NAscensionPanel panel)
	{
		var mode = GetFieldValue<MultiplayerUiMode>(AscensionPanelModeField, panel);
		return mode == MultiplayerUiMode.Singleplayer || mode == MultiplayerUiMode.Host;
	}

	private static HoverTip BuildExtraAscensionHoverTip(IRunState runState)
	{
		return BuildAscensionHoverTip(GetLocalCharacter(runState), runState.AscensionLevel);
	}

	private static HoverTip BuildAscensionHoverTip(CharacterModel character, int ascensionLevel)
	{
		var title = new LocString("ascension", "PORTRAIT_TITLE");
		title.Add("character", character.Title);
		title.Add("ascension", ascensionLevel);
		return new HoverTip(title, string.Join("\n", BuildAscensionHoverLines(ascensionLevel)));
	}

	private static IEnumerable<string> BuildAscensionHoverLines(int ascensionLevel)
	{
		for (var level = 1; level <= ascensionLevel; level++)
		{
			yield return "+" + GetAscensionTitle(level);
		}
	}

	private static string GetAscensionTitle(int ascensionLevel)
	{
		if (ascensionLevel >= 1 && ascensionLevel <= BaseGameMaxAscension)
		{
			return AscensionHelper.GetTitle(ascensionLevel).GetFormattedText();
		}

		if (ascensionLevel >= 0 && ascensionLevel < ExtraTitlesZh.Length)
		{
			return IsChineseLanguage() ? ExtraTitlesZh[ascensionLevel] : ExtraTitlesEn[ascensionLevel];
		}

		return IsChineseLanguage() ? $"进阶{ascensionLevel}" : $"Ascension {ascensionLevel}";
	}

	private static string GetAscensionDescription(int ascensionLevel)
	{
		if (ascensionLevel >= 1 && ascensionLevel <= BaseGameMaxAscension)
		{
			return AscensionHelper.GetDescription(ascensionLevel).GetFormattedText();
		}

		if (ascensionLevel >= 0 && ascensionLevel < ExtraDescriptionsZh.Length)
		{
			return IsChineseLanguage() ? ExtraDescriptionsZh[ascensionLevel] : ExtraDescriptionsEn[ascensionLevel];
		}

		return string.Empty;
	}

	private static bool HasExtraAscension(IRunState? runState, int requiredLevel)
	{
		return runState != null && runState.AscensionLevel >= requiredLevel;
	}

	private static IRunState? GetCurrentRunState()
	{
		return RunManager.Instance == null ? null : (IRunState?)RunManagerStateProperty.GetValue(RunManager.Instance);
	}

	private static bool IsAscendersBanePlus(CardModel card)
	{
		return card is AscendersBane && HasExtraAscension(card.Owner?.RunState, 14);
	}

	private static float GetPotionBaseOddsForAct(int currentActIndex)
	{
		return currentActIndex switch
		{
			0 => 0.35f,
			1 => 0.25f,
			_ => 0.15f
		};
	}

	private static double GetUnknownMonsterRateForAct(int actIndex)
	{
		return actIndex switch
		{
			0 => 0.10,
			1 => 0.25,
			_ => 0.40
		};
	}

	private static void EnsureAllAscensionsUnlocked()
	{
		try
		{
			var progress = SaveManager.Instance.Progress;
			if (progress.MaxMultiplayerAscension < MaxExtraAscension)
			{
				progress.MaxMultiplayerAscension = MaxExtraAscension;
			}

			foreach (var character in ModelDb.AllCharacters)
			{
				var stats = progress.GetOrCreateCharacterStats(character.Id);
				if (stats.MaxAscension < MaxExtraAscension)
				{
					stats.MaxAscension = MaxExtraAscension;
				}
			}
		}
		catch
		{
		}
	}

	private static CharacterModel GetLocalCharacter(IRunState runState)
	{
		return LocalContext.GetMe(runState)!.Character;
	}

	private static int? GetSavedExtraAscension(ModelId characterId)
	{
		EnsurePrefsLoaded();
		if (ExtraAscensionPrefs.TryGetValue(characterId.Entry, out var value))
		{
			return value;
		}

		return null;
	}

	private static void SetSavedExtraAscension(ModelId characterId, int ascension)
	{
		EnsurePrefsLoaded();
		ExtraAscensionPrefs[characterId.Entry] = Math.Clamp(ascension, MinExtraAscension, MaxExtraAscension);
		SaveExtraAscensionPrefs();
	}

	private static void ClearSavedExtraAscension(ModelId characterId)
	{
		EnsurePrefsLoaded();
		if (ExtraAscensionPrefs.Remove(characterId.Entry))
		{
			SaveExtraAscensionPrefs();
		}
	}

	private static Dictionary<string, int> LoadExtraAscensionPrefs()
	{
		try
		{
			var preferencePath = GetPreferencePath();
			if (!File.Exists(preferencePath))
			{
				return new Dictionary<string, int>(StringComparer.Ordinal);
			}

			var json = File.ReadAllText(preferencePath);
			var loaded = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
			if (loaded == null)
			{
				return new Dictionary<string, int>(StringComparer.Ordinal);
			}

			return loaded
				.Where(static pair => pair.Value > BaseGameMaxAscension)
				.ToDictionary(static pair => pair.Key, static pair => Math.Clamp(pair.Value, MinExtraAscension, MaxExtraAscension), StringComparer.Ordinal);
		}
		catch (Exception ex)
		{
			Log.Warn($"[MoreAscensionChallenge] Failed to load preferences: {ex.Message}");
			return new Dictionary<string, int>(StringComparer.Ordinal);
		}
	}

	private static void SaveExtraAscensionPrefs()
	{
		try
		{
			EnsurePrefsLoaded();
			var preferencePath = GetPreferencePath();
			var directory = Path.GetDirectoryName(preferencePath);
			if (!string.IsNullOrEmpty(directory))
			{
				Directory.CreateDirectory(directory);
			}

			var json = JsonSerializer.Serialize(ExtraAscensionPrefs, new JsonSerializerOptions
			{
				WriteIndented = true
			});
			File.WriteAllText(preferencePath, json);
		}
		catch (Exception ex)
		{
			Log.Warn($"[MoreAscensionChallenge] Failed to save preferences: {ex.Message}");
		}
	}

	private static void EnsurePrefsLoaded()
	{
		var preferencePath = GetPreferencePath();
		if (string.Equals(ExtraAscensionPrefsPath, preferencePath, StringComparison.Ordinal))
		{
			return;
		}

		var previous = ExtraAscensionPrefs;
		var loaded = LoadExtraAscensionPrefs();
		foreach (var pair in previous)
		{
			if (!loaded.ContainsKey(pair.Key))
			{
				loaded[pair.Key] = pair.Value;
			}
		}

		ExtraAscensionPrefs = loaded;
		ExtraAscensionPrefsPath = preferencePath;
	}

	private static string GetPreferencePath()
	{
		try
		{
			var profileId = SaveManager.Instance.CurrentProfileId;
			return ProjectSettings.GlobalizePath(UserDataPathProvider.GetProfileScopedPath(profileId, PreferenceFileName));
		}
		catch
		{
			return Path.Combine(OS.GetUserDataDir(), PreferenceFileName);
		}
	}

	private static bool IsChineseLanguage()
	{
		var language = LocManager.Instance?.Language;
		return string.Equals(language, "zhs", StringComparison.OrdinalIgnoreCase) || string.Equals(language, "zht", StringComparison.OrdinalIgnoreCase);
	}

	private static MethodInfo RequireMethod(Type type, string name, BindingFlags flags, params Type[] parameters)
	{
		var method = type.GetMethod(name, flags, binder: null, parameters, modifiers: null);
		if (method == null)
		{
			throw new InvalidOperationException($"Could not find required method {type.FullName}.{name}.");
		}

		return method;
	}

	private static PropertyInfo RequireProperty(Type type, string name, BindingFlags flags)
	{
		var property = type.GetProperty(name, flags);
		if (property == null)
		{
			throw new InvalidOperationException($"Could not find required property {type.FullName}.{name}.");
		}

		return property;
	}

	private static MethodInfo RequirePropertySetter(Type type, string name, BindingFlags flags)
	{
		var setter = RequireProperty(type, name, flags).SetMethod ?? RequireProperty(type, name, flags).GetSetMethod(nonPublic: true);
		if (setter == null)
		{
			throw new InvalidOperationException($"Could not find required setter {type.FullName}.{name}.");
		}

		return setter;
	}

	private static MethodInfo RequirePropertyGetter(Type type, string name, BindingFlags flags)
	{
		var getter = RequireProperty(type, name, flags).GetMethod;
		if (getter == null)
		{
			throw new InvalidOperationException($"Could not find required getter {type.FullName}.{name}.");
		}

		return getter;
	}

	private static FieldInfo RequireField(Type type, string name)
	{
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
		var field = type.GetField(name, flags);
		if (field == null && name.Length > 1 && name[0] == '_' && char.IsLetter(name[1]))
		{
			var backingFieldName = $"<{char.ToUpperInvariant(name[1])}{name[2..]}>k__BackingField";
			field = type.GetField(backingFieldName, flags);
		}

		if (field == null)
		{
			throw new InvalidOperationException($"Could not find required field {type.FullName}.{name}.");
		}

		return field;
	}

	private static T GetFieldValue<T>(FieldInfo field, object instance)
	{
		return (T)field.GetValue(instance)!;
	}

	private static void SetFieldValue<T>(FieldInfo field, object instance, T value)
	{
		field.SetValue(instance, value);
	}
}
