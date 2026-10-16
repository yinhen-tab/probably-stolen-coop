using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ProbablyStolenCoop;

/// <summary>
/// 拖拽状态同步(视觉):hook 物品节点的拖拽开始/结束 → 上报队友
/// 队友画面显示"拖动中"幽灵跟随其光标
/// </summary>
public static class DragSync
{
    /// <summary>最近在物品节点上按下的那一件(拖拽身份兜底:OnBeginDrag 的节点有时拿不到物品)</summary>
    private static GameItem _lastPressed;

    // 拖动上下文(供 TickSend 用)
    private static GameItem _dragItem;
    private static string _hint = "";
    private static int _uid;
    private static string _ident = "";
    private static float _lastPosAt;

    /// <summary>
    /// 拖动期间持续上报"被拖物品在哪"(归一化屏幕坐标,50ms 一次)。
    /// 对端只取相对起点的**增量**,所以不需要处理坐标原点/pivot/分辨率差异。
    /// </summary>
    private static int _sendCount;

    public static void TickSend()
    {
        if (!Dragging) return;
        if (Time.time - _lastPosAt < 0.05f) return;
        _lastPosAt = Time.time;
        float nx = 0f, ny = 0f;
        bool ok = false;
        Vector2 sp = Vector2.zero;
        string info = "";
        try
        {
            var ge = _dragItem != null ? _dragItem.TryCast<GameItemElement>() : null;
            var rt = ge != null ? ge.rectTransform : null;
            if (rt != null)
            {
                // ★ 实测:拖动期间主 rectTransform / inner / middle / rotator / background **恒定不动**,
                //    真正在移动的是 fakeInner 这组"拾起替身层"(游戏 UpdateFakeReal 用的那一组)。
                Transform vis = null;
                try { if (ge.fakeInner != null) vis = ge.fakeInner.transform; } catch { }
                if (vis == null) { try { if (ge.fakeMiddle != null) vis = ge.fakeMiddle.transform; } catch { } }
                if (vis == null) { try { if (ge.inner != null) vis = ge.inner.transform; } catch { } }
                sp = vis != null ? TransformToScreen(vis) : ToScreen(rt);
                nx = sp.x / Mathf.Max(1f, Screen.width);
                ny = sp.y / Mathf.Max(1f, Screen.height);
                ok = true;
                if (_sendCount < 3) info = (vis != null ? " [用假象层]" : " [用主rt]") + DescribeItem(ge, rt);
            }
        }
        catch { }
        if (!ok)
        {
            try
            {
                var m = Input.mousePosition;      // 兜底:用鼠标位置
                nx = m.x / Mathf.Max(1f, Screen.width);
                ny = m.y / Mathf.Max(1f, Screen.height);
                info = " [物品取不到,用鼠标位置兜底]";
            }
            catch { return; }
        }
        _sendCount++;
        // 诊断:确认拖动期间坐标到底动不动、动在哪个节点上
        if (_sendCount <= 3 || _sendCount % 10 == 0)
            CoopLog.Info($"[Coop][拖标定发] #{_sendCount} 屏幕=({sp.x:F0},{sp.y:F0}) n=({nx:F3},{ny:F3}){info}");
        CoopClient.SendDrag(true, _hint, _uid, _ident, nx, ny);
    }

    /// <summary>任意 Transform → 屏幕像素坐标(Overlay 下 position 即屏幕坐标)</summary>
    static Vector2 TransformToScreen(Transform tr)
    {
        Camera cam = null;
        try
        {
            var cv = tr.GetComponentInParent<Canvas>();
            if (cv != null && cv.renderMode != RenderMode.ScreenSpaceOverlay) cam = cv.worldCamera;
        }
        catch { }
        return RectTransformUtility.WorldToScreenPoint(cam, tr.position);
    }

    /// <summary>RectTransform → 屏幕像素坐标(Overlay 下 position 即屏幕坐标;相机模式走 WorldToScreenPoint)</summary>
    static Vector2 ToScreen(RectTransform rt)
    {
        Camera cam = null;
        try
        {
            var cv = rt.GetComponentInParent<Canvas>();
            if (cv != null && cv.renderMode != RenderMode.ScreenSpaceOverlay) cam = cv.worldCamera;
        }
        catch { }
        return RectTransformUtility.WorldToScreenPoint(cam, rt.position);
    }

    /// <summary>
    /// 诊断:物品的**每一个**节点现在在哪 —— 拖动时真正在移动的可能是 fakeInner 之类的"假象层",
    /// 而不是主 rectTransform。一次拖动就能看出该读哪个。
    /// </summary>
    static string DescribeItem(GameItemElement ge, RectTransform rt)
    {
        try
        {
            var cv = rt.GetComponentInParent<Canvas>();
            string s = $" 画布={Screen.width}x{Screen.height} mode={(cv != null ? cv.renderMode.ToString() : "无")}"
                     + $" scale={(cv != null ? cv.scaleFactor : 0f):F2}";
            s += " | 主rt=" + rt.position.ToString("F0") + "/" + rt.anchoredPosition.ToString("F0");
            s += NodeP(ge.handler, "handler");
            s += NodeP(ge.inner, "inner");
            s += NodeP(ge.middle, "middle");
            s += NodeP(ge.rotator, "rotator");
            s += NodeP(ge.background, "bg");
            s += NodeP(ge.fakeInner, "fakeIn");
            s += NodeP(ge.fakeMiddle, "fakeMid");
            s += NodeP(ge.fakeRotator, "fakeRot");
            s += NodeP(ge.fakeBackground, "fakeBg");
            s += " 鼠标=" + Input.mousePosition.ToString("F0");
            return s;
        }
        catch (Exception e) { return " [诊断异常:" + e.Message + "]"; }
    }

