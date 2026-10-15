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
    public static void Postfix(PixelWindow __instance) { WindowSync.Report(__instance, true); }
}

[HarmonyPatch(typeof(PixelWindow), "Show", new[] { typeof(Vector2), typeof(Vector2), typeof(bool) })]
public static class PixelWindowShow2Patch
{
    public static void Postfix(PixelWindow __instance) { WindowSync.Report(__instance, true); }
}

[HarmonyPatch(typeof(PixelWindow), "Hide", new Type[] { })]
public static class PixelWindowHidePatch
{
    public static void Postfix(PixelWindow __instance) { WindowSync.Report(__instance, false); }
}
