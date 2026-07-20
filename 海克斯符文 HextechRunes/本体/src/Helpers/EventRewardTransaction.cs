namespace HextechRunes;

internal sealed class EventRewardTransaction<T>
{
	private readonly List<T> _items = [];
	private bool _sealed;

	public int Count => _items.Count;

	public void Record(T item)
	{
		if (_sealed)
		{
			throw new InvalidOperationException("Cannot record rewards after the event transaction has been sealed.");
		}

		_items.Add(item);
	}

	public async Task CommitSequentially(Func<T, Task> commit)
	{
		ArgumentNullException.ThrowIfNull(commit);
		if (_sealed)
		{
			throw new InvalidOperationException("The event transaction has already been committed.");
		}

		_sealed = true;
		foreach (T item in _items)
		{
			await commit(item);
		}
	}
}
