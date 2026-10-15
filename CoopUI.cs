using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ProbablyStolenCoop;

/// <summary>
/// 多人联机 UI:
///  - 入口按钮:克隆主菜单"载入游戏"按钮(原生样式),插入按钮列
///  - 房间面板:原生像素风(深色底 + 标题栏 + 右上角 ✕),按钮同为原生样式
///  - 流程:创建/加入房间成功后直接打开游戏存档界面(从自己的存档进入,少一步)
///  - 单机零打扰:点开面板才连接服务器;不进房间不发送任何数据
/// </summary>
public static class CoopUI
{
    private static GameObject _root;
    private static GameObject _buttonGo;      // 入口按钮(克隆)
    private static GameObject _panelGo;
    private static GameObject _btnTemplate;   // 原生按钮模板(GameObject,用于克隆)
    private static Text _statusText;
    private static Text _roomCodeText;
    private static Text _playersText;
    private static InputField _joinInput;
    private static Button _createBtn, _joinBtn;
    private static GameObject _createBtnGo, _joinBtnGo;   // 按钮引用(打开面板时校正文字用)
    private static Font _font;
    private static bool _panelBuilt;
    private static bool _loadUiShown;   // 测试:存档界面是否已自动打开过

    public static bool GetLoadUiShown() => _loadUiShown;
    public static void MarkLoadUiShown() { _loadUiShown = true; }

    // ================= 对外 API =================

    /// <summary>主菜单 Awake 时调用:克隆原生按钮样式并插入按钮列,随后建面板</summary>
    public static void AttachToMainMenu(MainMenuUIController menu)
    {
        try
        {
            EnsureRoot();
            if (_panelGo != null) _panelGo.SetActive(false);
            try { if (_buttonGo != null) UnityEngine.Object.Destroy(_buttonGo); } catch { }
            _buttonGo = null;
            if (menu != null)
            {
                try { menu.StartCoroutine(CloneEntryButton(menu).WrapToIl2Cpp()); }
                catch (Exception e) { Plugin.Log.LogWarning($"[Coop] 克隆入口按钮失败: {e.Message}"); }
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[Coop] UI 创建失败: {e.Message}");
        }
    }

    /// <summary>主菜单阶段心跳:面板状态刷新 + 点击特效(协程挂在主菜单上,随它销毁)</summary>
    public static void StartMainMenuTick(MainMenuUIController menu)
    {
        if (menu == null) return;
        try { menu.StartCoroutine(MainMenuTickRoutine().WrapToIl2Cpp()); }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop] 主菜单心跳启动失败: {e.Message}"); }
    }

    static IEnumerator MainMenuTickRoutine()
    {
        while (true)
        {
            try
            {
                ClickFx.Tick();
                Tick();
                CoopClient.AutoOpenLoadUi();   // 测试:进房后自动打开存档界面
            }
            catch { }
            yield return null;
        }
    }

    public static void HideAll()
    {
        try
        {
            if (_buttonGo != null) _buttonGo.SetActive(false);
            if (_panelGo != null) _panelGo.SetActive(false);
        }
        catch { }
    }

    /// <summary>每帧刷新面板状态</summary>
    public static void Tick()
    {
        if (_panelGo == null || !_panelGo.activeSelf) return;
        try
        {
            if (_statusText != null) _statusText.text = CoopClient.GetStatusLine();
            if (_roomCodeText != null)
                _roomCodeText.text = string.IsNullOrEmpty(CoopClient.RoomCode) ? "" : ("房间码  " + CoopClient.RoomCode);
            if (_playersText != null) _playersText.text = CoopClient.GetPlayersLine();
            if (_createBtn != null) _createBtn.interactable = CoopClient.Handshaked;
            if (_joinBtn != null) _joinBtn.interactable = CoopClient.Handshaked && string.IsNullOrEmpty(CoopClient.RoomCode);
        }
        catch { }
    }

