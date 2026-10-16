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
                ApplyRemoteContainer(msg, msg.Kind == "container_open");
                return;
            }
            if (msg.Kind == "container_move")
            {
                // 队友拖动了容器窗口 → 本地把同一个窗口挪过去
                var w = _remoteWindow != null ? _remoteWindow : FindWindowByTitle(_remoteOpenTitle);
                if (w != null)
                {
                    SetWindowPos(w, msg.Wx, msg.Wy);
                    CoopLog.Info($"[Coop] 已同步容器窗口位置: ({msg.Wx:0},{msg.Wy:0})");
                }
                else CoopLog.Warn("[Coop] 容器窗口位置同步失败: 本端还不持有该窗口引用");
                return;
            }
            if (msg.Kind == "wnd_open" || msg.Kind == "wnd_close")
            {
                UiWindowSync.Apply(msg);
                return;
            }
            if (msg.Kind == "event_on" || msg.Kind == "event_off")
            {
                StoreEventSync.Apply(msg);
                return;
            }
            if (msg.Kind == "machine_start" || msg.Kind == "machine_done")
            {
                MachineSync.Apply(msg);
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
    private static PixelWindow _openWindow;      // 本地当前打开的容器窗口(用于同步位置)
    private static PixelWindow _remoteWindow;    // 镜像过来的远端容器窗口(位置同步用)
    private static float _lastWinX, _lastWinY, _winCheckedAt;

    /// <summary>按标题找窗口(共用实现;PixelWindow 不是 UnityEngine.Object,不能用 FindObjectsOfTypeAll)</summary>
    static PixelWindow FindWindowByTitle(string title) => CoopClient.FindWindowByTitle(title);

    static Vector2 WindowPos(PixelWindow w)
    {
        try
        {
            var rt = w != null ? w.rectTransform : null;
            if (rt != null) return rt.anchoredPosition;
        }
        catch { }
        return Vector2.zero;
    }

    static void SetWindowPos(PixelWindow w, float x, float y)
    {
        try
        {
            var rt = w != null ? w.rectTransform : null;
            if (rt != null) rt.anchoredPosition = new Vector2(x, y);
        }
        catch { }
    }

    /// <summary>心跳:本地容器窗口被拖动 → 上报位置(1 秒最多检查一次)</summary>
    public static void TickWindow()
    {
        try
        {
            if (ApplyingContainer) return;
            if (_openWindow == null) return;
            if (Time.time - _winCheckedAt < 0.5f) return;
            _winCheckedAt = Time.time;
            var p = WindowPos(_openWindow);
            if (Mathf.Abs(p.x - _lastWinX) < 1f && Mathf.Abs(p.y - _lastWinY) < 1f) return;
            _lastWinX = p.x; _lastWinY = p.y;
            if (CoopClient.Net == null || !CoopClient.Handshaked || string.IsNullOrEmpty(CoopClient.RoomCode)) return;
            CoopClient.Net.Send(MsgType.ObjectUse, new Coop.ObjectUse
            {
                PlayerId = CoopClient.PlayerId,
                Kind = "container_move",
                ItemId = _openItemId,
                Wx = p.x,
                Wy = p.y,
            });
            CoopLog.Detail($"[Coop] 本地上报容器窗口位置: ({p.x:0},{p.y:0})");
        }
        catch { }
    }

    /// <summary>本地打开/关闭了某个物品的容器 → 上报</summary>
    public static void ReportContainer(bool open, string itemId, string title, PixelWindow win = null)
    {
        try
        {
            if (ApplyingContainer) return;
            if (CoopClient.Net == null || !CoopClient.Handshaked) return;
            if (string.IsNullOrEmpty(CoopClient.RoomCode)) return;
            if (string.IsNullOrEmpty(itemId)) return;
            if (open)
            {
                _openItemId = itemId;
                _openItemTitle = title ?? "";
                _openWindow = win;                                  // 优先用物品自己的内容窗引用
                if (_openWindow == null) _openWindow = FindWindowByTitle(_openItemTitle);
                var wp = WindowPos(_openWindow);
                _lastWinX = wp.x; _lastWinY = wp.y;
                if (_openWindow == null)
                {
                    // 窗口可能刚创建还没挂上 → 稍后补挂;补挂后 TickWindow 会发 container_move 纠正位置
                    var it = itemId;
                    var tt = title;
                    CoopClient.RunOnMainAfter(0.25f, () => AttachLate(it, tt));
                }
            }
            else if (itemId != _openItemId) return;
            else { _openItemId = ""; _openItemTitle = ""; _openWindow = null; }

            CoopClient.Net.Send(MsgType.ObjectUse, new Coop.ObjectUse
            {
                PlayerId = CoopClient.PlayerId,
                Kind = open ? "container_open" : "container_close",
                ItemId = itemId,
                Wx = open ? _lastWinX : 0f,
                Wy = open ? _lastWinY : 0f,
            });
            CoopLog.Info($"[Coop] 本地{(open ? "打开" : "关闭")}容器: {title} ({itemId})"
                         + (open ? $" 窗口({_lastWinX:0},{_lastWinY:0})" : ""));
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop] 容器上报异常: {e.Message}"); }
    }

    /// <summary>延后补挂本地容器窗口引用(双击后窗口可能还没就绪)</summary>
    static void AttachLate(string itemId, string title)
    {
        try
        {
            if (_openItemId != itemId) return;          // 已经关了/换了,不补
            if (_openWindow != null) return;
            var ps = PlayerStore.Instance;
            if (ps == null) return;
            var all = ps.FindAllItem(true);
            if (all != null)
                for (int i = 0; i < all.Count; i++)
                {
                    var it = all[i];
                    if (it == null || it.identifier != itemId) continue;
                    var w = it.contentWindow;
                    if (w != null)
                    {
                        WindowSync.Register(w);
                        _openWindow = w;
                        var p = WindowPos(w);
                        _lastWinX = p.x; _lastWinY = p.y;
                        CoopLog.Info($"[Coop] 已补挂容器窗口: {title} ({p.x:0},{p.y:0})");
                    }
                    break;
                }
        }
        catch { }
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
            _openItemId = ""; _openItemTitle = ""; _openWindow = null;
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

    /// <summary>队友打开的容器窗口在本端的标题(用于后续位置同步)</summary>
    private static string _remoteOpenTitle = "";

    /// <summary>接收方:打开/关闭本地同 ID 物品的容器(内容由本端自己那份物品提供,天然一致)</summary>
    static void ApplyRemoteContainer(Coop.ObjectUse msg, bool open)
    {
        string itemId = msg.ItemId;
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
                // 用游戏自己的静态实例(比 FindObjectsOfType 可靠;那要求类型是 UnityEngine.Object)
                var h = ItemMouseDoubleClickHandler.current;
                if (h == null)
                {
                    var hs = UnityEngine.Object.FindObjectsOfType<ItemMouseDoubleClickHandler>();
                    if (hs != null && hs.Length > 0) h = hs[0];
                }
                if (h == null)
                {
                    CoopLog.Warn("[Coop] 未找到容器打开处理器,无法镜像容器");
                    return;
                }
                h.OpenContentAction(item, Vector2.zero);
                _remoteOpenTitle = title;
                // 关键:物品自己持有内容窗引用(item.contentWindow) → 不必按标题猜窗口
                PlaceRemoteWindow(item, msg.Wx, msg.Wy, 0);
                CoopLog.Info($"[Coop] 已打开队友的容器: {title} → 目标位置({msg.Wx:0},{msg.Wy:0})");
            }
            else
            {
                // 关它自己的内容窗(游戏原生方法),不依赖任何窗口查找
                bool ok = false;
                try { item.CloseContentWindow(false); ok = true; }
                catch (Exception e) { CoopLog.Warn($"[Coop] 关闭容器失败: {e.Message}"); }
                if (!ok)
                {
                    var w2 = _remoteWindow != null ? _remoteWindow : FindWindowByTitle(title);
                    if (w2 != null) { w2.Hide(); ok = true; }
                }
                CoopLog.Info($"[Coop] 已关闭队友的容器: {title}{(ok ? "" : " (没找到窗口)")}");
                _remoteWindow = null; _remoteOpenTitle = "";
            }
        }
        finally { ApplyingContainer = false; WindowSync.ApplyingRemote = prevWin; }
    }

    /// <summary>
    /// 把远端容器的窗口摆到队友那边相同的位置。
    /// item.contentWindow 在 OpenContentAction 之后可能还没就绪 → 每 0.15s 重试,最多 8 次。
    /// </summary>
    static void PlaceRemoteWindow(GameItem item, float wx, float wy, int attempt)
    {
        try
        {
            var w = item != null ? item.contentWindow : null;
            if (w == null)
            {
                if (attempt < 8)
                    CoopClient.RunOnMainAfter(0.15f, () => PlaceRemoteWindow(item, wx, wy, attempt + 1));
                else
                    CoopLog.Warn("[Coop] 容器窗口引用一直拿不到(item.contentWindow 为空),位置无法对齐");
                return;
            }
            WindowSync.Register(w);
            _remoteWindow = w;
            if (wx != 0f || wy != 0f) SetWindowPos(w, wx, wy);
        }
        catch { }
    }

    /// <summary>
    /// 心跳:高亮圈淡出(圈已取消,保留调用以兼容)
    /// </summary>
    public static void Tick()
    {
        try { PeerMarker.Tick(_markUntil); }
        catch { }
    }
}

