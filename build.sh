#!/bin/bash
# 编译并部署联机插件到游戏目录
set -e
GAME="E:/SteamLibrary/steamapps/common/Probably Stolen Demo"
cd "$(dirname "$0")"

echo "[1/3] 编译..."
"C:/Users/31074/.dotnet/dotnet.exe" build -c Release

echo "[2/3] 部署插件与依赖..."
cp bin/Release/net6.0/ProbablyStolenCoop.dll "$GAME/BepInEx/plugins/"
# BepInEx 不自动加载 NuGet 依赖,需从 NuGet 缓存复制
cp "C:/Users/31074/.nuget/packages/google.protobuf/3.25.1/lib/net5.0/Google.Protobuf.dll" "$GAME/BepInEx/plugins/"

echo "[3/3] 完成。日志: $GAME/BepInEx/LogOutput.log"
