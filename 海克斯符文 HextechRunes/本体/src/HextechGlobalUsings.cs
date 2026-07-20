// 全程序集 global usings(跨域高频命名空间;领域专属的一批在 EnemyHexes/HextechEnemyHexGlobalUsings.cs)。
// 提升前用 sts2.dll 全类型表核对过无同名冲突;新增命名空间前先确认不会与 BCL/GodotSharp 产生歧义
// (global using Godot 明确不可加:Environment/HttpClient/Range/Timer 与 BCL 冲突且有真实裸用)。
global using System.Reflection;
global using MegaCrit.Sts2.Core.HoverTips;
global using MegaCrit.Sts2.Core.Localization.DynamicVars;
global using MegaCrit.Sts2.Core.Logging;
global using MegaCrit.Sts2.Core.Saves.Runs;
#if STS2_109_OR_NEWER
// 0.109.0 起 SavedPropertiesTypeCache 并入 Multiplayer.Serialization.ModelIdSerializationCache
// (新增 category/entry/epoch 映射与 ContentSorter 确定性排序+XxHash32 哈希)。私有字段
// _netIdToPropertyNameMap/_propertyNameToNetIdMap 同名保留,反射点经别名继续可用;
// 差异 API(NetIdBitSize→PropertyIdBitSize、InjectTypeIntoCache 移除)在各消费点逐一 #if。
global using SavedPropertiesTypeCache = MegaCrit.Sts2.Core.Multiplayer.Serialization.ModelIdSerializationCache;
#endif
