namespace UniversalDominionSword;

internal sealed class ErasurePersistenceLease : IDisposable
{
	private readonly Action _onCommit;
	private readonly Action _onAbandon;
	private bool _committed;
	private bool _disposed;

	internal ErasurePersistenceLease(
		Action onCommit,
		Action onAbandon)
	{
		_onCommit = onCommit;
		_onAbandon = onAbandon;
	}

	public void Commit()
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(ErasurePersistenceLease));
		}
		if (_committed)
		{
			return;
		}

		_onCommit();
		_committed = true;
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}
		if (!_committed)
		{
			_onAbandon();
		}
		_disposed = true;
	}
}
