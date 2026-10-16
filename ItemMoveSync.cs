using System;
using System.Collections.Generic;
using Coop;
using UnityEngine;

namespace ProbablyStolenCoop;

/// <summary>
/// 物品位置同步(非破坏性,只动"被移动的那一件"):
/// - 采集: 每 ~0.8 秒对比一次**全部物品**(含容器内部的嵌套物品)的落位,只把**变化的那件**发出去
///   (不 hook 拖拽内部 —— 拖放/自动排序/买卖落位/容器内整理都能覆盖)
/// - 应用: 按 uniqueId 找到同一件物品,做两件事:
///     1) 数据: 改 modifiedShape 的网格坐标(位置就存在这里;item.shape 恒为 0)
///     2) 画面: GameItemElement.SetCenteredPosition(像素坐标) —— 不改这个物品不会真的画到新位置
/// - 为什么不用"整份重建": PlayerStore.DecodeSaveItem 运行时抛 NRE,且"先清空再还原"失败会清空库存
/// </summary>
public static class ItemMoveSync
{
    public static bool Enabled = true;
    public static bool ApplyingRemote;
    /// <summary>拖动跟手期间为 true:ApplyRemote 不打日志(否则 50ms 一条刷屏)</summary>
    public static bool QuietRemote;
    private static int _dragApplyCount;
    private static int _blockedCount;

    /// <summary>本地玩家正在拖动物品(游戏自己的拖拽处理器为真)</summary>
    public static bool IsLocalDragging
    {
        get
        {
            try
            {
                if (DragSync.Dragging) return true;          // 我们自己的拖拽标志(可靠)
                var h = ItemMouseDragHandler.current;
                return h != null && h.IsDraggingItem;
            }
            catch { return false; }
        }
    }

    /// <summary>拖动中:60ms 采集一次(跟手);平时:0.8s</summary>
    const float DragInterval = 0.06f;

    private struct Node
    {
        public int X, Y, O;
        public bool F;
        public int InvUid;      // 所在容器物品 uniqueId(0=主库存)
        public int Count;       // 数量
        public int Dur;         // 当前耐久(-1=该物品无耐久)
        public string Id;
    }

    private static readonly Dictionary<int, Node> _last = new();
    private static float _lastAt;
    private static bool _rebuildBaseline;      // 应用远端后重建基线(避免回声)
    private static bool _rebuildLogged;        // 基线只打一次日志
    private const float Interval = 0.8f;

    // ================= 工具 =================

    public static GridShape Placed(GameItem it)
    {
        try { var m = it.modifiedShape; if (m != null) return m; } catch { }
        try { return it.shape; } catch { }
        return null;
    }

    public static GameGridInventory GridOf(GameItem it)
    {
        try { return it.parentInventory != null ? it.parentInventory.TryCast<GameGridInventory>() : null; }
        catch { return null; }
    }

    /// <summary>格子→像素中心(把模型的网格坐标换算成渲染坐标)</summary>
    public static Vector2 ToPixel(GameGridInventory inv, GridShape sh, int minX, int minY)
    {
        float cw = 16f, ch = 16f;
        try
        {
            if (inv != null && inv.inventoryShape != null && inv.inventoryShape.width > 0)
                cw = (float)inv.widthPixels / inv.inventoryShape.width;
            if (inv != null && inv.inventoryShape != null && inv.inventoryShape.height > 0)
                ch = (float)inv.heightPixels / inv.inventoryShape.height;
        }
        catch { }
        return new Vector2((minX + sh.width * 0.5f) * cw, (minY + sh.height * 0.5f) * ch);
    }

