using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace HextechMpLab;

/// <summary>
/// 本机双客户端联机实验的自动驾驶。按 HEXTECH_MPLAB_ROLE(host/client)在角色选择、地图投票、
/// 海克斯符文选择、战斗回合上替玩家做最简单的确定性选择,并在每个玩家的牌组里塞三张恶魔形态、
/// 给一个"升级恶魔形态"(开局自动打出所有形态)符文,让形态批处理在第一场战斗开局必然触发。
/// 不做任何 Harmony 补丁;所有驱动都发生在两端一致执行的本地入口上。
/// </summary>
[ModInitializer(nameof(Initialize))]
public static class MpLabEntry
{
	public static void Initialize()
	{
		string? role = System.Environment.GetEnvironmentVariable("HEXTECH_MPLAB_ROLE");
		if (string.IsNullOrWhiteSpace(role))
		{
			return;
		}

		MpLabDriver.Start(role.Trim().ToLowerInvariant());
	}
}

internal static class MpLabDriver
{
	private const string Tag = "[MpLab]";
	private static string _role = "host";
	private static int _expectedPlayers = 2;
	private static string _seed = "HEXTECHLAB";
	private static ulong _startMsec;
	private static ulong _lastTickMsec;
	private static double _maxSeconds = 420;
	private static SceneTree? _tree;

	private static bool _characterChosen;
	private static bool _seedSet;
	private static bool _ready;
	private static bool _deckSeeded;
	private static Task? _relicGrant;
	private static readonly HashSet<ulong> VotedScreens = [];
	private static readonly HashSet<ulong> PickedRuneScreens = [];
	private static bool _combatSeen;
	private static int _turnsEnded;
	private static int _armedRound = -1;
	private static ulong _endTurnDueMsec;
	private static bool _finished;
	private static ulong _quitAtMsec;
	private static Type? _runeSelectionScreenType;
	private static Type? _demonFormRuneType;

	internal static void Start(string role)
	{
		_role = role;
		_expectedPlayers = int.TryParse(System.Environment.GetEnvironmentVariable("HEXTECH_MPLAB_PLAYERS"), out int players) ? players : 2;
		_seed = System.Environment.GetEnvironmentVariable("HEXTECH_MPLAB_SEED") ?? _seed;
		_maxSeconds = double.TryParse(System.Environment.GetEnvironmentVariable("HEXTECH_MPLAB_MAX_SEC"), out double max) ? max : _maxSeconds;
		_tree = Engine.GetMainLoop() as SceneTree;
		if (_tree == null)
		{
			Log.Warn($"{Tag} no SceneTree; driver disabled", 2);
			return;
		}

		_startMsec = Time.GetTicksMsec();
		_tree.Connect(SceneTree.SignalName.ProcessFrame, Callable.From(OnFrame));
		Info($"driver started role={_role} expectedPlayers={_expectedPlayers} seed={_seed}");
	}

	private static void Info(string text) => Log.Info($"{Tag}[{_role}] {text}", 2);

	private static void OnFrame()
	{
		try
		{
			ulong now = Time.GetTicksMsec();
			if (_quitAtMsec != 0 && now >= _quitAtMsec)
			{
				Info("quitting");
				_quitAtMsec = 0;
				_tree!.Quit();
				return;
			}

			if (!_finished && now - _startMsec > (ulong)(_maxSeconds * 1000))
			{
				Finish("timeout");
				return;
			}

			if (now - _lastTickMsec < 500)
			{
				return;
			}

			_lastTickMsec = now;
			Tick(now);
		}
		catch (Exception ex)
		{
			Log.Warn($"{Tag}[{_role}] frame error: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}", 2);
		}
	}

