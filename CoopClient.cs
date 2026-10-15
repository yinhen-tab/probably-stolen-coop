using System;
using System.Collections;
using System.Collections.Concurrent;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using Coop;
using UnityEngine;

namespace ProbablyStolenCoop;

/// <summary>
/// 联机状态机:连接 → 握手 → 建房/加入 → 消息处理
///
/// 线程模型(诊断版 v2):
///   网络线程:Handle() 只动日志与网络;碰游戏对象的动作一律入 _mainQueue
///   主线程心跳:进游戏后用 "游戏自己的 MonoBehaviour.StartCoroutine" 驱动
///             (每帧消化 _mainQueue + 自动测试调度),不依赖任何 patch 是否生效
/// 权威锁步:本地 NextTurn 只发意图;推进由服务端 TURN_ADVANCED 广播驱动(含发起者)
/// </summary>
public static class CoopClient
{
    public static NetClient Net;
    public static bool WantsConnect;
    public static string ServerHost = "127.0.0.1";
    public static int ServerPort = 27500;
    public static bool UseWss = false;   // true=wss://(公网/CDN), false=ws://(本地直连)
    /// <summary>最近一次服务端错误(展示在联机面板;成功进房时清除)</summary>
    public static volatile string LastErr = "";

    /// <summary>本端是否为房主(创建房间者为房主,顾客队列以其为权威)</summary>
    public static bool IsHost;

    /// <summary>测试用:启动后自动加入该房间码(留空=不自动加入)</summary>
    public static string AutoJoinCode = "";

    public static string RoomCode = "";
    public static int RoomTurnCount;
    public static int RoomDay = 1;
    /// <summary>我当前的天数(StoreStation.dayCounter 巡检上报)</summary>
    public static int MyDay;
    /// <summary>队友最近上报的天数(收到 DAY_SYNC)</summary>
    public static int PeerDay;
    public static string PeerDayName = "";
    public static int PlayerId;
    public static bool Handshaked;

    // ---------- 体验同步：开关与规则 ----------
    /// <summary>是否同步音效(队友音效本地回放)</summary>
    public static bool SyncAudio = true;
    /// <summary>是否同步顾客行为事件(进店/挑选/成交)</summary>
    public static bool SyncBehavior = true;
    /// <summary>通话效果规则:true=对方观看的通话也真实生效;false(默认)=纯观看,对方通话不产生任何效果</summary>
    public static bool PhoneEffectOn = false;
    /// <summary>远端回放上下文:为 true 时本地的"效果"钩子应跳过(避免对方观看/回放时污染本地状态)</summary>
    public static bool BlockGameEffects;

    // ---------- 掉线重连 ----------
    private static bool _reconnecting;
    private static bool _rejoinPending;
    private static int _reconnectAttempts;
    private static float _lastReconnectTry;
    public static int ReconnectAttempts => _reconnectAttempts;
    public static bool Reconnecting => _reconnecting;

    /// <summary>本 mod 协议版本(与服务端校验;不匹配会明确提示而不是静默错乱)</summary>
    public const int ModProtocol = 1;

    /// <summary>自动测试:进主菜单后自动开局,进游戏后自动推进回合(诊断用,正式版关)</summary>
    public static bool AutoTest = false;

    /// <summary>待主线程执行的动作(网络线程入队,心跳协程消化)</summary>
    private static readonly ConcurrentQueue<Action> _mainQueue = new();
    private static readonly ConcurrentQueue<int> _pendingTurns = new();
    private static bool _applyingServer;

    // 光标采集(操作可见性)
    private static int _cursorTick;
    private static float _lastNx = -1f, _lastNy = -1f;

    // 天数巡检(天同步)
    private static int _dayTick;
    private static int _lastSentDay = -999;

    // 玩家信息(联机面板 + 光标名字)
    public static string MyName = "";
    private static readonly System.Collections.Generic.Dictionary<int, string> _players = new();

    public static bool IsApplyingServer => _applyingServer;

    // ---------- 连接 ----------

    public static void TryConnect()
    {
        if (Net != null || !WantsConnect) return;
        try
        {
            Net = new NetClient();
            Net.OnEnvelope = Handle;   // 收包线程直接处理(不碰游戏对象)
            Net.Connect(ServerHost, ServerPort, UseWss);
            Net.Send(MsgType.Hello, new Hello
            {
                PlayerName = string.IsNullOrEmpty(MyName) ? ("玩家" + Environment.TickCount % 10000) : MyName,
                ModVersion = MyPluginInfo.PLUGIN_VERSION,
                GameBuild = "25382790",
                Protocol = ModProtocol,
            });
            CoopLog.Info($"[Coop] 已连接 {ServerHost}:{ServerPort}");
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[Coop] 连接失败: {e.Message}");
            Net = null;
        }
    }

    /// <summary>心跳调用:掉线时自动重连(仅在"曾经进过房间"时启用,单机不受影响)</summary>
    static void TickReconnect()
    {
        if (!WantsConnect) return;
        if (string.IsNullOrEmpty(RoomCode)) return;   // 没进过房 = 单机,不重连

        if (Net != null && Net.IsConnected)
        {
            if (_reconnecting)
            {
                _reconnecting = false;   // 已恢复(由 WELCOME/RoomJoined 完成 rejoin)
                _reconnectAttempts = 0;
                CoopLog.Info("[Coop] 连接已恢复");
            }
            return;
        }

        // 已断开
        if (!_reconnecting)
        {
            _reconnecting = true;
            _rejoinPending = true;
            Plugin.Log.LogWarning("[Coop] 连接已断开,启动自动重连…");
            RunOnMain(() => CoopHud.Toast("连接断开,正在重连…"));
        }
        if (Time.time - _lastReconnectTry < 3f) return;
        _lastReconnectTry = Time.time;
        _reconnectAttempts++;
        if (_reconnectAttempts > 60)
        {
            Plugin.Log.LogWarning("[Coop] 重连已达 60 次仍未成功,停止重连(可手动重新打开联机面板)");
            _reconnecting = false;
            _rejoinPending = false;
            return;
        }
        try
        {
            CoopLog.Info($"[Coop] 重连尝试 #{_reconnectAttempts} → {ServerHost}:{ServerPort}");
            var old = Net;
            Net = null;
            try { old?.Dispose(); } catch { }
            TryConnect();   // 重建连接并握手(WELCOME 里会自动 rejoin)
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[Coop] 重连异常: {e.Message}");
            Net = null;
        }
    }

