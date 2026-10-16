using System;
using System.Collections.Generic;
using Coop;
using HarmonyLib;
using UnityEngine;

namespace ProbablyStolenCoop;

/// <summary>
/// 通用界面同步(表驱动,自动生成 —— 见 reverse/gen_ui_sync.py)。
/// 覆盖"按物品打开"的机器/物品界面与"开关式"界面:
///   上报 wnd_open/wnd_close + arg=界面键(+ item_id 指向具体物品, 或有 idx)
///   接收端按同一个键调用对应管理器的 Open/Close, 内容由本端同一份数据提供, 天然一致
/// 刻意不同步: 设置/暂停菜单/确认弹窗/存档界面/Mod 界面/教程提示(这些不属于"共享的店铺世界")
/// </summary>
public static class UiWindowSync
{
    public static bool Applying;

    private static readonly Dictionary<string, string> _lastItem = new();

    /// <summary>本地打开了某个界面</summary>
    public static void ReportOpen(string key, GameItem item, GameItem item2 = null, int idx = -1,
                                  string t1 = "", string t2 = "", string t3 = "")
    {
        try
        {
            if (Applying) return;
            if (CoopClient.Net == null || !CoopClient.Handshaked) return;
            if (string.IsNullOrEmpty(CoopClient.RoomCode)) return;
            string id = "";
            if (item != null) { id = item.identifier ?? ""; _lastItem[key] = id; }
            string id2 = item2 != null ? (item2.identifier ?? "") : "";
            CoopClient.SendUiWindow(true, key, id, idx, id2, t1, t2, t3);
            CoopLog.Info($"[Coop] 本地打开界面: {key}{(string.IsNullOrEmpty(id) ? "" : " (" + id + ")")}");
        }
        catch (Exception e) { CoopLog.Warn($"[Coop] 界面上报异常: {e.Message}"); }
    }

    /// <summary>本地关闭了某个界面</summary>
    public static void ReportClose(string key)
    {
        try
        {
            if (Applying) return;
            if (CoopClient.Net == null || !CoopClient.Handshaked) return;
            if (string.IsNullOrEmpty(CoopClient.RoomCode)) return;
            string id = _lastItem.TryGetValue(key, out var v) ? v : "";
            CoopClient.SendUiWindow(false, key, id, -1);
            CoopLog.Info($"[Coop] 本地关闭界面: {key}");
        }
        catch (Exception e) { CoopLog.Warn($"[Coop] 界面上报异常: {e.Message}"); }
    }

    /// <summary>接收方:按同一个键打开/关闭界面</summary>
    public static void Apply(Coop.ObjectUse msg)
    {
        bool open = msg.Kind == "wnd_open";
        string key = msg.Arg ?? "";
        GameItem item = null;
        GameItem item2 = null;
        if (!string.IsNullOrEmpty(msg.ItemId2)) item2 = ItemMoveSync.FindItem(0, msg.ItemId2);
        if (!string.IsNullOrEmpty(msg.ItemId))
        {
            item = ItemMoveSync.FindItem(0, msg.ItemId);
            if (item == null)
            {
                CoopLog.Info($"[Coop] 界面同步: 本地没有物品 {msg.ItemId},跳过界面 {key}");
                return;
            }
        }
        bool prevWin = WindowSync.ApplyingRemote;
        Applying = true;
        WindowSync.ApplyingRemote = true;
        try
        {
            if (!Dispatch(key, open, item, item2, msg.Idx, msg.Text1, msg.Text2, msg.Text3))
                CoopLog.Detail($"[Coop] 界面同步: 未处理的界面键 {key}");
            else
                CoopLog.Info($"[Coop] 已{(open ? "打开" : "关闭")}队友的界面: {key}");
        }
        catch (Exception e) { CoopLog.Warn($"[Coop] 界面同步执行异常({key}): {e.Message}"); }
        finally
        {
            WindowSync.ApplyingRemote = prevWin;
            Applying = false;
        }
    }

