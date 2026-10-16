"""生成 UiWindowSync.cs:表驱动的通用界面同步
覆盖:所有"按物品打开"与"开关式"的界面(排除设置/暂停/确认弹窗/菜单等不该同步的)

用法: python gen_ui_sync.py
"""
import os

HERE = os.path.dirname(os.path.abspath(__file__))

# key, 类名, 打开方法, 参数类型(item/none/int/bool), 关闭方法
TABLE = [
    # ---- 按物品打开的机器/物品界面 ----
    ('chemfinisher',    'ChemFinisherUIManager', 'OpenUI',  'item', 'HideUI'),
    ('chem_scanner',    'ChemScannerUIManager',  'Open',    'item2', 'Close'),
    ('water_scanner',   'WaterScannerUIManager', 'Open',    'item2', 'Close'),
    ('water_strip',     'WaterStripUIManager',   'Open',    'item', 'Close'),
    ('exchange',        'ExchangeUIManager',     'OpenPanel', 'item', 'ClosePanel'),
    ('labeler',         'LabelerUIManager',      'OpenUI',  'item', 'CloseUI'),
    ('labeler_blue',    'LabelerBlueUIManager',  'OpenUI',  'item', 'CloseUI'),
    ('plant_analyser',  'PlantAnalyserUIManager', 'OpenUI', 'item', 'HideUI'),
    ('scratcher',       'ScratcherUIManager',    'OpenUI',  'item', 'CloseUI'),
    ('confront_cig',    'InspectionUIManager',   'OpenCigaretteConfrontPanel', 'item', 'CloseConfrontPanel'),
    ('confront_stamp',  'InspectionUIManager',   'OpenStampConfrontPanel',     'item', 'CloseConfrontPanel'),
    ('confront_inj',    'InspectionUIManager',   'OpenInjectorConfrontPanel',  'item', 'CloseConfrontPanel'),
    # ---- 开关式界面(无物品参数) ----
    ('notebook',        'NotebookUIManager',     'OpenUI',  'none', 'CloseUI'),
    ('recorder_ui',     'RecorderUIManager',     'OpenUI',  'none', 'CloseUI'),
    ('record_ui',       'RecordUIManager',       'OpenUI',  'none', 'CloseUI'),
    ('map',             'MapUIManager',          'OpenUI',  'none', 'CloseUI'),
    ('commissary',      'CommissaryUIManager',   'OpenUI',  'none', 'CloseUI'),
    ('clientele',       'ClienteleUIManager',    'OpenUI',  'none', 'CloseUI'),
    ('bargain',         'BargainUIManager',      'OpenUI',  'none', 'CloseUI'),
    ('reputation',      'ReputationUIManager',   'OpenUI',  'none', 'CloseUI'),
    ('loan',            'LoanPanelUIManager',    'OpenUI',  'none', 'CloseUI'),
    ('perk',            'PerkUIController',      'OpenUI',  'none', 'CloseUI'),
    ('inspection',      'InspectionUIManager',   'OpenUI',  'none', 'CloseUI'),
    ('calendar',        'AdvCalendarUIManager',  'OpenUI',  'none', 'CloseUI'),
    ('dialog_setting',  'DialogSettingUIManager', 'OpenUI', 'none', 'CloseUI'),
    ('wild',            'WildUIManager',         'OpenUI',  'bool', 'CloseUI'),
    ('republic_logo',   'RepublicLogoUIManager', 'OpenPanel', 'none', 'ClosePanel'),
    # ---- 已手写实现过的,统一并入表驱动(避免重复钩子) ----
    # ---- 第二档:早间报告 / 纸张文书(物品版) ----
    ('start_of_day',    'StartOfDayUIManager',   'OpenUI',  'none', 'HideUI'),
    ('paper_book',      'PaperUIManager',        'OpenBook', 'item', 'CloseBook'),
    ('paper_slip',      'PaperUIManager',        'OpenSlipUI', 'item', 'CloseSlipUI'),
    ('paper_gen_slip',  'PaperUIManager',        'OpenGeneralSlipUI', 'item', 'CloseGeneralSlipUI'),
    ('paper_index',     'PaperUIManager',        'OpenIndex', 'none', 'CloseIndex'),
    ('log',             'LogUIManager',          'OpenUI',  'item', 'CloseUI'),
    ('handnote',        'HandnoteUIManager',     'OpenPanel', 'item', 'ClosePanel'),
]

HEADER = '''using System;
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
'''

FOOTER = '''}
'''


