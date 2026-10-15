@echo off
setlocal enabledelayedexpansion
title Probably Stolen 联机 Mod 卸载
echo 正在卸载联机 Mod...
echo.

set "GAME="
for %%D in (C D E F G H) do (
  if not defined GAME if exist "%%D:/SteamLibrary/steamapps/common/Probably Stolen Demo\BepInEx\plugins\ProbablyStolenCoop.dll" set "GAME=%%D:/SteamLibrary/steamapps/common/Probably Stolen Demo"
  if not defined GAME if exist "%%D:/Steam/steamapps/common/Probably Stolen Demo\BepInEx\plugins\ProbablyStolenCoop.dll" set "GAME=%%D:/Steam/steamapps/common/Probably Stolen Demo"
  if not defined GAME if exist "%%D:/Program Files (x86)\Steam\steamapps\common\Probably Stolen Demo\BepInEx\plugins\ProbablyStolenCoop.dll" set "GAME=%%D:/Program Files (x86)\Steam\steamapps\common\Probably Stolen Demo"
)

if not defined GAME (
  echo [!] 没有自动找到已安装的联机 Mod。
  echo     请把游戏文件夹拖到本窗口后回车(或手动输入路径):
  set /p GAME=
)
set GAME=!GAME:"=!
for /f "tokens=* delims= " %%i in ("!GAME!") do set GAME=%%i

if exist "!GAME!\BepInEx\plugins\ProbablyStolenCoop.dll" (
  del /Q "!GAME!\BepInEx\plugins\ProbablyStolenCoop.dll"
  del /Q "!GAME!\BepInEx\plugins\Google.Protobuf.dll"
  echo [√] 联机插件已删除。
  echo     (BepInEx 运行环境保留, 不影响其他 Mod; 全部清理可整目录删除 BepInEx/dotnet/winhttp.dll)
) else (
  echo [!] 未在游戏目录检测到联机插件。
)
pause
