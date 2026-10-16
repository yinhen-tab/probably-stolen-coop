using System;
using Coop;
using HarmonyLib;

namespace ProbablyStolenCoop;

/// <summary>
/// 店铺随机事件同步(停电/威胁/装饰事件):
/// - 本地 ActivateEvent / DeactivateEvent → 上报事件 identifier
/// - 对端按 identifier 在事件蓝图里找到同一个事件、生成后 ActivateEvent(force:true)
///   两端各自跑同一份事件逻辑,数据同源,天然一致
/// </summary>
public static class StoreEventSync
{
    public static bool Applying;

    public static void Report(bool on, StoreEvent ev)
    {
        try
        {
            if (Applying) return;
            if (ev == null) return;
            string id = ev.identifier ?? "";
            if (string.IsNullOrEmpty(id)) return;
            if (CoopClient.Net == null || !CoopClient.Handshaked) return;
            if (string.IsNullOrEmpty(CoopClient.RoomCode)) return;
            CoopClient.Net.Send(MsgType.ObjectUse, new Coop.ObjectUse
            {
                PlayerId = CoopClient.PlayerId,
                Kind = on ? "event_on" : "event_off",
                Arg = id,
            });
            CoopLog.Info($"[Coop] 本地上报店铺事件: {(on ? "开始" : "结束")} {id} ({ev.displayName})");
        }
        catch (Exception e) { CoopLog.Warn($"[Coop] 事件上报异常: {e.Message}"); }
    }

    /// <summary>在各类事件蓝图里按 identifier 找事件并生成实例</summary>
    static StoreEvent FindByIdentifier(string id)
    {
        Il2CppSystem.Collections.Generic.List<StoreEventBlueprint>[] lists =
        {
            StoreEventManager.normalEventBlueprints,
            StoreEventManager.threatEventBlueprints,
            StoreEventManager.cosmeticEventBlueprints,
            StoreOperationManager.operationBlueprints,
        };
        for (int li = 0; li < lists.Length; li++)
        {
            var list = lists[li];
            if (list == null) continue;
            for (int i = 0; i < list.Count; i++)
            {
                var bp = list[i];
                if (bp == null) continue;
                if ((bp.identifier ?? "") != id) continue;
                try
                {
                    var f = bp.storeEventFunc;
                    if (f != null) return f.Invoke();
                }
                catch (Exception e) { CoopLog.Detail($"[Coop] 事件蓝图生成失败 {id}: {e.Message}"); }
            }
        }
        return null;
    }

    public static void Apply(Coop.ObjectUse msg)
    {
        try
        {
            bool on = msg.Kind == "event_on";
            var st = StoreStation.Instance;          // StoreEventManager 没有 Instance,走 StoreStation
            var mgr = st != null ? st.storeEventManager : null;
            if (mgr == null) { CoopLog.Detail("[Coop] 事件同步: 本端没有事件管理器,跳过"); return; }

            Applying = true;
            try
            {
                if (!on)
                {
                    // 结束:在已激活列表里找同一个事件
                    var act = mgr.GetActiveEvents();
                    if (act != null)
                        for (int i = 0; i < act.Count; i++)
                            if (act[i] != null && (act[i].identifier ?? "") == msg.Arg)
                            {
                                mgr.DeactivateEvent(act[i]);
                                CoopLog.Info($"[Coop] 已结束队友的店铺事件: {msg.Arg}");
                                return;
                            }
                    CoopLog.Detail($"[Coop] 事件同步: 本端未激活 {msg.Arg}");
                    return;
                }
                var ev = FindByIdentifier(msg.Arg);
                if (ev == null) { CoopLog.Info($"[Coop] 事件同步: 本端找不到事件 {msg.Arg}(跳过)"); return; }
                mgr.ActivateEvent(ev, false, true);
                CoopLog.Info($"[Coop] 已同步队友的店铺事件: {msg.Arg} ({ev.displayName})");
            }
            finally { Applying = false; }
        }
        catch (Exception e) { CoopLog.Warn($"[Coop] 事件同步异常: {e.Message}"); }
    }
}

// ---- 钩子:事件激活/结束 ----

[HarmonyPatch(typeof(StoreEventManager), nameof(StoreEventManager.ActivateEvent))]
public static class StoreEventActivatePatch
{
    public static void Postfix(StoreEvent storeEvent) { StoreEventSync.Report(true, storeEvent); }
}

[HarmonyPatch(typeof(StoreEventManager), nameof(StoreEventManager.DeactivateEvent))]
public static class StoreEventDeactivatePatch
{
    public static void Postfix(StoreEvent storeEvent) { StoreEventSync.Report(false, storeEvent); }
}
