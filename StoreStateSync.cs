using System;
using System.Collections.Generic;
using System.Text;
using Coop;
using UnityEngine;

namespace ProbablyStolenCoop;

/// <summary>
/// 店铺状态同步("同店铺" + 双向平等经营):
///  - 双向增量:双方都广播本地变化;接收端按 delta 应用资金(两人赚的/花的是同一笔钱,都能回流)
///  - 初始对齐:房主发一次权威全量(authoritative=true)→ 队友直接覆盖对齐
///  - 状态型字段(服务/声望/机器数/机器库存/饮水机)直接覆盖
///  - 物品:数量对齐(缺则补、多则移除;只移除"本会话补发过"的物品,避免误删玩家自有物品)
///  - 防回环:应用远端快照后同步刷新本地上报基线,避免把"因应用产生的变化"再广播回去
/// </summary>
public static class StoreStateSync
{
    private static string _lastHash = "";
    private static int _tick;
    private static bool _forceSend;
    private static bool _forceAuth;
    private static string _lastAppliedHash = "";

    /// <summary>对端资金基准(按 playerId):双向增量的参照点</summary>
    private static readonly Dictionary<int, int> _peerCashBase = new();

    /// <summary>进房/有人加入时立即发一次;authoritative=true → 房主发权威全量做初始对齐</summary>
    public static void ForceSendNextTick(bool authoritative = false)
    {
        _forceSend = true;
        if (authoritative) _forceAuth = true;
    }

    /// <summary>心跳驱动:采集并发送(主线程;双向都发)</summary>
    public static void TickSend()
    {
        try
        {
            _tick++;
            if (!_forceSend && _tick % 6 != 0) return;   // 采样节流
            if (CoopClient.Net == null || !CoopClient.Handshaked || string.IsNullOrEmpty(CoopClient.RoomCode)) return;

            var ps = PlayerStore.Instance;
            if (ps == null) return;

            var msg = Capture(ps);
            bool auth = _forceAuth && CoopClient.IsHost;
            msg.Authoritative = auth;

            string hash = Hash(msg) + (auth ? "|A" : "");
            if (hash == _lastHash) { _forceSend = false; _forceAuth = false; return; }
            _lastHash = hash;
            _forceSend = false;
            _forceAuth = false;

            CoopClient.Net.Send(MsgType.ShopSnapshot, msg);
            Plugin.Log.LogInfo($"[Coop] 上报店铺快照{(auth ? "(权威对齐)" : "")}: 资金={msg.Cash} 第{msg.Day}天 服务={msg.Services.Count} 物品={msg.Items.Count}种");
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop] 店铺快照发送异常: {e.Message}"); }
    }

    static Coop.StoreSnapshot Capture(PlayerStore ps)
    {
        var m = new Coop.StoreSnapshot { PlayerId = CoopClient.PlayerId, Day = CoopClient.MyDay };
        try { m.Cash = ps.playerCash; } catch { }
        try { m.DayUntilRent = ps.dayUntilRent; } catch { }
        try { m.RentValue = ps.rentValue; } catch { }
        try { m.DayUntilLoan = ps.dayUntilLoan; } catch { }
        try { m.LoanValue = ps.loanValue; } catch { }
        try { m.RetailMarkup = ps.retailMarkup; } catch { }
        try { m.ContrLow = ps.contrabandMarkupLow; } catch { }
        try { m.ContrMid = ps.contrabandMarkupMid; } catch { }
        try { m.ContrHigh = ps.contrabandMarkupHigh; } catch { }
        try { m.ContrCritical = ps.contrabandMarkupCritial; } catch { }
        try { m.BaseAttractiveness = ps.baseStoreAttractiveness; } catch { }
        try { m.ProjectorBonus = ps.projectorAttractivenessBonus; } catch { }
        try { m.DealmakerBonus = ps.dealMakerBargainBonus; } catch { }
        try { m.GarbageLevel = ps.garbageLevel; } catch { }
        try { m.IsPowerOn = ps.isPowerOn; } catch { }
        try { m.MachineryBought = ps.machineryBoughtCount; } catch { }
        try { m.CassetteInvJson = ps.cassettePlayerInvJSON ?? ""; } catch { }
        try { m.VendingInvJson = ps.vendingMachineInvJSON ?? ""; } catch { }
        try { m.FountainInvJson = ps.vendingFountainInvJSON ?? ""; } catch { }

        // 服务
        try
        {
            var list = ps.storeService;
            if (list != null)
                for (int i = 0; i < list.Count; i++)
                {
                    var s = list[i];
                    if (s == null) continue;
                    m.Services.Add(new ServiceState { Id = s.id ?? "", Unlocked = s.unlocked, IsUsing = s.isUsing });
                }
        }
        catch { }

        // 声望
        try
        {
            var reps = ps.storeReputations;
            if (reps != null)
                for (int i = 0; i < reps.Count; i++)
                {
                    var r = reps[i];
                    if (r == null) continue;
                    m.Reputations.Add(new ReputationState { FactionId = r.factionId ?? "", Strength = r.strength, Influence = r.influence });
                }
        }
        catch { }

        // 持有物品(按 id 汇总数量)
        try
        {
            var items = ps.FindAllItem(true);
            if (items != null)
            {
                var counts = new Dictionary<string, int>();
                for (int i = 0; i < items.Count; i++)
                {
                    var it = items[i];
                    if (it == null) continue;
                    string id = it.identifier ?? "";
                    if (string.IsNullOrEmpty(id)) continue;
                    int c = 1;
                    try { c = Mathf.Max(1, it.unitCount); } catch { }
                    counts[id] = counts.TryGetValue(id, out var old) ? old + c : c;
                }
                foreach (var kv in counts)
                    m.Items.Add(new ItemState { Id = kv.Key, Count = kv.Value });
            }
        }
        catch { }

        return m;
    }