    // ---------- 主线程调度(队列 + 心跳协程消化) ----------

    /// <summary>从任意线程把动作排到 Unity 主线程(由心跳协程执行)</summary>
    public static void RunOnMain(Action action)
    {
        _mainQueue.Enqueue(action);
    }

    // ---------- 网络线程:消息处理 ----------

    static void Handle(Envelope env)
    {
        switch ((MsgType)env.Type)
        {
            case MsgType.Welcome:
            {
                var w = Welcome.Parser.ParseFrom(env.Payload);
                PlayerId = w.PlayerId;
                Handshaked = true;
                LastErr = "";
                CoopLog.Info($"[Coop] WELCOME id={w.PlayerId} proto={w.ServerProtocol} 服务端最低要求={w.MinProtocol}");
                if (w.MinProtocol > ModProtocol)
                {
                    LastErr = $"版本不匹配: 服务器要求 mod 协议 v{w.MinProtocol},当前 v{ModProtocol}" +
                              (string.IsNullOrEmpty(w.MinModVersion) ? "" : $"(需要 {w.MinModVersion})") + " — 请更新 mod";
                    Plugin.Log.LogWarning($"[Coop] {LastErr}");
                    string err = LastErr;
                    RunOnMain(() => CoopHud.Toast(err));
                }
                TryAutoJoin();   // 测试:自动加入(网络线程直接发,无需主线程)
                // 掉线重连:重连成功后自动重新加入原房间
                if (_rejoinPending && !string.IsNullOrEmpty(RoomCode))
                {
                    CoopLog.Info($"[Coop] 重连成功,重新加入房间 {RoomCode}");
                    Net.Send(MsgType.JoinRoom, new JoinRoom { RoomCode = RoomCode });
                }
                // 不自动建房/加入:只有玩家在"多人联机"面板操作才会进房间(单机不受影响)
                break;
            }
            case MsgType.RoomJoined:
            {
                var j = RoomJoined.Parser.ParseFrom(env.Payload);
                LastErr = "";
                RoomCode = j.State.RoomCode;
                RoomTurnCount = j.State.TurnCount;
                RoomDay = j.State.Day;
                if (!string.IsNullOrEmpty(RoomCode))
                {
                    _lastSentDay = -999;   // 重新上报我的天数
                    if (IsHost) CustomerQueueSync.ForceSendNextTick();   // 进房即发队列快照
                    // 进房即发店铺快照:房主发"权威全量"做初始对齐;队友发普通快照建立增量基准
                    StoreStateSync.ForceSendNextTick(true);
                    SaveGuard.BeginSession();   // 存档保护:以加入者身份进房时备份本地存档
                }
                for (int i = 0; i < j.State.Players.Count; i++)
                {
                    var p = j.State.Players[i];
                    _players[p.PlayerId] = p.PlayerName;
                }
                UpdatePeerName();
                CoopLog.Info($"[Coop] 房间 {RoomCode} | {j.State.Players.Count} 人 | 回合 {RoomTurnCount}");
                if (_rejoinPending)
                {
                    _rejoinPending = false;
                    _reconnecting = false;
                    _reconnectAttempts = 0;
                    RequestResync();   // 请求房主重发全量快照,重连期间的状态差异一次对齐
                    RunOnMain(() => CoopHud.Toast($"已重连到房间 {RoomCode}"));
                    CoopLog.Info($"[Coop] 重连完成,已请求状态对齐(房间 {RoomCode})");
                }
                break;
            }
            case MsgType.PlayerJoined:
            {
                var p = PlayerInfo.Parser.ParseFrom(env.Payload);
                _players[p.PlayerId] = p.PlayerName;
                UpdatePeerName();
                CoopLog.Info($"[Coop] {p.PlayerName} 进入房间");
                string nm = p.PlayerName;
                if (IsHost) CustomerQueueSync.ForceSendNextTick();   // 有人加入 → 重发队列快照
                if (IsHost) StoreStateSync.ForceSendNextTick(true);   // 房主发权威全量,让新加入者立即对齐
                RunOnMain(() => CoopHud.Toast($"{nm} 加入了房间"));
                break;
            }
            case MsgType.PlayerLeft:
            {
                var p = PlayerInfo.Parser.ParseFrom(env.Payload);
                CoopLog.Info($"[Coop] {p.PlayerName} 离开房间");
                break;
            }
            case MsgType.TurnAdvanced:
            {
                var ta = TurnAdvanced.Parser.ParseFrom(env.Payload);
                RoomDay = ta.Day;
                var turn = ta.TurnCount;
                CoopLog.Info($"[Coop] 服务端回合 → {turn}(第 {ta.Day} 天)");
                RunOnMain(() => { RoomTurnCount = turn; ApplyServerTurn(turn); });
                break;
            }
            case MsgType.CursorPos:
            {
                var c = CursorPos.Parser.ParseFrom(env.Payload);
                float nx = c.Nx, ny = c.Ny;
                RunOnMain(() => FriendCursor.Update(nx, ny));
                break;
            }
            case MsgType.UiEvent:
            {
                var ev = UiEvent.Parser.ParseFrom(env.Payload);
                RunOnMain(() => ApplyRemoteUiEvent(ev));
                break;
            }
            case MsgType.PlayerClick:
            {
                var c = Coop.ClickFx.Parser.ParseFrom(env.Payload);
                float nx = c.Nx, ny = c.Ny;
                RunOnMain(() => ClickFx.Show(nx, ny));
                break;
            }
            case MsgType.GameEvent:
            {
                var ev = GameEvent.Parser.ParseFrom(env.Payload);
                RunOnMain(() => ApplyRemoteGameEvent(ev));
                break;
            }
            case MsgType.DragState:
            {
                var d = DragState.Parser.ParseFrom(env.Payload);
                bool dragging = d.Dragging;
                string hint = d.ItemHint;
                RunOnMain(() => FriendCursor.SetDragging(dragging, hint));
                break;
            }
            case MsgType.CustomerLeft:
            {
                var cl = Coop.CustomerLeft.Parser.ParseFrom(env.Payload);
                if (cl.PlayerId != PlayerId)
                    RunOnMain(() => CustomerQueueSync.ApplyRemoteLeft(cl));
                break;
            }
            case MsgType.DaySync:
            {
                var d = DaySync.Parser.ParseFrom(env.Payload);
                if (d.PlayerId == PlayerId)
                {
                    MyDay = d.Day;   // 自环(测试模式)回显
                }
                else
                {
                    PeerDay = d.Day;
                    _players.TryGetValue(d.PlayerId, out var nm);
                    PeerDayName = string.IsNullOrEmpty(nm) ? "队友" : nm;
                    string who = PeerDayName;
                    int dd = d.Day;
                    CoopLog.Detail($"[Coop] 队友天数更新: {who} 第 {dd} 天");
                    RunOnMain(() => CoopHud.Toast($"{who} 已进入第 {dd} 天"));
                }
                break;
            }
            case MsgType.ClientQueueSync:
            {
                var qs = Coop.ClientQueueSync.Parser.ParseFrom(env.Payload);
                if (qs.PlayerId != PlayerId)
                    RunOnMain(() => CustomerQueueSync.ApplyHostQueue(qs));
                break;
            }
            case MsgType.ShopSnapshot:
            {
                var ss = Coop.StoreSnapshot.Parser.ParseFrom(env.Payload);
                if (ss.PlayerId != PlayerId)
                    RunOnMain(() => StoreStateSync.ApplySnapshot(ss));
                break;
            }
            case MsgType.ObjectUse:
            {
                var ou = Coop.ObjectUse.Parser.ParseFrom(env.Payload);
                if (ou.PlayerId != PlayerId)
                    RunOnMain(() => ObjectUseSync.ApplyRemoteUse(ou));
                break;
            }
            case MsgType.PhoneSync:
            {
                var ph = Coop.PhoneSync.Parser.ParseFrom(env.Payload);
                if (ph.PlayerId != PlayerId)
                    RunOnMain(() => PhoneSync.ApplyRemote(ph));
                break;
            }
            case MsgType.SfxSync:
            {
                var sx = Coop.SfxSync.Parser.ParseFrom(env.Payload);
                if (sx.PlayerId != PlayerId)
                    RunOnMain(() => AudioSync.ApplyRemote(sx));
                break;
            }
            case MsgType.ClientBehavior:
            {
                var cb = Coop.ClientBehavior.Parser.ParseFrom(env.Payload);
                if (cb.PlayerId != PlayerId)
                    RunOnMain(() => CustomerBehaviorSync.ApplyRemote(cb));
                break;
            }
            case MsgType.ResyncRequest:
            {
                // 有人重连请求对齐:作为房主强制重发全量快照(队列 + 店铺)
                if (IsHost)
                {
                    RunOnMain(() =>
                    {
                        CustomerQueueSync.ForceSendNextTick();
                        StoreStateSync.ForceSendNextTick();
                    });
                    CoopLog.Info("[Coop] 队友请求重新同步 → 已排程重发快照");
                }
                break;
            }
            case MsgType.ExpeditionSynced:
            {
                var s = ExpeditionSynced.Parser.ParseFrom(env.Payload);
                CoopLog.Info($"[Coop] 远征战利品已入账: {s.ExpeditionName} 总值 {s.LootTotalValue}");
                break;
            }
            case MsgType.Error:
            {
                var e = ErrorMsg.Parser.ParseFrom(env.Payload);
                LastErr = $"{e.Message}({e.Code})";
                Plugin.Log.LogWarning($"[Coop] 服务端错误 {e.Code}: {e.Message}");
                break;
            }
        }
    }

