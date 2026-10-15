using System;
using System.Text;
using UnityEngine;

namespace ProbablyStolenCoop;

/// <summary>
/// 物品补发:队友(房主)有的物品本地没有时,按 id 造出来塞进本地库存。
/// 造物品用游戏官方入口 DirectoryMaster.Item(id);库存用场景里的 GameGridInventory.UncheckedAccept。
/// 失败不阻断(只记日志),避免因为个别物品 id 认识不了就卡住整个快照。
/// </summary>
public static class ItemGrant
{
    private static GameInventory _cachedInv;
    private static float _lastFind;
    private static readonly System.Collections.Generic.HashSet<string> _grantedIds = new System.Collections.Generic.HashSet<string>();

    /// <summary>尝试补发 count 个 id 的物品;newlyCreated=本次真正新建了物品;返回 true=物品处于已满足状态</summary>
    public static bool TryGrant(string id, int count, out bool newlyCreated)
    {
        newlyCreated = false;
        if (string.IsNullOrEmpty(id) || count <= 0) return false;
        if (_grantedIds.Contains(id)) return true;   // 本会话已补发过:个别物品创建后 identifier 与请求 id 不一致,用它兜底防重复
        try
        {
            var inv = FindPlayerInventory();
            if (inv == null) return false;
            var ps = PlayerStore.Instance;
            bool any = false;
            for (int i = 0; i < count; i++)
            {
                var item = DirectoryMaster.Item(id, true);
                if (item == null) break;
                bool acc = false;
                try { acc = inv.UncheckedAccept(item); } catch (Exception e2) { Plugin.Log.LogWarning($"[Coop] accept 异常 {id}: {e2.Message}"); }
                if (acc && ps != null)
                {
                    // 游戏原生"物品已加入主库存区"处理:刷新玩家物品索引/UI
                    try { ps.AddedItemToMainInvArea(item); } catch { }
                }
                if (!_deepDiagDone && acc && ps != null)
                {
                    _deepDiagDone = true;
                    DeepDiag(id, item, inv, ps);
                }
                if (!acc) break;
                any = true;
            }
            if (any)
            {
                _grantedIds.Add(id);
                newlyCreated = true;
                if (ps != null)
                {
                    try { ps.OnWeightedAreaChanged(); } catch (Exception e) { Plugin.Log.LogWarning($"[Coop] OnWeightedAreaChanged 异常: {e.Message}"); }
                    try { ps.OnMainInvAreaChange(); } catch { }   // 库存区变化 → 重建索引/刷新界面
                }
            }
            return any;
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[Coop] 补发物品失败 {id}: {e.Message}");
            return false;
        }
    }

    /// <summary>兼容旧签名</summary>
    public static bool TryGrant(string id, int count) => TryGrant(id, count, out _);

    static bool _deepDiagDone;

