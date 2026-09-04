namespace IntegratedStrategyEvents.TreeHoles;

internal static class TreeHoleTransitionSettlement
{
	// 帧只用于让出执行权，不能决定事件是否完成或允许拆房。
	internal static async Task<bool> Await(Func<bool> isSettled, Func<Task> pendingChoices,
		Func<Task> nextFrame, Func<bool> isCurrentRun)
	{
		while (isCurrentRun())
		{
			Task pending = pendingChoices();
			while (!pending.IsCompleted && isCurrentRun()) await nextFrame();
			if (!isCurrentRun())
			{
				// 已退出的跑局不再等待网络选项；仍观察迟到的任务异常。
				_ = pending.ContinueWith(task => { _ = task.Exception; },
					CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
				return false;
			}
			await pending;
			if (!isCurrentRun()) return false;
			if (isSettled()) return true;
			await nextFrame();
		}
		return false;
	}
}