    // ---------- 主线程心跳(游戏协程驱动) ----------

    /// <summary>进游戏后调用:启动长驻心跳(消化主线程队列) + 自动回合测试</summary>
    public static void OnEnteredGame(EmporiumEntry entry)
    {
        try
        {
            entry.StartCoroutine(HeartbeatRoutine().WrapToIl2Cpp());
            CoopLog.Info("[Coop] 主线程心跳已启动(游戏协程驱动)");
            CoopLog.Info(string.IsNullOrEmpty(RoomCode)
                ? "[Coop] 当前为单机模式(未加入联机房间,同步功能静默)"
                : $"[Coop] 当前为联机模式(房间 {RoomCode})");
            if (AutoTest)
            {
                entry.StartCoroutine(AutoTurnRoutine().WrapToIl2Cpp());
                CoopLog.Info("[Coop][测试] 自动回合测试已排程");
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[Coop] 启动心跳失败: {e.Message}");
        }
    }

    static IEnumerator HeartbeatRoutine()
    {
        bool turnSeen = false;
        int hb = 0;
        while (true)
        {
            while (_mainQueue.TryDequeue(out var act))
            {
                try { act(); }
                catch (Exception e) { Plugin.Log.LogWarning($"[Coop] 主线程任务异常: {e.Message}"); }
            }
            while (_pendingTurns.TryDequeue(out var turn))
            {
                RoomTurnCount = turn;
                ApplyServerTurn(turn);
            }
            hb++;
            if (hb % 1800 == 0 && !string.IsNullOrEmpty(RoomCode))   // ~30 秒一次心跳确认(仅联机时)
                CoopLog.Detail($"[Coop] 心跳活跃(第 {hb} 帧), turnManager={(GameMaster.current?.turnManager != null)}");
            if (!turnSeen && GameMaster.current?.turnManager != null)
            {
                turnSeen = true;
                CoopLog.Info("[Coop] ★ TurnManager 出现了!");
            }

            // ---- 光标采集上报(操作可见性,~20Hz;静止时降频) ----
            _cursorTick++;
            if (_cursorTick % 3 == 0 && Net != null && Handshaked && !string.IsNullOrEmpty(RoomCode))
            {
                try
                {
                    var mp = Input.mousePosition;
                    float w = Screen.width, h = Screen.height;
                    if (w > 0 && h > 0)
                    {
                        float nx = mp.x / w, ny = mp.y / h;
                        bool moved = Mathf.Abs(nx - _lastNx) > 0.0012f || Mathf.Abs(ny - _lastNy) > 0.0012f;
                        if (moved || _cursorTick % 180 == 0)   // 静止时每 ~3 秒重发一次防丢
                        {
                            _lastNx = nx; _lastNy = ny;
                            Net.Send(MsgType.CursorPos, new CursorPos { Nx = nx, Ny = ny });
                        }
                    }
                }
                catch { }
            }

            // ---- 天数巡检上报(每 ~1 秒;数据源:StoreStation.dayCounter) ----
            _dayTick++;
            if (_dayTick % 60 == 0 && Net != null && Handshaked && !string.IsNullOrEmpty(RoomCode))
            {
                try
                {
                    var ss = StoreStation.Instance;
                    if (ss != null)
                    {
                        int day = ss.dayCounter;
                        if (day > 0 && day != _lastSentDay)
                        {
                            _lastSentDay = day;
                            MyDay = day;
                            Net.Send(MsgType.DaySync, new DaySync { PlayerId = PlayerId, Day = day });
                            CoopLog.Detail($"[Coop] 本地上报天数: 第 {day} 天");
                        }
                    }
                }
                catch { }
            }

            // ---- 顾客队列快照(房主权威,防抖发送) ----
            CustomerQueueSync.TickSend();

            // ---- 店铺状态快照(房主权威) ----
            StoreStateSync.TickSend();

            // ---- 队友物件高亮圈淡出 ----
            ObjectUseSync.Tick();

            // ---- 队友光标平滑插值 ----
            FriendCursor.Tick();

            // ---- 掉线自动重连 ----
            TickReconnect();

            // ---- 测试:进房后自动打开存档界面 ----
            AutoOpenLoadUi();

            // ---- 点击特效 & UI 刷新 ----
            ClickFx.Tick();
            CoopUI.Tick();
            CoopHud.Tick();
            if (Net != null && Handshaked && !string.IsNullOrEmpty(RoomCode))
            {
                try
                {
                    if (Input.GetMouseButtonDown(0))
                    {
                        var mp = Input.mousePosition;
                        float w = Screen.width, h = Screen.height;
                        if (w > 0 && h > 0) SendClick(mp.x / w, mp.y / h);
                    }
                }
                catch { }
            }

            yield return null;
        }
    }

    static IEnumerator AutoTurnRoutine()
    {
        // 快照 0:进游戏 3 秒
        yield return new WaitForSeconds(3f);
        DumpStoreState("进游戏3s");

        // 大招1:关掉所有教程(教程可能锁住顾客流程)
        yield return new WaitForSeconds(2f);
        try
        {
            var tut = TutorialUIManager.Instance;
            if (tut != null)
            {
                CoopLog.Info("[Coop][测试] 调 TutorialUIManager.CloseAllTutorial() 关教程");
                tut.CloseAllTutorial();
            }
            else
            {
                Plugin.Log.LogWarning("[Coop][测试] TutorialUIManager.Instance 为空");
            }
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop][测试] 关教程异常: {e.Message}"); }

        // 大招2:正式"开始新的一天"(若可)
        yield return new WaitForSeconds(3f);
        try
        {
            var ps = PlayerStore.Instance;
            if (ps != null)
            {
                DumpStoreState("BeginDay前");
                if (ps.CanStartDay())
                {
                    CoopLog.Info("[Coop][测试] CanStartDay=true → 调 BeginDay()");
                    ps.BeginDay();
                }
                else
                {
                    CoopLog.Info("[Coop][测试] CanStartDay=false,跳过 BeginDay");
                }
            }
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop][测试] BeginDay 异常: {e.Message}"); }

        // 大招3:逻辑层开门 + UI 层卷帘门
        yield return new WaitForSeconds(3f);
        try
        {
            var ps = PlayerStore.Instance;
            ps?.OpenShutter();
            CoopLog.Info("[Coop][测试] 已调 PlayerStore.OpenShutter()");
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop][测试] 开门异常: {e.Message}"); }
        yield return new WaitForSeconds(2f);
        try
        {
            var ssb = StoreShutterButton.Instance;
            if (ssb != null)
            {
                CoopLog.Info("[Coop][测试] 调 StoreShutterButton.Instance.OpenShutter()");
                ssb.OpenShutter();
            }
            else
            {
                Plugin.Log.LogWarning("[Coop][测试] StoreShutterButton.Instance 为空");
            }
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop][测试] UI 开门异常: {e.Message}"); }

        // 快照 1:操作后
        yield return new WaitForSeconds(4f);
        DumpStoreState("操作后");

        // 大招4:顾客调度入口(OnNewDay),让顾客真正开始来店
        yield return new WaitForSeconds(2f);
        DumpClientState("调度前");
        try
        {
            var scm = PlayerStore.Instance?.storeClientManager;
            if (scm != null)
            {
                CoopLog.Info("[Coop][测试] 调 StoreClientManager.OnNewDay() 触发顾客调度");
                scm.OnNewDay();
            }
            else
            {
                Plugin.Log.LogWarning("[Coop][测试] storeClientManager 为空");
            }
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop][测试] OnNewDay 异常: {e.Message}"); }
        yield return new WaitForSeconds(3f);
        DumpClientState("OnNewDay后");
        try
        {
            var scm = PlayerStore.Instance?.storeClientManager;
            int stack = -1;
            try { stack = scm?.clientStack?.Count ?? -1; } catch { stack = -2; }
            if (scm != null && stack <= 0)
            {
                CoopLog.Info("[Coop][测试] 顾客栈为空,补调 GenerateClient()");
                scm.GenerateClient();
            }
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop][测试] GenerateClient 异常: {e.Message}"); }
        yield return new WaitForSeconds(3f);
        DumpClientState("GenerateClient后");

        // 大招5:栈里已有顾客 → 叫下一位进店
        try
        {
            var ps = PlayerStore.Instance;
            if (ps != null)
            {
                CoopLog.Info("[Coop][测试] 调 TryCallNextClient()(栈内已有顾客)");
                ps.TryCallNextClient();
            }
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop][测试] TryCallNextClient 异常: {e.Message}"); }
        yield return new WaitForSeconds(5f);
        DumpClientState("叫号后");

        // TurnManager 检查(如出现则做 NextTurn 测试)
        yield return new WaitForSeconds(6f);
        var gm = GameMaster.current;
        var tm = gm?.turnManager;
        CoopLog.Info($"[Coop][测试] {23}f 状态: turnManager={(tm != null)}");
        if (tm != null)
        {
            CoopLog.Info($"[Coop][测试] 开始 NextTurn 测试 (turns={tm.turns})");
            tm.NextTurn();
            yield return new WaitForSeconds(4f);
            CoopLog.Info($"[Coop][测试] 第 1 次 NextTurn 后: turns={tm.turns}, 房间回合={RoomTurnCount}");
        }

        // 快照 2:最终(不再调结束一天,保持白天状态)
        yield return new WaitForSeconds(15f);
        DumpStoreState("最终");
        CoopLog.Info("[Coop][测试] 全部测试完成(保持白天,未结束一天)");
    }

    /// <summary>顾客管理器状态快照</summary>
    static void DumpClientState(string tag)
    {
        try
        {
            var scm = PlayerStore.Instance?.storeClientManager;
            if (scm == null)
            {
                Plugin.Log.LogWarning($"[Coop][顾客·{tag}] storeClientManager 为空");
                return;
            }
            int stack = -1;
            try { stack = scm.clientStack?.Count ?? -1; } catch { stack = -2; }
            int normal = -1;
            try { normal = scm.GetNormalCustomerCount(); } catch { normal = -2; }
            CoopLog.Info($"[Coop][顾客·{tag}] 顾客栈={stack} 普通顾客数={normal}");
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[Coop][顾客·{tag}] 异常: {e.Message}");
        }
    }

    /// <summary>玩家商店状态快照(诊断锁定/顾客队列)</summary>
    static void DumpStoreState(string tag)
    {
        try
        {
            var ps = PlayerStore.Instance;
            if (ps == null)
            {
                Plugin.Log.LogWarning($"[Coop][状态·{tag}] PlayerStore.Instance 为空");
                return;
            }
            var q = ps.futurStoreClientDayQueue;
            int qc = -1;
            try { qc = q != null ? q.Count : -2; } catch { qc = -3; }
            CoopLog.Info($"[Coop][状态·{tag}] 现金={ps.playerCash} 电源={ps.isPowerOn} 租金日={ps.GetRentDayCounter()} CanStartDay={ps.CanStartDay()} 顾客队列={qc} | 锁[All={ps.lockAllFlag} Shutter={ps.lockShutterButtonFlag} NextBell={ps.lockNextBellFlag} ClientClick={ps.lockClientClickFlag} Physical={ps.lockStorePhysicalFlag} Inv={ps.lockAllInventoryFlag}]");
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[Coop][状态·{tag}] 异常: {e.Message}");
        }
    }

    // ---------- 回合 ----------

    /// <summary>兜底:GameMaster.Update 每帧调用(正常路径已走心跳)</summary>
    public static void FlushPending()
    {
        while (_pendingTurns.TryDequeue(out var turn))
        {
            RoomTurnCount = turn;
            ApplyServerTurn(turn);
        }
    }

    /// <summary>TurnManager.NextTurn 的 Postfix 调用:联机模式下转为意图上报</summary>
    public static void OnLocalNextTurn()
    {
        if (Net == null || !Handshaked || _applyingServer) return;
        CoopLog.Info("[Coop] 本地推进触发 → 上报服务端意图");
        Net.Send(MsgType.AdvanceTurn, new AdvanceTurn());
    }

    /// <summary>服务端广播驱动本地推进(防递归标志)</summary>
    static void ApplyServerTurn(int turn)
    {
        var tm = GameMaster.current?.turnManager;
        if (tm == null)
        {
            Plugin.Log.LogWarning("[Coop] TurnManager 不可用,回合无法本地同步");
            return;
        }
        _applyingServer = true;
        try
        {
            CoopLog.Info($"[Coop] 服务端驱动本地推进 → turn {turn}");
            tm.NextTurn();
        }
        finally
        {
            _applyingServer = false;
        }
    }

    // ---------- 操作可见性(UI 窗口同步) ----------

    /// <summary>上报本地 UI 事件(由 WindowSync 在 PixelWindow.Show/Hide 时调用)</summary>
    public static void SendUiEvent(string path, string title, bool open)
    {
        if (Net == null || !Handshaked || string.IsNullOrEmpty(RoomCode)) return;
        try
        {
            Net.Send(MsgType.UiEvent, new UiEvent { WindowPath = path, WindowTitle = title, Open = open });
            CoopLog.Detail($"[Coop] 本地上报UI: {(open ? "打开" : "关闭")} {title} [{path}]");
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[Coop] UI上报失败: {e.Message}");
        }
    }

    /// <summary>上报本地电话操作(界面开关/拨打/挂断)</summary>
    public static void SendPhoneSync(string kind, long number)
    {
        if (Net == null || !Handshaked || string.IsNullOrEmpty(RoomCode)) return;
        try
        {
            Net.Send(MsgType.PhoneSync, new Coop.PhoneSync { PlayerId = PlayerId, Kind = kind, Number = number });
            CoopLog.Detail($"[Coop] 本地上报电话: {kind}{(number != 0 ? " " + number : "")}");
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[Coop] 电话上报失败: {e.Message}");
        }
    }

    /// <summary>音效上报(队友本地回放)</summary>
    public static void SendSfx(string identifier, int variant)
    {
        if (Net == null || !Handshaked || string.IsNullOrEmpty(RoomCode)) return;
        try { Net.Send(MsgType.SfxSync, new Coop.SfxSync { PlayerId = PlayerId, Identifier = identifier, Variant = variant }); }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop] 音效上报失败: {e.Message}"); }
    }

