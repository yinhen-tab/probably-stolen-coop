using System;
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

    /// <summary>队友是否正在拖动物品(供 ItemMoveSync 抑制采集回传)</summary>
    public static bool IsDraggingPeer => _dragging;

    /// <summary>
    /// 每帧:让"同一件物品"跟着队友光标走。
    /// ★ 必须改**模型**(GridShapeBuilder.minX/minY) —— 游戏的布局是按模型重算的,
    ///   只调 SetCenteredPosition 改画面会被下一帧覆盖(实测:界面差分显示物品纹丝不动)。
    ///   改模型 + 按格吸附,和单机拖拽的观感一致。
    /// </summary>
    static void TickDragFollow()
    {
        if (!_dragging || _dragItem == null) return;
        try
        {
            var inv = ItemMoveSync.GridOf(_dragItem);
            var rt = inv != null ? inv.rectTransform : null;
            if (rt == null) { Diag("跟随失败: 物品没有可用的库存 rectTransform"); return; }
            if (!ToInvLocal(rt, _tgtX, _tgtY, out var local))
            { Diag("跟随失败: 屏幕坐标无法换算到库存局部坐标"); return; }

            var sh = ItemMoveSync.Placed(_dragItem);
            if (sh == null) { Diag("跟随失败: 拿不到物品形状"); return; }

            float cw = 16f, ch = 16f;
            int gw = 0, gh = 0;
            try
            {
                var gin = inv.TryCast<GameGridInventory>();
                if (gin != null)
                {
                    gw = gin.inventoryShape != null ? gin.inventoryShape.width : 0;
                    gh = gin.inventoryShape != null ? gin.inventoryShape.height : 0;
                    if (gw > 0) cw = (float)gin.widthPixels / gw;
                    if (gh > 0) ch = (float)gin.heightPixels / gh;
                }
            }
            catch { }

            int nx = Mathf.RoundToInt(local.x / cw - sh.width * 0.5f);
            int ny = Mathf.RoundToInt(local.y / ch - sh.height * 0.5f);
            if (gw > 0) nx = Mathf.Clamp(nx, 0, Mathf.Max(0, gw - sh.width));
            if (gh > 0) ny = Mathf.Clamp(ny, 0, Mathf.Max(0, gh - sh.height));

            if (nx == sh.minX && ny == sh.minY) { _followed = true; return; }

            bool ok = false;
            try
            {
                var b = sh.TryCast<GridShapeBuilder>();
                if (b != null) { b.minX = nx; b.minY = ny; ok = true; }
            }
            catch { }
            if (!ok) { Diag("跟随失败: 形状不可写(拿不到 GridShapeBuilder)"); return; }

            ItemMoveSync.PlaceVisual(_dragItem, ItemMoveSync.ToPixel(inv, sh, nx, ny));
            _followed = true;
        }
        catch (Exception e) { Diag("跟随异常: " + e.Message); }
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
    public static void SetDragging(bool dragging, string hint, int uid = 0, string ident = "")
    {
        try
        {
            _dragging = dragging;
            if (dragging)
            {
                _dragItem = (uid != 0 || !string.IsNullOrEmpty(ident))
                    ? ItemMoveSync.FindItem(uid, ident) : null;
                _dragBaseSet = false;
                _diagDone = false;
                _followed = false;
                _dragStartAt = Time.unscaledTime;
                if (_dragItem == null)
                {
                    CoopLog.Info($"[Coop] 队友开始拖动,但本端找不到该物品(uid={uid} ident='{ident}') → 只显示提示");
                    // uid 和 ident 都为空 = 对方根本没告诉我们拖的是哪一件 ——
                    // 只有一个原因:对方的 mod 是旧版(那时候 DragState 还没带物品标识)
                    if (uid == 0 && string.IsNullOrEmpty(ident) && !_warnedOldPeer)
                    {
                        _warnedOldPeer = true;
                        CoopLog.Warn("[Coop] ⚠ 对方 mod 版本较旧:收不到\"拖的是哪一件\",物品跟随不会生效。"
                                     + "请让对方重新安装最新的一键包(install.bat 会打印版本号,可截图核对)。");
                    }
                }
                else
                    CoopLog.Info($"[Coop] 队友开始拖动: {_dragItem.identifier} uid={uid}");
            }
            else
            {
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
