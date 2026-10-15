using System;
using System.Collections.Generic;
using HarmonyLib;
using Coop;
using UnityEngine;

namespace ProbablyStolenCoop;

/// <summary>
/// P0 店内物件交互可见性:
/// 店内 IClickable 物件(录音机/售货机/饮水机/电话/海报/灯/展示柜/信用屏/铃铛)被点击时
/// 广播 {kind + 屏幕位置},对方屏幕在该物件上画高亮圈 + 提示「队友打开了录音机」。
/// 覆盖了不产生 PixelWindow 事件的交互(录音机面板是普通 GameObject,磁带机是纯逻辑)。
/// </summary>
public static class ObjectUseSync
{
    /// <summary>类型名 → 中文名(提示用)</summary>
    static readonly Dictionary<string, string> Names = new()
    {
        { "recorder", "录音机" },
        { "vending_machine", "售货机" },
        { "vending_fountain", "饮水机" },
        { "phone", "电话" },
        { "poster", "海报" },
        { "lamp", "灯" },
        { "clientele_display", "展示柜" },
        { "credit_display", "信用显示屏" },
        { "bell", "铃铛" },
    };

    static readonly Dictionary<string, float> _markUntil = new();   // kind → 淡出截止

    public static void Report(string kind, UnityEngine.Object target)
    {
        try
        {
            if (CoopClient.Net == null || !CoopClient.Handshaked) return;
            if (string.IsNullOrEmpty(CoopClient.RoomCode)) return;
            float nx = 0.5f, ny = 0.5f;
            try
            {
                if (target is Component comp && comp != null)
                {
                    var sp = comp.transform.position;
                    var cam = Camera.main;
                    if (cam != null)
                    {
                        var vp = cam.WorldToViewportPoint(sp);
                        nx = vp.x; ny = vp.y;
                    }
                }
            }
            catch { }
            CoopClient.Net.Send(MsgType.ObjectUse, new Coop.ObjectUse
            {
                PlayerId = CoopClient.PlayerId,
                Kind = kind,
                Nx = nx,
                Ny = ny,
            });
            string cn = Names.TryGetValue(kind, out var c) ? c : kind;
            CoopLog.Info($"[Coop] 本地使用物件: {cn}");
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop] 物件上报异常: {e.Message}"); }
    }

    /// <summary>接收方:顶部提示队友使用了什么物件(高亮圈按用户要求已取消)</summary>
    public static void ApplyRemoteUse(Coop.ObjectUse msg)
    {
        try
        {
            string cn = Names.TryGetValue(msg.Kind, out var c) ? c : msg.Kind;
            CoopLog.Detail($"[Coop] 队友使用了: {cn}");
            CoopHud.Toast($"队友打开了{cn}");
            // 物件高亮圈已取消显示(仅保留顶部提示);如需恢复,取消下一行注释即可
            // PeerMarker.Show(msg.Kind, msg.Nx, msg.Ny);
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop] 物件同步异常: {e.Message}"); }
    }

    /// <summary>心跳:高亮圈淡出(圈已取消,保留调用以兼容)</summary>
    public static void Tick()
    {
        try { PeerMarker.Tick(_markUntil); }
        catch { }
    }
}

// ---- 各类店内物件的 OnClick 钩子 ----

[HarmonyPatch(typeof(StoreRecorder), nameof(StoreRecorder.OnClick))]
public static class UseRecorderPatch
{
    public static void Postfix(StoreRecorder __instance) { ObjectUseSync.Report("recorder", __instance); }
}

[HarmonyPatch(typeof(StoreVendingMachine), nameof(StoreVendingMachine.OnClick))]
public static class UseVendingMachinePatch
{
    public static void Postfix(StoreVendingMachine __instance) { ObjectUseSync.Report("vending_machine", __instance); }
}

[HarmonyPatch(typeof(StoreVendingFountain), nameof(StoreVendingFountain.OnClick))]
public static class UseVendingFountainPatch
{
    public static void Postfix(StoreVendingFountain __instance) { ObjectUseSync.Report("vending_fountain", __instance); }
}

[HarmonyPatch(typeof(StorePhoneObject), nameof(StorePhoneObject.OnClick))]
public static class UsePhonePatch
{
    public static void Postfix(StorePhoneObject __instance) { ObjectUseSync.Report("phone", __instance); }
}

[HarmonyPatch(typeof(StorePoster), nameof(StorePoster.OnClick))]
public static class UsePosterPatch
{
    public static void Postfix(StorePoster __instance) { ObjectUseSync.Report("poster", __instance); }
}

[HarmonyPatch(typeof(StoreLamp), nameof(StoreLamp.OnClick))]
public static class UseLampPatch
{
    public static void Postfix(StoreLamp __instance) { ObjectUseSync.Report("lamp", __instance); }
}

[HarmonyPatch(typeof(StoreClienteleDisplay), nameof(StoreClienteleDisplay.OnClick))]
public static class UseClienteleDisplayPatch
{
    public static void Postfix(StoreClienteleDisplay __instance) { ObjectUseSync.Report("clientele_display", __instance); }
}

[HarmonyPatch(typeof(StoreCreditDisplay), nameof(StoreCreditDisplay.OnClick))]
public static class UseCreditDisplayPatch
{
    public static void Postfix(StoreCreditDisplay __instance) { ObjectUseSync.Report("credit_display", __instance); }
}

[HarmonyPatch(typeof(StoreDebugBell), nameof(StoreDebugBell.OnClick))]
public static class UseDebugBellPatch
{
    public static void Postfix(StoreDebugBell __instance) { ObjectUseSync.Report("bell", __instance); }
}