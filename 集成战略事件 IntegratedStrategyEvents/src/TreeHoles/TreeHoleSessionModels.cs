using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace IntegratedStrategyEvents.TreeHoles;

internal sealed record TreeHoleSession(
	ActMap OriginalMap,
	IReadOnlyList<MapCoord> OriginalVisitedMapCoords,
	IReadOnlyList<IReadOnlyList<MapPointHistoryEntry>> OriginalMapPointHistory,
	int OriginalActFloor,
	SerializableActModel OriginalActSave,
	uint TreeHoleMapSeed,
	MapCoord? EntryMapCoord,
	string StageLabel,
	string DestinationActName,
	ActMap TreeHoleMap,
	MapCoord TerminalCoord);

internal sealed record EndlessFinaleSession(
	ActMap OriginalMap,
	IReadOnlyList<MapCoord> OriginalVisitedMapCoords,
	IReadOnlyList<IReadOnlyList<MapPointHistoryEntry>> OriginalMapPointHistory,
	int OriginalActFloor,
	SerializableActModel OriginalActSave,
	string StageLabel,
	string DestinationActName,
	ActMap FinaleMap,
	SpecialFinaleKind Kind);

internal sealed record BossNodeRenderSwap(
	RunState State,
	SerializableActModel OriginalActSave);

internal enum TreeHoleSaveKind
{
	TreeHole,
	EndlessFinale,
	EternalDustFinale,
	RadiantApexFinale,
	CarefreeViharaFinale,
	AbyssalJungleFinale,
	AbyssalJungleIsharmlaFinale,
	ProphetHornFragment,
	DesireHallFinale
}

internal enum SpecialFinaleKind
{
	EndlessFinale,
	EternalDust,
	RadiantApex,
	CarefreeVihara,
	AbyssalJungle,
	AbyssalJungleIsharmla,
	ProphetHornFragment,
	DesireHall
}

internal enum TreeHoleResumeRoom
{
	None,
	Map,
	Architect
}

internal sealed record TreeHoleSaveSnapshot(
	TreeHoleSaveKind Kind,
	int CurrentActIndex,
	string ParentActId,
	ActMap CurrentMap,
	MapCoord? CurrentMapCoord,
	IReadOnlyList<MapCoord> CurrentVisitedMapCoords,
	IReadOnlyList<IReadOnlyList<MapPointHistoryEntry>> CurrentMapPointHistory,
	int CurrentActFloor,
	ActMap OriginalMap,
	IReadOnlyList<MapCoord> OriginalVisitedMapCoords,
	IReadOnlyList<IReadOnlyList<MapPointHistoryEntry>> OriginalMapPointHistory,
	int OriginalActFloor,
	SerializableActModel OriginalActSave,
	uint TreeHoleMapSeed,
	string StageLabel,
	string DestinationActName,
	MapCoord TerminalCoord);

internal sealed record TreeHoleRestoreSnapshot(
	TreeHoleSaveKind Kind,
	int CurrentActIndex,
	string ParentActId,
	int CurrentActFloor,
	MapCoord? CurrentMapCoord,
	SerializableActMap? TemporaryMap,
	SerializableActMap OriginalMap,
	IReadOnlyList<MapCoord> OriginalVisitedMapCoords,
	IReadOnlyList<int> OriginalMapPointHistoryCounts,
	int OriginalActFloor,
	SerializableActModel OriginalActSave,
	uint TreeHoleMapSeed,
	string StageLabel,
	string DestinationActName,
	MapCoord TerminalCoord);
