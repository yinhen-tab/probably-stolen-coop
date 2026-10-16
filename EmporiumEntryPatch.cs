using HarmonyLib;

namespace ProbablyStolenCoop;

/// <summary>游戏场景探针:EmporiumEntry 是店铺场景入口(每次进游戏必触发)</summary>
[HarmonyPatch(typeof(EmporiumEntry), "Awake")]
public static class EmporiumAwakePatch
{
    public static void Postfix()
    {
        CoopLog.Info("[Coop] EmporiumEntry.Awake 触发(游戏场景加载中)");
        CoopClient.DumpWorldState("Emporium.Awake");
        CoopUI.HideAll();   // 进游戏后隐藏联机入口按钮
    }
}

[HarmonyPatch(typeof(EmporiumEntry), "Start")]
public static class EmporiumStartPatch
{
    public static void Postfix(EmporiumEntry __instance)
    {
        CoopLog.Info("[Coop] EmporiumEntry.Start 触发(游戏场景已就绪)");
        CoopClient.DumpWorldState("Emporium.Start");
        CoopClient.OnEnteredGame(__instance);
    }
}