    /// <summary>界面键 → 具体管理器(生成的 switch)</summary>
    static bool Dispatch(string key, bool open, GameItem item, GameItem item2, int idx, string t1 = "", string t2 = "", string t3 = "")
    {
        // 需要文本参数/要先设置状态的界面交给手写扩展
        if (UiWindowCustom.Dispatch(key, open, item, idx, t1, t2, t3)) return true;
        switch (key)
        {
            case "chemfinisher":
            {
                var ui = ChemFinisherUIManager.Instance;
                if (ui == null) return false;
                if (open) ui.OpenUI(item); else ui.HideUI();
                return true;
            }
            case "chem_scanner":
            {
                var ui = ChemScannerUIManager.Instance;
                if (ui == null) return false;
                if (open) ui.Open(item, item2); else ui.Close();
                return true;
            }
            case "water_scanner":
            {
                var ui = WaterScannerUIManager.Instance;
                if (ui == null) return false;
                if (open) ui.Open(item, item2); else ui.Close();
                return true;
            }
            case "water_strip":
            {
                var ui = WaterStripUIManager.Instance;
                if (ui == null) return false;
                if (open) ui.Open(item); else ui.Close();
                return true;
            }
            case "exchange":
            {
                var ui = ExchangeUIManager.Instance;
                if (ui == null) return false;
                if (open) ui.OpenPanel(item); else ui.ClosePanel();
                return true;
            }
            case "labeler":
            {
                var ui = LabelerUIManager.Instance;
                if (ui == null) return false;
                if (open) ui.OpenUI(item); else ui.CloseUI();
                return true;
            }
            case "labeler_blue":
            {
                var ui = LabelerBlueUIManager.Instance;
                if (ui == null) return false;
                if (open) ui.OpenUI(item); else ui.CloseUI();
                return true;
            }
            case "plant_analyser":
            {
                var ui = PlantAnalyserUIManager.Instance;
                if (ui == null) return false;
                if (open) ui.OpenUI(item); else ui.HideUI();
                return true;
            }
            case "scratcher":
            {
                var ui = ScratcherUIManager.Instance;
                if (ui == null) return false;
                if (open) ui.OpenUI(item); else ui.CloseUI();
                return true;
            }
            case "confront_cig":
            {
                var ui = InspectionUIManager.Instance;
                if (ui == null) return false;
                if (open) ui.OpenCigaretteConfrontPanel(item); else ui.CloseConfrontPanel();
                return true;
            }
            case "confront_stamp":
            {
                var ui = InspectionUIManager.Instance;
                if (ui == null) return false;
                if (open) ui.OpenStampConfrontPanel(item); else ui.CloseConfrontPanel();
                return true;
            }
            case "confront_inj":
            {
                var ui = InspectionUIManager.Instance;
                if (ui == null) return false;
                if (open) ui.OpenInjectorConfrontPanel(item); else ui.CloseConfrontPanel();
                return true;
            }
            case "notebook":
            {
                var ui = NotebookUIManager.Instance;
                if (ui == null) return false;
                if (open) ui.OpenUI(); else ui.CloseUI();
                return true;
            }
            case "recorder_ui":
            {
                var ui = RecorderUIManager.Instance;
                if (ui == null) return false;
                if (open) ui.OpenUI(); else ui.CloseUI();
                return true;
            }
            case "record_ui":
            {
                var ui = RecordUIManager.Instance;
                if (ui == null) return false;
                if (open) ui.OpenUI(); else ui.CloseUI();
                return true;
            }
            case "map":
            {
                var ui = MapUIManager.Instance;
                if (ui == null) return false;
                if (open) ui.OpenUI(); else ui.CloseUI();
                return true;
            }
            case "commissary":
            {
                var ui = CommissaryUIManager.Instance;
                if (ui == null) return false;
                if (open) ui.OpenUI(); else ui.CloseUI();
                return true;
            }
            case "clientele":
            {
                var ui = ClienteleUIManager.Instance;
                if (ui == null) return false;
                if (open) ui.OpenUI(); else ui.CloseUI();
                return true;
            }
            case "bargain":
            {
                var ui = BargainUIManager.Instance;
                if (ui == null) return false;
                if (open) ui.OpenUI(); else ui.CloseUI();
                return true;
            }
            case "reputation":
            {
                var ui = ReputationUIManager.Instance;
                if (ui == null) return false;
                if (open) ui.OpenUI(); else ui.CloseUI();
                return true;
            }
            case "loan":
            {
                var ui = LoanPanelUIManager.Instance;
                if (ui == null) return false;
                if (open) ui.OpenUI(); else ui.CloseUI();
                return true;
            }
            case "perk":
            {
                var ui = PerkUIController.Instance;
                if (ui == null) return false;
                if (open) ui.OpenUI(); else ui.CloseUI();
                return true;
            }
            case "inspection":
            {
                var ui = InspectionUIManager.Instance;
                if (ui == null) return false;
                if (open) ui.OpenUI(); else ui.CloseUI();
                return true;
            }
            case "calendar":
            {
                var ui = AdvCalendarUIManager.Instance;
                if (ui == null) return false;
                if (open) ui.OpenUI(); else ui.CloseUI();
                return true;
            }
            case "dialog_setting":
            {
                var ui = DialogSettingUIManager.Instance;
                if (ui == null) return false;
                if (open) ui.OpenUI(); else ui.CloseUI();
                return true;
            }
            case "wild":
            {
                var ui = WildUIManager.Instance;
                if (ui == null) return false;
                if (open) ui.OpenUI(false); else ui.CloseUI();
                return true;
            }
            case "republic_logo":
            {
                var ui = RepublicLogoUIManager.Instance;
                if (ui == null) return false;
                if (open) ui.OpenPanel(); else ui.ClosePanel();
                return true;
            }
            case "start_of_day":
            {
                var ui = StartOfDayUIManager.Instance;
                if (ui == null) return false;
                if (open) ui.OpenUI(); else ui.HideUI();
                return true;
            }
            case "paper_book":
            {
                var ui = PaperUIManager.Instance;
                if (ui == null) return false;
                if (open) ui.OpenBook(item); else ui.CloseBook();
                return true;
            }
            case "paper_slip":
            {
                var ui = PaperUIManager.Instance;
                if (ui == null) return false;
                if (open) ui.OpenSlipUI(item); else ui.CloseSlipUI();
                return true;
            }
            case "paper_gen_slip":
            {
                var ui = PaperUIManager.Instance;
                if (ui == null) return false;
                if (open) ui.OpenGeneralSlipUI(item); else ui.CloseGeneralSlipUI();
                return true;
            }
            case "paper_index":
            {
                var ui = PaperUIManager.Instance;
                if (ui == null) return false;
                if (open) ui.OpenIndex(); else ui.CloseIndex();
                return true;
            }
            case "log":
            {
                var ui = LogUIManager.Instance;
                if (ui == null) return false;
                if (open) ui.OpenUI(item); else ui.CloseUI();
                return true;
            }
            case "handnote":
            {
                var ui = HandnoteUIManager.Instance;
                if (ui == null) return false;
                if (open) ui.OpenPanel(item); else ui.ClosePanel();
                return true;
            }
            default: return false;
        }
    }

// ================= 自动生成的钩子 =================

[HarmonyPatch(typeof(ChemFinisherUIManager), nameof(ChemFinisherUIManager.OpenUI))]
public static class UiOpen_chemfinisher
{
    public static void Postfix(GameItem __0) { UiWindowSync.ReportOpen("chemfinisher", __0); }
}

[HarmonyPatch(typeof(ChemFinisherUIManager), nameof(ChemFinisherUIManager.HideUI))]
public static class UiClose_HideUI
{
    public static void Postfix() { UiWindowSync.ReportClose("chemfinisher"); }
}

[HarmonyPatch(typeof(ChemScannerUIManager), nameof(ChemScannerUIManager.Open))]
public static class UiOpen_chem_scanner
{
    public static void Postfix(GameItem __0, GameItem __1) { UiWindowSync.ReportOpen("chem_scanner", __0, __1); }
}

[HarmonyPatch(typeof(ChemScannerUIManager), nameof(ChemScannerUIManager.Close))]
public static class UiClose_Close
{
    public static void Postfix() { UiWindowSync.ReportClose("chem_scanner"); }
}

[HarmonyPatch(typeof(WaterScannerUIManager), nameof(WaterScannerUIManager.Open))]
public static class UiOpen_water_scanner
{
    public static void Postfix(GameItem __0, GameItem __1) { UiWindowSync.ReportOpen("water_scanner", __0, __1); }
}

[HarmonyPatch(typeof(WaterStripUIManager), nameof(WaterStripUIManager.Open))]
public static class UiOpen_water_strip
{
    public static void Postfix(GameItem __0) { UiWindowSync.ReportOpen("water_strip", __0); }
}

[HarmonyPatch(typeof(ExchangeUIManager), nameof(ExchangeUIManager.OpenPanel))]
public static class UiOpen_exchange
{
    public static void Postfix(GameItem __0) { UiWindowSync.ReportOpen("exchange", __0); }
}

[HarmonyPatch(typeof(ExchangeUIManager), nameof(ExchangeUIManager.ClosePanel))]
public static class UiClose_ClosePanel
{
    public static void Postfix() { UiWindowSync.ReportClose("exchange"); }
}

[HarmonyPatch(typeof(LabelerUIManager), nameof(LabelerUIManager.OpenUI))]
public static class UiOpen_labeler
{
    public static void Postfix(GameItem __0) { UiWindowSync.ReportOpen("labeler", __0); }
}

[HarmonyPatch(typeof(LabelerUIManager), nameof(LabelerUIManager.CloseUI))]
public static class UiClose_CloseUI
{
    public static void Postfix() { UiWindowSync.ReportClose("labeler"); }
}

[HarmonyPatch(typeof(LabelerBlueUIManager), nameof(LabelerBlueUIManager.OpenUI))]
public static class UiOpen_labeler_blue
{
    public static void Postfix(GameItem __0) { UiWindowSync.ReportOpen("labeler_blue", __0); }
}

[HarmonyPatch(typeof(PlantAnalyserUIManager), nameof(PlantAnalyserUIManager.OpenUI))]
public static class UiOpen_plant_analyser
{
    public static void Postfix(GameItem __0) { UiWindowSync.ReportOpen("plant_analyser", __0); }
}

[HarmonyPatch(typeof(ScratcherUIManager), nameof(ScratcherUIManager.OpenUI))]
public static class UiOpen_scratcher
{
    public static void Postfix(GameItem __0) { UiWindowSync.ReportOpen("scratcher", __0); }
}

[HarmonyPatch(typeof(InspectionUIManager), nameof(InspectionUIManager.OpenCigaretteConfrontPanel))]
public static class UiOpen_confront_cig
{
    public static void Postfix(GameItem __0) { UiWindowSync.ReportOpen("confront_cig", __0); }
}

[HarmonyPatch(typeof(InspectionUIManager), nameof(InspectionUIManager.CloseConfrontPanel))]
public static class UiClose_CloseConfrontPanel
{
    public static void Postfix() { UiWindowSync.ReportClose("confront_cig"); }
}

[HarmonyPatch(typeof(InspectionUIManager), nameof(InspectionUIManager.OpenStampConfrontPanel))]
public static class UiOpen_confront_stamp
{
    public static void Postfix(GameItem __0) { UiWindowSync.ReportOpen("confront_stamp", __0); }
}

[HarmonyPatch(typeof(InspectionUIManager), nameof(InspectionUIManager.OpenInjectorConfrontPanel))]
public static class UiOpen_confront_inj
{
    public static void Postfix(GameItem __0) { UiWindowSync.ReportOpen("confront_inj", __0); }
}

[HarmonyPatch(typeof(NotebookUIManager), nameof(NotebookUIManager.OpenUI))]
public static class UiOpen_notebook
{
    public static void Postfix() { UiWindowSync.ReportOpen("notebook", null); }
}

[HarmonyPatch(typeof(RecorderUIManager), nameof(RecorderUIManager.OpenUI))]
public static class UiOpen_recorder_ui
{
    public static void Postfix() { UiWindowSync.ReportOpen("recorder_ui", null); }
}

[HarmonyPatch(typeof(RecordUIManager), nameof(RecordUIManager.OpenUI))]
public static class UiOpen_record_ui
{
    public static void Postfix() { UiWindowSync.ReportOpen("record_ui", null); }
}

[HarmonyPatch(typeof(MapUIManager), nameof(MapUIManager.OpenUI))]
public static class UiOpen_map
{
    public static void Postfix() { UiWindowSync.ReportOpen("map", null); }
}

[HarmonyPatch(typeof(CommissaryUIManager), nameof(CommissaryUIManager.OpenUI))]
public static class UiOpen_commissary
{
    public static void Postfix() { UiWindowSync.ReportOpen("commissary", null); }
}

[HarmonyPatch(typeof(ClienteleUIManager), nameof(ClienteleUIManager.OpenUI))]
public static class UiOpen_clientele
{
    public static void Postfix() { UiWindowSync.ReportOpen("clientele", null); }
}

[HarmonyPatch(typeof(BargainUIManager), nameof(BargainUIManager.OpenUI))]
public static class UiOpen_bargain
{
    public static void Postfix() { UiWindowSync.ReportOpen("bargain", null); }
}

[HarmonyPatch(typeof(ReputationUIManager), nameof(ReputationUIManager.OpenUI))]
public static class UiOpen_reputation
{
    public static void Postfix() { UiWindowSync.ReportOpen("reputation", null); }
}

[HarmonyPatch(typeof(LoanPanelUIManager), nameof(LoanPanelUIManager.OpenUI))]
public static class UiOpen_loan
{
    public static void Postfix() { UiWindowSync.ReportOpen("loan", null); }
}

[HarmonyPatch(typeof(PerkUIController), nameof(PerkUIController.OpenUI))]
public static class UiOpen_perk
{
    public static void Postfix() { UiWindowSync.ReportOpen("perk", null); }
}

[HarmonyPatch(typeof(InspectionUIManager), nameof(InspectionUIManager.OpenUI))]
public static class UiOpen_inspection
{
    public static void Postfix() { UiWindowSync.ReportOpen("inspection", null); }
}

[HarmonyPatch(typeof(AdvCalendarUIManager), nameof(AdvCalendarUIManager.OpenUI))]
public static class UiOpen_calendar
{
    public static void Postfix() { UiWindowSync.ReportOpen("calendar", null); }
}

[HarmonyPatch(typeof(DialogSettingUIManager), nameof(DialogSettingUIManager.OpenUI))]
public static class UiOpen_dialog_setting
{
    public static void Postfix() { UiWindowSync.ReportOpen("dialog_setting", null); }
}

[HarmonyPatch(typeof(WildUIManager), nameof(WildUIManager.OpenUI))]
public static class UiOpen_wild
{
    public static void Postfix() { UiWindowSync.ReportOpen("wild", null); }
}

[HarmonyPatch(typeof(RepublicLogoUIManager), nameof(RepublicLogoUIManager.OpenPanel))]
public static class UiOpen_republic_logo
{
    public static void Postfix() { UiWindowSync.ReportOpen("republic_logo", null); }
}

[HarmonyPatch(typeof(StartOfDayUIManager), nameof(StartOfDayUIManager.OpenUI))]
public static class UiOpen_start_of_day
{
    public static void Postfix() { UiWindowSync.ReportOpen("start_of_day", null); }
}

[HarmonyPatch(typeof(PaperUIManager), nameof(PaperUIManager.OpenBook))]
public static class UiOpen_paper_book
{
    public static void Postfix(GameItem __0) { UiWindowSync.ReportOpen("paper_book", __0); }
}

[HarmonyPatch(typeof(PaperUIManager), nameof(PaperUIManager.CloseBook))]
public static class UiClose_CloseBook
{
    public static void Postfix() { UiWindowSync.ReportClose("paper_book"); }
}

[HarmonyPatch(typeof(PaperUIManager), nameof(PaperUIManager.OpenSlipUI))]
public static class UiOpen_paper_slip
{
    public static void Postfix(GameItem __0) { UiWindowSync.ReportOpen("paper_slip", __0); }
}

[HarmonyPatch(typeof(PaperUIManager), nameof(PaperUIManager.CloseSlipUI))]
public static class UiClose_CloseSlipUI
{
    public static void Postfix() { UiWindowSync.ReportClose("paper_slip"); }
}

[HarmonyPatch(typeof(PaperUIManager), nameof(PaperUIManager.OpenGeneralSlipUI))]
public static class UiOpen_paper_gen_slip
{
    public static void Postfix(GameItem __0) { UiWindowSync.ReportOpen("paper_gen_slip", __0); }
}

[HarmonyPatch(typeof(PaperUIManager), nameof(PaperUIManager.CloseGeneralSlipUI))]
public static class UiClose_CloseGeneralSlipUI
{
    public static void Postfix() { UiWindowSync.ReportClose("paper_gen_slip"); }
}

[HarmonyPatch(typeof(PaperUIManager), nameof(PaperUIManager.OpenIndex))]
public static class UiOpen_paper_index
{
    public static void Postfix() { UiWindowSync.ReportOpen("paper_index", null); }
}

[HarmonyPatch(typeof(PaperUIManager), nameof(PaperUIManager.CloseIndex))]
public static class UiClose_CloseIndex
{
    public static void Postfix() { UiWindowSync.ReportClose("paper_index"); }
}

[HarmonyPatch(typeof(LogUIManager), nameof(LogUIManager.OpenUI))]
public static class UiOpen_log
{
    public static void Postfix(GameItem __0) { UiWindowSync.ReportOpen("log", __0); }
}

[HarmonyPatch(typeof(HandnoteUIManager), nameof(HandnoteUIManager.OpenPanel))]
public static class UiOpen_handnote
{
    public static void Postfix(GameItem __0) { UiWindowSync.ReportOpen("handnote", __0); }
}
}
