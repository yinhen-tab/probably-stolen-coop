using HarmonyLib;

namespace ProbablyStolenCoop;

/// <summary>TurnManager.NextTurn:本地推进转为意图上报(权威锁步)</summary>
[HarmonyPatch(typeof(TurnManager), nameof(TurnManager.NextTurn))]
public static class TurnManagerNextTurnPatch
{
    public static void Postfix()
    {
        Plugin.Log.LogInfo($"[Coop] TurnManager.NextTurn 被调用(服务端驱动={CoopClient.IsApplyingServer})");
        CoopClient.OnLocalNextTurn();
    }
}

[HarmonyPatch(typeof(TurnManager), nameof(TurnManager.NextNTurns))]
public static class TurnManagerNextNTurnsPatch
{
    public static void Postfix(int n)
    {
        Plugin.Log.LogInfo($"[Coop] TurnManager.NextNTurns({n}) 被调用");
        CoopClient.OnLocalNextTurn();
    }
}
