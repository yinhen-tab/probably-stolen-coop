using System;
using UnityEngine;
using UnityEngine.UI;

namespace ProbablyStolenCoop;

/// <summary>
/// 游戏内共享 HUD:房间码 + 我方/队友天数(左上角) + 事件提示(中上,自动淡出)
/// 仅在有房间且已在店铺场景时显示;由心跳协程驱动
/// </summary>
public static class CoopHud
{
    private static GameObject _root;
    private static Text _label;
    private static GameObject _toastGo;
    private static Text _toast;
    private static float _toastUntil;
    private static bool _created;

    /// <summary>游戏内文字显示总开关:按用户要求关闭(只保留光标/窗口等视觉同步);
    /// 想恢复左上 HUD 和顶部提示时改回 true 即可</summary>
    public static bool TextEnabled = false;

    /// <summary>游戏内角落房间码(独立开关,不受 TextEnabled 影响):只一行小字,方便随时报码</summary>
    public static bool ShowRoomCode = true;

    private static GameObject _codeRoot;
    private static Text _codeText;

    public static void Tick()
    {
        try
        {
            EnsureCreated();
            EnsureCodeCreated();

            // ---- 角落房间码(独立于文字总开关:只显示"房间 XXXX"一行小字) ----
            bool inScene = GameMaster.current != null;
            string rc = CoopClient.RoomCode;
            bool showCode = ShowRoomCode && !string.IsNullOrEmpty(rc) && inScene;
            if (_codeRoot != null && _codeRoot.activeSelf != showCode) _codeRoot.SetActive(showCode);
            if (showCode && _codeText != null) _codeText.text = "房间 " + rc;

            bool show = TextEnabled && !string.IsNullOrEmpty(rc) && inScene;
            if (_root == null) return;
            if (_root.activeSelf != show) _root.SetActive(show);
            if (!show) return;

            if (_label != null)
            {
                string me = CoopClient.MyDay > 0 ? $"我 第{CoopClient.MyDay}天" : "我";
                string peer = "";
                if (CoopClient.PeerDay > 0)
                {
                    string nm = string.IsNullOrEmpty(CoopClient.PeerDayName) ? "队友" : CoopClient.PeerDayName;
                    peer = $" · {nm} 第{CoopClient.PeerDay}天";
                }
                _label.text = $"房间 {CoopClient.RoomCode} · {me}{peer}";
            }

            if (_toastGo != null && _toastGo.activeSelf && Time.time > _toastUntil)
                _toastGo.SetActive(false);
        }
        catch { }
    }

    /// <summary>中上提示(3.5 秒后自动消失);文字总开关关闭时为空操作</summary>
    public static void Toast(string text)
    {
        if (!TextEnabled) return;
        try
        {
            EnsureCreated();
            if (_toast == null || _toastGo == null) return;
            _toast.text = text;
            _toastGo.SetActive(true);
            _toastUntil = Time.time + 3.5f;
        }
        catch { }
    }

    private static void EnsureCreated()
    {
        if (_created && _root != null) return;
        _root = new GameObject("CoopHud");
        UnityEngine.Object.DontDestroyOnLoad(_root);
        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 31500;

        // 左上角状态行(房间码 + 天数)
        var lg = new GameObject("status");
        lg.transform.SetParent(_root.transform, false);
        _label = lg.AddComponent<Text>();
        _label.font = CoopUI.GetFont();
        _label.text = "";
        _label.fontSize = 15;
        _label.color = new Color(0.9f, 0.95f, 1f, 0.92f);
        _label.alignment = TextAnchor.UpperLeft;
        _label.raycastTarget = false;
        var ol = lg.AddComponent<Outline>();
        ol.effectColor = new Color(0f, 0f, 0f, 0.85f);
        ol.effectDistance = new Vector2(1f, -1f);
        var lrt = lg.GetComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0f, 1f);
        lrt.anchorMax = new Vector2(0f, 1f);
        lrt.pivot = new Vector2(0f, 1f);
        lrt.sizeDelta = new Vector2(560f, 24f);
        lrt.anchoredPosition = new Vector2(14f, -10f);

        // 中上提示(淡出)
        _toastGo = new GameObject("toast");
        _toastGo.transform.SetParent(_root.transform, false);
        _toast = _toastGo.AddComponent<Text>();
        _toast.font = CoopUI.GetFont();
        _toast.text = "";
        _toast.fontSize = 20;
        _toast.color = new Color(1f, 0.88f, 0.5f, 0.98f);
        _toast.alignment = TextAnchor.MiddleCenter;
        _toast.raycastTarget = false;
        var tol = _toastGo.AddComponent<Outline>();
        tol.effectColor = new Color(0f, 0f, 0f, 0.9f);
        tol.effectDistance = new Vector2(1f, -1f);
        var trt = _toastGo.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0.5f, 1f);
        trt.anchorMax = new Vector2(0.5f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.sizeDelta = new Vector2(800f, 30f);
        trt.anchoredPosition = new Vector2(0f, -64f);
        _toastGo.SetActive(false);

        _root.SetActive(false);   // 有房间且进店铺场景后才显示
        _created = true;
        CoopLog.Info("[Coop] 共享 HUD(房间/天数/提示)已创建");
    }

    /// <summary>角落房间码:独立 Canvas,一行小字,半透明(不打扰)</summary>
    private static void EnsureCodeCreated()
    {
        if (_codeRoot != null) return;
        _codeRoot = new GameObject("CoopRoomCode");
        UnityEngine.Object.DontDestroyOnLoad(_codeRoot);
        var canvas = _codeRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 31900;

        var go = new GameObject("code");
        go.transform.SetParent(_codeRoot.transform, false);
        _codeText = go.AddComponent<Text>();
        _codeText.font = CoopUI.GetFont();
        _codeText.text = "";
        _codeText.fontSize = 13;
        _codeText.color = new Color(1f, 1f, 1f, 0.42f);   // 半透明,尽量不打扰
        _codeText.alignment = TextAnchor.UpperLeft;
        _codeText.raycastTarget = false;
        var ol = go.AddComponent<Outline>();
        ol.effectColor = new Color(0f, 0f, 0f, 0.55f);
        ol.effectDistance = new Vector2(1f, -1f);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);   // 左下角(店铺左下是地面,不挡 UI)
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.sizeDelta = new Vector2(220f, 20f);
        rt.anchoredPosition = new Vector2(14f, 10f);
        _codeRoot.SetActive(false);
    }
}
