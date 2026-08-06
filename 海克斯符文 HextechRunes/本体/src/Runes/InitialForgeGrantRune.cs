namespace HextechRunes;

public abstract class InitialForgeGrantRune : HextechRelicBase
{
	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public bool SavedInitialForgeGrantPending { get; set; }

	public override bool HasUponPickupEffect => true;

	protected abstract int InitialForgeCount { get; }

	public sealed override async Task AfterObtained()
	{
		if (Owner == null)
		{
			return;
		}

		// RelicCmd 已在调用 AfterObtained 前把本体放进背包。这个标记必须在第一次打开选择界面前落下，
		// 让中途退出留下的存档能区分“拾取效果已完成”和“只有海克斯本体已入库”。
		SavedInitialForgeGrantPending = true;
		Flash();
		await ResumePendingInitialForgeGrant();
	}

	internal async Task<bool> ResumePendingInitialForgeGrant()
	{
		if (!SavedInitialForgeGrantPending)
		{
			return true;
		}

		if (Owner == null)
		{
			return false;
		}

		int count = Math.Max(0, InitialForgeCount);
		bool completed = count == 0
			|| await HextechForgeGrantHelper.TryObtainRandomForges(Owner, count);
		if (completed)
		{
			SavedInitialForgeGrantPending = false;
		}

		return completed;
	}
}
