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

    // ---------- 目标格推算(拖动跟手) ----------
    private static bool _baseSet;                 // 起点已标定
    private static float _basePx, _basePy;        // 起点:假象层屏幕像素
    private static int _baseGX = -1, _baseGY = -1;// 起点:物品所在格
    private static float _cellPx = 0f, _cellPy = 0f;  // 每格多少屏幕像素(实测/推算)
    private static int _lastGX = -1, _lastGY = -1;
    private static float _lastSpX, _lastSpY;

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

        // ---- 用假象层位移推算"目标格"(发送端自己算,接收端照格摆放) ----
        int gx = -1, gy = -1;
        try
        {
            var ge2 = _dragItem != null ? _dragItem.TryCast<GameItemElement>() : null;
            if (ge2 != null)
            {
                if (!_baseSet)
                {
                    _basePx = sp.x; _basePy = sp.y;
                    var ms0 = ge2.modifiedShape;
                    if (ms0 != null) { _baseGX = ms0.minX; _baseGY = ms0.minY; }
                    int sw0 = ms0 != null ? Mathf.Max(1, ms0.width) : 1;
                    int sh0 = ms0 != null ? Mathf.Max(1, ms0.height) : 1;
                    float rawW = 0f, rawH = 0f, sf = 1f;
                    try { rawW = ge2.widthPixels / (float)sw0; } catch { }
                    try { rawH = ge2.heightPixels / (float)sh0; } catch { }
                    try
                    {
                        var rtc = ge2.rectTransform;
                        var cv = rtc != null ? rtc.GetComponentInParent<Canvas>() : null;
                        if (cv != null && cv.scaleFactor > 0.01f) sf = cv.scaleFactor;
                    }
                    catch { }
                    // widthPixels 若为 canvas 单位则需乘 scaleFactor;结果不合理就退回实测值 32
                    float cw = rawW > 1f ? rawW * sf : 32f;
                    float ch = rawH > 1f ? rawH * sf : 32f;
                    if (cw < 6f || cw > 200f) cw = 32f;
                    if (ch < 6f || ch > 200f) ch = 32f;
                    _cellPx = cw; _cellPy = ch;
                    _baseSet = true;
                    CoopLog.Info($"[Coop][拖格子] widthPixels=({SafeW(ge2)},{SafeH(ge2)}) shape=({sw0},{sh0}) "
                                 + $"raw=({rawW:F1},{rawH:F1}) scale={sf:F2} cellPx=({_cellPx:F1},{_cellPy:F1}) "
                                 + $"起始格=({_baseGX},{_baseGY}) 起始像素=({_basePx:F0},{_basePy:F0})");
                }
                _lastSpX = sp.x; _lastSpY = sp.y;
                if (_baseGX >= 0 && _cellPx > 2f && _cellPy > 2f)
                {
                    // Unity 屏幕 y 向上、网格 y 向下 → y 取负
                    gx = _baseGX + Mathf.RoundToInt((sp.x - _basePx) / _cellPx);
                    gy = _baseGY - Mathf.RoundToInt((sp.y - _basePy) / _cellPy);
                }
                _lastGX = gx; _lastGY = gy;
            }
        }
        catch { }

        // 完整轨迹落盘(每帧)—— 回放工具据此 100% 复现
        try
        {
            var tp = TrackPath();
            if (tp != null && _dragItem != null)
                System.IO.File.AppendAllText(tp, $"{gx},{gy},{sp.x:F1},{sp.y:F1}\n");
        }
        catch { }

        // 诊断:确认拖动期间坐标到底动不动、动在哪个节点上
        if (_sendCount <= 3 || _sendCount % 10 == 0)
            CoopLog.Info($"[Coop][拖标定发] #{_sendCount} 屏幕=({sp.x:F0},{sp.y:F0}) n=({nx:F3},{ny:F3}) 目标格=({gx},{gy}){info}");
        CoopClient.SendDrag(true, _hint, _uid, _ident, gx, gy);
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

    /// <summary>拖动轨迹文件(每帧一行 gx,gy,屏幕x,屏幕y)—— 供回放工具 100% 复现真实拖动</summary>
    static string TrackPath()
    {
        try { return System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "coop_drag_track.txt"); }
        catch { return null; }
    }

    static int SafeW(GameItemElement ge) { try { return ge.widthPixels; } catch { return -1; } }
    static int SafeH(GameItemElement ge) { try { return ge.heightPixels; } catch { return -1; } }

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
            // 落点候选:游戏自己算的"会落在哪一格"(modifiedShape=当前格 / tempShape=拖拽预览)
            try { var ms = ge.modifiedShape; if (ms != null) s += $" modShape=({ms.minX},{ms.minY})"; } catch { }
            try { var ts = ge.tempShape; if (ts != null) s += $" tempShape=({ts.minX},{ts.minY})"; } catch { }
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
                _baseSet = false; _baseGX = -1; _lastGX = -1; _lastGY = -1;
                try { var tp = TrackPath(); if (tp != null) System.IO.File.WriteAllText(tp, ""); } catch { }
            }
            else if (gi != null)
            {
                // 松手:延迟读游戏提交的**真实落点**,与我们的推算对比(自校准)
                var it2 = gi; int lx = _lastGX, ly = _lastGY;
                int bgx = _baseGX, bgy = _baseGY;
                float bpx = _basePx, bpy = _basePy, lpx = _lastSpX, lpy = _lastSpY, cw = _cellPx, ch = _cellPy;
                try
                {
                    CoopClient.RunOnMainAfter(0.15f, () =>
                    {
                        try
                        {
                            var ms2 = it2.modifiedShape;
                            if (ms2 == null) return;
                            int dgx = ms2.minX - bgx, dgy = ms2.minY - bgy;
                            float pdx = lpx - bpx, pdy = lpy - bpy;
                            string real = "";
                            // 用"真实格位移"反算每格像素 —— 与当前值不符就自动修正(下次拖动即准)
                            // ⚠️ 位移太小(手抖/只挪一格)会让反算严重失真(实测出 20/42 这种假值),
                            //    所以**必须跨 3 格以上**才允许修正
                            if (Mathf.Abs(dgx) >= 3)
                            {
                                float r = Mathf.Abs(pdx / dgx);
                                if (r > 6f && r < 200f)
                                {
                                    real += $" 实测cellPx={r:F1}";
                                    if (Mathf.Abs(r - cw) > 1.5f) { _cellPx = r; real += "(已修正)"; }
                                }
                            }
                            else if (dgx != 0) real += $" (x位移仅{dgx}格,不参与校准)";
                            if (Mathf.Abs(dgy) >= 3)
                            {
                                float r = Mathf.Abs(pdy / dgy);
                                if (r > 6f && r < 200f)
                                {
                                    real += $" 实测cellPy={r:F1}";
                                    if (Mathf.Abs(r - ch) > 1.5f) { _cellPy = r; real += "(已修正)"; }
                                }
                            }
                            else if (dgy != 0) real += $" (y位移仅{dgy}格,不参与校准)";
                            CoopLog.Info($"[Coop][拖校准] 推算=({lx},{ly}) 真实=({ms2.minX},{ms2.minY}) "
                                         + $"起点格=({bgx},{bgy}) 格位移=({dgx},{dgy}) 像素位移=({pdx:F0},{pdy:F0}) "
                                         + $"当前cellPx=({cw:F1},{ch:F1}){real}");
                        }
                        catch { }
                    });
                }
                catch { }
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
