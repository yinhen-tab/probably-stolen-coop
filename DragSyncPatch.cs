using HarmonyLib;
using UnityEngine.EventSystems;

namespace ProbablyStolenCoop;

/// <summary>
/// 拖拽状态同步(视觉):hook 物品节点的拖拽开始/结束 → 上报队友
/// 队友画面显示"拖动中"幽灵跟随其光标
/// </summary>
public static class DragSync
{
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
        try
        {
            var el = rend != null ? rend.element : null;
            var ge = el != null ? el.TryCast<GameItemElement>() : null;
            if (ge != null)
            {
                uid = ge.uniqueId;
                ident = ge.identifier ?? "";
                if (string.IsNullOrEmpty(hint)) hint = ident;
            }
        }
        catch { }
        CoopLog.Detail($"[Coop] 本地拖拽: {(dragging ? "开始" : "结束")} {ident} uid={uid}");
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
