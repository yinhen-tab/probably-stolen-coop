using HarmonyLib;

namespace ProbablyStolenCoop;

/// <summary>
/// 游戏事件同步:叫号 / 开门 / 结束一天
/// 本地操作 → 上报队友;队友操作 → 本地执行(ApplyingRemote 防回环)
/// </summary>
public static class GameEventSync
{
    public static bool ApplyingRemote;
    public const string KIND_CALL_NEXT = "call_next";
    public const string KIND_OPEN_SHUTTER = "open_shutter";
    public const string KIND_CLOSE_SHUTTER = "close_shutter";
    public const string KIND_END_DAY = "end_day";

    public static void Report(string kind)
    {
        if (ApplyingRemote) return;
        CoopClient.SendGameEvent(kind);
    }
}

/// <summary>呼叫下一位顾客</summary>
[HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.TryCallNextClient))]
public static class CallNextClientPatch
{
    public static void Postfix() { GameEventSync.Report(GameEventSync.KIND_CALL_NEXT); }
}

/// <summary>开门营业(UI 层,内部会连带逻辑层)</summary>
[HarmonyPatch(typeof(StoreShutterButton), nameof(StoreShutterButton.OpenShutter))]
public static class StoreOpenShutterPatch
{
    public static void Postfix() { GameEventSync.Report(GameEventSync.KIND_OPEN_SHUTTER); }
}

/// <summary>关门打烊(UI 层)</summary>
[HarmonyPatch(typeof(StoreShutterButton), nameof(StoreShutterButton.CloseShutter))]
public static class StoreCloseShutterPatch
{
    public static void Postfix() { GameEventSync.Report(GameEventSync.KIND_CLOSE_SHUTTER); }
}

/// <summary>结束一天(夜结算)</summary>
[HarmonyPatch(typeof(EndOfDayUIManager), nameof(EndOfDayUIManager.OnEndDayButtonClick))]
public static class EndDaySyncPatch
{
    public static void Postfix() { GameEventSync.Report(GameEventSync.KIND_END_DAY); }
}
