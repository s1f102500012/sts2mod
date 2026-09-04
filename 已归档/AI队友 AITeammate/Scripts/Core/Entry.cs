using Godot.Bridge;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace AITeammate.Scripts;

[ModInitializer("Init")]
public class Entry
{
    private const string ModId = "sts2.aiteammate";

    public static void Init()
    {
        Logger.SetLogLevelForType(LogType.Generic, LogLevel.Debug);

        var harmony = new Harmony(ModId);
        harmony.PatchAll();

        ScriptManagerBridge.LookupScriptsInAssembly(typeof(Entry).Assembly);

        Log.Info("[AITeammate] Generic log level set to Debug.");
        Log.Info("[AITeammate] Init reached.");
        Log.Debug("[AITeammate] Debug log reached.");
    }
}
