namespace UniversalDominionSword;

internal readonly record struct LineageCompletionCertificate(
	long OperationSequence,
	long ActivityRevision,
	int MemberCount);

internal sealed partial class ErasureLineage
{
	private long _activityRevision;
	private long _stableRevision = -1;
	private int _stableMemberCount;
	private int _outstandingContinuationLeaseCount;

	public long ActivityRevision => _activityRevision;

	public int MemberCount => _members.Count;

	public int OutstandingContinuationLeaseCount =>
		_outstandingContinuationLeaseCount;

	public void MarkActivity()
	{
		_activityRevision++;
		_stableRevision = -1;
		_stableMemberCount = 0;
	}

	public void AcquireContinuationLease()
	{
		_outstandingContinuationLeaseCount++;
		_stableRevision = -1;
		_stableMemberCount = 0;
	}

	public void ReleaseContinuationLease()
	{
		if (_outstandingContinuationLeaseCount <= 0)
		{
			throw new InvalidOperationException(
				"Cannot release an inactive continuation lease.");
		}

		_outstandingContinuationLeaseCount--;
	}

	public bool TryIssueCompletionCertificate(
		long activityRevision,
		int memberCount)
	{
		if (activityRevision != _activityRevision
			|| memberCount != _members.Count
			|| _outstandingContinuationLeaseCount != 0)
		{
			return false;
		}

		_stableRevision = activityRevision;
		_stableMemberCount = memberCount;
		return true;
	}

	public bool TryGetCompletionCertificate(
		out LineageCompletionCertificate certificate)
	{
		if (_stableRevision != _activityRevision
			|| _stableMemberCount != _members.Count
			|| _outstandingContinuationLeaseCount != 0)
		{
			certificate = default;
			return false;
		}

		certificate = new LineageCompletionCertificate(
			OperationSequence,
			_activityRevision,
			_members.Count);
		return true;
	}
}