    /// <summary>
    /// 让物品"真的画到新位置":运行时物品是 GameItemElement(GameItem + PixelElement),
    /// 渲染靠 SetCenteredPosition —— 只改 modifiedShape 只动数据不动画面。
    /// </summary>
    public static bool PlaceVisual(GameItem it, Vector2 pixel)
    {
        try
        {
            var ge = it.TryCast<GameItemElement>();
            if (ge == null) return false;
            ge.SetCenteredPosition(pixel.x, pixel.y);
            // ★ 关键:SetCenteredPosition 只是设目标值,必须再 UpdateRectTransform() 才真正写进画面
            //   (之前只调 SetCenteredPosition → 日志说"已重绘"但屏幕上根本没动)
            bool updated = false;
            try { ge.UpdateRectTransform(); updated = true; }
            catch (Exception e) { CoopLog.Detail($"[Coop] UpdateRectTransform 失败: {e.Message}"); }
            if (!updated)
            {
                try { var rt = ge.rectTransform; if (rt != null) rt.anchoredPosition = pixel; }
                catch { }
            }
            return true;
        }
        catch (Exception e) { CoopLog.Detail($"[Coop] SetCenteredPosition 失败: {e.Message}"); return false; }
    }

    /// <summary>物品所在容器的容器物品 uniqueId(0=主库存/非物品容器)</summary>
    static int OwnerUid(GameItem it)
    {
        try
        {
            var inv = it.parentInventory;
            var p = inv != null ? inv.parent : null;
            var owner = p != null ? p.TryCast<GameItem>() : null;
            return owner != null ? owner.uniqueId : 0;
        }
        catch { return 0; }
    }

    /// <summary>找某个容器物品的内部库存(遍历它的子节点)</summary>
    public static GameInventory ContentInvOf(GameItem container)
    {
        try
        {
            var ch = container != null ? container.children : null;
            if (ch == null) return null;
            for (int i = 0; i < ch.Count; i++)
            {
                var g = ch[i]?.TryCast<GameInventory>();
                if (g != null) return g;
            }
        }
        catch { }
        return null;
    }

    /// <summary>全部物品(含容器内部的嵌套物品)</summary>
    static Il2CppSystem.Collections.Generic.List<GameItem> AllItems(PlayerStore ps)
    {
        try { return ps.FindAllItem(true); } catch { return null; }
    }

    /// <summary>对外:按 uniqueId(兜底 identifier)找物品(供拖拽跟随用)</summary>
    public static GameItem FindItem(int uid, string ident = "")
    {
        var ps = PlayerStore.Instance;
        if (ps == null) return null;
        return FindByUid(AllItems(ps), uid, ident);
    }

