using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace ProbablyStolenCoop;

/// <summary>
/// 朋友光标:置顶的鼠标箭头图标 + 名字标签 + 拖拽幽灵
/// 图案由代码生成(14×20 点阵:K=黑描边 W=白填充),热点(尖端)对准光标坐标
/// </summary>
public static class FriendCursor
{
    // 14×20 鼠标箭头点阵(y 向下)
    private static readonly string[] ART =
    {
        "KKK...........",
        "KWKK..........",
        "KWWKK.........",
        "KWWWKK........",
        "KWWWWKK.......",
        "KWWWWWKK......",
        "KWWWWWWKK.....",
        "KWWWWWWWKK....",
        "KWWWWWWWWKK...",
        "KWWWWWWWWWKK..",
        "KWWWWWWWWWWK..",
        "KWWWWWWKKKKK..",
        "KWWWWWWWK.....",
        "KWWKKWWWK.....",
        "KWKKKWWWKK....",
        "KKK.KWWWWK....",
        "....KKWWWK....",
        ".....KWKKK....",
        ".....KKK......",
        "..............",
    };
    private const int TEX_W = 14;
    private const int TEX_H = 20;
    private const float SCALE = 2f;   // 显示放大倍数
    private const float HOT_X = 1f;   // 尖端坐标(含 1px 边距)
    private const float HOT_Y = 1f;

    private static GameObject _root;
    private static RectTransform _dot;
    private static Text _nameText;
    private static GameObject _dragGhost;
    private static Text _dragLabel;
    private static bool _created;
    private static bool _visible;
    private static string _peerName = "队友";

    // 插值:网络更新只设目标,每帧平滑逼近(消除"一跳一跳")
    private static float _tgtX, _tgtY;      // 目标位置(屏幕像素)
    private static float _curX, _curY;      // 当前渲染位置(屏幕像素)
    private static bool _hasTarget;

    // ---------- 拖动跟随(让"同一件物品"跟着队友光标走,效果与单机拖拽一致) ----------
    private static bool _dragging;            // 队友是否正在拖拽
    private static GameItem _dragItem;        // 队友正在拖的那一件(本地对应的物品)
    private static Vector2 _dragBasePx;       // 起始时的模型像素位
    private static Vector2 _dragBaseLocal;    // 起始时的光标(库存局部坐标)
    private static bool _dragBaseSet;         // 起点已标定
    private static float _dragStartAt;
    private static bool _dragGhostUsed;
    private static bool _diagDone;      // 每次拖动只报一条诊断
    private static bool _followed;      // 本次拖动是否成功跟随过
    private static bool _warnedOldPeer; // "对方版本旧"只提示一次
    private static bool _calibLogged;   // 拖动标定日志(每次拖动一次)
    private static float _peerNx, _peerNy;      // 队友那边"被拖物品"的归一化屏幕位置
    private static bool _peerGot;               // 是否收到过位置
    private static Vector2 _baseAnchor;         // 起始时本端物品的 anchoredPosition
    private static float _basePeerNx, _basePeerNy;

    static void Diag(string msg)
    {
        if (_diagDone) return;
        _diagDone = true;
        CoopLog.Info($"[Coop] 拖动跟随: {msg}" + (_dragItem != null ? $" (物品 {_dragItem.identifier} uid={_dragItem.uniqueId})" : " (物品为空)"));
    }