    /// <summary>顾客行为事件上报</summary>
    public static void SendClientBehavior(string clientId, string kind, string detail, int amount)
    {
        if (Net == null || !Handshaked || string.IsNullOrEmpty(RoomCode)) return;
        try
        {
            Net.Send(MsgType.ClientBehavior, new Coop.ClientBehavior
            { PlayerId = PlayerId, ClientId = clientId, Kind = kind, Detail = detail ?? "", Amount = amount });
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop] 顾客行为上报失败: {e.Message}"); }
    }

    /// <summary>重连成功后请求房主重发全量快照(状态对齐)</summary>
    public static void RequestResync()
    {
        if (Net == null || !Handshaked || string.IsNullOrEmpty(RoomCode)) return;
        try
        {
            Net.Send(MsgType.ResyncRequest, new Coop.ResyncRequest { PlayerId = PlayerId });
            CoopLog.Info("[Coop] 已请求重新同步(重连对齐)");
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop] 请求重同步失败: {e.Message}"); }
    }

    /// <summary>执行远端 UI 事件(主线程;期间置 ApplyingRemote 防回环上报)</summary>
    static void ApplyRemoteUiEvent(UiEvent ev)
    {
        if (GameMaster.current == null) return;   // 未在店铺场景(主菜单/加载中),忽略远端 UI 事件
        try
        {
            WindowSync.ApplyingRemote = true;
            var win = FindWindowByPath(ev.WindowPath);
            if (win == null)
            {
                Plugin.Log.LogWarning($"[Coop] 远端窗口未找到: {ev.WindowTitle} [{ev.WindowPath}]");
                return;
            }
            if (ev.Open) win.Show();
            else win.Hide();
            CoopLog.Detail($"[Coop] 远端UI已执行: {(ev.Open ? "打开" : "关闭")} {ev.WindowTitle}");
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[Coop] 远端UI执行异常: {e.Message}");
        }
        finally
        {
            WindowSync.ApplyingRemote = false;
        }
    }

