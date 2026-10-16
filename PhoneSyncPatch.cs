using System;
using HarmonyLib;

namespace ProbablyStolenCoop;

/// <summary>
/// 电话同步(A 界面镜像 + B 通话可见):
/// - A: 拨号盘(PhoneUIManager.OpenUI/CloseUI)与电话簿(PhoneBookUIManager)的开关镜像,
///      对方打开/关闭电话界面时本地同步弹出/收起同一界面。
/// - B: 拨打(HandleCall 号码)/挂断(StopCall)镜像 —— 号码驱动,
///      两端各自用同一份游戏数据播放同一段通话,天然一致(与顾客队列同思路)。
/// 防回环:ApplyingRemote 期间不上报。
/// </summary>
public static class PhoneSync
{
    public static bool ApplyingRemote;

    /// <summary>本地电话操作上报</summary>
    public static void Report(string kind, long number = 0, string arg = "", int index = -1)
    {
        try
        {
            if (ApplyingRemote) return;
            if (CoopClient.Net == null || !CoopClient.Handshaked) return;
            if (string.IsNullOrEmpty(CoopClient.RoomCode)) return;
            CoopClient.SendPhoneSync(kind, number, arg, index);
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop] 电话上报异常: {e.Message}"); }
    }

    /// <summary>接收方:镜像队友的电话操作</summary>
    public static void ApplyRemote(Coop.PhoneSync m)
    {
        try
        {
            var ui = PhoneUIManager.Instance;
            if (ui == null) return;
            ApplyingRemote = true;
            bool prevBlock = CoopClient.BlockGameEffects;
            // 通话效果规则:默认"观看"→ 屏蔽本地效果钩子,避免对方通话污染本端状态
            if (!CoopClient.PhoneEffectOn) CoopClient.BlockGameEffects = true;
            try
            {
                switch (m.Kind)
                {
                    case "open":
                        ui.OpenUI();
                        CoopLog.Info("[Coop] 队友打开了电话");
                        break;
                    case "close":
                        ui.CloseUI();
                        break;
                    case "book_open":
                        if (ui.phoneBookUIManager != null) ui.phoneBookUIManager.OpenUI();
                        break;
                    case "book_close":
                        if (ui.phoneBookUIManager != null) ui.phoneBookUIManager.CloseUI();
                        break;
                    case "call":
                        CoopLog.Info($"[Coop] 队友正在拨打 {m.Number}" +
                            (CoopClient.PhoneEffectOn ? "" : "(观看模式:不产生效果)"));
                        ui.HandleCall(m.Number);
                        break;
                    case "hangup":
                        ui.StopCall();
                        break;

                    // ---- 通话对话面板(打电话给联系人后弹出的那个面板) ----
                    case "dlg_start":
                    {
                        var dlgUi = DialoguePhoneUIManager.Instance;
                        if (dlgUi == null) { CoopLog.Warn("[Coop] 对话面板实例为空,无法镜像"); break; }
                        if (dlgUi.isConversationActiveFlag != 0)
                        {
                            CoopLog.Info($"[Coop] 队友打开了对话面板({m.Arg}),本端已在对话中,跳过");
                            break;
                        }
                        // 走本地同一条通话流程,让面板由游戏自己打开(数据同源)
                        CoopLog.Info($"[Coop] 队友打开了对话面板({m.Arg}) → 本地触发同流程");
                        try { ui.StartCalling(); }
                        catch (Exception e) { CoopLog.Warn($"[Coop] 本地触发对话失败: {e.Message}"); }
                        break;
                    }
                    // ---- 店内顾客对话(DialogUIManager;电话版是 DialoguePhoneUIManager) ----
                    case "dlg2_choice":
                        if (DialogUIManager.Instance != null && m.Index >= 0)
                        {
                            DialogUIManager.Instance.SelectChoice(m.Index);
                            CoopLog.Info($"[Coop] 队友选了店内对话选项 {m.Index}(本端同步)");
                        }
                        break;
                    case "dlg2_end":
                        if (DialogUIManager.Instance != null)
                        {
                            DialogUIManager.Instance.HideAllDialogUI();
                            CoopLog.Info("[Coop] 队友结束了店内对话");
                        }
                        break;
                    case "dlg2_start":
                        CoopLog.Info($"[Coop] 队友在与顾客对话({m.Arg})");
                        break;

                    case "dlg_end":
                        if (DialoguePhoneUIManager.Instance != null)
                        {
                            DialoguePhoneUIManager.Instance.EndConversation();
                            CoopLog.Info("[Coop] 队友结束了通话对话");
                        }
                        break;
                    case "dlg_choice":
                        if (DialoguePhoneUIManager.Instance != null && m.Index >= 0)
                        {
                            DialoguePhoneUIManager.Instance.SelectChoice(m.Index);
                            CoopLog.Info($"[Coop] 队友选择了对话选项 {m.Index}(本端同步选择)");
                        }
                        break;
                }
            }
            finally
            {
                CoopClient.BlockGameEffects = prevBlock;
                ApplyingRemote = false;
            }
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop] 电话同步执行异常: {e.Message}"); }
    }
}

