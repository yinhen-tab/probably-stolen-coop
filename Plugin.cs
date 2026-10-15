using System;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace ProbablyStolenCoop;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    internal static new ManualLogSource Log;

    public override void Load()
    {
        Log = base.Log;
        Log.LogInfo($"{MyPluginInfo.PLUGIN_NAME} v{MyPluginInfo.PLUGIN_VERSION} 已加载(诊断版)");
        CoopClient.LoadConfig(Config);

        var harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        harmony.PatchAll();

        try
        {
            int n = 0;
            foreach (var m in harmony.GetPatchedMethods())
            {
                n++;
                Log.LogInfo($"[Coop] 已挂补丁 {n}: {m.FullDescription()}");
            }
        }
        catch (Exception e)
        {
            Log.LogWarning($"[Coop] 打印补丁列表失败: {e.Message}");
        }

        Log.LogInfo("Harmony 补丁已应用");
    }
}
