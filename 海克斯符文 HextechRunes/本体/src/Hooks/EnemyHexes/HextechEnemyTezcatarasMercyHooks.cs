using HarmonyLib;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

internal static class HextechEnemyTezcatarasMercyHooks
{


	[HarmonyPatch(typeof(RelicCmd), nameof(RelicCmd.Obtain), typeof(RelicModel), typeof(Player), typeof(int))]
	[HextechPatch("enemy-hex.tezcataras-mercy", "敌方海克斯:特斯卡塔拉的仁慈")]
	private static class ObtainPatch
	{
		[HarmonyPrefix]
		private static void Prefix(RelicModel relic, Player player)
		{
			if (TezcatarasMercyEnemyHex.ShouldConvertRelic(player, relic))
			{
				relic.IsWax = true;
			}
		}
	}
}
