using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ProbablyStolenCoop;

/// <summary>
/// 队友物件高亮圈:在对方使用的物件位置画一个脉动圆环,数秒后淡出。
/// 与点击特效共用一张程序生成的圆环贴图。
/// </summary>
public static class PeerMarker
{
    private class Mark
    {
        public RectTransform rt;
        public Image img;
        public float until;
    }

    private static GameObject _root;
    private static Sprite _ring;
    private static bool _created;
    private static readonly List<Mark> _marks = new();
    private static readonly float[] Pulse = { 1.0f, 1.18f, 1.34f, 1.18f };

    public static void Show(string kind, float nx, float ny)
    {
        try
        {
            EnsureCreated();
            if (_root == null || _ring == null) return;
            var go = new GameObject("mark_" + kind);
            go.transform.SetParent(_root.transform, false);
            var img = go.AddComponent<Image>();
            img.sprite = _ring;
            img.raycastTarget = false;
            img.color = new Color(0.45f, 0.95f, 0.75f, 0.0f);   // 起始淡入
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(110f, 110f);
            rt.position = new Vector3(nx * Screen.width, ny * Screen.height, 0f);
            _marks.Add(new Mark { rt = rt, img = img, until = Time.time + 4f });
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop] 高亮圈异常: {e.Message}"); }
    }

    public static void Tick(Dictionary<string, float> markUntil)
    {
        try
        {
            if (_marks.Count == 0) return;
            float now = Time.time;
            for (int i = _marks.Count - 1; i >= 0; i--)
            {
                var m = _marks[i];
                if (m.rt == null) { _marks.RemoveAt(i); continue; }
                float remain = m.until - now;
                if (remain <= 0f)
                {
                    UnityEngine.Object.Destroy(m.rt.gameObject);
                    _marks.RemoveAt(i);
                    continue;
                }
                float t = Mathf.Min(1f, remain / 4f);          // 末尾 1 秒淡出
                float pulse = Pulse[(int)(Time.time * 4f) % Pulse.Length];
                m.rt.sizeDelta = new Vector2(110f * pulse, 110f * pulse);
                var c = m.img.color;
                c.a = 0.85f * t;
                m.img.color = c;
            }
        }
        catch { }
    }

    private static void EnsureCreated()
    {
        if (_created && _root != null) return;
        _root = new GameObject("CoopPeerMarker");
        UnityEngine.Object.DontDestroyOnLoad(_root);
        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30500;
        _ring = ClickFx.BuildRingSpritePublic();
        _created = true;
    }
}