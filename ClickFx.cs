using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ProbablyStolenCoop;

/// <summary>
/// 点击特效:队友点击位置显示扩散的圆环("朋友点了这里")
/// Show() 添加特效;Tick() 由心跳协程每帧驱动(扩散+淡出)
/// </summary>
public static class ClickFx
{
    private class Item
    {
        public RectTransform rt;
        public Image img;
        public float startAt;
    }

    private static GameObject _root;
    private static readonly List<Item> _items = new();
    private static Sprite _ring;
    private static bool _created;
    private const float DURATION = 0.7f;
    private const float MIN_SIZE = 18f;
    private const float MAX_SIZE = 96f;

    public static void Show(float nx, float ny)
    {
        try
        {
            EnsureCreated();
            if (_root == null || _ring == null) return;
            var go = new GameObject("fx");
            go.transform.SetParent(_root.transform, false);
            var img = go.AddComponent<Image>();
            img.sprite = _ring;
            img.raycastTarget = false;
            img.color = new Color(1f, 0.85f, 0.35f, 0.95f);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(MIN_SIZE, MIN_SIZE);
            rt.position = new Vector3(nx * Screen.width, ny * Screen.height, 0f);
            _items.Add(new Item { rt = rt, img = img, startAt = Time.time });
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[Coop] 点击特效异常: {e.Message}");
        }
    }

    /// <summary>心跳每帧驱动:扩散 + 淡出</summary>
    public static void Tick()
    {
        for (int i = _items.Count - 1; i >= 0; i--)
        {
            var it = _items[i];
            if (it.rt == null)
            {
                _items.RemoveAt(i);
                continue;
            }
            float t = (Time.time - it.startAt) / DURATION;
            if (t >= 1f)
            {
                UnityEngine.Object.Destroy(it.rt.gameObject);
                _items.RemoveAt(i);
                continue;
            }
            float size = MIN_SIZE + (MAX_SIZE - MIN_SIZE) * t;
            it.rt.sizeDelta = new Vector2(size, size);
            var c = it.img.color;
            c.a = 0.95f * (1f - t);
            it.img.color = c;
        }
    }

    private static void EnsureCreated()
    {
        if (_created && _root != null) return;
        _root = new GameObject("CoopClickFx");
        UnityEngine.Object.DontDestroyOnLoad(_root);
        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 31000;
        _ring = BuildRingSprite();
        _created = true;
        Plugin.Log.LogInfo("[Coop] 点击特效已创建");
    }

    private static Sprite BuildRingSprite()
    {
        const int SIZE = 64;
        var tex = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false);
        float c = (SIZE - 1) / 2f;
        float rOut = SIZE / 2f - 1f;
        float rIn = rOut - 6f;
        for (int y = 0; y < SIZE; y++)
        {
            for (int x = 0; x < SIZE; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = (d <= rOut && d >= rIn) ? 1f : 0f;
                // 内外缘各柔化 1px
                if (d < rOut && d > rOut - 1f) a = rOut - d;
                if (d > rIn && d < rIn + 1f) a = d - rIn;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, SIZE, SIZE), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>供 PeerMarker 复用的圆环贴图</summary>
    public static Sprite BuildRingSpritePublic() => BuildRingSprite();
}
