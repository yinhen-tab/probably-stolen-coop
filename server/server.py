#!/usr/bin/env python3
"""Probably Stolen 联机 mod — 权威服务端(WebSocket 版)

架构:Python asyncio + WebSocket(websockets 库) + Protobuf 消息
- 每连接一个 asyncio task,广播用 gather 扇出
- 权威回合推进:客户端只发意图,服务端发号并广播
- 房间码 4 位,空房自动回收;掉线由 WebSocket 关闭事件检测
- 公网部署:适配 CDN/反代(如腾讯云 EdgeOne 的 wss 转发);
  本地自测直连 ws://127.0.0.1:27500

性能要点:无每帧对象分配、__slots__、单连接写锁防乱序
"""
import asyncio
import itertools
import os
import random
import string
import sys
import time

import websockets

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import coop_pb2 as pb

HOST = os.environ.get('COOP_HOST', '0.0.0.0')
PORT = int(os.environ.get('COOP_PORT', '27500'))
SERVER_VERSION = '0.1.0'
PROTOCOL_VERSION = pb.V1
MAX_FRAME = 1 << 20          # 1MB 帧上限,防恶意长度
TURNS_PER_DAY = 50           # 20min/回合 × 50 = 10h/天(与游戏一致)
ECHO = os.environ.get('COOP_ECHO', '') == '1'   # 单机自测:把自己的操作可见性消息也发回给自己

def log(msg: str) -> None:
    print('[%s] %s' % (time.strftime('%H:%M:%S'), msg), flush=True)


class Connection:
    __slots__ = ('ws', 'player_id', 'player_name', 'room', 'send_lock', 'closed')

    def __init__(self, ws):
        self.ws = ws
        self.player_id = 0
        self.player_name = ''
        self.room = None
        self.send_lock = asyncio.Lock()
        self.closed = False

    async def send(self, msg_type: int, payload: bytes = b'') -> None:
        env = pb.Envelope(version=PROTOCOL_VERSION, type=msg_type, payload=payload)
        data = env.SerializeToString()
        async with self.send_lock:
            if self.closed:
                return
            try:
                await self.ws.send(data)
            except Exception:
                self.closed = True

    async def recv_envelope(self) -> pb.Envelope:
        raw = await self.ws.recv()
        if isinstance(raw, str):
            raw = raw.encode('utf-8')
        if len(raw) == 0 or len(raw) > MAX_FRAME:
            raise ValueError('帧长度非法: %d' % len(raw))
        env = pb.Envelope()
        env.ParseFromString(raw)
        return env


class Room:
    __slots__ = ('code', 'turn_count', 'day', 'players', 'created_at')

    def __init__(self, code: str):
        self.code = code
        self.turn_count = 0
        self.day = 1
        self.players = {}   # player_id -> Connection
        self.created_at = time.monotonic()

    def broadcast(self, msg_type: int, payload: bytes = b'', exclude: int = 0) -> None:
        # exclude: 不发给某个 player_id(如加入者本人不需要知道自己加入)
        targets = [c for pid, c in list(self.players.items()) if pid != exclude]
        if targets:
            asyncio.gather(*(c.send(msg_type, payload) for c in targets))

    def player_infos(self):
        return [pb.PlayerInfo(player_id=c.player_id, player_name=c.player_name)
                for c in self.players.values()]

    def state_msg(self) -> pb.RoomState:
        return pb.RoomState(
            room_code=self.code, turn_count=self.turn_count,
            players=self.player_infos(), day=self.day)


rooms = {}
_next_id = itertools.count(1)
_CODE_ALPHABET = string.ascii_uppercase + string.digits


def new_room_code() -> str:
    while True:
        code = ''.join(random.choices(_CODE_ALPHABET, k=4))
        if code not in rooms:
            return code


async def handle(ws) -> None:
    conn = Connection(ws)
    peer = getattr(ws, 'remote_address', None)
    log('连接来自 %s' % (peer,))
    try:
        while True:
            env = await conn.recv_envelope()
            await dispatch(conn, env)
    except (websockets.exceptions.ConnectionClosed, ConnectionError):
        pass
    except ValueError as e:
        log('协议错误(%s): %s' % (peer, e))
    finally:
        await cleanup(conn)
        try:
            await ws.close()
        except Exception:
            pass


