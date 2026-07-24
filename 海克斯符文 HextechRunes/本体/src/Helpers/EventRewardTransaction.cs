namespace HextechRunes;

internal sealed class EventRewardTransaction<T>
{
	private readonly List<T> _items = [];
	private bool _acceptingRecords = true;
	private bool _commitStarted;

	public int Count => _items.Count;

	public bool IsAcceptingRecords => _acceptingRecords;

	public void Record(T item)
	{
		if (!TryRecord(item))
		{
			throw new InvalidOperationException("Cannot record rewards after the event transaction has been sealed.");
		}
	}

	public bool TryRecord(T item)
	{
		if (!_acceptingRecords)
		{
			return false;
		}

		_items.Add(item);
		return true;
	}

	public void CloseForRecording()
	{
		_acceptingRecords = false;
	}

	public async Task CommitSequentially(Func<T, Task> commit)
	{
		ArgumentNullException.ThrowIfNull(commit);
		if (_commitStarted)
		{
			throw new InvalidOperationException("The event transaction has already been committed.");
		}

		CloseForRecording();
		_commitStarted = true;
		foreach (T item in _items)
		{
			await commit(item);
		}
	}
}
