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

    /// <summary>接收方:顶部提示队友使用了什么物件 + 容器类操作真正打开/关闭同一物品的容器</summary>
    public static void ApplyRemoteUse(Coop.ObjectUse msg)
    {
        try
        {
            if (msg.Kind == "container_open" || msg.Kind == "container_close")
            {
                CoopLog.Detail($"[Coop] 收到容器事件: {(msg.Kind == "container_open" ? "打开" : "关闭")} [{msg.ItemId}]");
                ApplyRemoteContainer(msg.ItemId, msg.Kind == "container_open");
                return;
            }

            string cn = Names.TryGetValue(msg.Kind, out var c) ? c : msg.Kind;
            CoopLog.Detail($"[Coop] 队友使用了: {cn}");
            CoopHud.Toast($"队友打开了{cn}");
            // 物件高亮圈已取消显示(仅保留顶部提示);如需恢复,取消下一行注释即可
            // PeerMarker.Show(msg.Kind, msg.Nx, msg.Ny);
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop] 物件同步异常: {e.Message}"); }
    }

    // ================= 容器(箱子/背包等)同步:按"物品"同步,保证内容一致 =================

    /// <summary>正在执行远端容器操作(防回环上报)</summary>
    public static bool ApplyingContainer;

    private static string _openItemId = "";      // 本地当前打开的容器物品(用于上报关闭)
    private static string _openItemTitle = "";

    /// <summary>本地打开/关闭了某个物品的容器 → 上报</summary>
    public static void ReportContainer(bool open, string itemId, string title)
    {
        try
        {
            if (ApplyingContainer) return;
            if (CoopClient.Net == null || !CoopClient.Handshaked) return;
            if (string.IsNullOrEmpty(CoopClient.RoomCode)) return;
            if (string.IsNullOrEmpty(itemId)) return;
            if (open) { _openItemId = itemId; _openItemTitle = title ?? ""; }
            else if (itemId != _openItemId) return;
            else { _openItemId = ""; _openItemTitle = ""; }

            CoopClient.Net.Send(MsgType.ObjectUse, new Coop.ObjectUse
            {
                PlayerId = CoopClient.PlayerId,
                Kind = open ? "container_open" : "container_close",
                ItemId = itemId,
            });
            CoopLog.Info($"[Coop] 本地{(open ? "打开" : "关闭")}容器: {title} ({itemId})");
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop] 容器上报异常: {e.Message}"); }
    }

    /// <summary>窗口被隐藏:若是本地打开的容器窗口,则上报关闭</summary>
    public static void OnWindowHidden(PixelWindow w)
    {
        try
        {
            if (ApplyingContainer) return;
            if (string.IsNullOrEmpty(_openItemId)) return;
            string title = "";
            try { title = (w?.titleString ?? "").Trim(); } catch { }
            if (string.IsNullOrEmpty(title) || title != _openItemTitle) return;
            string id = _openItemId;
            string t = _openItemTitle;
            _openItemId = ""; _openItemTitle = "";
            ReportContainerCloseOnly(id, t);
        }
        catch { }
    }

    static void ReportContainerCloseOnly(string itemId, string title)
    {
        try
        {
            if (CoopClient.Net == null || !CoopClient.Handshaked) return;
            if (string.IsNullOrEmpty(CoopClient.RoomCode)) return;
            CoopClient.Net.Send(MsgType.ObjectUse, new Coop.ObjectUse
            {
                PlayerId = CoopClient.PlayerId,
                Kind = "container_close",
                ItemId = itemId,
            });
            CoopLog.Info($"[Coop] 本地关闭容器: {title} ({itemId})");
        }
        catch { }
    }

    /// <summary>接收方:打开/关闭本地同 ID 物品的容器(内容由本端自己那份物品提供,天然一致)</summary>
    static void ApplyRemoteContainer(string itemId, bool open)
    {
        if (string.IsNullOrEmpty(itemId)) return;
        var ps = PlayerStore.Instance;
        if (ps == null) return;

        GameItem item = null;
        try
        {
            var all = ps.FindAllItem(true);
            if (all != null)
                for (int i = 0; i < all.Count; i++)
                    if (all[i] != null && all[i].identifier == itemId) { item = all[i]; break; }
        }
        catch { }
        if (item == null)
        {
            // 诊断:把本地实际拥有的物品 id 打出来(排查"找不到"的原因)
            try
            {
                var all = ps.FindAllItem(true);
                var sb = new System.Text.StringBuilder();
                int n = 0;
                if (all != null)
                    for (int i = 0; i < all.Count && n < 24; i++)
                        if (all[i] != null) { sb.Append(all[i].identifier).Append(','); n++; }
                CoopLog.Detail($"[Coop] 容器同步: 本地没有物品 {itemId}(拥有物品 {all?.Count ?? -1} 个: {sb})");
            }
            catch { CoopLog.Detail($"[Coop] 容器同步: 本地没有物品 {itemId}"); }
            return;
        }

        string title = "";
        try { title = item.GetDisplayName() ?? ""; } catch { }

        bool prevWin = WindowSync.ApplyingRemote;
        ApplyingContainer = true;
        WindowSync.ApplyingRemote = true;   // 抑制窗口同步回环
        try
        {
            if (open)
            {
                var hs = UnityEngine.Object.FindObjectsOfType<ItemMouseDoubleClickHandler>();
                if (hs != null && hs.Length > 0)
                {
                    hs[0].OpenContentAction(item, Vector2.zero);
                    CoopLog.Info($"[Coop] 已打开队友的容器: {title}");
                }
                else CoopLog.Warn("[Coop] 未找到容器打开处理器,无法镜像容器");
            }
            else
            {
                // 关闭:用当前已打开的窗口(标题匹配)
                try
                {
                    var wins = Resources.FindObjectsOfTypeAll(Il2CppInterop.Runtime.Il2CppType.Of<PixelWindow>());
                    if (wins != null)
                        for (int i = 0; i < wins.Length; i++)
                        {
                            var w = wins[i]?.TryCast<PixelWindow>();
                            if (w == null) continue;
                            string t = "";
                            try { t = (w.titleString ?? "").Trim(); } catch { }
                            if (!string.IsNullOrEmpty(t) && t == title) { w.Hide(); CoopLog.Info($"[Coop] 已关闭队友的容器: {title}"); break; }
                        }
                }
                catch { }
            }
        }
        finally { ApplyingContainer = false; WindowSync.ApplyingRemote = prevWin; }
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

// ---- 容器(箱子/背包等)同步:双击物品打开内容物的入口 ----

[HarmonyPatch(typeof(ItemMouseDoubleClickHandler), nameof(ItemMouseDoubleClickHandler.OpenContentAction))]
public static class OpenContentActionPatch
{
    public static void Postfix(GameItem newItem)
    {
        try
        {
            if (newItem == null) return;
            string id = newItem.identifier ?? "";
            string title = "";
            try { title = newItem.GetDisplayName() ?? ""; } catch { }
            ObjectUseSync.ReportContainer(true, id, title);
        }
        catch { }
    }
}

[HarmonyPatch(typeof(PixelWindow), nameof(PixelWindow.Hide))]
public static class ContainerWindowClosePatch
{
    public static void Postfix(PixelWindow __instance) { ObjectUseSync.OnWindowHidden(__instance); }
}