	private static void Tick(ulong now)
	{
		if (_finished)
		{
			return;
		}

		Node root = _tree!.Root;
		RunState? runState = RunManager.Instance?.DebugOnlyGetState();
		NCharacterSelectScreen? select = FindNode<NCharacterSelectScreen>(root);
		if (select != null && runState == null)
		{
			DriveLobby(select);
			return;
		}

		if (runState == null)
		{
			return;
		}

		if (!_deckSeeded)
		{
			SeedDecks(runState);
		}

		if (_relicGrant is { IsCompleted: false })
		{
			return;
		}

		NCardGridSelectionScreen? grid = FindNode<NCardGridSelectionScreen>(root);
		if (grid != null)
		{
			DriveCardGrid(grid, now);
			return;
		}

		Node? runeScreen = FindRuneSelectionScreen(root);
		if (runeScreen != null)
		{
			PickFirstRune(runeScreen);
			return;
		}

		NMapScreen? map = FindNode<NMapScreen>(root);
		if (map != null && map.IsTravelEnabled && !map.IsTraveling)
		{
			VoteFirstTravelablePoint(map);
			return;
		}

		if (NCombatRoom.Instance != null)
		{
			_combatSeen = true;
			DriveCombat(runState, now);
			return;
		}

		if (_combatSeen)
		{
			Finish("first combat finished");
			return;
		}

		if (NEventRoom.Instance is { } eventRoom)
		{
			DriveEvent(eventRoom, now);
		}
	}

	private static readonly HashSet<ulong> HandledGrids = [];
	private static NCardGridSelectionScreen? _gridToConfirm;
	private static ulong _gridConfirmDueMsec;