    public static Font GetFont()
    {
        if (_font != null) return _font;
        try
        {
            var texts = Resources.FindObjectsOfTypeAll(Il2CppInterop.Runtime.Il2CppType.Of<Text>());
            for (int i = 0; i < texts.Length; i++)
            {
                try
                {
                    var t = texts[i]?.TryCast<Text>();
                    if (t != null && t.font != null) { _font = t.font; break; }
                }
                catch { }
            }
        }
        catch { }
        if (_font == null)
        {
            try { _font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
        }
        return _font;
    }

    // ================= 联机流程(创建/加入 → 直接选存档) =================

    /// <summary>"创建房间":建房成功后直接打开游戏存档界面(少一个步骤)</summary>
    static void CreateRoomAndPickSave()
    {
        if (!string.IsNullOrEmpty(CoopClient.RoomCode)) { OpenLoadUiNow(); return; }   // 已在房间 → 直接选存档
        if (!CoopClient.Handshaked) { CoopClient.TryConnect(); Plugin.Log.LogInfo("[Coop] 正在连接服务器,请稍候再点"); return; }
        CoopClient.RequestCreateRoom();
        WaitRoomThenOpen();
    }

    /// <summary>"加入房间":加入成功后直接打开游戏存档界面</summary>
    static void JoinRoomAndPickSave()
    {
        string code = _joinInput != null ? (_joinInput.text ?? "").Trim().ToUpper() : "";
        if (code.Length < 4) { Plugin.Log.LogWarning("[Coop] 房间码不足 4 位"); return; }
        if (!string.IsNullOrEmpty(CoopClient.RoomCode)) { OpenLoadUiNow(); return; }
        if (!CoopClient.Handshaked) { CoopClient.TryConnect(); Plugin.Log.LogInfo("[Coop] 正在连接服务器,请稍候再点"); return; }
        CoopClient.RequestJoinRoom(code);
        WaitRoomThenOpen();
    }

    /// <summary>等房间确认(最多 ~4 秒)后打开存档界面</summary>
    static void WaitRoomThenOpen()
    {
        var host = MainMenuUIController.Instance;
        if (host == null) { OpenLoadUiNow(); return; }
        try { host.StartCoroutine(WaitRoomRoutine().WrapToIl2Cpp()); }
        catch { OpenLoadUiNow(); }
    }

    static IEnumerator WaitRoomRoutine()
    {
        var wait = new WaitForSeconds(0.1f);
        for (int i = 0; i < 40; i++)
        {
            yield return wait;
            if (!string.IsNullOrEmpty(CoopClient.RoomCode)) { OpenLoadUiNow(); yield break; }
        }
        Plugin.Log.LogWarning("[Coop] 等待房间确认超时(服务器无响应),未打开存档界面");
    }

    /// <summary>关面板 → 打开游戏原生"载入存档"界面(玩家选自己的存档进入)</summary>
    static void OpenLoadUiNow()
    {
        try
        {
            if (_panelGo != null) _panelGo.SetActive(false);
            var m = MainMenuUIController.Instance;
            if (m == null) { Plugin.Log.LogWarning("[Coop] 主菜单实例为空,无法打开存档界面"); return; }
            m.OnLoadGameClick();
            Plugin.Log.LogInfo("[Coop] 已打开游戏存档界面(请选择自己的存档进入)");
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop] 打开存档界面失败: {e.Message}"); }
    }

    static void ClosePanel()
    {
        if (_panelGo != null) _panelGo.SetActive(false);
        Plugin.Log.LogInfo("[Coop] 联机面板已关闭");
    }

    // ================= 入口按钮(克隆) =================

    static string GetButtonText(Button b)
    {
        try
        {
            var tmps = b.GetComponentsInChildren<TextMeshProUGUI>(true);
            if (tmps.Length > 0 && !string.IsNullOrEmpty(tmps[0].text)) return tmps[0].text;
            var texts = b.GetComponentsInChildren<Text>(true);
            if (texts.Length > 0 && !string.IsNullOrEmpty(texts[0].text)) return texts[0].text;
        }
        catch { }
        return "";
    }

    static int MatchMenuOrder(string t)
    {
        if (string.IsNullOrEmpty(t)) return -1;
        if (t.Contains("新游戏")) return 0;
        if (t.Contains("载入游戏")) return 1;
        if (t.Contains("选项")) return 2;
        if (t.Contains("制作人员名单")) return 3;
        if (t.Contains("退出游戏")) return 4;
        return -1;
    }

    static IEnumerator CloneEntryButton(MainMenuUIController menu)
    {
        yield return new WaitForSeconds(1.2f);   // 等主菜单布局完成
        List<Button> list = new List<Button>();
        try
        {
            var all = Resources.FindObjectsOfTypeAll(Il2CppInterop.Runtime.Il2CppType.Of<Button>());
            for (int i = 0; i < all.Length; i++)
            {
                try
                {
                    var b = all[i]?.TryCast<Button>();
                    if (b == null) continue;
                    if (b.gameObject.name == "CoopMultiplayerButton") continue;
                    if (MatchMenuOrder(GetButtonText(b)) < 0) continue;
                    list.Add(b);
                }
                catch { }
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[Coop] 按钮收集异常: {e.Message}");
        }
        list.Sort((a, c) =>
        {
            try { return MatchMenuOrder(GetButtonText(a)).CompareTo(MatchMenuOrder(GetButtonText(c))); }
            catch { return 0; }
        });
        Plugin.Log.LogInfo($"[Coop] 主菜单按钮找到 {list.Count} 个");
        for (int i = 0; i < list.Count; i++)
        {
            try
            {
                var rt = list[i].GetComponent<RectTransform>();
                Plugin.Log.LogInfo($"[Coop]   [{i}] \"{GetButtonText(list[i])}\" y={rt.position.y:F0} x={rt.position.x:F0}");
            }
            catch { }
        }
        if (list.Count < 2)
        {
            Plugin.Log.LogWarning("[Coop] 未找到主菜单按钮列,入口按钮未创建");
            yield break;
        }

        // 以 [1](载入游戏) 为模板
        GameObject clone = null;
        try
        {
            var cloneObj = UnityEngine.Object.Instantiate(list[1].gameObject);
            clone = cloneObj != null ? cloneObj.TryCast<GameObject>() : null;
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[Coop] 克隆按钮异常: {e.Message}");
        }
        if (clone == null)
        {
            Plugin.Log.LogWarning("[Coop] 克隆结果为空,入口按钮未创建");
            yield break;
        }
        clone.name = "CoopMultiplayerButton";
        _btnTemplate = clone;   // 留作面板按钮模板

        SetButtonLabel(clone, "多人联机");
        AttachShield(clone, () => TogglePanel());
        StartKeepText(menu, clone, "多人联机");

        // 重排:克隆体占据 [2] 原位置,[2..] 各下移一个间距
        try
        {
            int n = list.Count;
            var rts = new RectTransform[n];
            var ys = new float[n];
            for (int i = 0; i < n; i++)
            {
                rts[i] = list[i].GetComponent<RectTransform>();
                ys[i] = rts[i].anchoredPosition.y;
            }
            float gap = ys[0] - ys[1];
            var crt = clone.GetComponent<RectTransform>();
            crt.SetParent(rts[1].parent, false);
            crt.anchorMin = rts[1].anchorMin;
            crt.anchorMax = rts[1].anchorMax;
            crt.pivot = rts[1].pivot;
            crt.sizeDelta = rts[1].sizeDelta;
            crt.anchoredPosition = new Vector2(rts[1].anchoredPosition.x, n > 2 ? ys[2] : (ys[1] - gap));
            int insIdx = (n > 2) ? rts[2].GetSiblingIndex() : (rts[1].GetSiblingIndex() + 1);
            clone.transform.SetSiblingIndex(insIdx);
            for (int i = 2; i < n; i++)
                rts[i].anchoredPosition = new Vector2(rts[i].anchoredPosition.x, ys[i] - gap);
            _buttonGo = clone;
            Plugin.Log.LogInfo($"[Coop] 多人联机按钮已插入主菜单(gap={gap:F0})");
            try { menu.StartCoroutine(LogBtnScreenPos(clone).WrapToIl2Cpp()); } catch { }
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[Coop] 按钮重排异常: {e.Message}");
        }

        // 构建原生风格面板
        if (!_panelBuilt) BuildPanel(menu);
    }

    /// <summary>布局完成后打印按钮屏幕坐标(自动化测试定位用)</summary>
    static IEnumerator LogBtnScreenPos(GameObject go)
    {
        yield return new WaitForSeconds(0.6f);
        try
        {
            if (go != null)
                Plugin.Log.LogInfo($"[Coop] 多人联机按钮屏幕坐标: {go.GetComponent<RectTransform>().position}");
        }
        catch { }
    }

    /// <summary>设置按钮文字(全部文本组件)</summary>
    static void SetButtonLabel(GameObject go, string label)
    {
        if (go == null) return;
        try
        {
            var tmps = go.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < tmps.Length; i++) tmps[i].text = label;
            var texts = go.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++) texts[i].text = label;
        }
        catch { }
    }

