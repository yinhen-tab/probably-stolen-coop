using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Coop;

namespace ProbablyStolenCoop;

/// <summary>
/// 库存布局同步:把"谁拿着什么、放在哪个格子、朝向如何"整份同步给对方。
/// 用游戏自带的编解码(与存档同一条通道):
///   捕获 = PlayerStore.EncodeItem(每个物品) → JSON(含位置/朝向/容器内容)
///   应用 = 清空主库存(ExpelAll) → PlayerStore.DecodeSaveItem(JSON, 库存) 原样还原
/// 触发:拖放结束(OnEndDrag)后防抖发送;接收端整份对齐。
/// 说明:这是"事件级"同步(拖完落地才同步),拖动过程中的逐帧跟随另议。
/// </summary>
public static class InvLayoutSync
{
    /// <summary>布局同步总开关:实测"运行时重建库存"不可用,默认关闭(待改用非破坏性方案)</summary>
    public static bool Enabled = false;
    public static bool ApplyingRemote;

    private static float _dirtyAt;
    private static bool _force;
    private static string _lastSentHash = "";

    /// <summary>本端布局发生变化(拖放/买卖等)</summary>
    public static void MarkDirty()
    {
        if (ApplyingRemote) return;
        if (!Enabled) return;
        if (string.IsNullOrEmpty(CoopClient.RoomCode)) return;
        _dirtyAt = Time.time;
    }

    public static void ForceSendNextTick()
    {
        if (!Enabled) return;
        _force = true;
    }

    /// <summary>心跳:防抖后发送整份布局</summary>
    public static void TickSend()
    {
        try
        {
            if (!Enabled) return;
            if (!_force && _dirtyAt <= 0f) return;
            if (CoopClient.Net == null || !CoopClient.Handshaked || string.IsNullOrEmpty(CoopClient.RoomCode)) return;
            if (!_force && Time.time - _dirtyAt < 0.4f) return;
            var jsons = Capture();
            if (jsons == null || jsons.Count == 0) { _force = false; _dirtyAt = 0f; return; }
            string hash = string.Join("|", jsons).GetHashCode().ToString("X8");
            if (!_force && hash == _lastSentHash) { _dirtyAt = 0f; return; }
            _lastSentHash = hash;
            _force = false; _dirtyAt = 0f;

            var msg = new Coop.InvSync { PlayerId = CoopClient.PlayerId };
            msg.ItemsJson.AddRange(jsons);
            CoopClient.Net.Send(MsgType.InvSync, msg);
            CoopLog.Detail($"[Coop] 本地上报库存布局: {jsons.Count} 件");
        }
        catch (Exception e) { CoopLog.Warn($"[Coop] 库存布局上报异常: {e.Message}"); }
    }

    /// <summary>捕获主库存布局(每个物品一个 JSON)</summary>
    public static List<string> Capture()
    {
        var outList = new List<string>();
        try
        {
            var ps = PlayerStore.Instance;
            if (ps == null) return outList;
            var inv = ItemGrant.FindPlayerInventoryPublic();
            if (inv == null) return outList;
            var items = inv.childItems;
            if (items == null || items.Count == 0) return outList;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it == null) continue;
                try
                {
                    string j = ps.EncodeItem(it);
                    if (!string.IsNullOrEmpty(j)) outList.Add(j);
                }
                catch (Exception e) { CoopLog.Detail($"[Coop] 编码物品失败: {e.Message}"); }
            }
        }
        catch (Exception e) { CoopLog.Warn($"[Coop] 捕获库存布局异常: {e.Message}"); }
        return outList;
    }

    /// <summary>
    /// 接收方:整份对齐到对方的布局。
    /// ⚠️ 实测结论(2026-10-15): `PlayerStore.DecodeSaveItem` 在**运行时**会抛
    /// NullReferenceException(它是给"读档流程"用的,依赖读档期上下文),
    /// 所以"清空 + 逐件还原"这条路走不通 —— 而且已经先清空再还原失败会**清空玩家库存**。
    /// 因此这里**不再做破坏性操作**,只记录;改为后续用"按物品移动"的非破坏性方案。
    /// </summary>
    public static void ApplyRemote(Coop.InvSync m)
    {
        try
        {
            if (m.ItemsJson.Count == 0)
            {
                // 空布局 = 对端请求"把你的布局发我"(自测用)
                ForceSendNextTick();
                return;
            }
            if (!_warnedUnsupported)
            {
                _warnedUnsupported = true;
                CoopLog.Warn($"[Coop] 库存布局同步: 收到 {m.ItemsJson.Count} 件,但运行时重建库存不可用(游戏接口限制)," +
                             "已跳过(不会清空你的物品)。");
            }
        }
        catch (Exception e) { CoopLog.Warn($"[Coop] 库存布局应用异常: {e.Message}"); }
    }

    private static bool _warnedUnsupported;
}

// ---- 钩子:拖放结束 → 触发一次布局同步 ----

[HarmonyPatch(typeof(TreeNodeRender), "OnEndDrag")]
public static class InvLayoutDragEndPatch
{
    public static void Postfix() { InvLayoutSync.MarkDirty(); }
}
