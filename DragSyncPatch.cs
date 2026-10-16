using HarmonyLib;
using UnityEngine.EventSystems;

namespace ProbablyStolenCoop;

/// <summary>
/// 拖拽状态同步(视觉):hook 物品节点的拖拽开始/结束 → 上报队友
/// 队友画面显示"拖动中"幽灵跟随其光标
/// </summary>
public static class DragSync
{
    /// <summary>最近在物品节点上按下的那一件(拖拽身份兜底:OnBeginDrag 的节点有时拿不到物品)</summary>
    private static GameItem _lastPressed;

    public static void NotePressed(TreeNodeRender rend)
    {
        try
        {
            var el = rend != null ? rend.element : null;
            var ge = el != null ? el.TryCast<GameItemElement>() : null;
            if (ge != null) _lastPressed = ge;
        }
        catch { }
    }

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
        string why = "";
        try
        {
            var el = rend != null ? rend.element : null;
            var ge = el != null ? el.TryCast<GameItemElement>() : null;
            GameItem gi = ge;                                  // GameItemElement : GameItem
            if (gi == null && dragging && _lastPressed != null)
            {
                gi = _lastPressed;                             // 兜底:用"按下时记下的那一件"
                why = " [用按下记录的物品兜底]";
            }
            if (gi != null)
            {
                uid = gi.uniqueId;
                ident = gi.identifier ?? "";
                if (string.IsNullOrEmpty(hint)) hint = ident;
            }
            else why = $" [取不到物品: element={(el == null ? "null" : "非 GameItemElement")}]";
        }
        catch (Exception e) { why = " [取物品异常: " + e.Message + "]"; }
        // 提到 Info:这条是排查"拖动不跟随"的唯一线索,Detail 级在"正常"模式下看不见
        CoopLog.Info($"[Coop] 本地拖拽{(dragging ? "开始" : "结束")}: uid={uid} ident='{ident}'{why}");
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

[HarmonyPatch(typeof(TreeNodeRender), "OnPointerDown")]
public static class TreeNodePointerDownPatch
{
    public static void Postfix(TreeNodeRender __instance) { DragSync.NotePressed(__instance); }
}
