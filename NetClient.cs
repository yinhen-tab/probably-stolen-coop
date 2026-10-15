using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Coop;
using Google.Protobuf;

namespace ProbablyStolenCoop;

/// <summary>
/// 网络客户端(WebSocket 版):后台收发线程 + 并发队列,游戏主线程只做 Pump()
/// - 协议:ws://(本地直连) 或 wss://(公网,经 CDN/反代,如 ws.yinhen.top)
/// - 每条 WebSocket 消息 = 一个 Envelope(protobuf),无长度前缀
/// - 线程安全约定:网络线程绝不碰游戏对象,只回调 OnEnvelope(由上层切换到主线程)
/// </summary>
public sealed class NetClient : IDisposable
{
    private ClientWebSocket _ws;
    private Thread _recvThread;
    private Thread _sendThread;
    private readonly ConcurrentQueue<Envelope> _outbox = new();
    private readonly SemaphoreSlim _outSignal = new(0);
    private volatile bool _running;

    public volatile string Status = "未连接";
    public int PlayerId;
    public System.Action<Envelope> OnEnvelope;   // 收包线程回调

    public bool IsConnected => _running && _ws != null && _ws.State == WebSocketState.Open;

    public void Connect(string host, int port, bool useWss = false)
    {
        var scheme = useWss ? "wss" : "ws";
        var uri = new Uri($"{scheme}://{host}:{port}");
        _ws = new ClientWebSocket();
        try { _ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(20); } catch { }   // 协议级心跳,防中间层空闲断线
        _ws.ConnectAsync(uri, CancellationToken.None).GetAwaiter().GetResult();
        _running = true;
        _recvThread = new Thread(() => RecvLoop().GetAwaiter().GetResult()) { IsBackground = true, Name = "CoopRecv" };
        _sendThread = new Thread(() => SendLoop().GetAwaiter().GetResult()) { IsBackground = true, Name = "CoopSend" };
        _recvThread.Start();
        _sendThread.Start();
        Status = "已连接";
    }

    public void Send(MsgType type, IMessage payload = null)
    {
        if (!_running) return;
        var env = new Envelope { Version = (int)ProtocolVersion.V1, Type = type };
        if (payload != null)
            env.Payload = payload.ToByteString();
        _outbox.Enqueue(env);
        _outSignal.Release();
    }

    private async Task RecvLoop()
    {
        var buf = new byte[65536];
        var ms = new MemoryStream();
        try
        {
            while (_running)
            {
                ms.SetLength(0);
                WebSocketReceiveResult r;
                do
                {
                    r = await _ws.ReceiveAsync(new ArraySegment<byte>(buf), CancellationToken.None);
                    if (r.MessageType == WebSocketMessageType.Close) throw new EndOfStreamException("对端关闭");
                    ms.Write(buf, 0, r.Count);
                    if (ms.Length > (1 << 20)) throw new InvalidDataException("帧超限");
                } while (!r.EndOfMessage);

                var env = Envelope.Parser.ParseFrom(ms.GetBuffer(), 0, (int)ms.Length);
                OnEnvelope?.Invoke(env);
            }
        }
        catch (Exception e)
        {
            Status = "连接断开: " + e.Message;
        }
        _running = false;
    }

    private async Task SendLoop()
    {
        try
        {
            while (_running)
            {
                await _outSignal.WaitAsync();
                if (!_running) break;
                while (_outbox.TryDequeue(out var env))
                {
                    var data = env.ToByteArray();
                    await _ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Binary, true, CancellationToken.None);
                }
            }
        }
        catch
        {
            _running = false;
        }
    }

    public void Dispose()
    {
        _running = false;
        _outSignal.Release();
        try { _ws?.Abort(); } catch { }
        try { _ws?.Dispose(); } catch { }
    }
}
