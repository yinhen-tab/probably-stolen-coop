@echo off
setlocal enabledelayedexpansion
title Probably Stolen 联机 Mod 一键安装
echo ================================================
echo    Probably Stolen 联机 Mod 一键安装
echo ================================================
echo.

set "GAME="
for %%D in (C D E F G H) do (
  if not defined GAME if exist "%%D:/SteamLibrary/steamapps/common/Probably Stolen Demo\Probably Stolen.exe" set "GAME=%%D:/SteamLibrary/steamapps/common/Probably Stolen Demo"
  if not defined GAME if exist "%%D:/Steam/steamapps/common/Probably Stolen Demo\Probably Stolen.exe" set "GAME=%%D:/Steam/steamapps/common/Probably Stolen Demo"
  if not defined GAME if exist "%%D:/Program Files (x86)\Steam\steamapps\common\Probably Stolen Demo\Probably Stolen.exe" set "GAME=%%D:/Program Files (x86)\Steam\steamapps\common\Probably Stolen Demo"
  if not defined GAME if exist "%%D:/Games/Probably Stolen Demo\Probably Stolen.exe" set "GAME=%%D:/Games/Probably Stolen Demo"
  if not defined GAME if exist "%%D:/SteamLibrary/steamapps/common/Probably Stolen\Probably Stolen.exe" set "GAME=%%D:/SteamLibrary/steamapps/common/Probably Stolen"
)

if not defined GAME (
  echo [!] 没有自动找到游戏目录。
  echo     请把游戏文件夹直接拖到本窗口, 然后按回车(或手动输入完整路径):
  set /p GAME=
)
set GAME=!GAME:"=!
for /f "tokens=* delims= " %%i in ("!GAME!") do set GAME=%%i

if not exist "!GAME!\Probably Stolen.exe" (
  echo.
  echo [x] 目录里没有找到 Probably Stolen.exe, 安装已取消。
  echo     请确认选的是游戏根目录(能看到 Probably Stolen.exe 的那个文件夹)。
  pause
  exit /b 1
)

echo.
echo [i] 游戏目录: !GAME!
echo [1/2] 正在复制运行环境(约 150MB, 请稍候)...
xcopy /E /I /Y "payload\dotnet" "!GAME!\dotnet" >nul
xcopy /E /I /Y "payload\BepInEx" "!GAME!\BepInEx" >nul
copy /Y "payload\winhttp.dll" "!GAME!\" >nul
copy /Y "payload\doorstop_config.ini" "!GAME!\" >nul

if errorlevel 1 (
  echo [x] 复制失败。请尝试右键 - 以管理员身份运行。
  pause
  exit /b 1
)

echo [2/2] 安装完成!
echo.
echo ================================================
echo   安装成功! 直接启动游戏即可。
echo.
echo   联机玩法:
echo     主菜单 - 多人联机 - 创建房间(把房间码发给朋友)
echo     朋友: 多人联机 - 输入房间码 - 加入
echo   服务器已内置, 无需任何设置。
echo ================================================
pause
