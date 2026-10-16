using System;
using System.IO;
using UnityEngine;

namespace ProbablyStolenCoop;

/// <summary>
/// 存档传输:队友进房 → 直接载入房主的档(世界状态天然 100% 一致:库存布局/资金/天数/机器/声望/顾客)。
/// - 房主: SaveGame() 落盘 → 读 save_&lt;slot&gt;.es3 → 分片上传(经服务端转发)
/// - 队友: 收分片 → 校验 → 备份自己的档(SaveGuard) → 写入同槽位 → 自动点"载入"进入
/// 传输走 WebSocket 二进制帧,单片 256KB(服务端 MAX_FRAME=1MB)。
/// </summary>
public static class SaveTransfer
{
    private const int CHUNK = 256 * 1024;

    /// <summary>功能开关(配置"使用房主的存档")</summary>
    public static bool Enabled = true;

    // ---- 接收状态 ----
    private static byte[] _buf;
    private static int _got;
    private static int _total;
    private static int _slot;
    private static string _hash = "";

    public static bool Receiving => _buf != null;

    public static string SavePath(int slot)
        => Path.Combine(Application.persistentDataPath, "save_" + slot + ".es3");

    // ================= 房主:上传 =================

    /// <summary>房主:把当前存档落盘并上传给队友(收到 SAVE_REQUEST 或有人进房时调用)</summary>
    private static float _lastUploadAt;
    private static string _doneHash = "";
    private static bool _loadTriggered;

    public static void HostUpload()
    {
        if (!Enabled) return;
        // 节流:有人进房(PLAYER_JOINED)和收到索取(SAVE_REQUEST)会先后触发两次,
        // 8 秒内只发一次 —— 否则队友会收到两份,第二份到达时游戏已在读档、主菜单已销毁 → 自动载入失败
        if (Time.time - _lastUploadAt < 8f)
        {
            CoopLog.Detail("[Coop][存档] 刚发过,忽略重复触发");
            return;
        }
        try
        {
            var ps = PlayerStore.Instance;
            if (ps == null) { CoopLog.Warn("[Coop][存档] 房主当前不在游戏中,无法提供存档"); return; }
            ps.SaveGame();                                   // 先落盘,保证是最新状态
            int slot = ps.saveSlotId;
            string path = SavePath(slot);
            if (!File.Exists(path)) { CoopLog.Warn($"[Coop][存档] 未找到存档文件: {path}"); return; }

            byte[] data = File.ReadAllBytes(path);
            string hash = Hash(data);
            CoopClient.SendSaveMeta(slot, data.Length, hash, "save_" + slot);
            int seq = 0;
            for (int off = 0; off < data.Length; off += CHUNK)
            {
                int n = Math.Min(CHUNK, data.Length - off);
                var slice = new byte[n];
                Buffer.BlockCopy(data, off, slice, 0, n);
                CoopClient.SendSaveChunk(seq++, slice);
            }
            _lastUploadAt = Time.time;
            CoopLog.Info($"[Coop][存档] 已上传房主存档: 槽位={slot} {data.Length / 1024}KB / {seq} 片");
        }
        catch (Exception e) { CoopLog.Warn($"[Coop][存档] 上传失败: {e.Message}"); }
    }

    // ================= 队友:接收 =================

    public static void OnMeta(Coop.SaveMeta m)
    {
        if (CoopClient.IsHost) return;          // 房主不接受他人的存档
        if (!Enabled) return;

        // ---- 去重:重复的存档不能再走一遍流程 ----
        string inc = m.Hash ?? "";
        bool inProgress = _buf != null && _got < _total;
        if (inProgress && inc == _hash)
        {
            CoopLog.Detail("[Coop][存档] 重复的存档头(本份正在接收),忽略");
            return;
        }
        if (!string.IsNullOrEmpty(inc) && inc == _doneHash)
        {
            CoopLog.Detail("[Coop][存档] 这份存档已经载入过了,忽略");
            return;
        }
        if (_loadTriggered || GameMaster.current != null)
        {
            CoopLog.Info("[Coop][存档] 已在游戏中,忽略重复的存档传输");
            return;
        }

        _total = m.Total; _slot = m.Slot; _hash = inc;
        _buf = new byte[Math.Max(1, m.Total)];
        _got = 0;
        CoopLog.Info($"[Coop][存档] 开始接收房主存档: 槽位={m.Slot} {m.Total / 1024}KB");
        if (_total <= 0) Finish();
    }

