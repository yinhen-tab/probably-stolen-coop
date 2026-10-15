# 一键安装包制作

## 给朋友用的安装包（一键安装）

1. 本机游戏目录装好 Mod（`BepInEx/plugins/ProbablyStolenCoop.dll` 等）
2. 运行打包器：

```bash
python make_package.py "E:\SteamLibrary\steamapps\common\Probably Stolen Demo"
```

产物：`dist/ProbablyStolen联机一键安装.zip`

3. 朋友拿到 zip → **解压** → **双击 `install.bat`** → 自动找游戏目录并复制环境 → 直接启动游戏即可联机

打包器可通过环境变量改预置服务器地址：

```bash
COOP_PACK_HOST=ws.yinhen.top COOP_PACK_PORT=443 COOP_PACK_WSS=true python make_package.py
```

## 安装包内容

```
install.bat              一键安装(自动找游戏目录, 支持拖拽)
uninstall.bat            卸载插件(保留 BepInEx 环境)
payload/
  winhttp.dll            Doorstop 代理(BepInEx 加载入口)
  doorstop_config.ini
  dotnet/                BepInEx 6 的 .NET 运行时
  BepInEx/
    core/                BepInEx 6 IL2CPP 核心
    plugins/             联机插件 + Google.Protobuf
    config/              预配置(服务器地址已填好)
```

## 说明

- 安装前请先在游戏目录装好 **BepInEx 6 IL2CPP (be.788)**（本包已包含运行时，无需单独下载）
- 游戏目录若有其他 Mod（`BepInEx/plugins/` 下其他 dll），安装不会影响它们