    /// <summary>透明点击挡板:拦截克隆体自带点击逻辑,只执行我们的</summary>
    static void AttachShield(GameObject go, Action onClick)
    {
        try
        {
            var btn = go.GetComponent<Button>();
            if (btn != null) { btn.onClick.RemoveAllListeners(); btn.interactable = true; }
            var shield = new GameObject("clickShield");
            shield.transform.SetParent(go.transform, false);
            var srt = shield.AddComponent<RectTransform>();
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
            srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;
            var simg = shield.AddComponent<Image>();
            simg.color = new Color(0f, 0f, 0f, 0f);
            simg.raycastTarget = true;
            var sbtn = shield.AddComponent<Button>();
            sbtn.transition = Selectable.Transition.None;
            sbtn.onClick.AddListener((UnityAction)(() => onClick()));
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop] 挡板异常: {e.Message}"); }
    }

    /// <summary>持续保证按钮文字(对抗本地化/文本动画器刷新)</summary>
    static void StartKeepText(MainMenuUIController host, GameObject go, string label)
    {
        try { host.StartCoroutine(KeepTextRoutine(go, label).WrapToIl2Cpp()); }
        catch { }
    }

    static IEnumerator KeepTextRoutine(GameObject go, string label)
    {
        while (true)   // 常驻:本地化/文本动画器随时可能刷回原标题(协程随主菜单销毁停止)
        {
            yield return new WaitForSeconds(0.3f);
            try
            {
                if (go == null) yield break;
                var tmps = go.GetComponentsInChildren<TextMeshProUGUI>(true);
                for (int j = 0; j < tmps.Length; j++)
                    if (tmps[j].text != label) tmps[j].text = label;
                var texts = go.GetComponentsInChildren<Text>(true);
                for (int j = 0; j < texts.Length; j++)
                    if (texts[j].text != label) texts[j].text = label;
            }
            catch { }
        }
    }