// ================== 容器开关:本地双击物品打开内容窗 ==================
// ⚠️ 这里曾经是整个功能不生效的根因:ReportContainer() 写好了却**没有任何地方调用它**,
//    所以"我打开了容器"从来没发给过对方 → 对方既不弹窗口,也看不到里面的物品动。
//    现在挂到游戏真正的入口 ItemMouseDoubleClickHandler.OpenContentAction 上。

[HarmonyPatch(typeof(ItemMouseDoubleClickHandler), nameof(ItemMouseDoubleClickHandler.OpenContentAction))]
public static class ContainerOpenPatch
{
    public static void Postfix(GameItem newItem)
    {
        try
        {
            if (newItem == null) return;
            string id = newItem.identifier ?? "";
            string title = "";
            try { title = newItem.GetDisplayName() ?? ""; } catch { }
            if (string.IsNullOrEmpty(title)) title = id;
            PixelWindow win = null;
            try { win = newItem.contentWindow; if (win != null) WindowSync.Register(win); } catch { }
            ObjectUseSync.ReportContainer(true, id, title, win);
        }
        catch (Exception e) { CoopLog.Warn($"[Coop] 容器打开上报异常: {e.Message}"); }
    }
}

[HarmonyPatch(typeof(ItemMouseDoubleClickHandler), nameof(ItemMouseDoubleClickHandler.OpenExamineAction))]
public static class ContainerExaminePatch
{
    public static void Postfix(GameItem newItem)
    {
        try
        {
            if (newItem == null) return;
            string id = newItem.identifier ?? "";
            if (string.IsNullOrEmpty(id)) return;
            string title = "";
            try { title = newItem.GetDisplayName() ?? ""; } catch { }
            CoopLog.Detail($"[Coop] 本地查看物品(不上报): {title} ({id})");
        }
        catch { }
    }
}
