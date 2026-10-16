using System;
using HarmonyLib;
using UnityEngine;

namespace ProbablyStolenCoop;

/// <summary>
/// 音效同步:队友触发的关键音效,本地回放同一个。
/// - 挂钩 AudioManager.Play/Play2/Play3(string identifier):这三个是"一次性音效"(环境/音乐走 PlayAmbient/PlayMusic,不参与同步)。
/// - 上报 identifier + 声道变体;对端本地用同一 identifier 播放 → 音画一致。
/// - 防回环:ApplyingRemote 期间不上报;同一音效 50ms 内去重(避免双击/多路触发重复)。
/// </summary>
public static class AudioSync
{
    public static bool ApplyingRemote;

    private static string _lastId = "";
    private static int _lastVariant = -1;
    private static float _lastAt;

    // ---- 降噪:名单过滤 + 同名去重 + 全局限速(解决"音效偏吵") ----
    private static readonly System.Collections.Generic.Dictionary<string, float> _recent = new();
    private static float _winStart;
    private static int _winCount;
    private const float DupWindow = 0.12f;
    private const int MaxPerSecond = 25;

    static bool Allow(string id)
    {
        float now = Time.time;
        var list = CoopClient.SfxFilter;
        if (list != null && list.Length > 0)
        {
            string low = id.ToLower();
            bool hit = false;
            for (int i = 0; i < list.Length; i++)
            {
                string k = list[i];
                if (!string.IsNullOrEmpty(k) && low.Contains(k)) { hit = true; break; }
            }
            if (CoopClient.SfxFilterIsWhitelist ? !hit : hit) return false;   // 白名单:不在名单就丢
        }
        if (_recent.TryGetValue(id, out var t) && now - t < DupWindow) return false;
        if (now - _winStart >= 1f) { _winStart = now; _winCount = 0; }
        if (++_winCount > MaxPerSecond) return false;
        _recent[id] = now;
        if (_recent.Count > 64) _recent.Clear();
        return true;
    }

    public static void Report(string identifier, int variant)
    {
        try
        {
            if (ApplyingRemote) return;
            if (CoopClient.BlockGameEffects) return;   // 观看/回放上下文衍生的音效不再转播
            if (!CoopClient.SyncAudio) return;
            if (string.IsNullOrEmpty(identifier)) return;
            if (CoopClient.Net == null || !CoopClient.Handshaked) return;
            if (string.IsNullOrEmpty(CoopClient.RoomCode)) return;
            if (identifier == _lastId && variant == _lastVariant && Time.time - _lastAt < 0.05f) return;
            if (!Allow(identifier)) return;
            _lastId = identifier; _lastVariant = variant; _lastAt = Time.time;
            CoopClient.SendSfx(identifier, variant);
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop] 音效上报异常: {e.Message}"); }
    }

    public static void ApplyRemote(Coop.SfxSync m)
    {
        try
        {
            var am = AudioManager.Instance;
            if (am == null) return;
            ApplyingRemote = true;
            try
            {
                bool ok;
                switch (m.Variant)
                {
                    case 1: ok = am.Play2(m.Identifier); break;
                    case 2: ok = am.Play3(m.Identifier); break;
                    default: ok = am.Play(m.Identifier); break;
                }
                if (ok) CoopLog.Detail($"[Coop] 回放队友音效: {m.Identifier}");
            }
            finally { ApplyingRemote = false; }
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop] 音效回放异常: {e.Message}"); }
    }
}

// ---- 钩子:三个一次性音效通道 ----

[HarmonyPatch(typeof(AudioManager), nameof(AudioManager.Play))]
public static class AudioPlayPatch
{
    public static void Postfix(string identifier) { AudioSync.Report(identifier, 0); }
}

[HarmonyPatch(typeof(AudioManager), nameof(AudioManager.Play2))]
public static class AudioPlay2Patch
{
    public static void Postfix(string identifier) { AudioSync.Report(identifier, 1); }
}

[HarmonyPatch(typeof(AudioManager), nameof(AudioManager.Play3))]
public static class AudioPlay3Patch
{
    public static void Postfix(string identifier) { AudioSync.Report(identifier, 2); }
}