def gen_dispatch():
    L = []
    L.append('    /// <summary>界面键 → 具体管理器(生成的 switch)</summary>')
    L.append('    static bool Dispatch(string key, bool open, GameItem item, GameItem item2, int idx,'
             ' string t1 = "", string t2 = "", string t3 = "")')
    L.append('    {')
    L.append('        // 需要文本参数/要先设置状态的界面交给手写扩展')
    L.append('        if (UiWindowCustom.Dispatch(key, open, item, idx, t1, t2, t3)) return true;')
    L.append('        switch (key)')
    L.append('        {')
    for key, cls, op, kind, cl in TABLE:
        L.append('            case "%s":' % key)
        L.append('            {')
        L.append('                var ui = %s.Instance;' % cls)
        L.append('                if (ui == null) return false;')
        if kind == 'item2':
            L.append('                if (open) ui.%s(item, item2); else ui.%s();' % (op, cl))
        elif kind == 'item':
            L.append('                if (open) ui.%s(item); else ui.%s();' % (op, cl))
        elif kind == 'none':
            L.append('                if (open) ui.%s(); else ui.%s();' % (op, cl))
        elif kind == 'int':
            L.append('                if (open) ui.%s(idx < 0 ? 0 : idx); else ui.%s();' % (op, cl))
        elif kind == 'bool':
            L.append('                if (open) ui.%s(false); else ui.%s();' % (op, cl))
        L.append('                return true;')
        L.append('            }')
    L.append('            default: return false;')
    L.append('        }')
    L.append('    }')
    return '\n'.join(L)


def gen_patches():
    L = ['']
    L.append('// ================= 自动生成的钩子 =================')
    # 避免同一方法被钩两次(InspectionUIManager 有 3 个不同的 Open,Close 同名 → 合并)
    close_seen = {}
    for i, (key, cls, op, kind, cl) in enumerate(TABLE):
        # 打开钩子
        if kind == 'item2':
            L.append('')
            L.append('[HarmonyPatch(typeof(%s), nameof(%s.%s))]' % (cls, cls, op))
            L.append('public static class UiOpen_%s' % key)
            L.append('{')
            L.append('    public static void Postfix(GameItem a, GameItem b) { UiWindowSync.ReportOpen("%s", a, b); }' % key)
            L.append('}')
        elif kind == 'item':
            L.append('')
            L.append('[HarmonyPatch(typeof(%s), nameof(%s.%s))]' % (cls, cls, op))
            L.append('public static class UiOpen_%s' % key)
            L.append('{')
            L.append('    public static void Postfix(GameItem a) { UiWindowSync.ReportOpen("%s", a); }' % key)
            L.append('}')
        elif kind == 'none':
            L.append('')
            L.append('[HarmonyPatch(typeof(%s), nameof(%s.%s))]' % (cls, cls, op))
            L.append('public static class UiOpen_%s' % key)
            L.append('{')
            L.append('    public static void Postfix() { UiWindowSync.ReportOpen("%s", null); }' % key)
            L.append('}')
        elif kind == 'int':
            L.append('')
            L.append('[HarmonyPatch(typeof(%s), nameof(%s.%s))]' % (cls, cls, op))
            L.append('public static class UiOpen_%s' % key)
            L.append('{')
            L.append('    public static void Postfix(int idx) { UiWindowSync.ReportOpen("%s", null, idx); }' % key)
            L.append('}')
        elif kind == 'bool':
            L.append('')
            L.append('[HarmonyPatch(typeof(%s), nameof(%s.%s))]' % (cls, cls, op))
            L.append('public static class UiOpen_%s' % key)
            L.append('{')
            L.append('    public static void Postfix() { UiWindowSync.ReportOpen("%s", null); }' % key)
            L.append('}')
        # 关闭钩子:同名方法只生成一次(取第一个 key)
        if cl in close_seen:
            continue
        close_seen[cl] = key
        L.append('')
        L.append('[HarmonyPatch(typeof(%s), nameof(%s.%s))]' % (cls, cls, cl))
        L.append('public static class UiClose_%s' % cl)
        L.append('{')
        L.append('    public static void Postfix() { UiWindowSync.ReportClose("%s"); }' % key)
        L.append('}')
    return '\n'.join(L)


if __name__ == '__main__':
    code = HEADER + '\n' + gen_dispatch() + '\n' + gen_patches() + '\n' + FOOTER
    out = os.path.join(HERE, '..', 'UiWindowSync.cs')
    out = os.path.normpath(out)
    open(out, 'w', encoding='utf-8').write(code)
    print('已生成', out, '(%d 行, %d 个界面)' % (len(code.splitlines()), len(TABLE)))
