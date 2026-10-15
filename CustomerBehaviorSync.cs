using System;
using HarmonyLib;

namespace ProbablyStolenCoop;

/// <summary>
/// 顾客行为全过程同步:把"进店→挑选→成交"的关键行为事件广播给对方,
/// 让对方同一顾客的状态保持一致(此前只同步了"队列是谁 + 叫号/离店")。
/// - introduced : OnIntroduced()       顾客开始被服务
/// - placed     : OnItemPlaced(item)   顾客拿起/放下某件商品
/// - sold       : PerformOnSoldCheck() 某件商品成交(物品+顾客)
/// - done       : CompleteBusiness()   该顾客交易结束
/// 权威规则:房主行为为权威;接收端只把"状态标志"对齐(isIntroduced/isBusinessComplete),
/// 不重复执行经济逻辑(物品增减仍由房主快照统一驱动,避免双算)。
/// </summary>
public static class CustomerBehaviorSync
{
    public static bool ApplyingRemote;

    private const string K_INTRO = "introduced";
    private const string K_PLACED = "placed";
    private const string K_SOLD = "sold";
    private const string K_DONE = "done";

    public static void Report(string clientId, string kind, string detail = "", int amount = 0)
    {
        try
        {
            if (ApplyingRemote) return;
            if (CoopClient.BlockGameEffects) return;   // 观看/回放上下文衍生的行为不再转播
            if (!CoopClient.SyncBehavior) return;
            if (string.IsNullOrEmpty(clientId)) return;
            if (CoopClient.Net == null || !CoopClient.Handshaked) return;
            if (string.IsNullOrEmpty(CoopClient.RoomCode)) return;
            CoopClient.SendClientBehavior(clientId, kind, detail ?? "", amount);
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop] 顾客行为上报异常: {e.Message}"); }
    }

    private static string IdOf(StoreClient c)
    {
        try { return c != null ? (c.identifier ?? "") : ""; } catch { return ""; }
    }

    public static void ReportIntroduced(StoreClient c) => Report(IdOf(c), K_INTRO);
    public static void ReportDone(StoreClient c) => Report(IdOf(c), K_DONE);

    public static void ReportPlaced(StoreClient c, GameItem item)
    {
        string iid = "";
        try { iid = item != null ? (item.identifier ?? "") : ""; } catch { }
        Report(IdOf(c), K_PLACED, iid);
    }

    public static void ReportSold(GameItem item, StoreClient c)
    {
        string iid = "";
        try { iid = item != null ? (item.identifier ?? "") : ""; } catch { }
        Report(IdOf(c), K_SOLD, iid);
    }

    /// <summary>接收方:对齐同一顾客的状态标志(不重跑经济逻辑)</summary>
    public static void ApplyRemote(Coop.ClientBehavior m)
    {
        try
        {
            var ps = PlayerStore.Instance;
            var mgr = ps != null ? ps.storeClientManager : null;
            var stack = mgr != null ? mgr.clientStack : null;
            if (stack == null)
            {
                CoopLog.Info($"[Coop] 顾客行为[{m.Kind}] {m.ClientId}(本地队列未就绪,忽略)");
                return;
            }
            StoreClient target = null;
            for (int i = 0; i < stack.Count; i++)
            {
                var c = stack[i];
                if (c != null && c.identifier == m.ClientId) { target = c; break; }
            }
            if (target == null)
            {
                // 该顾客已不在本地队列(可能本轮已离店):仅记录
                CoopLog.Info($"[Coop] 顾客行为[{m.Kind}] {m.ClientId}(本地已无此顾客)");
                return;
            }

            ApplyingRemote = true;
            try
            {
                switch (m.Kind)
                {
                    case K_INTRO:
                        if (!target.isIntroduced) target.isIntroduced = true;
                        CoopLog.Detail($"[Coop] 同步: 顾客 {m.ClientId} 开始服务");
                        break;
                    case K_DONE:
                        if (!target.isBusinessComplete) target.isBusinessComplete = true;
                        CoopLog.Detail($"[Coop] 同步: 顾客 {m.ClientId} 交易完成");
                        break;
                    case K_SOLD:
                        CoopLog.Detail($"[Coop] 同步: 顾客 {m.ClientId} 成交 {m.Detail}");
                        break;
                    case K_PLACED:
                        CoopLog.Detail($"[Coop] 同步: 顾客 {m.ClientId} 挑选 {m.Detail}");
                        break;
                }
            }
            finally { ApplyingRemote = false; }
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop] 顾客行为应用异常: {e.Message}"); }
    }
}

// ---- 钩子:顾客行为关键点 ----

[HarmonyPatch(typeof(StoreClient), nameof(StoreClient.OnIntroduced))]
public static class ClientIntroducedPatch
{
    public static void Postfix(StoreClient __instance) { CustomerBehaviorSync.ReportIntroduced(__instance); }
}

[HarmonyPatch(typeof(StoreClient), nameof(StoreClient.OnItemPlaced))]
public static class ClientItemPlacedPatch
{
    public static void Postfix(StoreClient __instance, GameItem gameItem) { CustomerBehaviorSync.ReportPlaced(__instance, gameItem); }
}

[HarmonyPatch(typeof(StoreClient), nameof(StoreClient.CompleteBusiness))]
public static class ClientCompleteBusinessPatch
{
    public static void Postfix(StoreClient __instance) { CustomerBehaviorSync.ReportDone(__instance); }
}

[HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.PerformOnSoldCheck))]
public static class PlayerStoreSoldPatch
{
    public static void Postfix(GameItem item, StoreClient currentClient) { CustomerBehaviorSync.ReportSold(item, currentClient); }
}