    private static readonly System.Collections.Generic.Dictionary<string, PixelWindow> _winCache = new();

    /// <summary>按场景路径查找窗口(缓存 + TreeNodeRender 全量扫描兜底,含未激活)</summary>
    static PixelWindow FindWindowByPath(string path)
    {
        if (_winCache.TryGetValue(path, out var cached) && cached != null) return cached;
        int scanned = 0, added = 0;
        try
        {
            // PixelWindow 是普通类(非 UnityEngine.Object),FindObjectsOfTypeAll 找不到;
            // 但 TreeNodeRender(MonoBehaviour,窗口节点)持有 window 引用 —— 从它反查
            var all = Resources.FindObjectsOfTypeAll(Il2CppInterop.Runtime.Il2CppType.Of<TreeNodeRender>());
            if (all != null)
            {
                for (int i = 0; i < all.Length; i++)
                {
                    try
                    {
                        var obj = all[i];
                        if (obj == null) continue;
                        scanned++;
                        var rend = obj.TryCast<TreeNodeRender>();
                        if (rend == null) continue;
                        var win = rend.window;
                        if (win == null) continue;
                        // 节点自身路径
                        try
                        {
                            var rt = rend.rectTransform;
                            if (rt != null)
                            {
                                string p1 = WindowSync.GetPath(rt);
                                if (!string.IsNullOrEmpty(p1)) { _winCache[p1] = win; added++; }
                            }
                        }
                        catch { }
                        // 窗口自身 rectTransform 路径
                        try
                        {
                            var wrt = win.rectTransform;
                            if (wrt != null)
                            {
                                string p2 = WindowSync.GetPath(wrt);
                                if (!string.IsNullOrEmpty(p2)) { _winCache[p2] = win; added++; }
                            }
                        }
                        catch { }
                    }
                    catch { }
                }
            }
            CoopLog.Detail($"[Coop] 窗口扫描: scanned={scanned} added={added} cache={_winCache.Count}");
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[Coop] 窗口扫描异常(scanned={scanned}): {e.Message}");
        }
        return _winCache.TryGetValue(path, out var found) ? found : null;
    }

