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
    static bool PlaceVisual(GameItem it, Vector2 pixel)
    {
        try
        {
            var ge = it.TryCast<GameItemElement>();
            if (ge == null) return false;
            ge.SetCenteredPosition(pixel.x, pixel.y);
            return true;
        }
        catch (Exception e) { CoopLog.Detail($"[Coop] SetCenteredPosition 失败: {e.Message}"); return false; }
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
            if (Time.time - _lastAt < Interval) return;
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
                        && old.O == kv.Value.O && old.F == kv.Value.F) continue;
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

            // 1) 数据:改网格坐标(modifiedShape 返回的就是物品持有的对象,直接改字段)
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

            var now = Placed(item);
            if (now != null)
                _last[m.UniqueId] = new Node
                { X = now.minX, Y = now.minY, O = now.orientation, F = now.flipped, Id = item.identifier ?? "" };

            CoopLog.Info($"[Coop] 已应用队友的物品移动: {item.identifier} uid={m.UniqueId} → ({m.MinX},{m.MinY}) "
                         + $"数据={(dataOk ? "改坐标" : "失败")} 画面={(visOk ? "已重绘" : "失败")}");
            _rebuildBaseline = true;   // 下次采集只重建基线,不回传(避免自己应用的变化被当成自己的变化再发一轮)
        }
        catch (Exception e) { CoopLog.Warn($"[Coop] 物品移动应用异常: {e.Message}"); }
        finally { ApplyingRemote = false; }
    }
}
