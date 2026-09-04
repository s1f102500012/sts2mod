using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Audio;

namespace AITeammate.Scripts;

internal static class AiTeammateSimulationPresentationGuard
{
    public static bool ShouldRunPresentation()
    {
        return !AiTeammateSimulationRuntime.IsPresentationSuppressed;
    }
}

[HarmonyPatch(typeof(SfxCmd), nameof(SfxCmd.Play), new[] { typeof(string), typeof(float) })]
internal static class AiTeammateSfxCmdPlayPatch
{
    private static bool Prefix() => AiTeammateSimulationPresentationGuard.ShouldRunPresentation();
}

[HarmonyPatch(typeof(SfxCmd), nameof(SfxCmd.Play), new[] { typeof(string), typeof(string), typeof(float), typeof(float) })]
internal static class AiTeammateSfxCmdPlayParamPatch
{
    private static bool Prefix() => AiTeammateSimulationPresentationGuard.ShouldRunPresentation();
}

[HarmonyPatch(typeof(SfxCmd), nameof(SfxCmd.PlayDamage))]
internal static class AiTeammateSfxCmdPlayDamagePatch
{
    private static bool Prefix() => AiTeammateSimulationPresentationGuard.ShouldRunPresentation();
}

[HarmonyPatch(typeof(SfxCmd), nameof(SfxCmd.PlayDeath), new[] { typeof(MonsterModel) })]
internal static class AiTeammateSfxCmdPlayMonsterDeathPatch
{
    private static bool Prefix() => AiTeammateSimulationPresentationGuard.ShouldRunPresentation();
}

[HarmonyPatch(typeof(SfxCmd), nameof(SfxCmd.PlayDeath), new[] { typeof(Player) })]
internal static class AiTeammateSfxCmdPlayPlayerDeathPatch
{
    private static bool Prefix() => AiTeammateSimulationPresentationGuard.ShouldRunPresentation();
}

[HarmonyPatch(typeof(SfxCmd), nameof(SfxCmd.PlayLoop))]
internal static class AiTeammateSfxCmdPlayLoopPatch
{
    private static bool Prefix() => AiTeammateSimulationPresentationGuard.ShouldRunPresentation();
}

[HarmonyPatch(typeof(SfxCmd), nameof(SfxCmd.SetParam))]
internal static class AiTeammateSfxCmdSetParamPatch
{
    private static bool Prefix() => AiTeammateSimulationPresentationGuard.ShouldRunPresentation();
}

[HarmonyPatch(typeof(SfxCmd), nameof(SfxCmd.StopLoop))]
internal static class AiTeammateSfxCmdStopLoopPatch
{
    private static bool Prefix() => AiTeammateSimulationPresentationGuard.ShouldRunPresentation();
}

[HarmonyPatch(typeof(NAudioManager), nameof(NAudioManager.PlayOneShot), new[] { typeof(string), typeof(float) })]
internal static class AiTeammateAudioManagerPlayOneShotPatch
{
    private static bool Prefix() => AiTeammateSimulationPresentationGuard.ShouldRunPresentation();
}

[HarmonyPatch(typeof(NAudioManager), nameof(NAudioManager.PlayOneShot), new[] { typeof(string), typeof(Dictionary<string, float>), typeof(float) })]
internal static class AiTeammateAudioManagerPlayOneShotParamPatch
{
    private static bool Prefix() => AiTeammateSimulationPresentationGuard.ShouldRunPresentation();
}

[HarmonyPatch(typeof(NAudioManager), nameof(NAudioManager.PlayLoop))]
internal static class AiTeammateAudioManagerPlayLoopPatch
{
    private static bool Prefix() => AiTeammateSimulationPresentationGuard.ShouldRunPresentation();
}

[HarmonyPatch(typeof(VfxCmd), nameof(VfxCmd.PlayFullScreenInCombat))]
internal static class AiTeammateVfxCmdPlayFullScreenPatch
{
    private static bool Prefix() => AiTeammateSimulationPresentationGuard.ShouldRunPresentation();
}

[HarmonyPatch(typeof(VfxCmd), nameof(VfxCmd.PlayNonCombatVfx))]
internal static class AiTeammateVfxCmdPlayNonCombatPatch
{
    private static bool Prefix(ref Node2D? __result)
    {
        if (AiTeammateSimulationPresentationGuard.ShouldRunPresentation())
        {
            return true;
        }

        __result = null;
        return false;
    }
}

[HarmonyPatch(typeof(VfxCmd), nameof(VfxCmd.PlayOnCreature))]
internal static class AiTeammateVfxCmdPlayOnCreaturePatch
{
    private static bool Prefix() => AiTeammateSimulationPresentationGuard.ShouldRunPresentation();
}

[HarmonyPatch(typeof(VfxCmd), nameof(VfxCmd.PlayOnCreatureCenter))]
internal static class AiTeammateVfxCmdPlayOnCreatureCenterPatch
{
    private static bool Prefix() => AiTeammateSimulationPresentationGuard.ShouldRunPresentation();
}

[HarmonyPatch(typeof(VfxCmd), nameof(VfxCmd.PlayOnCreatureCenters))]
internal static class AiTeammateVfxCmdPlayOnCreatureCentersPatch
{
    private static bool Prefix() => AiTeammateSimulationPresentationGuard.ShouldRunPresentation();
}

[HarmonyPatch(typeof(VfxCmd), nameof(VfxCmd.PlayOnCreatures))]
internal static class AiTeammateVfxCmdPlayOnCreaturesPatch
{
    private static bool Prefix() => AiTeammateSimulationPresentationGuard.ShouldRunPresentation();
}

[HarmonyPatch(typeof(VfxCmd), nameof(VfxCmd.PlayOnSide))]
internal static class AiTeammateVfxCmdPlayOnSidePatch
{
    private static bool Prefix(CombatSide side, CombatState combatState)
    {
        return AiTeammateSimulationPresentationGuard.ShouldRunPresentation();
    }
}

[HarmonyPatch(typeof(VfxCmd), nameof(VfxCmd.PlayVfx))]
internal static class AiTeammateVfxCmdPlayVfxPatch
{
    private static bool Prefix() => AiTeammateSimulationPresentationGuard.ShouldRunPresentation();
}