    // ---------- 联机面板支持 ----------

    /// <summary>UI:创建房间</summary>
    public static void RequestCreateRoom()
    {
        if (Net == null || !Handshaked) return;
        if (!string.IsNullOrEmpty(RoomCode)) return;
        IsHost = true;   // 创建者=房主(顾客队列权威)
        Net.Send(MsgType.CreateRoom);
        CoopLog.Info("[Coop] 请求创建房间…");
    }

    /// <summary>UI:加入房间</summary>
    public static void RequestJoinRoom(string code)
    {
        if (Net == null || !Handshaked) return;
        if (!string.IsNullOrEmpty(RoomCode)) return;
        IsHost = false;   // 加入者:顾客队列跟随房主
        Net.Send(MsgType.JoinRoom, new JoinRoom { RoomCode = code });
        CoopLog.Info($"[Coop] 请求加入房间 {code}…");
    }

    public static string GetStatusLine()
    {
        if (!string.IsNullOrEmpty(LastErr)) return "⚠ " + LastErr;
        string srv;
        if (Handshaked) srv = $"服务器:已连接({ServerHost}:{ServerPort})";
        else if (Net == null) srv = $"服务器:未连接({ServerHost}:{ServerPort})";
        else srv = "服务器:连接中…";
        string me = string.IsNullOrEmpty(MyName) ? "" : $" | 我:{MyName}";
        return srv + me;
    }

