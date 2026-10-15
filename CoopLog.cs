using BepInEx.Logging;

namespace ProbablyStolenCoop;

/// <summary>日志详细程度(配置"日志详细程度"控制)</summary>
public enum CoopLogLv
{
    Quiet = 0,     // 安静:只留警告与错误
    Normal = 1,    // 正常(默认):连接/房间/存档/错误 + 同步结果
    Verbose = 2,   // 详细:再带上逐条流水(每次上报/回放/心跳)
}

/// <summary>
/// 统一日志出口:按等级过滤,避免高频流水刷屏。
/// - Info  : 关键状态(连接/房间/错误/存档保护/重连)—— 安静模式下也静音
/// - Detail: 常规流水(每次上报、音效回放、顾客行为……)—— 仅"详细"模式输出
/// - Warn/Err: 始终输出
/// 用了 System.Diagnostics.Conditional 不可行(需按运行时等级),故用简单 if。
/// </summary>
public static class CoopLog
{
    public static CoopLogLv Level = CoopLogLv.Normal;

    private static ManualLogSource L => Plugin.Log;

    public static void Info(string msg)
    {
        if (Level >= CoopLogLv.Normal) L?.LogInfo(msg);
    }

    public static void Detail(string msg)
    {
        if (Level >= CoopLogLv.Verbose) L?.LogInfo(msg);
    }

    public static void Warn(string msg) => L?.LogWarning(msg);

    public static void Err(string msg) => L?.LogError(msg);

    public static bool Verbose => Level >= CoopLogLv.Verbose;
}