    static string Hash(Coop.StoreSnapshot m)
    {
        var sb = new StringBuilder();
        sb.Append(m.Cash).Append('|').Append(m.Day).Append('|').Append(m.DayUntilRent).Append('|')
          .Append(m.RentValue).Append('|').Append(m.DayUntilLoan).Append('|').Append(m.RetailMarkup).Append('|')
          .Append(m.ContrLow).Append(m.ContrMid).Append(m.ContrHigh).Append(m.ContrCritical).Append('|')
          .Append(m.BaseAttractiveness).Append('|').Append(m.ProjectorBonus).Append('|').Append(m.DealmakerBonus).Append('|')
          .Append(m.GarbageLevel).Append('|').Append(m.IsPowerOn).Append('|').Append(m.MachineryBought).Append('|')
          .Append(m.CassetteInvJson.Length).Append('|').Append(m.VendingInvJson.Length).Append('|')
          .Append(m.FountainInvJson.Length).Append('|');
        for (int i = 0; i < m.Services.Count; i++) sb.Append(m.Services[i].Id).Append(m.Services[i].Unlocked ? '1' : '0').Append(m.Services[i].IsUsing ? '1' : '0');
        for (int i = 0; i < m.Reputations.Count; i++) sb.Append(m.Reputations[i].FactionId).Append(':').Append(m.Reputations[i].Strength).Append(',').Append(m.Reputations[i].Influence);
        for (int i = 0; i < m.Items.Count; i++) sb.Append(m.Items[i].Id).Append('x').Append(m.Items[i].Count).Append(';');
        return sb.ToString();
    }

