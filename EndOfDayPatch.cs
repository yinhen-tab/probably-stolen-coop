using HarmonyLib;

namespace ProbablyStolenCoop;

/// <summary>夜结算按钮(观察用:看"结束一天"操作链)</summary>
[HarmonyPatch(typeof(EndOfDayUIManager), "OnEndDayButtonClick")]
public static class EndOfDayPatch
{
    public static void Postfix()
    {
        Plugin.Log.LogInfo("[Coop] EndOfDay 按钮被点击(夜结算)");
    }
}