async def dispatch(conn: Connection, env: pb.Envelope) -> None:
    t = env.type

    if t == pb.HELLO:
        hello = pb.Hello()
        hello.ParseFromString(env.payload)
        conn.player_id = next(_next_id)
        conn.player_name = hello.player_name or ('玩家%d' % conn.player_id)
        welcome = pb.Welcome(player_id=conn.player_id,
                             server_protocol=PROTOCOL_VERSION,
                             server_version=SERVER_VERSION)
        await conn.send(pb.WELCOME, welcome.SerializeToString())
        log('玩家 %d(%s) 握手 mod=%s build=%s' % (
            conn.player_id, conn.player_name, hello.mod_version, hello.game_build))
        return

    if conn.player_id == 0:
        await conn.send(pb.ERROR, pb.ErrorMsg(code='NO_HELLO', message='先发送 HELLO').SerializeToString())
        return

    if t == pb.CREATE_ROOM:
        code = new_room_code()
        room = Room(code)
        room.players[conn.player_id] = conn
        rooms[code] = room
        conn.room = room
        await conn.send(pb.ROOM_JOINED, pb.RoomJoined(state=room.state_msg()).SerializeToString())
        log('房间 %s 由玩家 %d 创建' % (code, conn.player_id))
        return

    if t == pb.JOIN_ROOM:
        req = pb.JoinRoom()
        req.ParseFromString(env.payload)
        room = rooms.get(req.room_code.upper())
        if room is None:
            await conn.send(pb.ERROR, pb.ErrorMsg(
                code='ROOM_NOT_FOUND', message='房间不存在: %s' % req.room_code).SerializeToString())
            return
        room.players[conn.player_id] = conn
        conn.room = room
        await conn.send(pb.ROOM_JOINED, pb.RoomJoined(state=room.state_msg()).SerializeToString())
        room.broadcast(pb.PLAYER_JOINED, pb.PlayerInfo(
            player_id=conn.player_id, player_name=conn.player_name).SerializeToString(),
            exclude=conn.player_id)
        log('玩家 %d 加入房间 %s(共 %d 人)' % (conn.player_id, room.code, len(room.players)))
        return

    if conn.room is None:
        # 展示/高频类消息:未入房时静默丢弃(避免刷屏回错)
        if t in (pb.CURSOR_POS, pb.UI_EVENT, pb.PLAYER_CLICK, pb.DRAG_STATE, pb.GAME_EVENT,
                 pb.OBJECT_USE, pb.PHONE_SYNC):
            return
        await conn.send(pb.ERROR, pb.ErrorMsg(code='NOT_IN_ROOM', message='先创建或加入房间').SerializeToString())
        return

    room = conn.room

    if t == pb.ADVANCE_TURN:
        # 权威推进:客户端只表达意图,发号权在服务端
        room.turn_count += 1
        if room.turn_count % TURNS_PER_DAY == 0:
            room.day += 1
        msg = pb.TurnAdvanced(turn_count=room.turn_count, day=room.day,
                              server_time_ms=int(time.time() * 1000))
        room.broadcast(pb.TURN_ADVANCED, msg.SerializeToString())
        log('房间 %s 回合 → %d(第 %d 天)' % (room.code, room.turn_count, room.day))
        return

    if t == pb.EXPEDITION_RESULT:
        # 远征异步化:客户端在自己实例里打完,回传战利品清单
        res = pb.ExpeditionResult()
        res.ParseFromString(env.payload)
        total = sum(l.count * l.unit_value for l in res.loot)
        synced = pb.ExpeditionSynced(player_id=conn.player_id,
                                     expedition_name=res.expedition_name,
                                     loot_total_value=total)
        room.broadcast(pb.EXPEDITION_SYNCED, synced.SerializeToString())
        log('房间 %s 远征回传: %s %d 件总值 %d' % (room.code, res.expedition_name, len(res.loot), total))
        return

    if t == pb.CURSOR_POS:
        # 操作可见性:光标位置高频转发(不记日志,免刷屏)
        if ECHO:
            room.broadcast(pb.CURSOR_POS, env.payload)
        else:
            room.broadcast(pb.CURSOR_POS, env.payload, exclude=conn.player_id)
        return

    if t == pb.UI_EVENT:
        # 操作可见性:UI 窗口开关转发
        ev = pb.UiEvent()
        ev.ParseFromString(env.payload)
        if ECHO:
            room.broadcast(pb.UI_EVENT, env.payload)
        else:
            room.broadcast(pb.UI_EVENT, env.payload, exclude=conn.player_id)
        log('房间 %s UI: 玩家 %d %s [%s]' % (room.code, conn.player_id,
                                             '打开' if ev.open else '关闭',
                                             ev.window_title or ev.window_path))
        return

    if t == pb.PLAYER_CLICK:
        # 点击特效转发(高频但少量)
        if ECHO:
            room.broadcast(pb.PLAYER_CLICK, env.payload)
        else:
            room.broadcast(pb.PLAYER_CLICK, env.payload, exclude=conn.player_id)
        return

    if t == pb.DRAG_STATE:
        # 拖拽状态转发
        if ECHO:
            room.broadcast(pb.DRAG_STATE, env.payload)
        else:
            room.broadcast(pb.DRAG_STATE, env.payload, exclude=conn.player_id)
        return

    if t == pb.DAY_SYNC:
        # 天同步:记录房间天数(取最大)并广播给其他人
        ds = pb.DaySync()
        ds.ParseFromString(env.payload)
        if ds.day > room.day:
            room.day = ds.day
        if ECHO:
            room.broadcast(pb.DAY_SYNC, env.payload)
        else:
            room.broadcast(pb.DAY_SYNC, env.payload, exclude=conn.player_id)
        log('房间 %s 天同步: 玩家 %d 第 %d 天' % (room.code, conn.player_id, ds.day))
        return

    if t == pb.CLIENT_QUEUE_SYNC:
        # 顾客队列快照(房主权威):转发给其他人
        qs = pb.ClientQueueSync()
        qs.ParseFromString(env.payload)
        if ECHO:
            room.broadcast(pb.CLIENT_QUEUE_SYNC, env.payload)
        else:
            room.broadcast(pb.CLIENT_QUEUE_SYNC, env.payload, exclude=conn.player_id)
        log('房间 %s 队列快照: 玩家 %d 共 %d 位 [%s]' % (
            room.code, conn.player_id, len(qs.client_ids),
            ','.join(qs.client_ids[:6])))
        return

    if t == pb.SHOP_SNAPSHOT:
        # 店铺快照(房主权威):转发(带日志摘要)
        ss = pb.StoreSnapshot()
        ss.ParseFromString(env.payload)
        if ECHO:
            room.broadcast(pb.SHOP_SNAPSHOT, env.payload)
        else:
            room.broadcast(pb.SHOP_SNAPSHOT, env.payload, exclude=conn.player_id)
        log('房间 %s 店铺快照: 玩家 %d 资金=%d 第%d天 服务%d项 声望%d项 物品%d种' % (
            room.code, conn.player_id, ss.cash, ss.day,
            len(ss.services), len(ss.reputations), len(ss.items)))
        return

    if t == pb.OBJECT_USE:
        # 店内物件交互:转发(用于对端显示"队友用了什么")
        ou = pb.ObjectUse()
        ou.ParseFromString(env.payload)
        if ECHO:
            room.broadcast(pb.OBJECT_USE, env.payload)
        else:
            room.broadcast(pb.OBJECT_USE, env.payload, exclude=conn.player_id)
        log('房间 %s 物件交互: 玩家 %d %s' % (room.code, conn.player_id, ou.kind))
        return

    if t == pb.PHONE_SYNC:
        # 电话同步:界面开关/拨打/挂断 转发
        ph = pb.PhoneSync()
        ph.ParseFromString(env.payload)
        if ECHO:
            room.broadcast(pb.PHONE_SYNC, env.payload)
        else:
            room.broadcast(pb.PHONE_SYNC, env.payload, exclude=conn.player_id)
        log('房间 %s 电话: 玩家 %d %s%s' % (room.code, conn.player_id, ph.kind,
            (' 号码=%d' % ph.number) if ph.number else ''))
        return

    if t == pb.GAME_EVENT:
        # 游戏事件(叫号/开门/结束一天):转发给其他人(发起者本地已执行)
        ev = pb.GameEvent()
        ev.ParseFromString(env.payload)
        if ECHO:
            room.broadcast(pb.GAME_EVENT, env.payload)
        else:
            room.broadcast(pb.GAME_EVENT, env.payload, exclude=conn.player_id)
        log('房间 %s 游戏事件: 玩家 %d %s' % (room.code, conn.player_id, ev.kind))
        return

    if t == pb.PING:
        ping = pb.Ping()
        ping.ParseFromString(env.payload)
        pong = pb.Pong(client_time_ms=ping.client_time_ms,
                       server_time_ms=int(time.time() * 1000))
        await conn.send(pb.PONG, pong.SerializeToString())
        return

    await conn.send(pb.ERROR, pb.ErrorMsg(code='UNKNOWN', message='未知消息类型 %d' % t).SerializeToString())


async def cleanup(conn: Connection) -> None:
    room = conn.room
    if room is not None:
        room.players.pop(conn.player_id, None)
        if not room.players:
            rooms.pop(room.code, None)
            log('房间 %s 空,已回收' % room.code)
        else:
            room.broadcast(pb.PLAYER_LEFT, pb.PlayerInfo(
                player_id=conn.player_id, player_name=conn.player_name).SerializeToString())
        log('玩家 %d 断开(房间 %s)' % (conn.player_id, room.code))


async def main() -> None:
    async with websockets.serve(handle, HOST, PORT, max_size=MAX_FRAME):
        log('联机服务端启动(WebSocket) %s:%d(协议 V%d, 版本 %s)' % (HOST, PORT, PROTOCOL_VERSION, SERVER_VERSION))
        await asyncio.Future()   # 永久运行


if __name__ == '__main__':
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        log('服务端停止')
