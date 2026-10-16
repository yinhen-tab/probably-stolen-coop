using System;
using Coop;
using HarmonyLib;

namespace ProbablyStolenCoop;

/// <summary>
/// 机器加工同步:只同步"开始加工 / 加工完成"两个事件,进度条由两端各自的时间驱动。
/// (同步进度数值没意义 —— 两端时钟不同步,而且游戏本身按时间推进)
/// </summary>
public static class MachineSync
{
    public static bool Applying;

    public static void Report(bool start, GameItem machine, GameItem source)
    {
        try
        {
            if (Applying) return;
            if (machine == null) return;
            if (CoopClient.Net == null || !CoopClient.Handshaked) return;
            if (string.IsNullOrEmpty(CoopClient.RoomCode)) return;
            CoopClient.Net.Send(MsgType.ObjectUse, new Coop.ObjectUse
            {
                PlayerId = CoopClient.PlayerId,
                Kind = start ? "machine_start" : "machine_done",
                Uid1 = machine.uniqueId,
                Uid2 = source != null ? source.uniqueId : 0,
            });
            CoopLog.Info($"[Coop] 本地上报机器: {(start ? "开始加工" : "加工完成")} {machine.identifier} uid={machine.uniqueId}");
        }
        catch (Exception e) { CoopLog.Warn($"[Coop] 机器上报异常: {e.Message}"); }
    }

    public static void Apply(Coop.ObjectUse msg)
    {
        try
        {
            var machine = ItemMoveSync.FindItem(msg.Uid1);
            if (machine == null) { CoopLog.Detail($"[Coop] 机器同步: 本端找不到机器 uid={msg.Uid1}"); return; }
            Applying = true;
            try
            {
                if (msg.Kind == "machine_start")
                {
                    var src = msg.Uid2 != 0 ? ItemMoveSync.FindItem(msg.Uid2) : null;
                    MachineProgressHelper.StartProgressTypeMachine(machine, src);
                    CoopLog.Info($"[Coop] 已同步队友的机器开机: {machine.identifier}");
                }
                else
                {
                    MachineProgressHelper.FinishProgressTypeMachine(machine);
                    CoopLog.Info($"[Coop] 已同步队友的机器完成: {machine.identifier}");
                }
            }
            finally { Applying = false; }
        }
        catch (Exception e) { CoopLog.Warn($"[Coop] 机器同步异常: {e.Message}"); }
    }
}

// ---- 钩子:机器加工 ----

[HarmonyPatch(typeof(MachineProgressHelper), nameof(MachineProgressHelper.StartProgressTypeMachine))]
public static class MachineStartPatch
{
    public static void Postfix(GameItem machine, GameItem sourceItem)
    {
        MachineSync.Report(true, machine, sourceItem);
    }
}

[HarmonyPatch(typeof(MachineProgressHelper), nameof(MachineProgressHelper.FinishProgressTypeMachine))]
public static class MachineFinishPatch
{
    public static void Postfix(GameItem machine)
    {
        MachineSync.Report(false, machine, null);
    }
}
