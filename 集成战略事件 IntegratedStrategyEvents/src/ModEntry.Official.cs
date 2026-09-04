#if STS2_109_OR_NEWER
namespace IntegratedStrategyEvents;
public static partial class ModEntry
{
	// STS2 0.110.1/0.111.0 由官方程序集发现建立 SavedProperty 缓存。
	private static void InjectSavedPropertyCaches() { }
}
#endif
