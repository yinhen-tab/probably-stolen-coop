using HarmonyLib;

namespace ProbablyStolenCoop;

/// <summary>GameMaster.Update(进游戏后每帧):FlushPending 兜底 + 触发诊断(首帧即报)</summary>
[HarmonyPatch(typeof(GameMaster), "Update")]
public static class GameMasterUpdatePatch
{
    private static bool _firstLogged;
    private static int _frames;

    public static void Postfix()
    {
        CoopClient.FlushPending();
        _frames++;
        if (!_firstLogged)
        {
            _firstLogged = true;
            var gm = GameMaster.current;
            Plugin.Log.LogInfo($"[Coop] GameMaster.Update 首次触发! current={(gm != null)} turnManager={(gm?.turnManager != null)}");
            return;
        }
        if (_frames == 600)
        {
            var gm = GameMaster.current;
            Plugin.Log.LogInfo($"[Coop] GameMaster.Update 已跑 600 帧(turnManager={(gm?.turnManager != null)})");
        }
    }
}
