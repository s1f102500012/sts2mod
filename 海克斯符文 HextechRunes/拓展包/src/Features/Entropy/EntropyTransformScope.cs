namespace HextechRunesSponsorPack;

// 熵增「变化」期间的递归守卫。变化出的复制品会入牌组,牌组里其它熵增牌的 AfterCardChangedPiles
// 因此被唤醒并再次变化,不挡住就是连锁反应。
//
// 守卫必须跟着 await 链走,所以用 AsyncLocal;计数而非布尔,是为了容忍嵌套变化。
// 作用域只覆盖本模组自己的 AfterCardChangedPiles 判定,不影响任何其他模组看到的入牌组事件。
internal static class EntropyTransformScope
{
	private static readonly AsyncLocal<int> Depth = new();

	internal static bool IsActive => Depth.Value > 0;

	internal static IDisposable Enter()
	{
		Depth.Value++;
		return new Scope();
	}

	private sealed class Scope : IDisposable
	{
		private bool _exited;

		public void Dispose()
		{
			if (_exited)
			{
				return;
			}

			_exited = true;
			Depth.Value--;
		}
	}
}