    static GameItem FindByUid(Il2CppSystem.Collections.Generic.List<GameItem> items, int uid, string ident)
    {
        if (items == null) return null;
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            if (it != null && it.uniqueId == uid) return it;
        }
        if (!string.IsNullOrEmpty(ident))
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it != null && it.identifier == ident) return it;
            }
        return null;
    }

    // ================= 采集 =================

    public static void TickCapture()
    {
        try
        {
            if (!Enabled) return;
            if (ApplyingRemote) return;
            // 注意:这里**不能**因"队友在拖"而停采集 —— 拖动同步靠的就是本端物品位置变化及时上报
            if (ApplyingRemote) return;
            float interval = IsLocalDragging ? DragInterval : Interval;
            if (Time.time - _lastAt < interval) return;
            if (CoopClient.Net == null || !CoopClient.Handshaked || string.IsNullOrEmpty(CoopClient.RoomCode)) return;
            if (GameMaster.current == null) return;   // 只在店铺场景
            _lastAt = Time.time;

            var ps = PlayerStore.Instance;
            if (ps == null) return;
            var items = AllItems(ps);
            if (items == null || items.Count == 0) return;

            var cur = new Dictionary<int, Node>();
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it == null) continue;
                var sh = Placed(it);
                if (sh == null) continue;
                int cnt = -1, dur = -1;
                try { cnt = it.unitCount; } catch { }
                try
                {
                    if (DurabilityHelper.GetMaxDurability(it) > 0) dur = DurabilityHelper.GetRealDurability(it);
                }
                catch { }
                cur[it.uniqueId] = new Node
                {
                    X = sh.minX, Y = sh.minY, O = sh.orientation, F = sh.flipped,
                    InvUid = OwnerUid(it), Count = cnt, Dur = dur, Id = it.identifier ?? "",
                };
            }

            if (_last.Count == 0 || _rebuildBaseline)   // 首次 / 应用远端后:只重建基线,不上报
            {
                _rebuildBaseline = false;
                _last.Clear();
                foreach (var kv in cur) _last[kv.Key] = kv.Value;
                if (!_rebuildLogged && _last.Count > 0)
                {
                    _rebuildLogged = true;
                    CoopLog.Info($"[Coop] 物品位置基线: {cur.Count} 件(含容器内部)");
                }
                return;
            }

            int sent = 0;
            foreach (var kv in cur)
            {
                if (_last.TryGetValue(kv.Key, out var old))
                {
                    if (old.X == kv.Value.X && old.Y == kv.Value.Y
                        && old.O == kv.Value.O && old.F == kv.Value.F
                        && old.InvUid == kv.Value.InvUid
                        && old.Count == kv.Value.Count && old.Dur == kv.Value.Dur) continue;
                }
                Send(kv.Key, kv.Value);
                _last[kv.Key] = kv.Value;
                if (++sent >= 12) break;   // 一次最多发 12 件,避免大整理时刷屏
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
                InvUid = n.InvUid,
                UnitCount = n.Count,
                Durability = n.Dur,
            });
            CoopLog.Info($"[Coop] 本地上报物品变化: {n.Id} uid={uid} → ({n.X},{n.Y})"
                         + (n.InvUid != 0 ? $" 容器={n.InvUid}" : "")
                         + (n.Count >= 0 ? $" 数量={n.Count}" : "")
                         + (n.Dur >= 0 ? $" 耐久={n.Dur}" : ""));
        }
        catch (Exception e) { CoopLog.Warn($"[Coop] 物品移动上报失败: {e.Message}"); }
    }

    private static GameInventory _hlInv;      // 上次高亮用的库存(换个库存时先清旧的)
    private static bool _hlOn;

    /// <summary>
    /// 把物品**占用的格子**涂成绿色 —— 与游戏拖动时完全一致(用的是原生接口):
    ///   TryInventorySlot(...) 让游戏自己算 slot,再 HighlightSlot(slot, Green)。
    /// </summary>
    static void HighlightOccupied(GameItem item)
    {
        try
        {
            if (item == null) return;
            var inv = item.parentInventory;
            if (inv == null) return;
            if (_hlInv != null && _hlInv.Pointer != inv.Pointer)
            {
                try { _hlInv.ClearHighlight(); } catch { }
            }
            _hlInv = inv;
            SlotMarker slot = null;
            try { slot = inv.TryInventorySlot(item, item.modifiedShape, item.state); } catch { }
            if (slot == null)
            {
                slot = new SlotMarker
                {
                    item = item, inventory = inv,
                    itemGridShape = item.modifiedShape, index = 0,
                };
            }
            inv.ClearHighlight();
            inv.HighlightSlot(slot, RenderHandler.BackgroundColor.Green, false);
            _hlOn = true;
        }
        catch (Exception e) { CoopLog.Detail($"[Coop] 高亮占用格失败: {e.Message}"); }
    }

    /// <summary>拖动结束:清掉绿色高亮</summary>
    public static void ClearDragHighlight()
    {
        try { if (_hlInv != null) _hlInv.ClearHighlight(); } catch { }
        _hlInv = null;
        _hlOn = false;
    }

    /// <summary>
    /// 拖动跟手:把物品挪到队友那边的**目标格**。
    /// 复用 ApplyRemote 的完整路径(改模型 GridShapeBuilder + 重绘),但:
    ///   · 保持本端朝向/翻转(不覆盖)
    ///   · 不改数量/耐久
    ///   · QuietRemote 抑制日志(拖动期间 50ms 一条会刷屏)
    /// </summary>
    public static void ApplyDragTarget(int uid, string ident, int gx, int gy)
    {
        if (!Enabled) return;
        try
        {
            var item = FindItem(uid, ident);
            if (item == null) return;
            var inv = item.parentInventory;
            if (inv == null) return;
            var sh = Placed(item);
            if (sh == null) return;
            if (sh.minX == gx && sh.minY == gy) return;      // 已经在目标格,什么都不用做

            // minX/minY 在 GridShape 上是只读的,只有 GridShapeBuilder 能写
            var gsb = sh.TryCast<GridShapeBuilder>();
            int ox = sh.minX, oy = sh.minY;
            int ori = 0; bool flp = false;
            try { ori = sh.orientation; } catch { }
            try { flp = sh.flipped; } catch { }

            SlotMarker slot = null;
            bool canPlace = true;    // 拿不到可写形状时,退化为"直接移动"(旧行为)

            if (gsb != null)
            {
                // ---- 1) 形状临时指向目标格,问游戏"这里能放下吗"(原游戏就是这么判断的) ----
                try
                {
                    gsb.minX = gx; gsb.minY = gy;
                    try { slot = inv.TryInventorySlot(item, sh, item.state); } catch { }
                    if (slot == null)
                        slot = new SlotMarker { item = item, inventory = inv, itemGridShape = sh, index = 0 };
                    try { canPlace = slot.IsValid(); } catch { canPlace = false; }

                    // ---- 2) 高亮:能放 = 绿; 撞了别的物品 = 红(和原游戏一致) ----
                    inv.ClearHighlight();
                    inv.HighlightSlot(slot, canPlace ? RenderHandler.BackgroundColor.Green
                                                     : RenderHandler.BackgroundColor.Red, false);
                }
                catch (Exception e) { CoopLog.Detail($"[Coop] 高亮占用格失败: {e.Message}"); }
                finally
                {
                    try { gsb.minX = ox; gsb.minY = oy; } catch { }   // 立刻还原本端形状
                }
            }

            // ---- 3) 只有能放才真的移动物品(撞位就不动,避免堆叠) ----
            if (!canPlace)
            {
                _blockedCount++;
                if (_blockedCount <= 3 || _blockedCount % 20 == 0)
                    CoopLog.Info($"[Coop][拖应用] 目标格=({gx},{gy}) 被占用 → 红色,不移动物品 (第{_blockedCount}次)");
                return;
            }

            _dragApplyCount++;
            QuietRemote = true;
            ApplyRemote(new Coop.ItemMove
            {
                UniqueId = uid, Identifier = ident ?? "",
                MinX = gx, MinY = gy,
                Orientation = ori, Flipped = flp,
                InvUid = 0, UnitCount = -1, Durability = -1,
            });
            if (_dragApplyCount <= 3 || _dragApplyCount % 10 == 0)
                CoopLog.Info($"[Coop][拖应用] #{_dragApplyCount} 目标格=({gx},{gy}) 物品={item.identifier} "
                             + $"实际=({sh.minX},{sh.minY})");
        }
        catch { }
        finally { QuietRemote = false; }
    }

    // ================= 应用 =================

    public static void ApplyRemote(Coop.ItemMove m)
    {
        if (!Enabled) return;
        try
        {
            var ps = PlayerStore.Instance;
            if (ps == null) return;
            var item = FindByUid(AllItems(ps), m.UniqueId, m.Identifier);
            if (item == null)
            {
                CoopLog.Detail($"[Coop] 物品移动: 本地找不到 uid={m.UniqueId} {m.Identifier}(跳过)");
                return;
            }

            ApplyingRemote = true;
            var sh = Placed(item);
            if (sh == null) return;

            // 0) 跨容器:队友把物品换到别的容器了 → 本端也跟着换
            bool moved = true;
            try
            {
                var wantInv = m.InvUid != 0
                    ? ContentInvOf(FindItem(m.InvUid))
                    : ItemGrant.FindPlayerInventoryPublic();
                var curInv = item.parentInventory;
                if (wantInv != null && curInv != null && curInv.Pointer != wantInv.Pointer)
                {
                    int n = GraphUtils.TryAccept(wantInv.Cast<GraphNodeStorage>(), item);
                    if (n <= 0)
                    {
                        try { GraphUtils.EmporiumTryAdd(item); } catch { }
                    }
                    moved = true;
                    CoopLog.Info($"[Coop] 物品换容器: {item.identifier} → 容器 {m.InvUid}(TryAccept={n})");
                }
            }
            catch (Exception e) { CoopLog.Detail($"[Coop] 换容器失败: {e.Message}"); moved = false; }

            // 1) 数据:改网格坐标(modifiedShape 返回的就是物品持有的对象,直接改字段)
            sh = Placed(item);
            if (sh == null) return;
            bool dataOk = false;
            try
            {
                var b = sh.TryCast<GridShapeBuilder>();
                if (b != null)
                {
                    b.minX = m.MinX; b.minY = m.MinY;
                    b.orientation = m.Orientation; b.flipped = m.Flipped;
                    dataOk = true;
                }
            }
            catch (Exception e) { CoopLog.Detail($"[Coop] 改坐标失败: {e.Message}"); }

            // 2) 画面:SetCenteredPosition(否则物品不会真的画到新位置)
            var inv = GridOf(item);
            var px = ToPixel(inv, sh, m.MinX, m.MinY);
            bool visOk = PlaceVisual(item, px);

            // 3) 数量(队友用掉/取得了 → 本端跟着变)
            bool cntOk = false, durOk = false;
            if (m.UnitCount >= 0)
            {
                try
                {
                    if (item.unitCount != m.UnitCount) { item.SetUnitCount(m.UnitCount); cntOk = true; }
                    else cntOk = true;
                }
                catch (Exception e) { CoopLog.Detail($"[Coop] 改数量失败: {e.Message}"); }
            }
            // 4) 耐久(磨损)
            if (m.Durability >= 0)
            {
                try
                {
                    if (DurabilityHelper.GetMaxDurability(item) > 0
                        && DurabilityHelper.GetRealDurability(item) != m.Durability)
                    {
                        DurabilityHelper.SetCurrentDurability(item, m.Durability);
                        durOk = true;
                    }
                }
                catch (Exception e) { CoopLog.Detail($"[Coop] 改耐久失败: {e.Message}"); }
            }

            var now = Placed(item);
            int nowCnt = -1, nowDur = -1;
            try { nowCnt = item.unitCount; } catch { }
            try { if (DurabilityHelper.GetMaxDurability(item) > 0) nowDur = DurabilityHelper.GetRealDurability(item); } catch { }
            if (now != null)
                _last[m.UniqueId] = new Node
                { X = now.minX, Y = now.minY, O = now.orientation, F = now.flipped,
                  InvUid = m.InvUid, Count = nowCnt, Dur = nowDur, Id = item.identifier ?? "" };

            if (!QuietRemote)
            CoopLog.Info($"[Coop] 已应用队友的物品移动: {item.identifier} uid={m.UniqueId} → ({m.MinX},{m.MinY}) "
                         + $"数据={(dataOk ? "改坐标" : "失败")} 画面={(visOk ? "已重绘" : "失败")}"
                         + (m.InvUid != 0 ? $" 容器={m.InvUid}" : "")
                         + (m.UnitCount >= 0 ? $" 数量→{m.UnitCount}" : "")
                         + (m.Durability >= 0 ? $" 耐久→{m.Durability}({(durOk ? "已改" : "未变/失败")})" : ""));
            _rebuildBaseline = true;   // 下次采集只重建基线,不回传(避免自己应用的变化被当成自己的变化再发一轮)
        }
        catch (Exception e) { CoopLog.Warn($"[Coop] 物品移动应用异常: {e.Message}"); }
        finally { ApplyingRemote = false; }
    }
}
