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
        CoopClient.LoadConfig(Config);   // 先读配置(含日志等级),后续日志才受等级控制

        CoopLog.Info($"[Coop] {MyPluginInfo.PLUGIN_NAME} v{MyPluginInfo.PLUGIN_VERSION} 已加载 (日志={CoopLog.Level})");

        var harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        harmony.PatchAll();

        int patched = 0;
        try
        {
            foreach (var m in harmony.GetPatchedMethods())
            {
                patched++;
                CoopLog.Detail($"[Coop] 已挂补丁 {patched}: {m.FullDescription()}");   // 仅详细模式逐条列出
            }
        }
        catch (Exception e)
        {
            CoopLog.Warn($"[Coop] 统计补丁列表失败: {e.Message}");
        }
        CoopLog.Info($"[Coop] Harmony 补丁已应用: {patched} 个");
    }
}
