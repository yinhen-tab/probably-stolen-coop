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

    public static void SetPeerName(string name)
    {
        if (string.IsNullOrEmpty(name)) return;
        _peerName = name;
        try { if (_nameText != null) _nameText.text = name; } catch { }
    }

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
            _dot.position = new Vector3(px, py, 0f);
            if (_nameText != null)
                _nameText.transform.position = new Vector3(px + 20, py - 12, 0f);
            if (_dragGhost != null)
                _dragGhost.transform.position = new Vector3(px + 20, py + 24, 0f);
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[Coop] 朋友光标更新异常: {e.Message}");
        }
    }

    /// <summary>拖拽状态(队友拖动物品时显示幽灵)</summary>
    public static void SetDragging(bool dragging, string hint)
    {
        try
        {
            if (_dragGhost == null) return;
            _dragGhost.SetActive(dragging && _visible);
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
        Plugin.Log.LogInfo("[Coop] 朋友光标(图标+名字)已创建(等待远端光标)");
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
