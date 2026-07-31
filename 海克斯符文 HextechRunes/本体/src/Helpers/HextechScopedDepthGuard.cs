namespace HextechRunes;

// 深度只在当前异步执行流内可见；嵌套响应链共享守卫，彼此并发的任务不互相抑制。
internal sealed class HextechScopedDepthGuard
{
	private readonly AsyncLocal<int> _depth = new();

	internal bool IsActive => _depth.Value > 0;

	internal void Enter()
	{
		_depth.Value++;
	}

	internal void Exit()
	{
		_depth.Value = Math.Max(0, _depth.Value - 1);
	}

	internal async Task RunAsync(Func<Task> action)
	{
		Enter();
		try
		{
			await action();
		}
		finally
		{
			Exit();
		}
	}

	// Harmony prefix 在原方法返回 Task 前进入守卫；包装任务捕获该执行流后，
	// 必须立即退出调用者执行流，避免守卫状态泄漏到后续无关命令。
	internal Task WrapEnteredTask(Task task, Func<Task>? afterCompletion = null)
	{
		try
		{
			return CompleteEnteredTask(task, afterCompletion);
		}
		finally
		{
			Exit();
		}
	}

	private async Task CompleteEnteredTask(Task task, Func<Task>? afterCompletion)
	{
		try
		{
			await task;
		}
		finally
		{
			Exit();
		}

		if (afterCompletion != null)
		{
			await afterCompletion();
		}
	}
}
