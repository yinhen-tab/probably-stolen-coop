using System;
using System.Text;
using HarmonyLib;
using Coop;
using UnityEngine;

namespace ProbablyStolenCoop;

/// <summary>
/// 顾客队列同步("同一批顾客"):
///  - 房主权威:本地 clientStack 变化(每日生成/叫号/新增)→ 防抖 1.2s 后广播队列快照(id 列表)
///  - 对方:收到快照 → 用 StoreClientListDict.CreateStoreClient(id) 重建同一队列 → 替换本地 clientStack
///  - 配合 call_next 动作镜像:两端"叫下一位"弹出同一位顾客
///  - 自愈:房主每 ~20 秒补发一次快照,晚进/掉线重进自动对齐
/// </summary>
public static class CustomerQueueSync
{
    /// <summary>正在应用房主队列(期间不重复上报)</summary>
    public static bool ApplyingHostQueue { get; private set; }

    private static float _dirtyAt;
    private static bool _force;
    private static int _autoTick;
    private static string _lastSentHash = "";   // 内容哈希:队列没变就不重复发
    private static string _lastAppliedHash = "";   // 收到的队列哈希:重复快照不重放

    /// <summary>本店队列变化(由 Harmony 钩子调用;仅房主生效)</summary>
    public static void MarkDirty()
    {
        if (ApplyingHostQueue) return;
        if (!CoopClient.IsHost) return;
        if (string.IsNullOrEmpty(CoopClient.RoomCode)) return;
        _dirtyAt = Time.time;
    }

    /// <summary>下一帧立即发送快照(进房/有人加入时)</summary>
    public static void ForceSendNextTick()
    {
        if (!CoopClient.IsHost) return;
        _force = true;
    }