	// 任何卡牌网格选择(升级/变化/移除……):按要求的最小张数点前几张,再按确认。
	private static void DriveCardGrid(NCardGridSelectionScreen grid, ulong now)
	{
		if (ReferenceEquals(_gridToConfirm, grid))
		{
			if (now < _gridConfirmDueMsec)
			{
				return;
			}

			_gridToConfirm = null;
			MethodInfo? confirm = grid.GetType().GetMethod("ConfirmSelection", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (confirm != null && GodotObject.IsInstanceValid(grid))
			{
				try
				{
					confirm.Invoke(grid, [null]);
					Info($"card grid {grid.GetType().Name}: confirmed");
				}
				catch (Exception ex)
				{
					Log.Warn($"{Tag}[{_role}] card grid confirm failed: {ex.GetBaseException().GetType().Name}: {ex.GetBaseException().Message}", 2);
				}
			}

			return;
		}

		ulong id = grid.GetInstanceId();
		if (HandledGrids.Contains(id))
		{
			return;
		}

		NCardGrid? cardGrid = typeof(NCardGridSelectionScreen).GetField("_grid", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(grid) as NCardGrid;
		List<CardModel> cards = cardGrid?.CurrentlyDisplayedCards?.ToList() ?? [];
		if (cards.Count == 0)
		{
			return;
		}

		int minSelect = 1;
		for (Type? type = grid.GetType(); type != null; type = type.BaseType)
		{
			if (type.GetField("_prefs", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(grid) is CardSelectorPrefs prefs)
			{
				minSelect = Math.Max(1, prefs.MinSelect);
				break;
			}
		}

		HandledGrids.Add(id);
		MethodInfo? click = grid.GetType().GetMethod("OnCardClicked", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, [typeof(CardModel)]);
		if (click == null)
		{
			Log.Warn($"{Tag} {grid.GetType().Name}.OnCardClicked not found", 2);
			return;
		}

		for (int i = 0; i < Math.Min(minSelect, cards.Count); i++)
		{
			click.Invoke(grid, [cards[i]]);
		}

		Info($"card grid {grid.GetType().Name}: clicked {Math.Min(minSelect, cards.Count)} of {cards.Count} cards ({string.Join(",", cards.Take(minSelect).Select(c => c.Id.Entry))})");
		_gridToConfirm = grid;
		_gridConfirmDueMsec = now + 1500;
	}

	private static string? _lastEventChoice;
	private static ulong _lastEventChoiceMsec;

	// 开局的涅奥等事件:每页都选最后一个选项(通常是最朴素的那个),走原版按钮点击路径以保持同步语义。
	private static void DriveEvent(NEventRoom eventRoom, ulong now)
	{
		EventModel? localEvent = RunManager.Instance?.EventSynchronizer?.GetLocalEvent();
		IReadOnlyList<EventOption>? options = localEvent?.CurrentOptions;
		if (localEvent == null || options == null || options.Count == 0)
		{
			return;
		}

		int index = -1;
		for (int i = options.Count - 1; i >= 0; i--)
		{
			if (!options[i].IsLocked)
			{
				index = i;
				break;
			}
		}

		if (index < 0)
		{
			return;
		}

		string label = options[index].HistoryName?.GetRawText() ?? options[index].Description?.GetRawText() ?? "?";
		string key = $"{localEvent.Id.Entry}:{options.Count}:{index}:{label}";
		// 同一页同一选项只点一次:重复点击会往同步队列重复入队,本身就是分叉源。
		if (key == _lastEventChoice)
		{
			return;
		}

		_lastEventChoice = key;
		_lastEventChoiceMsec = now;
		Info($"event {localEvent.Id.Entry}: choosing option {index}/{options.Count} '{label}'");
		eventRoom.OptionButtonClicked(options[index], index);
	}

	private static void DriveLobby(NCharacterSelectScreen select)
	{
		StartRunLobby? lobby = select.Lobby;
		if (lobby == null)
		{
			return;
		}

		if (!_characterChosen)
		{
			MethodInfo? change = typeof(StartRunLobby).GetMethod("ChangeCharacter", BindingFlags.Instance | BindingFlags.NonPublic);
			if (change == null)
			{
				Log.Warn($"{Tag} StartRunLobby.ChangeCharacter not found", 2);
				_characterChosen = true;
				return;
			}

			change.Invoke(lobby, [lobby.LocalPlayer.id, ModelDb.Character<Ironclad>(), false]);
			_characterChosen = true;
			Info($"character chosen: Ironclad (local id {lobby.LocalPlayer.id})");
			return;
		}

		if (_role == "host")
		{
			if (!_seedSet)
			{
				// 标准模式不允许改种子(SetSeed 会抛 NotImplementedException);两端共用主机的随机种子即可。
				_seedSet = true;
				Info($"lobby seed: {lobby.Seed ?? "(random)"}");
				return;
			}

			if (!_ready && lobby.Players.Count >= _expectedPlayers && lobby.Players.All(p => p.id == lobby.LocalPlayer.id || p.isReady))
			{
				lobby.SetReady(true);
				_ready = true;
				Info($"host ready with {lobby.Players.Count} players");
			}
		}
		else if (!_ready)
		{
			lobby.SetReady(true);
			_ready = true;
			Info("client ready");
		}
	}

	private static void SeedDecks(RunState runState)
	{
		_deckSeeded = true;
		_demonFormRuneType ??= FindType("HextechRunes.DemonFormUpgradeRune");
		CardModel canonical = ModelDb.Card<DemonForm>();
		List<Task> grants = [];
		foreach (Player player in runState.Players)
		{
			for (int i = 0; i < 3; i++)
			{
				CardModel card = runState.CreateCard(canonical, player);
				player.Deck.AddInternal(card, player.Deck.Cards.Count, silent: true);
			}

			if (_demonFormRuneType != null)
			{
				MethodInfo obtain = typeof(RelicCmd).GetMethods(BindingFlags.Public | BindingFlags.Static)
					.First(m => m.Name == nameof(RelicCmd.Obtain) && m.IsGenericMethodDefinition && m.GetParameters().Length == 1)
					.MakeGenericMethod(_demonFormRuneType);
				grants.Add((Task)obtain.Invoke(null, [player])!);
			}

			Info($"seeded player {player.NetId}: deck={player.Deck.Cards.Count} cards, rune={(_demonFormRuneType != null ? "granting" : "type missing")}");
		}

		_relicGrant = Task.WhenAll(grants);
		_ = _relicGrant.ContinueWith(t =>
		{
			if (t.IsFaulted)
			{
				Log.Warn($"{Tag}[{_role}] relic grant failed: {t.Exception?.GetBaseException().GetType().Name}: {t.Exception?.GetBaseException().Message}", 2);
			}
			else
			{
				Log.Info($"{Tag}[{_role}] relic grant done", 2);
			}
		}, TaskScheduler.Default);
	}

	private static Node? FindRuneSelectionScreen(Node root)
	{
		_runeSelectionScreenType ??= FindType("HextechRunes.HextechRuneSelectionScreen");
		return _runeSelectionScreenType == null ? null : FindNode(root, _runeSelectionScreenType);
	}

	private static void PickFirstRune(Node screen)
	{
		ulong id = screen.GetInstanceId();
		if (PickedRuneScreens.Contains(id))
		{
			return;
		}

		Type type = screen.GetType();
		if (type.GetProperty("CurrentRelics", BindingFlags.Instance | BindingFlags.Public)?.GetValue(screen) is not IReadOnlyList<RelicModel> relics || relics.Count == 0)
		{
			return;
		}

		MethodInfo? select = type.GetMethod("OnHolderSelected", BindingFlags.Instance | BindingFlags.NonPublic);
		PickedRuneScreens.Add(id);
		if (select == null)
		{
			Log.Warn($"{Tag} HextechRuneSelectionScreen.OnHolderSelected not found", 2);
			return;
		}

		select.Invoke(screen, [relics[0]]);
		Info($"rune picked: {relics[0].Id.Entry}");
	}

	private static void VoteFirstTravelablePoint(NMapScreen map)
	{
		ulong id = map.GetInstanceId();
		if (VotedScreens.Contains(id))
		{
			return;
		}

		List<NMapPoint> points = [];
		CollectNodes(map, points);
		NMapPoint? target = points.FirstOrDefault(p => p.State == MapPointState.Travelable);
		if (target == null)
		{
			return;
		}

		VotedScreens.Add(id);
		map.OnMapPointSelectedLocally(target);
		Info($"voted map point {target.Point.coord} ({target.Point.PointType})");
	}

	private static void DriveCombat(RunState runState, ulong now)
	{
		CombatManager? manager = CombatManager.Instance;
		if (manager == null || !manager.IsInProgress || manager.IsOverOrEnding)
		{
			return;
		}

		if (runState.CurrentRoom is not CombatRoom room || room.CombatState is not CombatState state || state.CurrentSide != CombatSide.Player)
		{
			return;
		}

		Player me = LocalContext.GetMe(state);
		if (manager.IsPlayerReadyToEndTurn(me))
		{
			return;
		}

		if (_armedRound != state.RoundNumber)
		{
			_armedRound = state.RoundNumber;
			_endTurnDueMsec = now + 2500;
			Info($"round {state.RoundNumber}: hp={me.Creature.CurrentHp}/{me.Creature.MaxHp} powers=[{string.Join(",", me.Creature.Powers.Select(p => $"{p.Id.Entry}:{p.Amount}"))}]");
			return;
		}

		if (now < _endTurnDueMsec)
		{
			return;
		}

		try
		{
			_turnsEnded++;
			Info($"ending turn #{_turnsEnded} round={state.RoundNumber} for {me.NetId}");
			PlayerCmd.EndTurn(me, canBackOut: false, actionDuringEnemyTurn: null!);
			if (_turnsEnded >= 6)
			{
				Finish("turn budget reached");
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"{Tag}[{_role}] end turn failed: {ex.GetType().Name}: {ex.Message}", 2);
		}
	}

	private static void Finish(string reason)
	{
		if (_finished)
		{
			return;
		}

		_finished = true;
		Info($"finished: {reason}; turnsEnded={_turnsEnded}");
		_quitAtMsec = Time.GetTicksMsec() + 4000;
	}

	private static T? FindNode<T>(Node root) where T : Node
	{
		if (root is T match)
		{
			return match;
		}

		foreach (Node child in root.GetChildren())
		{
			T? found = FindNode<T>(child);
			if (found != null)
			{
				return found;
			}
		}

		return null;
	}

	private static Node? FindNode(Node root, Type type)
	{
		if (type.IsInstanceOfType(root))
		{
			return root;
		}

		foreach (Node child in root.GetChildren())
		{
			Node? found = FindNode(child, type);
			if (found != null)
			{
				return found;
			}
		}

		return null;
	}

	private static void CollectNodes<T>(Node root, List<T> into) where T : Node
	{
		if (root is T match)
		{
			into.Add(match);
		}

		foreach (Node child in root.GetChildren())
		{
			CollectNodes(child, into);
		}
	}

	private static Type? FindType(string fullName)
	{
		foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
		{
			try
			{
				Type? type = assembly.GetType(fullName, throwOnError: false);
				if (type != null)
				{
					return type;
				}
			}
			catch (Exception)
			{
				// 加载失败的程序集跳过
			}
		}

		Log.Warn($"{Tag} type not found: {fullName}", 2);
		return null;
	}
}