    /// <summary>接收远端快照:资金走增量、状态型字段直接覆盖、物品数量对齐</summary>
    public static void ApplySnapshot(Coop.StoreSnapshot m)
    {
        try
        {
            var ps = PlayerStore.Instance;
            if (ps == null) { Plugin.Log.LogWarning("[Coop] 店铺同步: 本地店铺未就绪,忽略"); return; }
            if (m.PlayerId == CoopClient.PlayerId) return;   // 自己的快照忽略

            // 重复快照不重放
            string inHash = Hash(m) + (m.Authoritative ? "|A" : "");
            if (inHash == _lastAppliedHash) return;
            _lastAppliedHash = inHash;

            int cashBefore = 0;
            try { cashBefore = ps.playerCash; } catch { }

            // ---- 累积型字段:资金用增量(双向共同经营) ----
            int newCash = cashBefore;
            if (m.Authoritative)
            {
                newCash = m.Cash;                       // 房主权威全量 → 直接对齐
                _peerCashBase[m.PlayerId] = m.Cash;
            }
            else if (_peerCashBase.TryGetValue(m.PlayerId, out int baseCash))
            {
                int delta = m.Cash - baseCash;          // 对端变化量 → 累积到本地
                _peerCashBase[m.PlayerId] = m.Cash;
                newCash = cashBefore + delta;
            }
            else
            {
                _peerCashBase[m.PlayerId] = m.Cash;     // 首次收到:建立基准(等权威快照对齐)
            }
            try { ps.playerCash = newCash; } catch { }

            // ---- 状态型字段:直接覆盖 ----
            try { ps.dayUntilRent = m.DayUntilRent; } catch { }
            try { ps.rentValue = m.RentValue; } catch { }
            try { ps.dayUntilLoan = m.DayUntilLoan; } catch { }
            try { ps.loanValue = m.LoanValue; } catch { }
            try { ps.retailMarkup = m.RetailMarkup; } catch { }
            try { ps.contrabandMarkupLow = m.ContrLow; } catch { }
            try { ps.contrabandMarkupMid = m.ContrMid; } catch { }
            try { ps.contrabandMarkupHigh = m.ContrHigh; } catch { }
            try { ps.contrabandMarkupCritial = m.ContrCritical; } catch { }
            try { ps.baseStoreAttractiveness = m.BaseAttractiveness; } catch { }
            try { ps.projectorAttractivenessBonus = m.ProjectorBonus; } catch { }
            try { ps.dealMakerBargainBonus = m.DealmakerBonus; } catch { }
            try { ps.garbageLevel = m.GarbageLevel; } catch { }
            try { ps.isPowerOn = m.IsPowerOn; } catch { }
            try { ps.machineryBoughtCount = m.MachineryBought; } catch { }
            if (!string.IsNullOrEmpty(m.CassetteInvJson)) { try { ps.cassettePlayerInvJSON = m.CassetteInvJson; } catch { } }
            if (!string.IsNullOrEmpty(m.VendingInvJson)) { try { ps.vendingMachineInvJSON = m.VendingInvJson; } catch { } }
            if (!string.IsNullOrEmpty(m.FountainInvJson)) { try { ps.vendingFountainInvJSON = m.FountainInvJson; } catch { } }

            // 服务
            int svcChanged = 0;
            try
            {
                var list = ps.storeService;
                if (list != null)
                    for (int i = 0; i < list.Count; i++)
                    {
                        var s = list[i];
                        if (s == null) continue;
                        for (int j = 0; j < m.Services.Count; j++)
                        {
                            var src = m.Services[j];
                            if (src.Id == s.id)
                            {
                                if (s.isUsing != src.IsUsing || s.unlocked != src.Unlocked) svcChanged++;
                                s.isUsing = src.IsUsing;
                                s.unlocked = src.Unlocked;
                                break;
                            }
                        }
                    }
            }
            catch { }

            // 声望
            try
            {
                var reps = ps.storeReputations;
                if (reps != null)
                    for (int i = 0; i < reps.Count; i++)
                    {
                        var r = reps[i];
                        if (r == null) continue;
                        for (int j = 0; j < m.Reputations.Count; j++)
                        {
                            var src = m.Reputations[j];
                            if (src.FactionId == r.factionId)
                            {
                                r.strength = src.Strength;
                                r.influence = src.Influence;
                                break;
                            }
                        }
                    }
            }
            catch { }

            // ---- 物品:数量对齐(补缺失 + 扣多余) ----
            int granted = 0, removed = 0, missing = 0;
            try
            {
                var own = ps.FindAllItem(true);
                var localCount = new Dictionary<string, int>();
                if (own != null)
                    for (int i = 0; i < own.Count; i++)
                    {
                        var it = own[i];
                        if (it == null) continue;
                        string id = it.identifier ?? "";
                        if (string.IsNullOrEmpty(id)) continue;
                        int c = 1;
                        try { c = Mathf.Max(1, it.unitCount); } catch { }
                        localCount[id] = localCount.TryGetValue(id, out var old) ? old + c : c;
                    }

                for (int i = 0; i < m.Items.Count; i++)
                {
                    var it = m.Items[i];
                    localCount.TryGetValue(it.Id, out int lc);
                    if (lc < it.Count)
                    {
                        int need = it.Count - lc;
                        if (ItemGrant.TryGrant(it.Id, need, out bool created)) { if (created) granted++; }
                        else missing++;
                    }
                    else if (lc > it.Count && ItemGrant.WasGrantedThisSession(it.Id))
                    {
                        // 只扣"联机期间补发过"的物品:队友消耗了 → 本地同步扣掉(避免误删你自己原有的库存)
                        int excess = lc - it.Count;
                        if (ItemGrant.TryRemove(it.Id, excess)) removed += excess;
                    }
                }
                // 远端"完全没有"、本地有、且是本会话补发进来的 → 队友那边已全部消耗,本地同步扣掉
                foreach (var kv in localCount)
                {
                    if (kv.Value <= 0) continue;
                    bool inRemote = false;
                    for (int i = 0; i < m.Items.Count; i++)
                        if (m.Items[i].Id == kv.Key) { inRemote = true; break; }
                    if (inRemote) continue;
                    if (!ItemGrant.WasGrantedThisSession(kv.Key)) continue;
                    if (ItemGrant.TryRemove(kv.Key, kv.Value)) removed += kv.Value;
                }
                if (granted > 0 || removed > 0 || missing > 0)
                    Plugin.Log.LogInfo($"[Coop] 物品数量对齐: 补发 {granted}, 扣除 {removed}, 缺失 {missing}");
            }
            catch { }

            // 防回环:应用后刷新本地上报基线(否则"应用产生的变化"会被当成自己的变化再广播回去)
            try { _lastHash = Hash(Capture(ps)); } catch { }

            Plugin.Log.LogInfo($"[Coop] 已同步店铺: 资金 {cashBefore}→{newCash}{(m.Authoritative ? " (权威对齐)" : " (增量)")} 服务变更={svcChanged} 补={granted} 扣={removed} 缺={missing}");
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop] 店铺快照应用异常: {e.Message}"); }
    }
}
