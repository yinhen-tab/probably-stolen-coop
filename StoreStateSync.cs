using System;
using System.Collections.Generic;
using System.Text;
using Coop;
using UnityEngine;

namespace ProbablyStolenCoop;

/// <summary>
/// 店铺状态镜像("同店铺"):
///  - 房主权威:资金/加价/吸引力/垃圾/供电/租金/贷款/服务/声望/机器库存/持有物品 → 定期快照广播
///  - 接收方:把快照写进本地 PlayerStore(资金等为可写字段),缺失的物品尝试补发
///  - 自愈:房主每 ~3 秒发一次(变化时才发),晚进/掉线自动对齐
/// </summary>
public static class StoreStateSync
{
    private static string _lastHash = "";
    private static int _tick;
    private static bool _forceSend;
    private static string _lastAppliedHash = "";   // 收到的快照哈希:重复的不重放

    /// <summary>进房/有人加入时立即发一次</summary>
    public static void ForceSendNextTick()
    {
        if (!CoopClient.IsHost) return;
        _forceSend = true;
    }

    /// <summary>心跳驱动:房主采集+发送快照(主线程)</summary>
    public static void TickSend()
    {
        try
        {
            _tick++;
            if (_tick % 600 == 0 && CoopClient.IsHost) _forceSend = true;   // ~10 秒自愈
            if (!CoopClient.IsHost) return;
            if (!_forceSend && _tick % 6 != 0) return;    // 采样节流
            if (CoopClient.Net == null || !CoopClient.Handshaked || string.IsNullOrEmpty(CoopClient.RoomCode)) return;

            var ps = PlayerStore.Instance;
            if (ps == null) return;

            var msg = Capture(ps);
            string hash = Hash(msg);
            // 内容没变就不发(自愈也不例外,否则每 10 秒刷屏一次)
            if (hash == _lastHash) { _forceSend = false; return; }
            _lastHash = hash;
            _forceSend = false;

            CoopClient.Net.Send(MsgType.ShopSnapshot, msg);
            Plugin.Log.LogInfo($"[Coop] 上报店铺快照: 资金={msg.Cash} 第{msg.Day}天 服务={msg.Services.Count} 声望={msg.Reputations.Count} 物品={msg.Items.Count}种");
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop] 店铺快照异常: {e.Message}"); }
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
          .Append(m.CassetteInvJson.Length).Append('|').Append(m.VendingInvJson.Length);
        for (int i = 0; i < m.Services.Count; i++) sb.Append(m.Services[i].Id).Append(m.Services[i].Unlocked ? '1' : '0').Append(m.Services[i].IsUsing ? '1' : '0');
        for (int i = 0; i < m.Reputations.Count; i++) sb.Append(m.Reputations[i].FactionId).Append(':').Append(m.Reputations[i].Strength).Append(',').Append(m.Reputations[i].Influence);
        for (int i = 0; i < m.Items.Count; i++) sb.Append(m.Items[i].Id).Append('x').Append(m.Items[i].Count).Append(';');
        return sb.ToString();
    }

    /// <summary>接收方:把房主快照写进本地店铺</summary>
    public static void ApplySnapshot(Coop.StoreSnapshot m)
    {
        try
        {
            var ps = PlayerStore.Instance;
            if (ps == null) { Plugin.Log.LogWarning("[Coop] 店铺同步: 本地店铺未就绪,忽略"); return; }
            if (CoopClient.IsHost) return;   // 房主不接受他人快照

            // 重复快照不重放(字段已一致,没必要再写一遍刷日志)
            string inHash = Hash(m);
            if (inHash == _lastAppliedHash) return;
            _lastAppliedHash = inHash;

            int cashBefore = 0;
            try { cashBefore = ps.playerCash; } catch { }

            try { ps.playerCash = m.Cash; } catch { }
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

            // 物品:补发缺失的
            int granted = 0, missing = 0;
            try
            {
                var own = ps.FindAllItem(true);
                var have = new HashSet<string>();
                if (own != null)
                    for (int i = 0; i < own.Count; i++)
                        if (own[i] != null && !string.IsNullOrEmpty(own[i].identifier))
                            have.Add(own[i].identifier);
                for (int i = 0; i < m.Items.Count; i++)
                {
                    var it = m.Items[i];
                    if (have.Contains(it.Id)) continue;
                    if (ItemGrant.TryGrant(it.Id, it.Count, out bool created))
                    {
                        if (created) granted++;
                    }
                    else missing++;
                }
                if (granted > 0 || missing > 0)
                    Plugin.Log.LogInfo($"[Coop] 物品同步: 本地 {have.Count} 种, 新补发 {granted}, 缺失 {missing}");
            }
            catch { }

            Plugin.Log.LogInfo($"[Coop] 已同步店铺: 资金 {cashBefore}→{m.Cash} 服务变更={svcChanged} 补发物品={granted} 缺失={missing}");
            if (cashBefore != m.Cash) CoopHud.Toast($"店铺资金已同步:{m.Cash}");
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop] 店铺快照应用异常: {e.Message}"); }
    }
}