    // ================= 原生风格面板 =================

    static void BuildPanel(MainMenuUIController menu)
    {
        try
        {
            var font = GetFont();
            _panelGo = new GameObject("panel");
            _panelGo.transform.SetParent(_root.transform, false);
            var prt = _panelGo.AddComponent<RectTransform>();
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
            var bg = _panelGo.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.55f);
            bg.raycastTarget = true;

            // 面板框(深色像素风)
            var frame = new GameObject("frame");
            frame.transform.SetParent(_panelGo.transform, false);
            var frt = frame.AddComponent<RectTransform>();
            frt.anchorMin = new Vector2(0.5f, 0.5f); frt.anchorMax = new Vector2(0.5f, 0.5f);
            frt.sizeDelta = new Vector2(560f, 380f); frt.anchoredPosition = Vector2.zero;
            // 边框层(亮色垫底)
            var borderGo = new GameObject("border");
            borderGo.transform.SetParent(frame.transform, false);
            var bort = borderGo.AddComponent<RectTransform>();
            bort.anchorMin = Vector2.zero; bort.anchorMax = Vector2.one;
            bort.offsetMin = new Vector2(-3f, -3f); bort.offsetMax = new Vector2(3f, 3f);
            var borimg = borderGo.AddComponent<Image>();
            borimg.color = new Color(0.30f, 0.42f, 0.45f, 0.95f);
            // 主体
            var bodyGo = new GameObject("body");
            bodyGo.transform.SetParent(frame.transform, false);
            var bodyrt = bodyGo.AddComponent<RectTransform>();
            bodyrt.anchorMin = Vector2.zero; bodyrt.anchorMax = Vector2.one;
            bodyrt.offsetMin = Vector2.zero; bodyrt.offsetMax = Vector2.zero;
            var bodyimg = bodyGo.AddComponent<Image>();
            bodyimg.color = new Color(0.10f, 0.12f, 0.18f, 0.97f);

            // 标题栏
            var titleBar = new GameObject("titleBar");
            titleBar.transform.SetParent(frame.transform, false);
            var tbrt = titleBar.AddComponent<RectTransform>();
            tbrt.anchorMin = new Vector2(0f, 1f); tbrt.anchorMax = new Vector2(1f, 1f);
            tbrt.pivot = new Vector2(0.5f, 1f);
            tbrt.sizeDelta = new Vector2(0f, 54f); tbrt.anchoredPosition = new Vector2(0f, 0f);
            var tbimg = titleBar.AddComponent<Image>();
            tbimg.color = new Color(0.16f, 0.20f, 0.27f, 1f);
            var title = MakeText(titleBar.transform, "多人联机", 26, Vector2.zero, new Vector2(400f, 36f), font, TextAnchor.MiddleLeft);
            title.color = new Color(0.62f, 0.92f, 0.80f);
            var titlert = title.GetComponent<RectTransform>();
            titlert.anchorMin = new Vector2(0f, 0.5f); titlert.anchorMax = new Vector2(0f, 0.5f);
            titlert.pivot = new Vector2(0f, 0.5f);
            titlert.anchoredPosition = new Vector2(20f, 0f);

            // 右上角 ✕ 关闭按钮(与游戏面板一致的位置)
            MakeCloseButton(frame.transform, ClosePanel);

            // 状态 / 房间码 / 玩家列表
            _statusText = MakeText(frame.transform, "服务器:未连接", 16, new Vector2(0f, 108f), new Vector2(520f, 24f), font, TextAnchor.MiddleCenter);
            _statusText.color = new Color(0.75f, 0.80f, 0.9f);
            _roomCodeText = MakeText(frame.transform, "", 34, new Vector2(0f, 56f), new Vector2(520f, 46f), font, TextAnchor.MiddleCenter);
            _roomCodeText.color = new Color(1f, 0.85f, 0.45f);
            _playersText = MakeText(frame.transform, "", 16, new Vector2(0f, 14f), new Vector2(520f, 24f), font, TextAnchor.MiddleCenter);
            _playersText.color = new Color(0.8f, 0.85f, 1f);

            // 按钮(全部克隆原生样式)
            if (_btnTemplate != null)
            {
                _createBtnGo = CloneStyledButton(frame.transform, "创建房间", new Vector2(200f, 52f), new Vector2(-115f, -40f), menu,
                    CreateRoomAndPickSave);
                _createBtn = _createBtnGo != null ? _createBtnGo.GetComponent<Button>() : null;
                _joinBtnGo = CloneStyledButton(frame.transform, "加入房间", new Vector2(200f, 52f), new Vector2(115f, -40f), menu,
                    JoinRoomAndPickSave);
                _joinBtn = _joinBtnGo != null ? _joinBtnGo.GetComponent<Button>() : null;
            }
            else
            {
                Plugin.Log.LogWarning("[Coop] 无按钮模板,面板按钮降级为手动样式");
                _createBtn = MakeStyledButton(frame.transform, "创建房间", new Vector2(200f, 52f), new Vector2(-115f, -40f), font,
                    CreateRoomAndPickSave);
                _createBtnGo = _createBtn != null ? _createBtn.gameObject : null;
                _joinBtn = MakeStyledButton(frame.transform, "加入房间", new Vector2(200f, 52f), new Vector2(115f, -40f), font,
                    JoinRoomAndPickSave);
                _joinBtnGo = _joinBtn != null ? _joinBtn.gameObject : null;
            }

            // 输入框
            _joinInput = MakeInput(frame.transform, new Vector2(0f, -96f), new Vector2(256f, 42f), font, "输入房间码…");

            // 底部提示
            var hint = MakeText(frame.transform, "创建/加入房间后直接进入存档选择", 14, new Vector2(0f, -152f), new Vector2(520f, 20f), font, TextAnchor.MiddleCenter);
            hint.color = new Color(0.55f, 0.62f, 0.72f);

            _panelGo.SetActive(false);
            _panelBuilt = true;
            Plugin.Log.LogInfo("[Coop] 多人联机面板已构建(原生风格 + 右上角关闭)");
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[Coop] 面板构建失败: {e.Message}");
        }
    }

    /// <summary>右上角关闭按钮(✕,画两条斜线,不依赖字体缺字)</summary>
    static void MakeCloseButton(Transform parent, Action onClick)
    {
        try
        {
            var go = new GameObject("closeBtn");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(40f, 40f);
            rt.anchoredPosition = new Vector2(-8f, -7f);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.20f, 0.26f, 0.33f, 1f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            try { btn.onClick.AddListener((UnityAction)(() => onClick())); } catch { }
            MakeCrossLine(go.transform, 45f);
            MakeCrossLine(go.transform, -45f);
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop] 关闭按钮创建失败: {e.Message}"); }
    }

    static void MakeCrossLine(Transform parent, float angle)
    {
        var go = new GameObject("line");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(17f, 2.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.85f, 0.92f, 0.95f, 1f);
        img.raycastTarget = false;
    }

    /// <summary>克隆原生样式按钮(改字+挡板+文字保持)</summary>
    static GameObject CloneStyledButton(Transform parent, string label, Vector2 size, Vector2 pos, MainMenuUIController menu, Action onClick)
    {
        try
        {
            var cloneObj = UnityEngine.Object.Instantiate(_btnTemplate);
            var g = cloneObj != null ? cloneObj.TryCast<GameObject>() : null;
            if (g == null) return null;
            g.name = "coopBtn";
            g.transform.SetParent(parent, false);
            var rt = g.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size; rt.anchoredPosition = pos;
            SetButtonLabel(g, label);
            AttachShield(g, onClick);
            if (menu != null) StartKeepText(menu, g, label);
            return g;
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[Coop] 克隆按钮失败: {e.Message}");
            return null;
        }
    }

    /// <summary>降级:手动手感的深色按钮(无模板时)</summary>
    static Button MakeStyledButton(Transform parent, string label, Vector2 size, Vector2 pos, Font font, Action onClick)
    {
        var go = new GameObject("btn");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.18f, 0.22f, 0.30f, 1f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        try { btn.onClick.AddListener((UnityAction)(() => onClick())); } catch { }
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = size; rt.anchoredPosition = pos;
        var t = MakeText(go.transform, label, 18, Vector2.zero, size, font, TextAnchor.MiddleCenter);
        t.color = new Color(0.92f, 0.95f, 1f);
        var trt = t.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        return btn;
    }

    static void TogglePanel()
    {
        if (_panelGo == null) return;
        _panelGo.SetActive(!_panelGo.activeSelf);
        if (_panelGo.activeSelf)
        {
            CoopClient.TryConnect();   // 打开面板时才连接服务器(单机零打扰)
            // 打开瞬间校正按钮文字(本地化系统会把克隆按钮的文字刷回"载入游戏")
            SetButtonLabel(_createBtnGo, "创建房间");
            SetButtonLabel(_joinBtnGo, "加入房间");
            Plugin.Log.LogInfo("[Coop] 联机面板已打开");
        }
    }

    // ================= 根 / 文字 / 输入框 =================

    static void EnsureRoot()
    {
        if (_root != null) return;
        _root = new GameObject("CoopUI");
        UnityEngine.Object.DontDestroyOnLoad(_root);
        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        _root.AddComponent<GraphicRaycaster>();
        try
        {
            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        }
        catch { }
    }

    static Text MakeText(Transform parent, string text, int size, Vector2 pos, Vector2 sizeDelta, Font font, TextAnchor align)
    {
        var go = new GameObject("text");
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = Color.white;
        t.alignment = align;
        t.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = sizeDelta; rt.anchoredPosition = pos;
        return t;
    }

    static InputField MakeInput(Transform parent, Vector2 pos, Vector2 size, Font font, string placeholder)
    {
        var go = new GameObject("input");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.07f, 0.08f, 0.12f, 1f);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = size; rt.anchoredPosition = pos;
        var input = go.AddComponent<InputField>();

        var textGo = new GameObject("text");
        textGo.transform.SetParent(go.transform, false);
        var text = textGo.AddComponent<Text>();
        text.font = font; text.fontSize = 20; text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.supportRichText = false;
        var trt = textGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(8f, 2f); trt.offsetMax = new Vector2(-8f, -2f);

        var phGo = new GameObject("placeholder");
        phGo.transform.SetParent(go.transform, false);
        var ph = phGo.AddComponent<Text>();
        ph.font = font; ph.fontSize = 18;
        ph.color = new Color(1f, 1f, 1f, 0.30f);
        ph.text = placeholder;
        ph.alignment = TextAnchor.MiddleCenter;
        var phrt = phGo.GetComponent<RectTransform>();
        phrt.anchorMin = Vector2.zero; phrt.anchorMax = Vector2.one;
        phrt.offsetMin = new Vector2(8f, 2f); phrt.offsetMax = new Vector2(-8f, -2f);

        input.textComponent = text;
        input.placeholder = ph;
        input.characterLimit = 4;
        return input;
    }
}
