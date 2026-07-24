using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Runs;

namespace IntegratedStrategyEvents.TreeHoles;

internal enum IntegratedStrategyTemporaryMapEntryKind
{
	TreeHole,
	SpecialFinale,
	ProphetHornFragment,
	TreeHoleReturn
}

internal sealed class IntegratedStrategyTemporaryMapAction : GameAction
{
	private readonly Player _player;
	private readonly IntegratedStrategyTemporaryMapEntryKind _entryKind;
	private readonly SpecialFinaleKind _finaleKind;
	private readonly string _destinationActName;
	private readonly string _stageLabel;

	public IntegratedStrategyTemporaryMapAction(
		Player player,
		IntegratedStrategyTemporaryMapEntryKind entryKind,
		SpecialFinaleKind finaleKind,
		string destinationActName,
		string stageLabel)
	{
		_player = player;
		_entryKind = entryKind;
		_finaleKind = finaleKind;
		_destinationActName = destinationActName;
		_stageLabel = stageLabel;
	}

	public override ulong OwnerId => _player.NetId;

	public override GameActionType ActionType => GameActionType.NonCombat;

	public static void EnqueueTreeHoleEntry(Player player, string destinationActName, string stageLabel)
	{
		Enqueue(new IntegratedStrategyTemporaryMapAction(
			player,
			IntegratedStrategyTemporaryMapEntryKind.TreeHole,
			default,
			destinationActName,
			stageLabel));
	}

	public static void EnqueueSpecialFinaleEntry(Player player, SpecialFinaleKind finaleKind)
	{
		Enqueue(new IntegratedStrategyTemporaryMapAction(
			player,
			IntegratedStrategyTemporaryMapEntryKind.SpecialFinale,
			finaleKind,
			string.Empty,
			string.Empty));
	}

	public static void EnqueueTreeHoleReturn(Player player)
	{
		Enqueue(new IntegratedStrategyTemporaryMapAction(
			player,
			IntegratedStrategyTemporaryMapEntryKind.TreeHoleReturn,
			default,
			string.Empty,
			string.Empty));
	}

	public static void EnqueueProphetHornFragmentEntry(Player player, string destinationActName, string stageLabel)
	{
		Enqueue(new IntegratedStrategyTemporaryMapAction(
			player,
			IntegratedStrategyTemporaryMapEntryKind.ProphetHornFragment,
			SpecialFinaleKind.ProphetHornFragment,
			destinationActName,
			stageLabel));
	}

	protected override Task ExecuteAction()
	{
		Log.Info(
			$"{ModInfo.LogPrefix} Executing synchronized temporary-map action " +
			$"{_entryKind} for player {_player.NetId}.");
		return _entryKind switch
		{
			IntegratedStrategyTemporaryMapEntryKind.TreeHole =>
				TreeHoleEntryCoordinator.EnterFromSyncedAction(_player, _destinationActName, _stageLabel),
			IntegratedStrategyTemporaryMapEntryKind.TreeHoleReturn =>
				TreeHoleSessionManager.RestoreOriginalMapFromSyncedAction(_player),
			IntegratedStrategyTemporaryMapEntryKind.SpecialFinale =>
				SpecialFinaleCoordinator.EnterSpecialFinaleFromSyncedAction(_player, _finaleKind),
			IntegratedStrategyTemporaryMapEntryKind.ProphetHornFragment =>
				SpecialFinaleCoordinator.EnterProphetHornFragmentFromSyncedAction(
					_player,
					_destinationActName,
					_stageLabel),
			_ => Task.CompletedTask
		};
	}

	public override INetAction ToNetAction()
	{
		return new NetIntegratedStrategyTemporaryMapAction
		{
			EntryKind = _entryKind,
			FinaleKind = _finaleKind,
			DestinationActName = _destinationActName,
			StageLabel = _stageLabel
		};
	}

	public override string ToString()
	{
		return
			$"{nameof(IntegratedStrategyTemporaryMapAction)} {_player.NetId} " +
			$"{_entryKind} {_finaleKind} {_stageLabel}/{_destinationActName}";
	}

	private static void Enqueue(IntegratedStrategyTemporaryMapAction action)
	{
		Log.Info($"{ModInfo.LogPrefix} Requesting synchronized temporary-map action: {action}.");
		RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(action);
	}
}

public sealed class NetIntegratedStrategyTemporaryMapAction : INetAction, IPacketSerializable
{
	internal IntegratedStrategyTemporaryMapEntryKind EntryKind;
	internal SpecialFinaleKind FinaleKind;
	internal string DestinationActName = string.Empty;
	internal string StageLabel = string.Empty;

	public GameAction ToGameAction(Player player)
	{
		return new IntegratedStrategyTemporaryMapAction(
			player,
			EntryKind,
			FinaleKind,
			DestinationActName,
			StageLabel);
	}

	public void Serialize(PacketWriter writer)
	{
		writer.WriteByte((byte)EntryKind, 8);
		writer.WriteByte((byte)FinaleKind, 8);
		writer.WriteString(DestinationActName);
		writer.WriteString(StageLabel);
	}

	public void Deserialize(PacketReader reader)
	{
		EntryKind = (IntegratedStrategyTemporaryMapEntryKind)reader.ReadByte(8);
		FinaleKind = (SpecialFinaleKind)reader.ReadByte(8);
		DestinationActName = reader.ReadString();
		StageLabel = reader.ReadString();
	}

	public override string ToString()
	{
		return
			$"{nameof(NetIntegratedStrategyTemporaryMapAction)} " +
			$"{EntryKind} {FinaleKind} {StageLabel}/{DestinationActName}";
	}
}