// ---- 钩子:拨号盘开关 ----

[HarmonyPatch(typeof(PhoneUIManager), nameof(PhoneUIManager.OpenUI))]
public static class PhoneOpenPatch
{
    public static void Postfix() { PhoneSync.Report("open"); }
}

[HarmonyPatch(typeof(PhoneUIManager), nameof(PhoneUIManager.CloseUI))]
public static class PhoneClosePatch
{
    public static void Postfix() { PhoneSync.Report("close"); }
}

// ---- 钩子:电话簿开关 ----

[HarmonyPatch(typeof(PhoneBookUIManager), nameof(PhoneBookUIManager.OpenUI))]
public static class PhoneBookOpenPatch
{
    public static void Postfix() { PhoneSync.Report("book_open"); }
}

[HarmonyPatch(typeof(PhoneBookUIManager), nameof(PhoneBookUIManager.CloseUI))]
public static class PhoneBookClosePatch
{
    public static void Postfix() { PhoneSync.Report("book_close"); }
}

// ---- 钩子:拨打/挂断 ----

[HarmonyPatch(typeof(PhoneUIManager), nameof(PhoneUIManager.HandleCall))]
public static class PhoneCallPatch
{
    public static void Postfix(long currentNumber) { PhoneSync.Report("call", currentNumber); }
}

[HarmonyPatch(typeof(PhoneUIManager), nameof(PhoneUIManager.StopCall))]
public static class PhoneStopCallPatch
{
    public static void Postfix() { PhoneSync.Report("hangup"); }
}

// ---- 钩子:通话对话面板(打电话给联系人后弹出的面板) ----

[HarmonyPatch(typeof(DialoguePhoneUIManager), nameof(DialoguePhoneUIManager.StartDialogue))]
public static class PhoneDialogStartPatch
{
    public static void Postfix(Dialogue dialogue)
    {
        string tag = "";
        try
        {
            tag = dialogue?.title ?? "";
            if (string.IsNullOrEmpty(tag)) tag = (dialogue?.plainText ?? "");
            if (tag != null && tag.Length > 30) tag = tag.Substring(0, 30);
        }
        catch { }
        CoopLog.Info($"[Coop] 本地打开通话对话面板: {tag}");
        PhoneSync.Report("dlg_start", 0, tag ?? "");
    }
}

[HarmonyPatch(typeof(DialoguePhoneUIManager), nameof(DialoguePhoneUIManager.EndConversation))]
public static class PhoneDialogEndPatch
{
    public static void Postfix() { PhoneSync.Report("dlg_end"); }
}

[HarmonyPatch(typeof(DialoguePhoneUIManager), nameof(DialoguePhoneUIManager.InterruptDialog))]
public static class PhoneDialogInterruptPatch
{
    public static void Postfix() { PhoneSync.Report("dlg_end"); }
}

[HarmonyPatch(typeof(DialoguePhoneUIManager), nameof(DialoguePhoneUIManager.SelectChoice))]
public static class PhoneDialogChoicePatch
{
    public static void Postfix(int index)
    {
        CoopLog.Info($"[Coop] 本地选择了对话选项 {index}");
        PhoneSync.Report("dlg_choice", 0, "", index);
    }
}

// ---- 钩子:店内顾客对话 ----

[HarmonyPatch(typeof(DialogUIManager), nameof(DialogUIManager.StartDialogue))]
public static class StoreDialogStartPatch
{
    public static void Postfix(Dialogue dialogue)
    {
        string tag = "";
        try
        {
            tag = dialogue?.title ?? "";
            if (string.IsNullOrEmpty(tag)) tag = dialogue?.plainText ?? "";
            if (tag != null && tag.Length > 30) tag = tag.Substring(0, 30);
        }
        catch { }
        CoopLog.Info($"[Coop] 本地开始店内对话: {tag}");
        PhoneSync.Report("dlg2_start", 0, tag ?? "");
    }
}

[HarmonyPatch(typeof(DialogUIManager), nameof(DialogUIManager.SelectChoice))]
public static class StoreDialogChoicePatch
{
    public static void Postfix(int index)
    {
        CoopLog.Info($"[Coop] 本地选择店内对话选项 {index}");
        PhoneSync.Report("dlg2_choice", 0, "", index);
    }
}

[HarmonyPatch(typeof(DialogUIManager), nameof(DialogUIManager.HideAllDialogUI))]
public static class StoreDialogEndPatch
{
    public static void Postfix() { PhoneSync.Report("dlg2_end"); }
}
