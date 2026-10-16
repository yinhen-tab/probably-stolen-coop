using System;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace ProbablyStolenCoop;

/// <summary>
/// 窗口同步:hook PixelWindow.Show/Hide → 上报队友;
/// 队友的 UI 事件由 CoopClient.ApplyRemoteUiEvent 执行(ApplyingRemote 防回环)
/// </summary>
public static class WindowSync
{
    /// <summary>正在执行远端 UI 事件(期间不重复上报)</summary>
    public static bool ApplyingRemote;

    private static string _lastPath = "";
    private static bool _lastOpen;
    private static float _lastAt;

    public static void Report(PixelWindow w, bool open)
    {
        if (ApplyingRemote || w == null) return;
        try
        {
            var rt = w.rectTransform;
            if (rt == null) return;
            string path = GetPath(rt);
            if (string.IsNullOrEmpty(path)) return;
            if (path.Contains("Tooltip")) return;   // 提示框不同步(高频且局部,不具共享意义)
            // 去重:同一窗口同状态 0.8 秒内不重复上报
            if (path == _lastPath && open == _lastOpen && Time.time - _lastAt < 0.8f) return;
            _lastPath = path; _lastOpen = open; _lastAt = Time.time;
            string title = "";
            try { title = w.titleString; } catch { }
            title = (title ?? "").Trim();
            if (title.Length == 0 || title == "Node")
                return;   // 无标题/匿名 Node 容器窗口不具共享意义,跳过防刷屏(进场景时批量开关)
            CoopClient.SendUiEvent(path, title, open);
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[Coop] 窗口上报异常: {e.Message}");
        }
    }

    // ---- 窗口实例登记表 ----
    // 为什么需要:远端只能给我们"路径 + 标题"字符串,而 PixelWindow 不是 UnityEngine.Object,
    // 没法用 FindObjectsOfTypeAll 全量扫描(而且窗口往往是懒创建的,没打开过就不存在)。
    // 于是改为:本端每次 Show/Hide 都登记一次实例,远端事件到达时先查这张表。
    private static readonly System.Collections.Generic.Dictionary<string, PixelWindow> _byTitle = new();
    private static readonly System.Collections.Generic.Dictionary<string, PixelWindow> _byPath = new();

    /// <summary>登记窗口实例(Show/Hide 钩子调用)</summary>
    public static void Register(PixelWindow w)
    {
        if (w == null) return;
        try
        {
            string t = "";
            try { t = (w.titleString ?? "").Trim(); } catch { }
            if (!string.IsNullOrEmpty(t)) _byTitle[t] = w;
            var rt = w.rectTransform;
            if (rt != null)
            {
                string p = GetPath(rt);
                if (!string.IsNullOrEmpty(p)) _byPath[p] = w;
            }
        }
        catch { }
    }

    /// <summary>按标题取已登记的窗口实例</summary>
    public static PixelWindow FindRegistered(string title)
    {
        if (string.IsNullOrEmpty(title)) return null;
        PixelWindow w = null;
        if (_byTitle.TryGetValue(title.Trim(), out w) && w != null) return w;
        return null;
    }

    /// <summary>按场景路径取已登记的窗口实例</summary>
    public static PixelWindow FindRegisteredByPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        PixelWindow w = null;
        if (_byPath.TryGetValue(path, out w) && w != null) return w;
        return null;
    }

    /// <summary>场景路径(双方场景一致,可据此互相定位窗口)</summary>
    public static string GetPath(Transform t)
    {
        if (t == null) return "";
        var sb = new StringBuilder();
        var cur = t;
        int guard = 0;
        while (cur != null && guard++ < 32)
        {
            sb.Insert(0, "/" + cur.name);
            cur = cur.parent;
        }
        return sb.ToString();
    }
}

[HarmonyPatch(typeof(PixelWindow), "Show", new Type[] { })]
public static class PixelWindowShowPatch
{
    public static void Postfix(PixelWindow __instance)
    {
        WindowSync.Register(__instance);          // 登记实例(远端按标题/路径回查用)
        WindowSync.Report(__instance, true);
    }
}

[HarmonyPatch(typeof(PixelWindow), "Show", new[] { typeof(Vector2), typeof(Vector2), typeof(bool) })]
public static class PixelWindowShow2Patch
{
    public static void Postfix(PixelWindow __instance)
    {
        WindowSync.Register(__instance);
        WindowSync.Report(__instance, true);
    }
}

[HarmonyPatch(typeof(PixelWindow), "Hide", new Type[] { })]
public static class PixelWindowHidePatch
{
    public static void Postfix(PixelWindow __instance)
    {
        WindowSync.Register(__instance);
        ObjectUseSync.OnWindowHidden(__instance);   // 容器窗口被关 → 上报关闭
        WindowSync.Report(__instance, false);
    }
}
