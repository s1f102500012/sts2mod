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
	private bool _canonicalTerminationStarted;

	public long ActivityRevision => _activityRevision;

	public int MemberCount => _members.Count;

	public void MarkActivity()
	{
		_activityRevision++;
		_stableRevision = -1;
		_stableMemberCount = 0;
	}

	public bool TryBeginCanonicalTermination()
	{
		if (_canonicalTerminationStarted)
		{
			return false;
		}

		_canonicalTerminationStarted = true;
		return true;
	}

	public bool TryIssueCompletionCertificate(
		long activityRevision,
		int memberCount)
	{
		if (activityRevision != _activityRevision
			|| memberCount != _members.Count)
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
			|| _stableMemberCount != _members.Count)
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
