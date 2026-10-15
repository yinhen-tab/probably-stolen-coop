#!/usr/bin/env python3
"""端到端测试:同进程起服务端 + 两个模拟客户端
验证:握手 → 建房 → 加入 → A 推进回合 → B 收到广播 → 断线通知
"""
import asyncio
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import server
import coop_pb2 as pb

HOST, PORT = '127.0.0.1', 27599
_LEN = server._LEN


async def send(writer, msg_type, payload=b''):
    env = pb.Envelope(version=pb.V1, type=msg_type, payload=payload)
    data = env.SerializeToString()
    writer.write(_LEN.pack(len(data)) + data)
    await writer.drain()


async def recv(reader, timeout=5.0):
    header = await asyncio.wait_for(reader.readexactly(4), timeout)
    (length,) = _LEN.unpack(header)
    data = await asyncio.wait_for(reader.readexactly(length), timeout)
    env = pb.Envelope()
    env.ParseFromString(data)
    return env


class Client:
    def __init__(self, name):
        self.name = name

    async def connect(self):
        self.reader, self.writer = await asyncio.open_connection(HOST, PORT)
        await send(self.writer, pb.HELLO,
                   pb.Hello(player_name=self.name, mod_version='test', game_build='25382790').SerializeToString())
        env = await recv(self.reader)
        assert env.type == pb.WELCOME, '应收到 WELCOME'
        welcome = pb.Welcome()
        welcome.ParseFromString(env.payload)
        self.player_id = welcome.player_id
        print('  [%s] WELCOME player_id=%d proto=%d' % (self.name, welcome.player_id, welcome.server_protocol))
        return self

    async def create_room(self):
        await send(self.writer, pb.CREATE_ROOM)
        env = await recv(self.reader)
        assert env.type == pb.ROOM_JOINED, '应收到 ROOM_JOINED'
        joined = pb.RoomJoined()
        joined.ParseFromString(env.payload)
        self.room_code = joined.state.room_code
        print('  [%s] 创建房间 %s' % (self.name, self.room_code))
        return self.room_code

    async def join_room(self, code):
        await send(self.writer, pb.JOIN_ROOM, pb.JoinRoom(room_code=code).SerializeToString())
        env = await recv(self.reader)
        assert env.type == pb.ROOM_JOINED, '应收到 ROOM_JOINED'
        print('  [%s] 加入房间 %s' % (self.name, code))
        return self

    async def advance_turn(self):
        await send(self.writer, pb.ADVANCE_TURN)

    async def wait_turn(self, expect_count):
        env = await recv(self.reader, timeout=5.0)
        assert env.type == pb.TURN_ADVANCED, '应收到 TURN_ADVANCED,实得 %d' % env.type
        ta = pb.TurnAdvanced()
        ta.ParseFromString(env.payload)
        print('  [%s] 收到 TURN_ADVANCED turn=%d day=%d' % (self.name, ta.turn_count, ta.day))
        assert ta.turn_count == expect_count, '回合数应为 %d,实得 %d' % (expect_count, ta.turn_count)
        return ta

    async def wait_player_joined(self):
        env = await recv(self.reader, timeout=5.0)
        assert env.type == pb.PLAYER_JOINED, '应收到 PLAYER_JOINED'
        pi = pb.PlayerInfo()
        pi.ParseFromString(env.payload)
        print('  [%s] 收到 PLAYER_JOINED: %s' % (self.name, pi.player_name))
        return pi

    async def wait_player_left(self):
        env = await recv(self.reader, timeout=5.0)
        assert env.type == pb.PLAYER_LEFT, '应收到 PLAYER_LEFT'
        pi = pb.PlayerInfo()
        pi.ParseFromString(env.payload)
        print('  [%s] 收到 PLAYER_LEFT: %s' % (self.name, pi.player_name))
        return pi

    def close(self):
        self.writer.close()


async def main():
    srv = await asyncio.start_server(server.handle, HOST, PORT)
    print('[1] 服务端已启动(测试端口 %d)' % PORT)

    a = await Client('A').connect()
    b = await Client('B').connect()
    code = await a.create_room()
    await b.join_room(code)
    await a.wait_player_joined()

    print('[2] 回合推进测试(A 推进 3 回合,B 都应收到)')
    for i in (1, 2, 3):
        await a.advance_turn()
        await a.wait_turn(i)
        await b.wait_turn(i)

    print('[3] 掉线通知测试(B 断开,A 应收到 PLAYER_LEFT)')
    b.close()
    await asyncio.sleep(0.3)
    await a.wait_player_left()

    print('[4] Ping/Pong 测试')
    await send(a.writer, pb.PING, pb.Ping(client_time_ms=12345).SerializeToString())
    env = await recv(a.reader)
    assert env.type == pb.PONG
    pong = pb.Pong()
    pong.ParseFromString(env.payload)
    assert pong.client_time_ms == 12345
    print('  [A] PONG 往返 OK(server_time=%d)' % pong.server_time_ms)

    a.close()
    await asyncio.sleep(0.2)
    srv.close()
    print('========== 端到端测试全部通过 ==========')


if __name__ == '__main__':
    asyncio.run(main())