    public static void OnChunk(Coop.SaveChunk c)
    {
        if (CoopClient.IsHost || _buf == null) return;
        try
        {
            byte[] d = c.Data.ToByteArray();
            if (_got + d.Length <= _buf.Length)
            {
                Buffer.BlockCopy(d, 0, _buf, _got, d.Length);
                _got += d.Length;
            }
            if (_got >= _total) Finish();
        }
        catch (Exception e) { CoopLog.Warn($"[Coop][存档] 接收分片异常: {e.Message}"); }
    }

    private static void Finish()
    {
        byte[] data = _buf;
        _buf = null;
        if (data == null) return;
        string h = Hash(data);
        if (!string.IsNullOrEmpty(_hash) && h != _hash)
        {
            CoopLog.Warn($"[Coop][存档] 校验失败(期望 {_hash} / 实际 {h}),已放弃");
            return;
        }
        int slot = _slot;
        try
        {
            SaveGuard.BeginSession();                        // 覆盖前先保护我自己的档
            string path = SavePath(slot);
            File.WriteAllBytes(path, data);
            CoopLog.Info($"[Coop][存档] 房主存档已写入: {path} ({data.Length / 1024}KB)");
        }
        catch (Exception e) { CoopLog.Warn($"[Coop][存档] 写入失败: {e.Message}"); return; }

        _doneHash = h;
        CoopClient.RunOnMain(() => OpenLoadUiThenPick(slot));
    }

    /// <summary>打开游戏原生存档列表,然后自动点对应槽位的"载入"</summary>
    private static void OpenLoadUiThenPick(int slot) => OpenLoadUiRetry(slot, 0);

    /// <summary>主菜单可能还没就绪(时序问题) → 重试几次;若其实已经进游戏了,静默跳过</summary>
    private static void OpenLoadUiRetry(int slot, int attempt)
    {
        try
        {
            var m = MainMenuUIController.Instance;
            if (m == null)
            {
                if (GameMaster.current != null)      // 已经在店里了 → 这份存档本来就是多余的
                {
                    CoopLog.Info("[Coop][存档] 已经进入游戏,无需再载入(忽略重复传输)");
                    return;
                }
                if (attempt < 6)
                {
                    CoopClient.RunOnMainAfter(0.4f, () => OpenLoadUiRetry(slot, attempt + 1));
                    return;
                }
                CoopLog.Warn("[Coop][存档] 主菜单实例为空,请手动载入槽位 " + slot);
                return;
            }
            _loadTriggered = true;
            m.OnLoadGameClick();
            CoopLog.Info($"[Coop][存档] 已打开存档列表,准备自动载入槽位 {slot}");
            CoopClient.RunOnMainAfter(0.6f, () => PickSlot(slot));
        }
        catch (Exception e) { CoopLog.Warn($"[Coop][存档] 打开存档列表失败: {e.Message}"); }
    }

    private static void PickSlot(int slot)
    {
        try
        {
            var arr = UnityEngine.Object.FindObjectsOfType<SaveSlotElement>();
            if (arr == null || arr.Length == 0)
            {
                CoopLog.Warn("[Coop][存档] 未找到存档槽元素,请手动点『载入』");
                return;
            }
            for (int i = 0; i < arr.Length; i++)
            {
                var e = arr[i];
                if (e != null && e.slotId == slot)
                {
                    e.OnLoadClick();
                    CoopLog.Info($"[Coop][存档] 已触发槽位 {slot} 载入(载入房主的档)");
                    return;
                }
            }
            CoopLog.Warn($"[Coop][存档] 存档列表中没有槽位 {slot},请手动选择");
        }
        catch (Exception e) { CoopLog.Warn($"[Coop][存档] 点击槽位异常: {e.Message}"); }
    }

    // ================= 工具 =================

    /// <summary>FNV-1a 32 位 + 长度,用于完整性校验</summary>
    private static string Hash(byte[] d)
    {
        unchecked
        {
            uint h = 2166136261u;
            for (int i = 0; i < d.Length; i++) { h ^= d[i]; h *= 16777619u; }
            return d.Length.ToString("X8") + "-" + h.ToString("X8");
        }
    }
}
