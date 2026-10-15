using System;
using HarmonyLib;

namespace ProbablyStolenCoop;

/// <summary>主菜单 Awake:挂"多人联机"入口按钮(不自动连接;点击面板后才连服务器)</summary>
[HarmonyPatch(typeof(MainMenuUIController), "Awake")]
public static class MainMenuAwakePatch
{
    public static void Postfix(MainMenuUIController __instance)
    {
        Plugin.Log.LogInfo("[Coop] 主菜单已就绪(单机零打扰:点击\"多人联机\"后才连接服务器)");
        SaveGuard.TryRestore();   // 存档保护:上次联机若未还原(含崩溃/被杀进程),在这里兜底还原
        CoopClient.AutoConnectIfConfigured();   // 测试:配置了自动加入码则立刻连接
        CoopUI.AttachToMainMenu(__instance);    // 克隆原生样式按钮,插入主菜单按钮列
        CoopUI.StartMainMenuTick(__instance);   // 主菜单阶段:面板刷新 + 点击特效

        if (CoopClient.AutoTest)
        {
            Plugin.Log.LogInfo("[Coop][测试] 启用自动开局流程");
            CoopClient.TryConnect();
            CoopClient.StartAutoBoot(__instance);
            return;
        }

        Plugin.Log.LogInfo("[Coop] 正式模式:通过\"多人联机\"面板创建/加入房间后选择存档");
    }
}
