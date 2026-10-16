using System;
using Coop;
using HarmonyLib;

namespace ProbablyStolenCoop;

/// <summary>
/// 手写的界面同步扩展 —— 处理"需要文本参数"或"打开前要先设置状态"的界面。
/// 由 UiWindowSync(生成的) 在分发前优先询问本类。
/// </summary>
public static class UiWindowCustom
{
    /// <summary>返回 true 表示本类已处理该界面键</summary>
    public static bool Dispatch(string key, bool open, GameItem item, int idx,
                                string t1, string t2, string t3)
    {
        switch (key)
        {
            case "paper_text":          // 纸张/文书(标题+正文+落款,文本版)
            {
                var ui = PaperUIManager.Instance;
                if (ui == null) return false;
                if (open) ui.OpenUI(t1 ?? "", t2 ?? "", t3 ?? "", idx <= 0 ? 1 : idx);
                else ui.CloseUI();
                return true;
            }
            case "negociation":         // 议价:先把"谈的是哪一件"摆上,再开窗
            {
                var ui = NegociationUIManager.Instance;
                if (ui == null) return false;
                if (open)
                {
                    if (item != null)
                    {
                        try { ui.currentNegociatedItem = item; } catch { }
                    }
                    ui.OpenUI();
                }
                else ui.CloseUI();
                return true;
            }
        }
        return false;
    }
}

// ---- 钩子:纸张/文书(文本版) ----

[HarmonyPatch(typeof(PaperUIManager), nameof(PaperUIManager.OpenUI))]
public static class PaperTextOpenPatch
{
    public static void Postfix(string title, string body, string signature, int pageType)
    {
        UiWindowSync.ReportOpen("paper_text", null, null, pageType,
                                title ?? "", body ?? "", signature ?? "");
    }
}

[HarmonyPatch(typeof(PaperUIManager), nameof(PaperUIManager.CloseUI))]
public static class PaperTextClosePatch
{
    public static void Postfix() { UiWindowSync.ReportClose("paper_text"); }
}

// ---- 钩子:议价 ----

[HarmonyPatch(typeof(NegociationUIManager), nameof(NegociationUIManager.OpenUI))]
public static class NegociationOpenPatch
{
    public static void Postfix(NegociationUIManager __instance)
    {
        GameItem it = null;
        try { it = __instance != null ? __instance.currentNegociatedItem : null; } catch { }
        if (it != null) UiWindowSync.ReportOpen("negociation", it);
        else UiWindowSync.ReportOpen("negociation", null);
    }
}

[HarmonyPatch(typeof(NegociationUIManager), nameof(NegociationUIManager.CloseUI))]
public static class NegociationClosePatch
{
    public static void Postfix() { UiWindowSync.ReportClose("negociation"); }
}
