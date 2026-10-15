using System;
using HarmonyLib;

namespace ProbablyStolenCoop;

/// <summary>
/// 电话同步(A 界面镜像 + B 通话可见):
/// - A: 拨号盘(PhoneUIManager.OpenUI/CloseUI)与电话簿(PhoneBookUIManager)的开关镜像,
///      对方打开/关闭电话界面时本地同步弹出/收起同一界面。
/// - B: 拨打(HandleCall 号码)/挂断(StopCall)镜像 —— 号码驱动,
///      两端各自用同一份游戏数据播放同一段通话,天然一致(与顾客队列同思路)。
/// 防回环:ApplyingRemote 期间不上报。
/// </summary>
public static class PhoneSync
{
    public static bool ApplyingRemote;

    /// <summary>本地电话操作上报</summary>
    public static void Report(string kind, long number = 0)
    {
        try
        {
            if (ApplyingRemote) return;
            if (CoopClient.Net == null || !CoopClient.Handshaked) return;
            if (string.IsNullOrEmpty(CoopClient.RoomCode)) return;
            CoopClient.SendPhoneSync(kind, number);
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop] 电话上报异常: {e.Message}"); }
    }

    /// <summary>接收方:镜像队友的电话操作</summary>
    public static void ApplyRemote(Coop.PhoneSync m)
    {
        try
        {
            var ui = PhoneUIManager.Instance;
            if (ui == null) return;
            ApplyingRemote = true;
            try
            {
                switch (m.Kind)
                {
                    case "open":
                        ui.OpenUI();
                        Plugin.Log.LogInfo("[Coop] 队友打开了电话");
                        break;
                    case "close":
                        ui.CloseUI();
                        break;
                    case "book_open":
                        if (ui.phoneBookUIManager != null) ui.phoneBookUIManager.OpenUI();
                        break;
                    case "book_close":
                        if (ui.phoneBookUIManager != null) ui.phoneBookUIManager.CloseUI();
                        break;
                    case "call":
                        Plugin.Log.LogInfo($"[Coop] 队友正在拨打 {m.Number}");
                        ui.HandleCall(m.Number);
                        break;
                    case "hangup":
                        ui.StopCall();
                        break;
                }
            }
            finally { ApplyingRemote = false; }
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop] 电话同步执行异常: {e.Message}"); }
    }
}

// ---- 钩子:拨号盘开关 ----

[HarmonyPatch(typeof(PhoneUIManager), nameof(PhoneUIManager.OpenUI))]
public static class PhoneOpenPatch
{
    public static void Postfix() { PhoneSync.Report("open"); }
}

[HarmonyPatch(typeof(PhoneUIManager), nameof(PhoneUIManager.CloseUI))]
public static class PhoneClosePatch
{
    public static void Postfix() { PhoneSync.Report("close"); }
}

// ---- 钩子:电话簿开关 ----

[HarmonyPatch(typeof(PhoneBookUIManager), nameof(PhoneBookUIManager.OpenUI))]
public static class PhoneBookOpenPatch
{
    public static void Postfix() { PhoneSync.Report("book_open"); }
}

[HarmonyPatch(typeof(PhoneBookUIManager), nameof(PhoneBookUIManager.CloseUI))]
public static class PhoneBookClosePatch
{
    public static void Postfix() { PhoneSync.Report("book_close"); }
}

// ---- 钩子:拨打/挂断 ----

[HarmonyPatch(typeof(PhoneUIManager), nameof(PhoneUIManager.HandleCall))]
public static class PhoneCallPatch
{
    public static void Postfix(long currentNumber) { PhoneSync.Report("call", currentNumber); }
}

[HarmonyPatch(typeof(PhoneUIManager), nameof(PhoneUIManager.StopCall))]
public static class PhoneStopCallPatch
{
    public static void Postfix() { PhoneSync.Report("hangup"); }
}