    /// <summary>深度诊断:对比补发物品与原有物品的图关系/归属字段</summary>
    static void DeepDiag(string id, GameItem newItem, GameInventory inv, PlayerStore ps)
    {
        try
        {
            // 1. FindAllItem 立即可见性
            var own = ps.FindAllItem(true);
            bool found = false;
            string firstIds = "";
            if (own != null)
                for (int k = 0; k < own.Count; k++)
                {
                    try
                    {
                        string oid = own[k]?.identifier ?? "";
                        if (k < 4) firstIds += oid + ",";
                        if (oid == id) { found = true; }
                    }
                    catch { }
                }
            Plugin.Log.LogInfo($"[Coop][深诊] FindAllItem 立即可见={found} 总数={own?.Count ?? -1} 头四个=[{firstIds}]");

            // 2. IsPlayerOwnThisItem
            try { Plugin.Log.LogInfo($"[Coop][深诊] IsPlayerOwn({id})={ps.IsPlayerOwnThisItem(id)}"); } catch (Exception e) { Plugin.Log.LogInfo($"[Coop][深诊] IsPlayerOwn 异常:{e.Message}"); }

            // 2b. FindAllItem(false) 对比
            try { var all2 = ps.FindAllItem(false); Plugin.Log.LogInfo($"[Coop][深诊] FindAllItem(false) 总数={all2?.Count ?? -1}"); } catch (Exception e) { Plugin.Log.LogInfo($"[Coop][深诊] FindAllItem(false) 异常:{e.Message}"); }

            // 2c. gridInv.children(图链接)里找新物品
            try
            {
                var ch = inv.children;
                int n = ch?.Count ?? -1, hit = 0;
                if (ch != null)
                    for (int i = 0; i < ch.Count; i++)
                    {
                        try { var gi = ch[i]?.TryCast<GameItem>(); if (gi != null && gi.identifier == id) hit++; } catch { }
                    }
                Plugin.Log.LogInfo($"[Coop][深诊] gridInv.children={n} 其中 {id} 数量={hit}");
            }
            catch (Exception e) { Plugin.Log.LogInfo($"[Coop][深诊] children 遍历异常:{e.Message}"); }

            // 2d. mainInvJSON 实时性(头 160 字符)
            try
            {
                var j = ps.mainInvJSON;
                Plugin.Log.LogInfo($"[Coop][深诊] mainInvJSON len={j?.Length ?? -1} head={(string.IsNullOrEmpty(j) ? "(空)" : j.Substring(0, Math.Min(160, j.Length)))}");
            }
            catch (Exception e) { Plugin.Log.LogInfo($"[Coop][深诊] mainInvJSON 异常:{e.Message}"); }

            // 3. 对照基准:从 FindAllItem(游戏认可的)里取物品
            var ft = Il2CppInterop.Runtime.Il2CppType.Of<GameItem>();
            var bf = Il2CppSystem.Reflection.BindingFlags.Public | Il2CppSystem.Reflection.BindingFlags.NonPublic | Il2CppSystem.Reflection.BindingFlags.Instance;
            GameItem refItem = null;
            try { var own0 = ps.FindAllItem(true); if (own0 != null && own0.Count > 0) refItem = own0[0]; } catch { }

            // 3a. 基准对照:游戏认可物品的归属检查(应为 True)
            try { if (refItem != null) Plugin.Log.LogInfo($"[Coop][深诊] 基准: IsPlayerOwn({refItem.identifier})= {ps.IsPlayerOwnThisItem(refItem.identifier)}"); } catch { }

            // 3a2. 关键:gridInv 与水物品的容器是否同一个对象(指针对比)
            try
            {
                var invPtr = Il2CppInterop.Runtime.IL2CPP.Il2CppObjectBaseToPtr(inv);
                Plugin.Log.LogInfo($"[Coop][深诊] gridInv 指针=0x{invPtr.ToInt64():X}");
            }
            catch { }

            // 3b. parents 链对比(新物品 vs 基准物品):图链接差异一目了然
            DumpParents("新物品 " + id, newItem);
            if (refItem != null) DumpParents("基准物品 " + (refItem.identifier ?? "?"), refItem);

            // 3c. 基础字段对比
            try { Plugin.Log.LogInfo($"[Coop][深诊] 基础字段: 新物品 unitCount={newItem.unitCount} uniqueId={newItem.uniqueId} name={newItem.name}"); } catch (Exception e) { Plugin.Log.LogInfo($"[Coop][深诊] 新物品字段异常:{e.Message}"); }
            try { if (refItem != null) Plugin.Log.LogInfo($"[Coop][深诊] 基础字段: 原物品 unitCount={refItem.unitCount} uniqueId={refItem.uniqueId} name={refItem.name}"); } catch { }

            foreach (var fname in new string[] { "parentInventory", "isOwn", "isOwned" })
            {
                string nv = "?", rv = "?";
                try { var f = ft.GetField(fname, bf); if (f != null) { nv = f.GetValue(newItem)?.ToString() ?? "null"; if (refItem != null) rv = f.GetValue(refItem)?.ToString() ?? "null"; } else nv = "(无此字段)"; } catch (Exception e) { nv = "err:" + e.Message; }
                Plugin.Log.LogInfo($"[Coop][深诊] 字段 {fname}: 新物品={nv} | 原物品={rv}");
            }
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop][深诊] 异常: {e.Message}"); }
    }

