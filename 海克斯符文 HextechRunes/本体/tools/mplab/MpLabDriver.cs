using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Powers;
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
	private static readonly Dictionary<ulong, ulong> RuneScreenNextPickMsec = [];
	private static bool _combatSeen;
	private static int _turnsEnded;
	private static int _armedRound = -1;
	private static ulong _endTurnDueMsec;
	private static bool _finished;
	private static bool _ftuesDisabled;
	private static ulong _quitAtMsec;
	private static Type? _runeSelectionScreenType;

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

	private static bool _faultInstalled;

	// HEXTECH_MPLAB_THROW_FORM_BATCH=1:让本端的形态批处理抛异常,模拟"只有一端在回合开始钩子里出错"。
	private static void InstallFaultInjection()
	{
		if (_faultInstalled || System.Environment.GetEnvironmentVariable("HEXTECH_MPLAB_THROW_FORM_BATCH") != "1")
		{
			return;
		}

		MethodInfo? target = FindType("HextechRunes.HextechFormAutoPlayHooks")
			?.GetMethod("PlayCardBatchVfx", BindingFlags.Static | BindingFlags.NonPublic);
		if (target == null)
		{
			return;
		}

		_faultInstalled = true;
		new HarmonyLib.Harmony("hextech.mplab.fault").Patch(
			target,
			prefix: new HarmonyLib.HarmonyMethod(typeof(MpLabDriver).GetMethod(nameof(ThrowingPrefix), BindingFlags.Static | BindingFlags.NonPublic)));
		Info("fault injection installed: HextechFormAutoPlayHooks.PlayCardBatchVfx throws");
	}

	private static bool ThrowingPrefix()
	{
		throw new InvalidOperationException("mplab injected fault in form batch");
	}

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

		InstallFaultInjection();

		// 隔离 HOME 是全新存档,有窗口运行时新手引导会在开战时抛空引用并杀死回合循环。
		if (!_ftuesDisabled && MegaCrit.Sts2.Core.Saves.SaveManager.Instance != null)
		{
			_ftuesDisabled = true;
			MegaCrit.Sts2.Core.Saves.SaveManager.Instance.SetFtuesEnabled(false);
		}

		Node root = _tree!.Root;
		RunState? runState = RunManager.Instance?.DebugOnlyGetState();
		// 子菜单栈里会同时挂着隐藏的选角界面,读档大厅要先于它判断。
		NMultiplayerLoadGameScreen? loadScreen = runState == null ? FindNode<NMultiplayerLoadGameScreen>(root) : null;
		if (loadScreen != null && loadScreen.IsVisibleInTree())
		{
			DriveLoadLobby(loadScreen);
			return;
		}

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

		// 读档复现(HEXTECH_MPLAB_NO_SEED=1):牌组与海克斯全部来自存档,不再追加。
		if (!_deckSeeded && System.Environment.GetEnvironmentVariable("HEXTECH_MPLAB_NO_SEED") == "1")
		{
			_deckSeeded = true;
			Info($"no-seed mode: players={string.Join("; ", runState.Players.Select(p => $"{p.NetId} {p.Character.Id.Entry} relics=[{string.Join(",", p.Relics.Select(r => r.Id.Entry))}]"))}");
			foreach (RelicModel r in runState.Players.SelectMany(p => p.Relics).Where(r => r.GetType().Name == "EchoFormUpgradeRune"))
			{
				Type? t = r.GetType().BaseType;
				while (t != null && !t.IsGenericType) { t = t.BaseType; }
				Type? arg = t?.GetGenericArguments()[0];
				static string Ctx(Type? x) => x == null ? "null" : $"{x.Assembly.GetName().Name}@{System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(x.Assembly)?.Name}#{x.Assembly.GetHashCode()} loc={x.Assembly.Location}";
				Info($"rune TCard sameAsGame={ReferenceEquals(arg, typeof(EchoForm))} runeTCard={Ctx(arg)} gameEchoForm={Ctx(typeof(EchoForm))} runeRelicModel={Ctx(r.GetType().Assembly.GetType("HextechRunes.HextechRelicBase")?.BaseType)} gameRelicModel={Ctx(typeof(RelicModel))} ownerIsPlayer={runState.Players.Any(p => ReferenceEquals(p, r.Owner))}");
			}

			foreach (Player p in runState.Players)
			{
				foreach (CardModel c in p.Deck.Cards.Where(c => c.Id.Entry.Contains("FORM")))
				{
					Info($"deck card {p.NetId} {c.Id.Entry}: type={c.GetType().FullName} asm={c.GetType().Assembly.GetName().Name} isVanillaEchoForm={c is EchoForm} base={c.GetType().BaseType?.FullName}");
				}
			}
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

		LogRunStateHeartbeat(runState, root, now);

		NMapScreen? map = FindNode<NMapScreen>(root);
		if (map != null && map.IsTravelEnabled && !map.IsTraveling)
		{
			// HEXTECH_MPLAB_MAP_COMMAND="act 2;limitless-chapter X":主机在可操作地图上按顺序发联网控制台命令
			// (走 ConsoleCmdGameAction 同步)。每条都等上一步流程(开局/每幕海克斯选择)结束 10 秒后再发。
			string[] commands = (System.Environment.GetEnvironmentVariable("HEXTECH_MPLAB_MAP_COMMAND") ?? "")
				.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			_firstMapMsec = _firstMapMsec == 0 ? now : _firstMapMsec;
			if (_mapCommandsSent < commands.Length && _role == "host" && FindRuneSelectionScreen(root) == null)
			{
				if (now - _firstMapMsec < 10000)
				{
					return;
				}

				string command = commands[_mapCommandsSent++];
				_firstMapMsec = now;
				Player? me = LocalContext.GetMe(runState);
				if (me != null)
				{
					Info($"sending console command '{command}' at act {runState.CurrentActIndex}");
					RunManager.Instance!.ActionQueueSynchronizer.RequestEnqueue(new MegaCrit.Sts2.Core.DevConsole.ConsoleCmdGameAction(me, command, false));
					return;
				}
			}

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

	private static int _mapCommandsSent;
	private static ulong _firstMapMsec;
	private static ulong _lastHeartbeatMsec;

	// 每 5 秒记一次所在幕/房间/可见界面,用于判断黑屏卡死(状态长期不变且没有可操作界面)。
	private static void LogRunStateHeartbeat(RunState runState, Node root, ulong now)
	{
		if (now - _lastHeartbeatMsec < 5000)
		{
			return;
		}

		_lastHeartbeatMsec = now;
		string room = runState.CurrentRoom?.GetType().Name ?? "null";
		NMapScreen? map = FindNode<NMapScreen>(root);
		Node? runeScreen = FindRuneSelectionScreen(root);
		Info($"heartbeat act={runState.CurrentActIndex} floor={runState.TotalFloor} room={room} map={(map == null ? "none" : $"visible={map.IsVisibleInTree()} travel={map.IsTravelEnabled} traveling={map.IsTraveling}")} runeScreen={(runeScreen == null ? "none" : "open")} combat={CombatManager.Instance?.IsInProgress}");
	}

	private static bool _loadReady;
	private static int _loadLobbyLogs;

	// --fastmp=load 的读档大厅:主机等客户端连上后再就绪,两端就绪即开局。
	private static void DriveLoadLobby(NMultiplayerLoadGameScreen screen)
	{
		if (!_loadReady && _loadLobbyLogs == 0)
		{
			Info($"load screen found: lobbyField={typeof(NMultiplayerLoadGameScreen).GetField("_runLobby", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(screen)?.GetType().Name ?? "null"}");
		}

		if (_loadReady
			|| typeof(NMultiplayerLoadGameScreen).GetField("_runLobby", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(screen) is not LoadRunLobby lobby)
		{
			return;
		}

		int connected = lobby.Players.Count;
		if (_loadLobbyLogs++ % 10 == 0)
		{
			Info($"load lobby seen: players={connected} ids=[{string.Join(",", lobby.Players.Select(p => p.id))}]");
		}

		if (_role == "host" && connected < _expectedPlayers)
		{
			return;
		}

		MethodInfo? setReady = typeof(LoadRunLobby).GetMethod("SetReady", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		if (setReady == null)
		{
			Log.Warn($"{Tag} LoadRunLobby.SetReady not found", 2);
			_loadReady = true;
			return;
		}

		setReady.Invoke(lobby, [true]);
		_loadReady = true;
		Info($"load lobby ready: players={connected}");
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
			// 必须走公开入口:私有 ChangeCharacter 只改本机,不广播,客户端的角色在主机那边仍是默认值。
			CharacterModel character = FormScenario.Character;
			lobby.SetLocalCharacter(character);
			_characterChosen = true;
			Info($"character chosen: {character.Id.Entry} (local id {lobby.LocalPlayer.id})");
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

	private static bool _starsPoked;

	private static string DescribePiles(Player player)
	{
		PlayerCombatState? combat = player.PlayerCombatState;
		if (combat == null)
		{
			return "cards=?";
		}

		Type formType = FormScenario.Card.GetType();
		string forms = string.Join(",", combat.AllCards
			.Where(card => card.GetType() == formType)
			.Select(card => (card.Pile?.Type.ToString() ?? "none") + (card.IsUpgraded ? "+" : "")));
		return $"cards={combat.AllCards.Count()} stars={combat.Stars} forms=[{forms}]";
	}

	// 逐个获得,避免同一玩家的多个 Obtain 并发交错。
	private static async Task GrantRunesInOrder(Player player, IReadOnlyList<Type> runeTypes)
	{
		MethodInfo obtain = typeof(RelicCmd).GetMethods(BindingFlags.Public | BindingFlags.Static)
			.First(m => m.Name == nameof(RelicCmd.Obtain) && m.IsGenericMethodDefinition && m.GetParameters().Length == 1);
		foreach (Type runeType in runeTypes)
		{
			await (Task)obtain.MakeGenericMethod(runeType).Invoke(null, [player])!;
		}
	}

	private static void SeedDecks(RunState runState)
	{
		_deckSeeded = true;
		List<Type> runeTypes = FormScenario.RuneTypeNames.Select(FindType).OfType<Type>().ToList();
		CardModel canonical = FormScenario.Card;
		// 1 张走逐张自动打出,≥2 张走合并批处理。
		int formCount = int.TryParse(System.Environment.GetEnvironmentVariable("HEXTECH_MPLAB_FORMS"), out int forms) ? Math.Max(0, forms) : 3;
		List<Task> grants = [];
		foreach (Player player in runState.Players)
		{
			for (int i = 0; i < formCount; i++)
			{
				CardModel card = runState.CreateCard(canonical, player);
				// 模拟实战里拿海克斯前就已在火堆升级过的形态牌。
				if (System.Environment.GetEnvironmentVariable("HEXTECH_MPLAB_FORMS_UPGRADED") == "1" && card.IsUpgradable)
				{
					card.UpgradeInternal();
					card.FinalizeUpgradeInternal();
				}

				// 带非克隆附魔的形态牌不满足合并条件,整批回退到逐张自动打出。
				if (System.Environment.GetEnvironmentVariable("HEXTECH_MPLAB_FORMS_ENCHANTED") == "1")
				{
					CardCmd.Enchant(ModelDb.Enchantment<MegaCrit.Sts2.Core.Models.Enchantments.Swift>().ToMutable(), card, 1m);
				}

				player.Deck.AddInternal(card, player.Deck.Cards.Count, silent: true);
			}

			grants.Add(GrantRunesInOrder(player, runeTypes));
			Info($"seeded player {player.NetId}: deck={player.Deck.Cards.Count} cards, runes={string.Join(",", runeTypes.Select(t => t.Name))}");
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
				Type formType = canonical.GetType();
				string decks = string.Join("; ", runState.Players.Select(p => $"{p.NetId}: forms={p.Deck.Cards.Count(c => c.GetType() == formType)} deck={p.Deck.Cards.Count} relics=[{string.Join(",", p.Relics.Select(r => r.Id.Entry))}]"));
				Log.Info($"{Tag}[{_role}] relic grant done: {decks}", 2);
				Log.Info($"{Tag}[{_role}] gold after grant: {string.Join(", ", runState.Players.Select(p => $"{p.NetId}={p.Gold}"))}", 2);
			}
		}, TaskScheduler.Default);
	}

	private static Node? FindRuneSelectionScreen(Node root)
	{
		_runeSelectionScreenType ??= FindType("HextechRunes.HextechRuneSelectionScreen");
		return _runeSelectionScreenType == null ? null : FindNode(root, _runeSelectionScreenType);
	}

	// 选择界面有防误触:刚打开时的点击会被丢弃。界面出现 2 秒后再点,只要界面还开着就每 2 秒重试。
	private static void PickFirstRune(Node screen)
	{
		ulong id = screen.GetInstanceId();
		ulong now = Time.GetTicksMsec();
		if (!RuneScreenNextPickMsec.TryGetValue(id, out ulong due))
		{
			RuneScreenNextPickMsec[id] = now + 2000;
			return;
		}

		if (now < due)
		{
			return;
		}

		RuneScreenNextPickMsec[id] = now + 2000;

		Type type = screen.GetType();
		if (type.GetProperty("CurrentRelics", BindingFlags.Instance | BindingFlags.Public)?.GetValue(screen) is not IReadOnlyList<RelicModel> relics || relics.Count == 0)
		{
			return;
		}

		MethodInfo? select = type.GetMethod("OnHolderSelected", BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(RelicModel)], null);
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

		// 回合开始钩子(含形态开局自动打出)执行期间是 NotPlayPhase:此时记录的能力还没结算,
		// 结束回合请求也会被原版丢弃,驱动就此卡住。只在出牌阶段记录和结束回合。
		if (RunManager.Instance?.ActionQueueSynchronizer?.CombatState != ActionSynchronizerCombatState.PlayPhase)
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
			foreach (Player player in state.Players)
			{
				Info($"round {state.RoundNumber} player {player.NetId} gold={player.Gold}");
				Info($"round {state.RoundNumber} player {player.NetId}{(player == me ? "(me)" : "")} {player.Character.Id.Entry}: hp={player.Creature.CurrentHp}/{player.Creature.MaxHp} powers=[{string.Join(",", player.Creature.Powers.Select(p => $"{p.Id.Entry}:{p.Amount}"))}] {DescribePiles(player)} relics=[{string.Join(",", player.Relics.Select(r => r.Id.Entry))}]");
			}
			Info($"round {state.RoundNumber} block={me.Creature.Block} enemies=[{string.Join(",", state.Enemies.Select(e => $"{e.ModelId.Entry}:{e.CurrentHp}hp{(e.IsAlive ? "" : "(dead)")} doom={e.GetPowerAmount<DoomPower>()}"))}]");
			if (FormScenario.PlayFormCardEachRound)
			{
				// 走原版出牌动作入队(与手动出牌同一路径),两端各自只出自己的牌,联机同步。
				CardModel? toPlay = PileType.Hand.GetPile(me).Cards.FirstOrDefault(c => c.GetType() == FormScenario.Card.GetType());
				if (toPlay != null)
				{
					Info($"playing {toPlay.Id.Entry} for {me.NetId}");
					RunManager.Instance!.ActionQueueSynchronizer.RequestEnqueue(new PlayCardAction(toPlay, null));
				}
			}

			if (FormScenario.PokeStarsInCombat && _role == "host" && !_starsPoked)
			{
				// 驱动不出牌:在出牌阶段直接给 1 辉星,模拟打出生星牌,点燃"生成牌→辉星→铸造"链。只在主机执行,仅用于卡死检测。
				_starsPoked = true;
				Info($"poking 1 star for {me.NetId}");
				_ = PlayerCmd.GainStars(1, me).ContinueWith(t => Log.Info($"{Tag}[{_role}] star poke finished: {t.Status} {DescribePiles(me)}", 2), TaskScheduler.Default);
			}
			return;
		}

		if (now < _endTurnDueMsec)
		{
			return;
		}

		try
		{
			_turnsEnded++;
			Info($"ending turn #{_turnsEnded} round={state.RoundNumber} for {me.NetId} {DescribePiles(me)}");
			// 走原版结束回合动作入队:本地 PlayerCmd.EndTurn 在联机下不会同步给其他端。
			RunManager.Instance!.ActionQueueSynchronizer.RequestEnqueue(new EndPlayerTurnAction(me, me.PlayerCombatState?.TurnNumber ?? state.RoundNumber));
			// 若请求没被接受(下一次 tick 仍未就绪),隔 5 秒再发,避免每 tick 重复请求。
			_endTurnDueMsec = now + 5000;
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

/// <summary>
/// HEXTECH_MPLAB_FORM 选择角色 + 形态牌 + 要发放的符文,默认战士恶魔形态。
/// regentloop:储君 + 王国军势/凝辉/王令(初始遗物神授之权进房给辉星即可点燃循环,配合 HEXTECH_MPLAB_FORMS=0)。
/// </summary>
internal static class FormScenario
{
	private static readonly string Key = (System.Environment.GetEnvironmentVariable("HEXTECH_MPLAB_FORM") ?? "demon").Trim().ToLowerInvariant();

	internal static CharacterModel Character => Key switch
	{
		"echo" => ModelDb.Character<Defect>(),
		"reaper" => ModelDb.Character<Necrobinder>(),
		"miserable" => ModelDb.Character<Necrobinder>(),
		"serpent" => ModelDb.Character<Silent>(),
		"void" or "regentloop" => ModelDb.Character<Regent>(),
		_ => ModelDb.Character<Ironclad>()
	};

	internal static CardModel Card => Key switch
	{
		"echo" => ModelDb.Card<EchoForm>(),
		"reaper" => ModelDb.Card<ReaperForm>(),
		"miserable" => ModelDb.Card<Deathbringer>(),
		"serpent" => ModelDb.Card<SerpentForm>(),
		"void" => ModelDb.Card<VoidForm>(),
		_ => ModelDb.Card<DemonForm>()
	};

	internal static bool PokeStarsInCombat => Key == "regentloop";

	// miserable:每回合出牌阶段打出一张死亡使者,观察悲惨命运的格挡时点与灾厄统计。
	internal static bool PlayFormCardEachRound => Key == "miserable";

	internal static string[] RuneTypeNames => Key switch
	{
		"echo" => ["HextechRunes.EchoFormUpgradeRune"],
		"reaper" => ["HextechRunes.ReaperFormUpgradeRune"],
		"serpent" => ["HextechRunes.SerpentFormUpgradeRune"],
		"void" => ["HextechRunes.VoidFormUpgradeRune"],
		"regentloop" => ["HextechRunes.KingdomArmyRune", "HextechRunes.CondensedRadianceRune", "HextechRunes.RoyalCommandRune"],
		"miserable" => ["HextechRunes.MiserableFateRune"],
		"mawbank" => ["HextechRunes.DoubleVisionRune", "MegaCrit.Sts2.Core.Models.Relics.MawBank"],
		_ => ["HextechRunes.DemonFormUpgradeRune"]
	};
}
