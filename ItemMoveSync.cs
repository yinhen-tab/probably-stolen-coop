using System;
using System.Collections.Generic;
using Coop;
using UnityEngine;

namespace ProbablyStolenCoop;

/// <summary>
/// 物品位置同步(非破坏性,只动"被移动的那一件"):
/// - 采集: 每 ~0.8 秒对比一次主库存布局(uniqueId → 网格坐标/朝向),只把**变化的那件**发出去
///   (不 hook 拖拽内部 —— 拖放/自动排序/买卖落位都能覆盖)
/// - 应用: 本地按 uniqueId 找到同一件物品, 用游戏自己的落位通道
///   `GameGridInventory.TryInventorySlot(item, -1, 落点中心像素, shape, state)` → `SlotMarker.AcceptUnchecked()`
///   落点由 (minX,minY) 换算: 格子边长 = widthPixels / inventoryShape.width
/// - 为什么不用"整份重建": PlayerStore.DecodeSaveItem 运行时抛 NRE, 且"先清空再还原"失败会清空库存
/// </summary>
public static class ItemMoveSync
{
    public static bool Enabled = true;
    public static bool ApplyingRemote;

    private struct Node
    {
        public int X, Y, O;
        public bool F;
        public string Id;
    }

    private static readonly Dictionary<int, Node> _last = new();
    private static float _lastAt;
    private static bool _rebuildBaseline;      // 应用远端后重建基线(避免回声)
    private static bool _rebuildLogged;        // 基线只打一次日志
    private const float Interval = 0.8f;

    // ================= 采集 =================

    /// <summary>取物品在库存里的实际落位形态(GridShape):优先 modifiedShape(放置后的),
    /// 因为 item.shape 只是物品自身的外形(实测 minX/minY 恒为 0)</summary>
    static GridShape Placed(GameItem it)
    {
        try { var m = it.modifiedShape; if (m != null) return m; } catch { }
        try { return it.shape; } catch { }
        return null;
    }

    public static void TickCapture()
    {
        try
        {
            if (!Enabled) return;
            if (ApplyingRemote) return;
            if (Time.time - _lastAt < Interval) return;
            if (CoopClient.Net == null || !CoopClient.Handshaked || string.IsNullOrEmpty(CoopClient.RoomCode)) return;
            if (GameMaster.current == null) return;   // 只在店铺场景
            _lastAt = Time.time;

            var inv = ItemGrant.FindPlayerInventoryPublic();
            if (inv == null) return;
            var items = inv.childItems;
            if (items == null) return;

            var cur = new Dictionary<int, Node>();
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it == null) continue;
                var sh = Placed(it);
                if (sh == null) continue;
                cur[it.uniqueId] = new Node
                {
                    X = sh.minX, Y = sh.minY, O = sh.orientation, F = sh.flipped,
                    Id = it.identifier ?? "",
                };
            }

            if (_last.Count == 0 || _rebuildBaseline)   // 首次 / 应用远端后:只重建基线,不上报
            {
                _rebuildBaseline = false;
                _last.Clear();
                foreach (var kv in cur) _last[kv.Key] = kv.Value;
                if (_last.Count > 0 && !_rebuildLogged)
                {
                    _rebuildLogged = true;
                    var sb = new System.Text.StringBuilder();
                    int n = 0;
                    for (int i = 0; i < items.Count && n < 12; i++)
                    {
                        var it = items[i];
                        if (it == null) continue;
                        n++;
                        var s1 = it.shape; var s2 = it.modifiedShape;
                        sb.Append(it.uniqueId).Append(':').Append(it.identifier)
                          .Append(" s=(").Append(s1 != null ? s1.minX : -1).Append(',').Append(s1 != null ? s1.minY : -1).Append(')')
                          .Append(" m=(").Append(s2 != null ? s2.minX : -1).Append(',').Append(s2 != null ? s2.minY : -1).Append("); ");
                    }
                    CoopLog.Info($"[Coop] 物品位置基线: {cur.Count} 件 | {sb}");
                }
                return;
            }