    /// <summary>打印物品的 parents 链(图父节点),对比新旧物品的挂载差异</summary>
    static void DumpParents(string tag, GameItem it)
    {
        try
        {
            var bf = Il2CppSystem.Reflection.BindingFlags.Public | Il2CppSystem.Reflection.BindingFlags.NonPublic | Il2CppSystem.Reflection.BindingFlags.Instance;
            var prop = Il2CppInterop.Runtime.Il2CppType.Of<GameItem>().GetProperty("parents", bf);
            if (prop == null) { Plugin.Log.LogInfo($"[Coop][深诊] {tag}: 无 parents 属性"); return; }
            var v = prop.GetValue(it);
            var list = v?.TryCast<Il2CppSystem.Collections.Generic.List<GraphNodeStorage>>();
            if (list == null) { Plugin.Log.LogInfo($"[Coop][深诊] {tag}: parents 取值失败"); return; }
            var sb = new StringBuilder();
            int cnt = 0;
            for (int i = 0; i < list.Count && i < 6; i++)
            {
                string desc = "?";
                try
                {
                    var o = list[i];
                    var gi = o?.TryCast<GameItem>();
                    if (gi != null) desc = "物品:" + (gi.identifier ?? "?");
                    else
                    {
                        var gv = o?.TryCast<GameInventory>();
                        if (gv != null)
                        {
                            long p = 0;
                            try { p = Il2CppInterop.Runtime.IL2CPP.Il2CppObjectBaseToPtr(gv).ToInt64(); } catch { }
                            desc = "库存:" + (gv.identifier ?? "?") + "@0x" + p.ToString("X");
                        }
                        else desc = "节点:" + (o?.ToString() ?? "null");
                    }
                }
                catch { }
                if (sb.Length > 0) sb.Append(" | ");
                sb.Append(desc);
                cnt++;
            }
            Plugin.Log.LogInfo($"[Coop][深诊] {tag} parents(列出{cnt}个)= {sb}");
        }
        catch (Exception e) { Plugin.Log.LogInfo($"[Coop][深诊] {tag} parents 异常: {e.Message}"); }
    }

    /// <summary>
    /// 找"玩家物品的真正容器":从 FindAllItem 认可的物品反推其父库存。
    /// (经验:PlayerStore.gridInv 不是物品容器,真正的容器挂在物品的 parents[0] 上)
    /// </summary>
    static GameInventory FindPlayerInventory()
    {
        if (_cachedInv != null) return _cachedInv;
        if (Time.time - _lastFind < 5f) return null;
        _lastFind = Time.time;

        var ps = PlayerStore.Instance;
        if (ps == null) return null;

        // 主路:从游戏认可的物品反推容器
        try
        {
            var own = ps.FindAllItem(true);
            if (own != null && own.Count > 0)
            {
                var probe = own[0];
                var bf = Il2CppSystem.Reflection.BindingFlags.Public | Il2CppSystem.Reflection.BindingFlags.NonPublic | Il2CppSystem.Reflection.BindingFlags.Instance;
                var prop = Il2CppInterop.Runtime.Il2CppType.Of<GameItem>().GetProperty("parents", bf);
                var v = prop?.GetValue(probe);
                var list = v?.TryCast<Il2CppSystem.Collections.Generic.List<GraphNodeStorage>>();
                if (list != null && list.Count > 0)
                {
                    var gv = list[0]?.TryCast<GameInventory>();
                    if (gv != null)
                    {
                        _cachedInv = gv;
                        Plugin.Log.LogInfo($"[Coop] 定位本地库存: 从物品({probe.identifier})反推容器成功");
                        return _cachedInv;
                    }
                }
                Plugin.Log.LogInfo("[Coop] 定位本地库存: 反推失败(父不是 GameInventory),尝试兜底");
            }
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop] 反推容器异常: {e.Message}"); }

        // 兜底:反射 gridInv(注意:它不一定是物品容器)
        try
        {
            var f = Il2CppInterop.Runtime.Il2CppType.Of<PlayerStore>()
                .GetField("gridInv", Il2CppSystem.Reflection.BindingFlags.NonPublic | Il2CppSystem.Reflection.BindingFlags.Instance);
            if (f != null)
            {
                var v = f.GetValue(ps);
                if (v != null)
                {
                    _cachedInv = v.TryCast<GameGridInventory>();
                    if (_cachedInv != null)
                    {
                        Plugin.Log.LogInfo("[Coop] 定位本地库存: 兜底反射 gridInv");
                        return _cachedInv;
                    }
                }
            }
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop] 反射 gridInv 失败: {e.Message}"); }

        return null;
    }
}