    /// <summary>心跳驱动:防抖后发送队列快照(主线程)</summary>
    public static void TickSend()
    {
        try
        {
            _autoTick++;
            if (_autoTick % 3600 == 0 && CoopClient.IsHost) _force = true;   // ~60 秒自愈补发
            if (!_force && _dirtyAt <= 0f) return;
            if (CoopClient.Net == null || !CoopClient.Handshaked || string.IsNullOrEmpty(CoopClient.RoomCode)) return;
            if (!_force && Time.time - _dirtyAt < 1.2f) return;

            var ps = PlayerStore.Instance;
            var mgr = ps != null ? ps.storeClientManager : null;
            if (mgr == null) return;   // 场景未就绪,保留待发状态

            _force = false; _dirtyAt = 0f;
            var list = mgr.clientStack;
            if (list == null) return;

            var msg = new Coop.ClientQueueSync { PlayerId = CoopClient.PlayerId, Day = CoopClient.MyDay };
            var sb = new StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                string id = (c != null && c.identifier != null) ? c.identifier : "";
                msg.ClientIds.Add(id);
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(id);
            }

            // 内容哈希:没变就不发(否则 UI 每帧重标脏会刷屏)
            string hash = sb.ToString();
            if (hash == _lastSentHash) return;
            _lastSentHash = hash;

            CoopClient.Net.Send(MsgType.ClientQueueSync, msg);
            CoopLog.Detail($"[Coop] 本地上报顾客队列: {msg.ClientIds.Count} 位 [{hash}]");
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop] 队列快照发送异常: {e.Message}"); }
    }

    /// <summary>接收方:用队友队列替换本店队列(同一批顾客)</summary>
    public static void ApplyHostQueue(Coop.ClientQueueSync msg)
    {
        try
        {
            var ps = PlayerStore.Instance;
            var mgr = ps != null ? ps.storeClientManager : null;
            if (mgr == null) { Plugin.Log.LogWarning("[Coop] 队列同步: 店铺未就绪,忽略"); return; }
            var cur = mgr.clientStack;
            if (cur == null) { Plugin.Log.LogWarning("[Coop] 队列同步: clientStack 未初始化,忽略"); return; }

            // 收到的队列哈希与上次相同 → 不重复重放
            var inSb = new StringBuilder();
            for (int i = 0; i < msg.ClientIds.Count; i++)
            {
                if (inSb.Length > 0) inSb.Append(", ");
                inSb.Append(msg.ClientIds[i]);
            }
            if (inSb.ToString() == _lastAppliedHash) return;
            _lastAppliedHash = inSb.ToString();

            // 相同则跳过(避免无谓替换)
            bool same = cur.Count == msg.ClientIds.Count;
            if (same)
            {
                for (int i = 0; i < cur.Count; i++)
                {
                    var c = cur[i];
                    string id = (c != null && c.identifier != null) ? c.identifier : "";
                    if (id != msg.ClientIds[i]) { same = false; break; }
                }
            }
            if (same) return;

            // 重建同一批顾客
            var fresh = new System.Collections.Generic.List<StoreClient>();
            var sb = new StringBuilder();
            for (int i = 0; i < msg.ClientIds.Count; i++)
            {
                string id = msg.ClientIds[i];
                try
                {
                    var c = StoreClientListDict.CreateStoreClient(id);
                    if (c != null)
                    {
                        fresh.Add(c);
                        if (sb.Length > 0) sb.Append(", ");
                        sb.Append(id);
                    }
                    else Plugin.Log.LogWarning($"[Coop] 队列同步: 未知顾客 {id}");
                }
                catch (Exception e) { Plugin.Log.LogWarning($"[Coop] 队列同步: 创建顾客失败 {id}: {e.Message}"); }
            }

            ApplyingHostQueue = true;
            try
            {
                cur.Clear();
                for (int i = 0; i < fresh.Count; i++) cur.Add(fresh[i]);
            }
            finally { ApplyingHostQueue = false; }

            CoopLog.Info($"[Coop] 已同步队友顾客队列: {fresh.Count} 位 [{sb}]");
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop] 队列同步应用异常: {e.Message}"); }
    }

    /// <summary>顾客成交离店(本端游戏事件)→ 广播,让两端队列实时一致(不必等周期性自愈)</summary>
    public static void ReportCustomerLeft(string id, string reason)
    {
        if (ApplyingHostQueue) return;
        if (string.IsNullOrEmpty(id)) return;
        if (CoopClient.Net == null || !CoopClient.Handshaked || string.IsNullOrEmpty(CoopClient.RoomCode)) return;
        try
        {
            CoopClient.Net.Send(MsgType.CustomerLeft, new Coop.CustomerLeft
            {
                PlayerId = CoopClient.PlayerId,
                CustomerId = id,
                Reason = reason ?? "left",
            });
            CoopLog.Info($"[Coop] 顾客成交离店上报: {id} ({reason})");
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop] 顾客离店上报失败: {e.Message}"); }
    }

    /// <summary>收到队友端顾客离店 → 从本地队列移除(两端叫号不会错位)</summary>
    public static void ApplyRemoteLeft(Coop.CustomerLeft msg)
    {
        try
        {
            var ps = PlayerStore.Instance;
            var mgr = ps != null ? ps.storeClientManager : null;
            var cur = mgr != null ? mgr.clientStack : null;
            if (cur == null) return;
            for (int i = cur.Count - 1; i >= 0; i--)
            {
                var c = cur[i];
                string id = (c != null && c.identifier != null) ? c.identifier : "";
                if (id != msg.CustomerId) continue;
                ApplyingHostQueue = true;
                try { cur.RemoveAt(i); } finally { ApplyingHostQueue = false; }
                CoopLog.Info($"[Coop] 已同步队友端顾客离店: {id}(本地队列剩 {cur.Count} 位)");
                return;
            }
            CoopLog.Info($"[Coop] 队友端顾客离店 {msg.CustomerId}(本地队列未见,应已一致)");
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop] 顾客离店应用异常: {e.Message}"); }
    }
}

/// <summary>顾客成交离店 → 上报(队列实时一致性)</summary>
[HarmonyPatch(typeof(StoreClient), nameof(StoreClient.CompleteBusiness))]
public static class CustomerCompleteBusinessPatch
{
    public static void Postfix(StoreClient __instance)
    {
        try { CustomerQueueSync.ReportCustomerLeft(__instance != null ? __instance.identifier : null, "dealt"); }
        catch { }
    }
}

// ---- 队列变化钩子(仅用于标记"本店队列已变化") ----

[HarmonyPatch(typeof(StoreClientManager), nameof(StoreClientManager.AddClient))]
public static class QueueAddClientPatch
{
    public static void Postfix() { CustomerQueueSync.MarkDirty(); }
}

[HarmonyPatch(typeof(StoreClientManager), nameof(StoreClientManager.AddNextClient))]
public static class QueueAddNextClientPatch
{
    public static void Postfix() { CustomerQueueSync.MarkDirty(); }
}

[HarmonyPatch(typeof(StoreClientManager), "TryAddClient", new Type[] { typeof(StoreClient), typeof(bool) })]
public static class QueueTryAddClientPatch
{
    public static void Postfix() { CustomerQueueSync.MarkDirty(); }
}

[HarmonyPatch(typeof(StoreClientManager), nameof(StoreClientManager.OnNewDay))]
public static class QueueOnNewDayPatch
{
    public static void Postfix() { CustomerQueueSync.MarkDirty(); }
}
