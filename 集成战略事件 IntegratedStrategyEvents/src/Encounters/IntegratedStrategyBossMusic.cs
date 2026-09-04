using MegaCrit.Sts2.Core.Models;

namespace IntegratedStrategyEvents.Encounters;

/// <summary>
/// BOSS 战音乐注册表：每个 BOSS 一行（播放器名 / 音轨 / 音量 / 遭遇匹配），
/// 替代原先每 BOSS 一个复制粘贴的 *MusicController 文件。
/// 新增 BOSS 音乐 = 加一个 TrackPath 常量 + 一行注册 + 加入 All。
/// 返回主菜单时的停止由 IntegratedStrategyMusicShutdown 统一负责。
/// </summary>
internal static class IntegratedStrategyBossMusic
{
	public const string IsharmlaTrackPath = $"res://{ModInfo.ModId}/audio/music/isharmla_boss.ogg";
	public const string FrostNovaTrackPath = $"res://{ModInfo.ModId}/audio/music/frost_nova_winter_scar.ogg";
	public const string BozhokastiTrackPath = $"res://{ModInfo.ModId}/audio/music/bozhokasti_boss.ogg";
	public const string IzumikTrackPath = $"res://{ModInfo.ModId}/audio/music/izumik_boss.ogg";
	public const string KuilongTrackPath = $"res://{ModInfo.ModId}/audio/music/kuilong_mahasattva_avatar.ogg";
	public const string CalendarKingsTrackPath = $"res://{ModInfo.ModId}/audio/music/calendar_kings.ogg";
	public const string SorrowfulLockTrackPath = $"res://{ModInfo.ModId}/audio/music/sorrowful_lock_boss.ogg";

	public static readonly EncounterMusicController Isharmla = new(
		"IntegratedStrategyIsharmlaMusic", IsharmlaTrackPath, 0.52f, "Isharmla",
		Matches<IsharmlaCorruptedHeartBossEncounter>);

	public static readonly EncounterMusicController FrostNova = new(
		"IntegratedStrategyFrostNovaMusic", FrostNovaTrackPath, 0.34f, "FrostNova",
		Matches<FrostNovaWinterScarBossEncounter>);

	public static readonly EncounterMusicController Bozhokasti = new(
		"IntegratedStrategyBozhokastiMusic", BozhokastiTrackPath, 0.38f, "Bozhokasti",
		Matches<BozhokastiSaintguardGunnerBossEncounter>);

	public static readonly EncounterMusicController Izumik = new(
		"IntegratedStrategyIzumikMusic", IzumikTrackPath, 0.40f, "Izumik",
		Matches<IzumikEcologicalFountainBossEncounter>);

	public static readonly EncounterMusicController Kuilong = new(
		"IntegratedStrategyKuilongMusic", KuilongTrackPath, 0.40f, "Kuilong",
		Matches<KuilongMahasattvaAvatarBossEncounter>);

	public static readonly EncounterMusicController CalendarKings = new(
		"IntegratedStrategyCalendarKingsMusic", CalendarKingsTrackPath, 0.82f, "calendar kings",
		Matches<CalendarKingsPincerBossEncounter>);

	public static readonly EncounterMusicController SorrowfulLock = new(
		"IntegratedStrategySorrowfulLockMusic", SorrowfulLockTrackPath, 0.55f, "SorrowfulLock",
		Matches<SorrowfulLockBossEncounter>);

	public static readonly EncounterMusicController[] All =
	[
		Isharmla,
		FrostNova,
		Bozhokasti,
		Izumik,
		Kuilong,
		CalendarKings,
		SorrowfulLock
	];

	public static void StopAll()
	{
		foreach (EncounterMusicController controller in All)
		{
			controller.Stop(restoreGameMusic: false);
		}
	}

	private static bool Matches<TEncounter>(EncounterModel encounter)
		where TEncounter : EncounterModel
	{
		return encounter is TEncounter || encounter.CanonicalInstance is TEncounter;
	}
}
