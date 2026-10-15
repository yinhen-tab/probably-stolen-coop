# Probably Stolen Co-op — 合作经营联机 Mod

为《Probably Stolen (Demo)》打造的合作联机：**你开你的店，朋友开他的店，但你们在同一间店里看得见彼此、共享经营状态。**

> 架构：自建权威服务端（Python asyncio + WebSocket）+ BepInEx 6 插件（C# / Harmony hooks）
> 支持公网部署（CDN/反代 wss，如腾讯云 EdgeOne）。

---

## 功能特性

### 联机基础
- 房间码创建 / 加入（4 位码），空房自动回收
- 主菜单原生风格「多人联机」按钮与面板（右上角 ✕ 关闭）
- 建房 / 加入成功后直接弹出存档选择，一步进店
- **单机零打扰**：不打开联机面板 → 不联网、不上报任何数据

### 看得见对方（操作可见性）
- 队友光标 + 昵称标签（实时跟随）
- 拖拽可见（「拖动中: 物品」幽灵跟随）
- 点击波纹（「朋友点了这里」）
- 窗口开关同步（对方打开的界面本地同步联动）
- 电话同步：对方打开拨号盘 / 电话簿本地同步弹出；对方拨号 → 本地进入同一段通话（号码驱动，两端播放同一内容）

### 状态同步（同一间店）
- **资金**：实时同步
- **天数**：共享 HUD（房间码 / 我第N天 / 队友第M天）
- **经营状态**：服务解锁、声望、机器数量、租金 / 贷款等
- **顾客队列**（房主权威）：叫号时两端弹出同一位顾客
- **物品共享**：队友持有的物品本地缺失时自动补发（经游戏原生通道，游戏正式认可）

### 动作镜像
- 叫下一位顾客 / 开门营业 / 关门打烊 / 结束一天

### 工程保障
- 不进房零上报 + 内容哈希去重 + 指令防回环
- 晚进 / 掉线自动对齐快照
- 服务端对非法 / 未入房消息静默兜底

---

## 架构

```
[游戏客户端 A] ─ BepInEx 6 (IL2CPP) + Harmony ─┐
   NetClient: ClientWebSocket                  ├─ WebSocket + Protobuf ─▶ [Python 权威服务端]
[游戏客户端 B] ─ 同上                          ┘        (wss 经 CDN 反代)      asyncio + websockets
```

- **协议**：`proto/coop.proto`（信封 + 房间 / 回合 / 状态快照 / 队列 / 物品 / 电话等，带版本号）
- **线程模型**：网络线程只做收发与状态处理，游戏对象操作全部回主线程执行
- **公网部署**：服务端监听 WebSocket（默认 27500），可经 EdgeOne / Nginx 反代为 `wss://域名`（443）

---

## 快速开始

### 1. 服务端（任选其一）

**Docker（推荐）**

```bash
docker compose up -d --build
# 或
docker build -t ps-coop-server .
docker run -d --name ps-coop -p 27500:27500 --restart unless-stopped ps-coop-server
```

**裸机（Python 3.10+）**

```bash
cd server
pip install -r requirements.txt
COOP_HOST=0.0.0.0 COOP_PORT=27500 python3 server.py
```

**systemd 常驻**：见 `deploy/coop-server.service`

```bash
sudo cp deploy/coop-server.service /etc/systemd/system/
sudo systemctl enable --now coop-server
```

### 2. 客户端

1. 游戏目录安装 **BepInEx 6 IL2CPP（be.788）**
2. 将 `ProbablyStolenCoop.dll` 与 `Google.Protobuf.dll` 放入 `BepInEx/plugins/`
3. 编辑 `BepInEx/config/com.questinggoose.probablystolen.coop.cfg`：

```ini
[联机]
服务器地址 = ws.yinhen.top   # 你的服务器域名 / IP
服务器端口 = 443             # wss 公网通常 443；本地直连填 27500
加密连接(wss) = true         # 本地 ws:// 直连填 false
```

4. 启动游戏 → 主菜单「多人联机」→ 建房 / 输房间码加入 → 选存档进店

> 一键安装包（含 BepInEx 全套环境）制作见 `installer/`。

### 3. 公网部署（wss，以腾讯云 EdgeOne 为例）

1. 服务端在源站服务器上监听 WebSocket（本仓库默认 27500）
2. EdgeOne 域名（如 `ws.yinhen.top`）配置 **回源到源站端口 27500**，并开启 **WebSocket 转发**
3. 客户端配置：`服务器地址=ws.yinhen.top`、`端口=443`、`加密连接(wss)=true`

---

## 构建（客户端插件）

```bash
# 依赖 .NET SDK 与游戏目录下的 BepInEx 引用（见 csproj 中的 HintPath）
dotnet build -c Release
```

- 协议生成：`protoc -I proto --csharp_out=client proto/coop.proto`（C#）
- 服务端：`python -m grpc_tools.protoc -I proto --python_out=server proto/coop.proto`

## 配置项（客户端 cfg）

| 键 | 说明 |
| --- | --- |
| 启用联机 | 是否允许联机功能（打开面板才连接） |
| 服务器地址 / 服务器端口 | 服务端域名与端口 |
| 加密连接(wss) | true=wss（公网 CDN）；false=ws（本地直连） |
| 玩家昵称 | 显示给队友的名字 |

## 目录结构

```
proto/coop.proto        协议定义（单一来源）
server/server.py        权威服务端（asyncio + websockets）
server/test_e2e.py      服务端集成测试（无需游戏）
client/Coop.cs          protoc 生成的 C# 协议
*.cs                    插件源码（Plugin / CoopClient / NetClient / 各同步模块与钩子）
installer/              一键安装包脚本与打包工具
deploy/                 systemd 服务文件
Dockerfile / docker-compose.yml   服务端容器化
```

## 已知限制

- 物品同步为"只增不减"：队友有的物品可补发，本地物品消耗不会从对方扣除
- 联机要求双方使用**同一游戏版本**与 **Mod 版本**
- 进度 / 存档仍为各自本地存档

## License

MIT