    public static string GetPlayersLine()
    {
        if (string.IsNullOrEmpty(RoomCode)) return "尚未加入房间";
        var sb = new System.Text.StringBuilder("房间成员: ");
        int n = 0;
        foreach (var kv in _players)
        {
            if (n++ > 0) sb.Append("、");
            sb.Append(kv.Value);
        }
        sb.Append($" ({_players.Count} 人)");
        return sb.ToString();
    }

    /// <summary>更新队友名字(光标标签用):取房间里"不是我"的第一个玩家</summary>
    static void UpdatePeerName()
    {
        foreach (var kv in _players)
        {
            if (kv.Key != PlayerId)
            {
                FriendCursor.SetPeerName(kv.Value);
                CoopLog.Info($"[Coop] 队友= {kv.Value}");
                return;
            }
        }
    }

    // ---------- 测试辅助:自动加入房间并自动载入存档 ----------

    /// <summary>配置了"自动加入房间码"时:连接握手后自动加入并打开存档界面</summary>
    public static void TryAutoJoin()
    {
        if (string.IsNullOrEmpty(AutoJoinCode)) return;
        try
        {
            if (!Handshaked) return;
            CoopLog.Info($"[Coop][测试] 自动加入房间 {AutoJoinCode}");
            IsHost = false;
            Net.Send(MsgType.JoinRoom, new JoinRoom { RoomCode = AutoJoinCode.Trim().ToUpper() });
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop][测试] 自动加入失败: {e.Message}"); }
    }

