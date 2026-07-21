using MegaCrit.Sts2.Core.Runs;

namespace IntegratedStrategyEvents.TreeHoles;

internal sealed class TreeHoleSessionStore
{
	private readonly Dictionary<RunState, TreeHoleSession> _treeHoleSessions = [];
	private readonly Dictionary<RunState, EndlessFinaleSession> _finaleSessions = [];
	private readonly Dictionary<RunState, TreeHoleRestoreSnapshot> _pendingRestoreSnapshots = [];
	private readonly HashSet<RunState> _pendingTreeHoleEntries = [];
	private readonly Dictionary<RunState, DateTime> _pendingTreeHoleReturns = [];
	private readonly HashSet<RunState> _pendingFinaleEntries = [];
	private readonly HashSet<RunState> _terminalRewardsProceeded = [];
	private readonly HashSet<RunState> _pendingArchitectCompletions = [];
	private readonly HashSet<RunState> _pendingMapRoomResumes = [];
	private readonly HashSet<RunState> _suppressCompletionUntilTerminalProceed = [];

	public bool IsActive(RunState state)
	{
		return _treeHoleSessions.ContainsKey(state) || _finaleSessions.ContainsKey(state);
	}

	public bool TryGetTreeHoleSession(RunState state, out TreeHoleSession session)
	{
		return _treeHoleSessions.TryGetValue(state, out session!);
	}

	public void SetTreeHoleSession(RunState state, TreeHoleSession session)
	{
		_treeHoleSessions[state] = session;
	}

	public bool RemoveTreeHoleSession(RunState state)
	{
		return _treeHoleSessions.Remove(state);
	}

	public bool TryGetFinaleSession(RunState state, out EndlessFinaleSession session)
	{
		return _finaleSessions.TryGetValue(state, out session!);
	}

	public void SetFinaleSession(RunState state, EndlessFinaleSession session)
	{
		_finaleSessions[state] = session;
	}

	public bool RemoveFinaleSession(RunState state)
	{
		return _finaleSessions.Remove(state);
	}

	public void QueueRestore(RunState state, TreeHoleRestoreSnapshot snapshot)
	{
		_pendingRestoreSnapshots[state] = snapshot;
	}

	public bool TryGetPendingRestore(RunState state, out TreeHoleRestoreSnapshot snapshot)
	{
		return _pendingRestoreSnapshots.TryGetValue(state, out snapshot!);
	}

	public bool RemovePendingRestore(RunState state)
	{
		return _pendingRestoreSnapshots.Remove(state);
	}

	public void SuppressCompletionUntilTerminalProceed(RunState state)
	{
		_suppressCompletionUntilTerminalProceed.Add(state);
	}

	public bool IsCompletionSuppressedUntilTerminalProceed(RunState state)
	{
		return _suppressCompletionUntilTerminalProceed.Contains(state);
	}

	public bool RemoveCompletionSuppression(RunState state)
	{
		return _suppressCompletionUntilTerminalProceed.Remove(state);
	}

	public bool AddPendingTreeHoleEntry(RunState state)
	{
		return _pendingTreeHoleEntries.Add(state);
	}

	public bool RemovePendingTreeHoleEntry(RunState state)
	{
		return _pendingTreeHoleEntries.Remove(state);
	}

	public bool AddPendingTreeHoleReturn(RunState state)
	{
		if (_pendingTreeHoleReturns.ContainsKey(state))
		{
			return false;
		}

		_pendingTreeHoleReturns[state] = DateTime.UtcNow;
		return true;
	}

	public bool IsPendingTreeHoleReturnExpired(RunState state, TimeSpan timeout)
	{
		return _pendingTreeHoleReturns.TryGetValue(state, out DateTime requestedAt) &&
			DateTime.UtcNow - requestedAt > timeout;
	}

	public bool RemovePendingTreeHoleReturn(RunState state)
	{
		return _pendingTreeHoleReturns.Remove(state);
	}

	public void AddTerminalRewardsProceeded(RunState state)
	{
		_terminalRewardsProceeded.Add(state);
	}

	public bool HasTerminalRewardsProceeded(RunState state)
	{
		return _terminalRewardsProceeded.Contains(state);
	}

	public bool RemoveTerminalRewardsProceeded(RunState state)
	{
		return _terminalRewardsProceeded.Remove(state);
	}

	public bool AddPendingFinaleEntry(RunState state)
	{
		return _pendingFinaleEntries.Add(state);
	}

	public bool RemovePendingFinaleEntry(RunState state)
	{
		return _pendingFinaleEntries.Remove(state);
	}

	public void AddPendingArchitectCompletion(RunState state)
	{
		_pendingArchitectCompletions.Add(state);
	}

	public bool HasPendingArchitectCompletion(RunState state)
	{
		return _pendingArchitectCompletions.Contains(state);
	}

	public bool RemovePendingArchitectCompletion(RunState state)
	{
		return _pendingArchitectCompletions.Remove(state);
	}

	public void AddPendingMapRoomResume(RunState state)
	{
		_pendingMapRoomResumes.Add(state);
	}

	public bool HasPendingMapRoomResume(RunState state)
	{
		return _pendingMapRoomResumes.Contains(state);
	}

	public bool RemovePendingMapRoomResume(RunState state)
	{
		return _pendingMapRoomResumes.Remove(state);
	}

	public void ClearForRunStarted(RunState state)
	{
		_pendingRestoreSnapshots.TryGetValue(state, out TreeHoleRestoreSnapshot? pendingRestoreSnapshot);
		bool suppressCompletionUntilTerminalProceed = _suppressCompletionUntilTerminalProceed.Contains(state);
		bool pendingArchitectCompletion = _pendingArchitectCompletions.Contains(state);
		bool pendingMapRoomResume = _pendingMapRoomResumes.Contains(state);
		_treeHoleSessions.Clear();
		_finaleSessions.Clear();
		_pendingRestoreSnapshots.Clear();
		if (pendingRestoreSnapshot != null)
		{
			_pendingRestoreSnapshots[state] = pendingRestoreSnapshot;
		}

		_pendingTreeHoleEntries.Clear();
		_pendingTreeHoleReturns.Clear();
		_pendingFinaleEntries.Clear();
		_terminalRewardsProceeded.Clear();
		_pendingArchitectCompletions.Clear();
		if (pendingArchitectCompletion)
		{
			_pendingArchitectCompletions.Add(state);
		}
		_pendingMapRoomResumes.Clear();
		if (pendingMapRoomResume)
		{
			_pendingMapRoomResumes.Add(state);
		}
		_suppressCompletionUntilTerminalProceed.Clear();
		if (suppressCompletionUntilTerminalProceed)
		{
			_suppressCompletionUntilTerminalProceed.Add(state);
		}
	}
}
