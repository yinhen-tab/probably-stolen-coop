using System;
using System.IO;
using UnityEngine;

namespace ProbablyStolenCoop;

/// <summary>
/// 联机存档保护(解决"队友存档被房主数据覆盖"):
///  - 以"加入者"身份进房时:把本地存档(save_*.es3 / saves_index.es3 / SaveFile.es3)备份到插件目录
///  - 回到主菜单时(启动 or 从游戏内返回):若存在未恢复的备份 → 还原,撤销联机期间的覆盖
///  - 用 marker 文件做状态标记,因此"进程被杀/崩溃"也能在下次启动时兜底还原
///  - 房主身份不备份(房主用的就是他自己的存档,数据本来就是他的)
/// </summary>
public static class SaveGuard
{
    private const string FlagName = "session.flag";

    static string PluginDir
    {
        get
        {
            try { return Path.GetDirectoryName(typeof(SaveGuard).Assembly.Location) ?? ""; }
            catch { return ""; }
        }
    }

    static string BackupDir
    {
        get
        {
            var d = PluginDir;
            return string.IsNullOrEmpty(d) ? null : Path.Combine(d, "coop_savebackup");
        }
    }

    static string SaveDir
    {
        get { try { return Application.persistentDataPath; } catch { return null; } }
    }

    static bool IsSaveFile(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        if (name == "saves_index.es3" || name == "SaveFile.es3") return true;
        return name.StartsWith("save_") && name.EndsWith(".es3");
    }

    /// <summary>是否有未恢复的备份</summary>
    public static bool HasPendingBackup
    {
        get
        {
            try
            {
                var b = BackupDir;
                return b != null && File.Exists(Path.Combine(b, FlagName));
            }
            catch { return false; }
        }
    }

    /// <summary>进房(加入者)时:备份本地存档</summary>
    public static void BeginSession()
    {
        try
        {
            if (CoopClient.IsHost)
            {
                Plugin.Log.LogInfo("[Coop][存档] 房主身份:使用自己的存档,无需备份");
                return;
            }
            var sdir = SaveDir;
            var bdir = BackupDir;
            if (string.IsNullOrEmpty(sdir) || !Directory.Exists(sdir) || string.IsNullOrEmpty(bdir))
            {
                Plugin.Log.LogWarning($"[Coop][存档] 存档目录不可用(save={sdir}),跳过备份");
                return;
            }
            if (File.Exists(Path.Combine(bdir, FlagName)))
            {
                Plugin.Log.LogInfo("[Coop][存档] 已有未恢复的备份,跳过本次备份(避免覆盖原始备份)");
                return;
            }

            Directory.CreateDirectory(bdir);
            int n = 0;
            foreach (var f in Directory.GetFiles(sdir))
            {
                string name = Path.GetFileName(f);
                if (!IsSaveFile(name)) continue;
                try { File.Copy(f, Path.Combine(bdir, name), true); n++; }
                catch (Exception e) { Plugin.Log.LogWarning($"[Coop][存档] 备份 {name} 失败: {e.Message}"); }
            }
            try
            {
                File.WriteAllText(Path.Combine(bdir, FlagName),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "|" + n + " files");
            }
            catch { }

            Plugin.Log.LogInfo($"[Coop][存档] 已保护你的本地存档:备份 {n} 个文件 → {bdir}");
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop][存档] 备份流程异常: {e.Message}"); }
    }

    /// <summary>回主菜单/启动时:若有未恢复的备份则还原</summary>
    public static void TryRestore()
    {
        try
        {
            var bdir = BackupDir;
            if (string.IsNullOrEmpty(bdir)) return;
            string flag = Path.Combine(bdir, FlagName);
            if (!File.Exists(flag)) return;

            var sdir = SaveDir;
            if (string.IsNullOrEmpty(sdir) || !Directory.Exists(sdir))
            {
                Plugin.Log.LogWarning("[Coop][存档] 存档目录不可用,无法还原");
                return;
            }

            int n = 0;
            foreach (var f in Directory.GetFiles(bdir))
            {
                string name = Path.GetFileName(f);
                if (!IsSaveFile(name)) continue;
                try { File.Copy(f, Path.Combine(sdir, name), true); n++; }
                catch (Exception e) { Plugin.Log.LogWarning($"[Coop][存档] 还原 {name} 失败: {e.Message}"); }
            }
            try { File.Delete(flag); } catch { }

            Plugin.Log.LogInfo($"[Coop][存档] 已还原你的本地存档({n} 个文件);联机期间被房主数据覆盖的进度已撤销");
            CoopHud.Toast("已还原你的本地存档(联机期间改动未保存)");
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop][存档] 还原流程异常: {e.Message}"); }
    }
}