    /// <summary>主菜单时:若配置了自动加入码,则立刻连接(不等玩家点面板)</summary>
    public static void AutoConnectIfConfigured()
    {
        if (string.IsNullOrEmpty(AutoJoinCode)) return;
        try { TryConnect(); }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop][测试] 自动连接失败: {e.Message}"); }
    }

    /// <summary>进房后(测试)自动打开存档界面</summary>
    public static void AutoOpenLoadUi()
    {
        if (string.IsNullOrEmpty(AutoJoinCode)) return;
        try
        {
            if (string.IsNullOrEmpty(RoomCode)) return;
            if (CoopUI.GetLoadUiShown()) return;
            var m = MainMenuUIController.Instance;
            if (m == null) return;
            CoopUI.MarkLoadUiShown();
            m.OnLoadGameClick();
            CoopLog.Info("[Coop][测试] 已自动打开存档界面");
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop][测试] 自动打开存档界面失败: {e.Message}"); }
    }

    // ---------- 事件发送(操作同步) ----------

    public static void SendGameEvent(string kind)
    {
        if (Net == null || !Handshaked || string.IsNullOrEmpty(RoomCode)) return;
        try
        {
            Net.Send(MsgType.GameEvent, new GameEvent { Kind = kind });
            CoopLog.Detail($"[Coop] 本地上报游戏事件: {kind}");
        }
        catch (Exception e) { Plugin.Log.LogWarning($"[Coop] 事件上报失败: {e.Message}"); }
    }

    public static void SendClick(float nx, float ny)
    {
        if (Net == null || !Handshaked || string.IsNullOrEmpty(RoomCode)) return;
        try { Net.Send(MsgType.PlayerClick, new Coop.ClickFx { Nx = nx, Ny = ny }); }
        catch { }
    }

    public static void SendDrag(bool dragging, string hint)
    {
        if (Net == null || !Handshaked || string.IsNullOrEmpty(RoomCode)) return;
        try { Net.Send(MsgType.DragState, new DragState { Dragging = dragging, ItemHint = hint ?? "" }); }
        catch { }
    }

    /// <summary>执行远端游戏事件(主线程;防回环)</summary>
    static void ApplyRemoteGameEvent(GameEvent ev)
    {
        if (GameMaster.current == null) return;   // 未在店铺场景,忽略远端游戏事件
        try
        {
            GameEventSync.ApplyingRemote = true;
            switch (ev.Kind)
            {
                case GameEventSync.KIND_CALL_NEXT:
                    PlayerStore.Instance?.TryCallNextClient();
                    break;
                case GameEventSync.KIND_OPEN_SHUTTER:
                    StoreShutterButton.Instance?.OpenShutter();
                    break;
                case GameEventSync.KIND_CLOSE_SHUTTER:
                    StoreShutterButton.Instance?.CloseShutter();
                    break;
                case GameEventSync.KIND_END_DAY:
                    EndOfDayUIManager.Instance?.OnEndDayButtonClick();
                    break;
                default:
                    Plugin.Log.LogWarning($"[Coop] 未知游戏事件: {ev.Kind}");
                    return;
            }
            CoopLog.Detail($"[Coop] 远端游戏事件已执行: {ev.Kind}");
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[Coop] 远端事件执行异常: {e.Message}");
        }
        finally
        {
            GameEventSync.ApplyingRemote = false;
        }
    }

    // ---------- 自动开局(诊断用) ----------

    /// <summary>主菜单就绪后调用:自动开局(新游戏 → 水商职业 → 开始)</summary>
    public static void StartAutoBoot(MainMenuUIController menu)
    {
        try
        {
            menu.StartCoroutine(BootRoutine(menu).WrapToIl2Cpp());
            CoopLog.Info("[Coop][测试] 自动开局协程已启动");
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[Coop][测试] 启动自动开局失败: {e.Message}");
        }
    }

    static IEnumerator BootRoutine(MainMenuUIController menu)
    {
        yield return new WaitForSeconds(1.5f);
        CoopLog.Info("[Coop][测试] 自动开局 1/4: OnNewGameClick");
        menu.OnNewGameClick();
        yield return new WaitForSeconds(1.5f);

        CoopLog.Info("[Coop][测试] 自动开局 2/4: OnWaterMerchantClick(选职业)");
        menu.OnWaterMerchantClick();
        yield return new WaitForSeconds(1.0f);

        // 设置开局开关(反汇编确认):
        //   OnSkipIntroToggled  = skipIntro = skipToggle.isOn(直接赋值,可安全双写)
        //   OnEndlessToggled    = endlessMode = !endlessMode(翻转!只能按需调一次)
        try
        {
            menu.skipToggle.isOn = true;
            menu.endlessToggle.isOn = true;
            menu.OnSkipIntroToggled();
            var ngd = NewGameData.Instance;
            if (ngd != null)
            {
                if (!ngd.endlessMode) menu.OnEndlessToggled();   // 读后修:确保 true
                if (!ngd.skipIntro) menu.OnSkipIntroToggled();   // 读后修:确保 true
                ngd.disableTutorialHint = true;
                CoopLog.Info($"[Coop][测试] 开关校验: skipIntro={ngd.skipIntro} endlessMode={ngd.endlessMode} startType={ngd.startType}");
            }
            else
            {
                Plugin.Log.LogWarning("[Coop][测试] NewGameData.Instance 为空,无法校验开关");
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[Coop][测试] 设置 Toggle 异常: {e.Message}");
        }

        yield return new WaitForSeconds(1.5f);
        CoopLog.Info("[Coop][测试] 自动开局 3/5: 触发开始按钮(onClick.Invoke) → 应打开初始特性界面");
        try
        {
            if (menu.startGameButton != null)
            {
                menu.startGameButton.onClick.Invoke();
            }
            else
            {
                Plugin.Log.LogWarning("[Coop][测试] startGameButton 为空,回退直接调方法");
                menu.StartNewGameClick();
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[Coop][测试] onClick 异常,回退直接调方法: {e.Message}");
            try { menu.StartNewGameClick(); } catch (Exception e2) { Plugin.Log.LogWarning($"[Coop][测试] 回退也失败: {e2.Message}"); }
        }

        // 4/5: 完成初始特性选择(这一步在真实流程里由玩家点"开始游戏"按钮完成)
        yield return new WaitForSeconds(2.5f);
        CoopLog.Info("[Coop][测试] 自动开局 4/5: OnPerkFinishedClick() 完成特性→进游戏");
        try
        {
            menu.OnPerkFinishedClick();
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[Coop][测试] OnPerkFinishedClick 异常: {e.Message}");
        }

        // 5/5: 兜底(若 3 秒后没进游戏,尝试直接开始)
        yield return new WaitForSeconds(3f);
        try
        {
            if (EmporiumEntry.Instance == null)
            {
                Plugin.Log.LogWarning("[Coop][测试] 3 秒后仍未进游戏,兜底调 StartNewGameClick()");
                menu.StartNewGameClick();
            }
            else
            {
                CoopLog.Info("[Coop][测试] 已进入游戏场景(EmporiumEntry 存在)");
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[Coop][测试] 兜底检查异常: {e.Message}");
        }
    }

    /// <summary>打印世界状态(探针)</summary>
    public static void DumpWorldState(string tag)
    {
        var gm = GameMaster.current;
        var tm = gm?.turnManager;
        CoopLog.Info($"[Coop] 世界状态[{tag}]: GameMaster={(gm != null)} turnManager={(tm != null)}" + (tm != null ? $" turns={tm.turns}" : ""));
    }

    // ---------- 配置 ----------

    public static void LoadConfig(ConfigFile config)
    {
        WantsConnect = config.Bind("联机", "启用联机", true, "是否允许联机功能(点击主菜单\"多人联机\"后才连接服务器)").Value;
        ServerHost = config.Bind("联机", "服务器地址", "127.0.0.1", "权威服务器域名(公网如 ws.yinhen.top)").Value;
        ServerPort = config.Bind("联机", "服务器端口", 27500, "权威服务器端口(WSS 公网通常填 443)").Value;
        UseWss = config.Bind("联机", "加密连接(wss)", false, "false=ws(本地/直连); true=wss(公网 CDN/HTTPS 转发,如 EdgeOne)").Value;
        MyName = config.Bind("联机", "玩家昵称", "玩家" + Environment.TickCount % 10000, "显示给队友的名字").Value;
        SyncAudio = config.Bind("联机", "同步音效", true, "true=队友触发的音效在你这边也回放(音画一致)").Value;
        SyncBehavior = config.Bind("联机", "同步顾客行为", true, "true=顾客进店/挑选/成交事件同步,保证同一顾客两端结果一致").Value;
        PhoneEffectOn = config.Bind("联机", "通话效果对对方生效", false, "false(默认)=你打电话时队友只是观看,不产生任何效果;true=队友端也真实生效").Value;
        var logLv = config.Bind("联机", "日志详细程度", "正常", "安静=只留警告与错误; 正常=连接/房间/存档/错误+同步结果; 详细=再含逐条流水(排查问题用)").Value;
        CoopLog.Level = logLv == "安静" ? CoopLogLv.Quiet : (logLv == "详细" ? CoopLogLv.Verbose : CoopLogLv.Normal);
        AutoTest = config.Bind("测试", "自动开局与回合测试", false, "诊断用:自动从主菜单开局并推进回合(正式联机请保持 false)").Value;
        // 自动加入码:优先环境变量,其次同目录测试文件 coop_test.txt(内容=房间码)
        try
        {
            var env = System.Environment.GetEnvironmentVariable("COOP_JOIN_CODE");
            if (!string.IsNullOrEmpty(env)) AutoJoinCode = env;
        }
        catch { }
        try
        {
            var testFile = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "", "coop_test.txt");
            if (System.IO.File.Exists(testFile))
            {
                var txt = System.IO.File.ReadAllText(testFile).Trim();
                if (txt.Length > 0) AutoJoinCode = txt.Split('\n')[0].Trim();
            }
        }
        catch { }
        CoopLog.Info($"[Coop] 配置载入: 自动加入码={(string.IsNullOrEmpty(AutoJoinCode) ? "(无)" : AutoJoinCode)}");
    }
}