            foreach (var kv in cur)
            {
                if (_last.TryGetValue(kv.Key, out var old))
                {
                    if (old.X == kv.Value.X && old.Y == kv.Value.Y
                        && old.O == kv.Value.O && old.F == kv.Value.F) continue;
                }
                Send(kv.Key, kv.Value);
                _last[kv.Key] = kv.Value;
            }
        }
        catch (Exception e) { CoopLog.Warn($"[Coop] 物品位置采集异常: {e.Message}"); }
    }

    static void Send(int uid, Node n)
    {
        try
        {
            CoopClient.Net.Send(MsgType.ItemMove, new Coop.ItemMove
            {
                PlayerId = CoopClient.PlayerId,
                UniqueId = uid,
                Identifier = n.Id ?? "",
                MinX = n.X,
                MinY = n.Y,
                Orientation = n.O,
                Flipped = n.F,
            });
            CoopLog.Info($"[Coop] 本地上报物品移动: {n.Id} uid={uid} → ({n.X},{n.Y}) rot={n.O}");
        }
        catch (Exception e) { CoopLog.Warn($"[Coop] 物品移动上报失败: {e.Message}"); }
    }

    // ================= 应用 =================

    public static void ApplyRemote(Coop.ItemMove m)
    {
        if (!Enabled) return;
        try
        {
            var inv = ItemGrant.FindPlayerInventoryPublic();
            if (inv == null) return;
            var items = inv.childItems;
            if (items == null) return;

            GameItem item = null;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it != null && it.uniqueId == m.UniqueId) { item = it; break; }
            }
            if (item == null && !string.IsNullOrEmpty(m.Identifier))
                for (int i = 0; i < items.Count; i++)
                {
                    var it = items[i];
                    if (it != null && it.identifier == m.Identifier) { item = it; break; }
                }
            if (item == null)
            {
                CoopLog.Detail($"[Coop] 物品移动: 本地找不到 uid={m.UniqueId} {m.Identifier}(跳过)");
                return;
            }

            ApplyingRemote = true;
            var sh = item.shape;
            if (sh == null) return;

            // 落点中心(像素): 格子边长 = widthPixels / inventoryShape.width
            float cw = 1f, ch = 1f;
            int GW = -1, GH = -1, WP = -1, HP = -1;
            try
            {
                var gin = inv.TryCast<GameGridInventory>();
                if (gin != null)
                {
                    GW = gin.inventoryShape != null ? gin.inventoryShape.width : -1;
                    GH = gin.inventoryShape != null ? gin.inventoryShape.height : -1;
                    WP = gin.widthPixels; HP = gin.heightPixels;
                    if (GW > 0) cw = (float)WP / GW;
                    if (GH > 0) ch = (float)HP / GH;
                }
            }
            catch { }
            var center = new Vector2((m.MinX + sh.width * 0.5f) * cw,
                                     (m.MinY + sh.height * 0.5f) * ch);

            bool done = false;
            string why = "";
            try
            {
                var slot = inv.TryInventorySlot(item, -1, center, sh, item.state);
                if (slot == null) why = "slot=null";
                else if (!slot.IsValid()) why = "slot 无效";
                else { slot.AcceptUnchecked(); done = true; }
            }
            catch (Exception e) { why = "异常: " + e.Message; }

            // 通道2: 直接改 modifiedShape 的坐标(实测位置就存在这里)
            if (!done)
            {
                try
                {
                    var cur2 = item.modifiedShape;
                    var b = cur2 != null ? cur2.TryCast<GridShapeBuilder>() : null;
                    if (b != null)
                    {
                        // modifiedShape 返回的就是物品持有的那个对象,直接改字段(无需回写)
                        b.minX = m.MinX; b.minY = m.MinY;
                        b.orientation = m.Orientation; b.flipped = m.Flipped;
                        done = true;
                        why = "改 modifiedShape";
                    }
                    else why = "modifiedShape 非 GridShapeBuilder";
                }
                catch (Exception e) { why = "改 modifiedShape 异常: " + e.Message; }
            }

            CoopLog.Info($"[Coop] 落位诊断: 物品shape={sh.width}x{sh.height} 网格={GW}x{GH} 像素={WP}x{HP} "
                         + $"格边={cw:0.#}x{ch:0.#} 目标格=({m.MinX},{m.MinY}) 落点像素=({center.x:0.#},{center.y:0.#}) → {(done ? "成功(" + why + ")" : why)}");

            // 读回落位结果写入基线,避免"应用产生的变化"被当成自己的变化再发回去
            var now = item.shape;
            if (now != null)
                _last[m.UniqueId] = new Node
                { X = now.minX, Y = now.minY, O = now.orientation, F = now.flipped, Id = item.identifier ?? "" };

            CoopLog.Info($"[Coop] 已应用队友的物品移动: {item.identifier} uid={m.UniqueId} → ({m.MinX},{m.MinY})"
                         + (done ? "" : " [落位未成功,已跳过]"));
            _rebuildBaseline = true;   // 下一次采集只重建基线,不回传(否则自己应用的变化会被当成自己的变化再发一轮)
        }
        catch (Exception e) { CoopLog.Warn($"[Coop] 物品移动应用异常: {e.Message}"); }
        finally { ApplyingRemote = false; }
    }
}