    static string NodeP(TreeNodeRender n, string nm)
    {
        try
        {
            if (n == null) return "";
            return $" {nm}={n.transform.position.ToString("F0")}";
        }
        catch { return ""; }
    }

    /// <summary>是否正在拖动(供 ItemMoveSync 切到高频采集)</summary>
    public static bool Dragging { get; private set; }

    public static void NotePressed(TreeNodeRender rend)
    {
        var it = FindDraggedItem(rend);
        if (it != null) _lastPressed = it;
    }

    /// <summary>
    /// 从触发的节点反查"拖的是哪一件"。
    /// ⚠️ `rend.element` 实测经常是 null(触发 IDragHandler 的往往是物品的某个子节点/容器节点),
    ///    所以三级兜底:① 自身/父链上的 element ② 反向比对物品的各个节点(handler/inner/...)
    ///    IL2CPP 的对象包装不能直接 ==,统一比 .Pointer。
    /// </summary>
    public static GameItem FindDraggedItem(TreeNodeRender rend)
    {
        if (rend == null) return null;
        // ① 自身 / 沿 parent 链找 element
        try
        {
            var cur = rend;
            int g = 0;
            while (cur != null && g++ < 10)
            {
                try
                {
                    var el = cur.element;
                    if (el != null)
                    {
                        var ge = el.TryCast<GameItemElement>();
                        if (ge != null) return ge;
                    }
                }
                catch { }
                try { cur = cur.parent; } catch { cur = null; }
            }
        }
        catch { }

        // ② 反向:rend(或其祖先)是不是某个物品的节点
        try
        {
            var ps = PlayerStore.Instance;
            var all = ps != null ? ps.FindAllItem(true) : null;
            if (all == null) return null;
            for (int i = 0; i < all.Count; i++)
            {
                var it = all[i];
                if (it == null) continue;
                var ge = it.TryCast<GameItemElement>();
                if (ge == null) continue;
                var c = rend;
                int k = 0;
                while (c != null && k++ < 10)
                {
                    if (IsNodeOf(ge, c)) return it;
                    try { c = c.parent; } catch { c = null; }
                }
            }
        }
        catch { }
        return null;
    }

    static bool SameObj(TreeNodeRender a, TreeNodeRender b)
    {
        try
        {
            if (a == null || b == null) return false;
            return a.Pointer == b.Pointer;
        }
        catch { return false; }
    }

    static bool IsNodeOf(GameItemElement ge, TreeNodeRender node)
    {
        try { if (SameObj(ge.handler, node)) return true; } catch { }
        try { if (SameObj(ge.inner, node)) return true; } catch { }
        try { if (SameObj(ge.middle, node)) return true; } catch { }
        try { if (SameObj(ge.rotator, node)) return true; } catch { }
        try { if (SameObj(ge.background, node)) return true; } catch { }
        try { if (SameObj(ge.fakeInner, node)) return true; } catch { }
        try { if (SameObj(ge.fakeMiddle, node)) return true; } catch { }
        try { if (SameObj(ge.fakeRotator, node)) return true; } catch { }
        try { if (SameObj(ge.fakeBackground, node)) return true; } catch { }
        return false;
    }

    public static void Report(bool dragging, TreeNodeRender rend)
    {
        if (GameEventSync.ApplyingRemote) return;
        string hint = "";
        int uid = 0;
        string ident = "";
        try
        {
            var w = rend != null ? rend.window : null;
            if (w != null) hint = w.titleString ?? "";
        }
        catch { }
        // 拖的是哪一件:TreeNodeRender.element 就是 GameItemElement(本身是 GameItem)
        string why = "";
        try
        {
            GameItem gi = FindDraggedItem(rend);
            if (gi == null && dragging && _lastPressed != null)
            {
                gi = _lastPressed;                             // 兜底:用"按下时记下的那一件"
                why = " [用按下记录的物品兜底]";
            }
            if (gi != null)
            {
                uid = gi.uniqueId;
                ident = gi.identifier ?? "";
                if (string.IsNullOrEmpty(hint)) hint = ident;
            }
            else why = " [三级反查都没找到物品]";
            if (dragging)
            {
                _dragItem = gi; _hint = hint ?? ""; _uid = uid; _ident = ident ?? "";
                _lastPosAt = 0f;      // 立刻发第一帧位置
            }
        }
        catch (Exception e) { why = " [取物品异常: " + e.Message + "]"; }
        Dragging = dragging;      // 供 ItemMoveSync 切高频采集
        CoopLog.Info($"[Coop] 本地拖拽{(dragging ? "开始" : "结束")}: uid={uid} ident='{ident}'{why}");
        CoopClient.SendDrag(dragging, hint, uid, ident);
    }
}

[HarmonyPatch(typeof(TreeNodeRender), "OnBeginDrag")]
public static class TreeNodeBeginDragPatch
{
    public static void Postfix(TreeNodeRender __instance) { DragSync.Report(true, __instance); }
}

[HarmonyPatch(typeof(TreeNodeRender), "OnEndDrag")]
public static class TreeNodeEndDragPatch
{
    public static void Postfix(TreeNodeRender __instance) { DragSync.Report(false, __instance); }
}

[HarmonyPatch(typeof(TreeNodeRender), "OnPointerDown")]
public static class TreeNodePointerDownPatch
{
    public static void Postfix(TreeNodeRender __instance) { DragSync.NotePressed(__instance); }
}