    /// <summary>队友鼠标位置(屏幕像素)换算到某个库存的局部坐标</summary>
    static bool ToInvLocal(RectTransform rt, float screenX, float screenY, out Vector2 local)
    {
        local = Vector2.zero;
        if (rt == null) return false;
        try
        {
            // overlay canvas(相机传 null);沿用客户光标用的屏幕像素坐标
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rt, new Vector2(screenX, screenY), null, out local);
        }
        catch { return false; }
    }

    public static bool IsDraggingPeer => _dragging;

    /// <summary>
    /// 拖动跟随说明:拖动过程本身由"高频 ITEM_MOVE"同步(对方游戏在拖动时会实时更新物品的
    /// modifiedShape,我们 60ms 采集一次发过来 → 逐格跟手,和单机一致)。
    /// 这里**不再**自己算坐标去画 —— 试过按 `rt.rect`/局部坐标换算,实测 rect 返回 0x0,
    /// 算出来的格子完全不对(标定日志可证),所以只保留"拖动中: xxx"提示。
    /// </summary>
    static void TickDragFollow()
    {
        if (!_dragging || _dragItem == null || !_peerGot) return;
        try
        {
            var ge = _dragItem.TryCast<GameItemElement>();
            var rt = ge != null ? ge.rectTransform : null;
            if (rt == null) { Diag("跟随失败: 物品没有 rectTransform"); return; }

            if (!_dragBaseSet)
            {
                _baseAnchor = rt.anchoredPosition;
                _basePeerNx = _peerNx; _basePeerNy = _peerNy;
                _dragBaseSet = true;
                return;
            }
            ApplyDragOffset(ge);
        }
        catch (Exception e) { Diag("跟随异常: " + e.Message); }
    }

    /// <summary>
    /// 把"相对起点的位移"写进物品的 anchoredPosition。
    /// 同时被两处调用:每秒的 Tick + 游戏自身定位方法(GameItemElement.UpdateRectTransform)的 Postfix ——
    /// ⚠️ 后者是为了保证"我们最后一次写" —— 游戏的布局会重写这个字段,只在自己 Tick 里写会被覆盖。
    /// </summary>
    private static GameItemElement _fakeItem;
    private static bool _fakeOrigActive;

    /// <summary>
    /// 拖动跟随:把本端物品的**假象层**(fakeInner 那组)显示出来并移到队友那边物品的位置。
    /// ⚠️ 全程不动主 rectTransform / 不动模型 —— 物品的真实位置、存档、松手落位都不受影响,
    ///    只是"视觉上的替身"跟着走(与发送端表现一致:发送端也是假象层在动、真实层不动)。
    /// </summary>
    public static void ApplyDragOffset(GameItemElement ge)
    {
        try
        {
            if (!CoopClient.DragFollowExperimental) return;
            if (!_dragging || _dragItem == null || !_peerGot) return;
            if (ge == null) return;
            var it = ge.TryCast<GameItem>();
            if (it == null) return;
            try { if (it.Pointer != _dragItem.Pointer) return; } catch { return; }

            var rt = ge.rectTransform;
            if (rt == null) return;
            if (!_calibLogged)
            {
                _calibLogged = true;
                CoopLog.Info($"[Coop][拖标定收] 对端n=({_peerNx:F3},{_peerNy:F3}) 本端fakeIn={NodePos(ge.fakeInner)} 主rt={rt.position} 屏幕={Screen.width}x{Screen.height} 假象可见={FakeActive(ge)}");
            }

            float sx = _peerNx * Screen.width, sy = _peerNy * Screen.height;
            ShowFake(ge);
            MoveNode(ge.fakeInner, sx, sy);
            MoveNode(ge.fakeMiddle, sx, sy);
            MoveNode(ge.fakeRotator, sx, sy);
            MoveNode(ge.fakeBackground, sx, sy);
            _followed = true;
            _diagLogged2 = true;
        }
        catch { }
    }

    private static bool _diagLogged2;

    /// <summary>显示假象层(记住原始可见状态,拖动结束时还原)</summary>
    static void ShowFake(GameItemElement ge)
    {
        try
        {
            if (_fakeItem == null || !SamePtr(_fakeItem, ge))
            {
                HideFake();
                _fakeItem = ge;
                try { _fakeOrigActive = ge.fakeInner != null && ge.fakeInner.gameObject.activeSelf; } catch { _fakeOrigActive = false; }
            }
            ToggleFake(ge, true);
        }
        catch { }
    }

    /// <summary>拖动结束:还原假象层可见状态(主 rectTransform 从未被动过,所以物品位置无需还原)</summary>
    public static void HideFake()
    {
        try { if (_fakeItem != null) ToggleFake(_fakeItem, _fakeOrigActive); } catch { }
        _fakeItem = null;
        _fakeOrigActive = false;
    }

    static void ToggleFake(GameItemElement ge, bool on)
    {
        try { if (ge.fakeInner != null) ge.fakeInner.gameObject.SetActive(on); } catch { }
        try { if (ge.fakeMiddle != null) ge.fakeMiddle.gameObject.SetActive(on); } catch { }
        try { if (ge.fakeRotator != null) ge.fakeRotator.gameObject.SetActive(on); } catch { }
        try { if (ge.fakeBackground != null) ge.fakeBackground.gameObject.SetActive(on); } catch { }
    }

    static bool SamePtr(GameItem a, GameItem b)
    {
        try { return a != null && b != null && a.Pointer == b.Pointer; } catch { return false; }
    }

    static string FakeActive(GameItemElement ge)
    {
        try { return ge.fakeInner != null ? ge.fakeInner.gameObject.activeSelf.ToString() : "null"; }
        catch { return "?"; }
    }

    static string NodePos(TreeNodeRender n)
    {
        try { return n != null ? n.transform.position.ToString("F0") : "null"; } catch { return "?"; }
    }

    /// <summary>把某个节点移到指定的屏幕像素位置(世界坐标直设;Overlay 下 position 即屏幕像素)</summary>
    static void MoveNode(TreeNodeRender n, float sx, float sy)
    {
        try
        {
            if (n == null) return;
            var tr = n.transform;
            Camera cam = null;
            try
            {
                var cv = tr.GetComponentInParent<Canvas>();
                if (cv != null && cv.renderMode != RenderMode.ScreenSpaceOverlay) cam = cv.worldCamera;
            }
            catch { }
            Vector3 w;
            if (cam == null) w = new Vector3(sx, sy, tr.position.z);
            else
            {
                float depth = Mathf.Abs(cam.transform.InverseTransformPoint(tr.position).z);
                w = cam.ScreenToWorldPoint(new Vector3(sx, sy, depth));
            }
            tr.position = w;
        }
        catch { }
    }

    public static void SetPeerName(string name)
    {
        if (string.IsNullOrEmpty(name)) return;
        _peerName = name;
        try { if (_nameText != null) _nameText.text = name; } catch { }
    }

    /// <summary>收到远端光标坐标(归一化):更新目标,由 Tick() 平滑跟随</summary>
    public static void Update(float nx, float ny)
    {
        try
        {
            EnsureCreated();
            if (_root == null || _dot == null) return;
            if (!_visible)
            {
                _root.SetActive(true);
                _visible = true;
            }
            float px = nx * Screen.width, py = ny * Screen.height;
            if (!_hasTarget)
            {
                // 首次:直接落位,避免从屏幕原点飞过来
                _curX = px; _curY = py;
                _hasTarget = true;
            }
            _tgtX = px; _tgtY = py;
            ApplyPos(_curX, _curY);   // 立即应用一次(屏幕尺寸变化时也能跟上)
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[Coop] 朋友光标更新异常: {e.Message}");
        }
    }

    /// <summary>每帧调用(心跳):指数平滑逼近目标位置,帧率无关</summary>
    public static void Tick()
    {
        if (!_visible || !_hasTarget) return;
        try
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) dt = 0.016f;
            float dx = _tgtX - _curX, dy = _tgtY - _curY;
            if (Mathf.Abs(dx) < 0.3f && Mathf.Abs(dy) < 0.3f)
            {
                _curX = _tgtX; _curY = _tgtY;
            }
            else
            {
                float k = 1f - Mathf.Exp(-16f * dt);   // ~16/s 逼近速度
                _curX += dx * k; _curY += dy * k;
            }
            ApplyPos(_curX, _curY);
            TickDragFollow();
        }
        catch { }
    }

    private static void ApplyPos(float px, float py)
    {
        if (_dot != null)
            _dot.position = new Vector3(px, py, 0f);
        if (_nameText != null)
            _nameText.transform.position = new Vector3(px + 20, py - 12, 0f);
        if (_dragGhost != null)
            _dragGhost.transform.position = new Vector3(px + 20, py + 24, 0f);
    }

    /// <summary>拖拽状态:优先让"同一件物品"跟着光标走;找不到该物品时才退回幽灵提示</summary>
    public static void SetDragging(bool dragging, string hint, int uid = 0, string ident = "",
                                   float nx = 0f, float ny = 0f)
    {
        try
        {
            bool wasDragging = _dragging;
            _dragging = dragging;
            if (dragging)
            {
                // 旧版的假象层跟随已废弃,这里不再接收屏幕坐标(nx<0 表示"没有")。
                // 拖动跟随现在走 ItemMoveSync.ApplyDragTarget(目标格 → 改模型 + 重绘)。
                if (nx >= 0f && ny >= 0f) { _peerNx = nx; _peerNy = ny; _peerGot = true; }
                if (wasDragging) return;          // 已在拖动中:只更新位置,不要重置基准
            }
            if (dragging)
            {
                _dragItem = (uid != 0 || !string.IsNullOrEmpty(ident))
                    ? ItemMoveSync.FindItem(uid, ident) : null;
                _dragBaseSet = false;
                _diagDone = false;
                _followed = false;
                _calibLogged = false;
                _dragStartAt = Time.unscaledTime;
                if (_dragItem == null)
                    CoopLog.Info($"[Coop] 队友开始拖动,但本端找不到该物品(uid={uid} ident='{ident}') → 只显示提示");
                else
                    CoopLog.Info($"[Coop] 队友开始拖动: {_dragItem.identifier} uid={uid}");
            }
            else
            {
                HideFake();               // 拖动结束:收起假象层,物品恢复原样(位置从未被动过)
                _dragItem = null;
                _dragBaseSet = false;
            }
            // 拖动期间始终显示提示:即使物品跟随生效也应该让人"知道对方在拖";
            // (之前只在找不到物品时才显示 → 跟随失败时用户既看不到跟随也不知道对方在拖)
            bool ghost = dragging && _visible;
            if (_dragGhost != null) _dragGhost.SetActive(ghost);
            if (_dragLabel != null)
                _dragLabel.text = string.IsNullOrEmpty(hint) ? "拖动中…" : ("拖动中: " + hint);
        }
        catch { }
    }

    public static void Hide()
    {
        try
        {
            if (_root != null) { _root.SetActive(false); _visible = false; }
            if (_dragGhost != null) _dragGhost.SetActive(false);
            _dragging = false;
            _dragItem = null;
            _dragBaseSet = false;
        }
        catch { }
    }

    private static void EnsureCreated()
    {
        if (_created && _root != null) return;
        _root = new GameObject("CoopFriendCursor");
        UnityEngine.Object.DontDestroyOnLoad(_root);
        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;

        // 光标箭头
        var dotGo = new GameObject("cursor");
        dotGo.transform.SetParent(_root.transform, false);
        var img = dotGo.AddComponent<Image>();
        img.sprite = BuildCursorSprite();
        img.color = Color.white;
        img.raycastTarget = false;
        _dot = dotGo.GetComponent<RectTransform>();
        _dot.sizeDelta = new Vector2(TEX_W * SCALE, TEX_H * SCALE);
        _dot.pivot = new Vector2(HOT_X / TEX_W, 1f - HOT_Y / TEX_H);

        // 名字标签
        var nameGo = new GameObject("name");
        nameGo.transform.SetParent(_root.transform, false);
        _nameText = nameGo.AddComponent<Text>();
        _nameText.font = CoopUI.GetFont();
        _nameText.text = _peerName;
        _nameText.fontSize = 15;
        _nameText.color = new Color(1f, 1f, 0.9f, 0.95f);
        _nameText.alignment = TextAnchor.UpperLeft;
        _nameText.raycastTarget = false;
        var outline = nameGo.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(1f, -1f);
        var nrt = nameGo.GetComponent<RectTransform>();
        nrt.sizeDelta = new Vector2(200f, 22f);
        nrt.pivot = new Vector2(0f, 1f);

        // 拖拽幽灵
        _dragGhost = new GameObject("dragGhost");
        _dragGhost.transform.SetParent(_root.transform, false);
        var gi = _dragGhost.AddComponent<Image>();
        gi.color = new Color(0.35f, 0.75f, 1f, 0.55f);
        gi.raycastTarget = false;
        var grt = _dragGhost.GetComponent<RectTransform>();
        grt.sizeDelta = new Vector2(30f, 30f);
        grt.pivot = new Vector2(0.5f, 0.5f);
        var dlGo = new GameObject("label");
        dlGo.transform.SetParent(_dragGhost.transform, false);
        _dragLabel = dlGo.AddComponent<Text>();
        _dragLabel.font = CoopUI.GetFont();
        _dragLabel.text = "拖动中…";
        _dragLabel.fontSize = 13;
        _dragLabel.color = Color.white;
        _dragLabel.alignment = TextAnchor.MiddleCenter;
        _dragLabel.raycastTarget = false;
        var dlrt = dlGo.GetComponent<RectTransform>();
        dlrt.sizeDelta = new Vector2(160f, 20f);
        dlrt.anchoredPosition = new Vector2(0f, -26f);
        _dragGhost.SetActive(false);

        _root.SetActive(false);   // 收到第一个远端光标前不显示
        _visible = false;
        _created = true;
        CoopLog.Info("[Coop] 朋友光标(图标+名字)已创建(等待远端光标)");
    }

    private static Sprite BuildCursorSprite()
    {
        var tex = new Texture2D(TEX_W, TEX_H, TextureFormat.RGBA32, false);
        for (int y = 0; y < TEX_H; y++)
        {
            string row = ART[y];
            for (int x = 0; x < TEX_W; x++)
            {
                char c = x < row.Length ? row[x] : '.';
                Color col;
                if (c == 'K') col = new Color(0.05f, 0.05f, 0.08f, 1f);   // 描边(近黑)
                else if (c == 'W') col = new Color(1f, 1f, 1f, 1f);      // 填充白
                else col = new Color(0f, 0f, 0f, 0f);                    // 透明
                tex.SetPixel(x, TEX_H - 1 - y, col);   // 点阵 y 向下 → 纹理 y 向上
            }
        }
        tex.filterMode = FilterMode.Point;   // 像素风,放缩不糊
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, TEX_W, TEX_H),
            new Vector2(HOT_X / TEX_W, 1f - HOT_Y / TEX_H), 100f);
    }
}

// ---- 拖动跟随:挂在游戏自己的定位方法之后,保证我们的写入是"最后一次" ----
[HarmonyPatch(typeof(GameItemElement), nameof(GameItemElement.UpdateRectTransform))]
public static class ItemElementRelayoutPatch
{
    public static void Postfix(GameItemElement __instance)
    {
        try { FriendCursor.ApplyDragOffset(__instance); } catch { }
    }
}
