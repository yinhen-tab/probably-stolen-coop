@echo off
chcp 936 >nul 2>&1
setlocal enabledelayedexpansion
title Probably Stolen 联机 Mod 一键安装
echo ================================================
echo    Probably Stolen 联机 Mod 一键安装
echo ================================================
echo.

set "SRC=%~dp0payload"
if not exist "%SRC%\winhttp.dll" goto NO_PAYLOAD

set "GAME=%~1"
if not "!GAME!"=="" goto HAVE_GAME

for %%D in (C D E F G H) do call :FIND %%D
if not "!GAME!"=="" goto HAVE_GAME

echo [注意] 没有自动找到游戏目录。
echo     请把游戏文件夹直接拖到本窗口，然后按回车。
echo.
set /p GAME=请输入游戏目录: 
goto HAVE_GAME

:HAVE_GAME
set GAME=!GAME:"=!
if not exist "!GAME!\Probably Stolen.exe" goto NO_GAME

echo.
echo [信息] 游戏目录: !GAME!
echo [1/3] 正在复制运行环境，约 150MB，请稍候...
xcopy /E /I /Y "%SRC%\dotnet" "!GAME!\dotnet" >nul 2>&1
if errorlevel 1 goto COPY_FAIL
rem --- 保留玩家已有配置(昵称/开关);新配置项由插件启动时自动补全 ---
set "KEEP=%TEMP%\coop_cfg_keep.ini"
if exist "!GAME!\BepInEx\config\com.questinggoose.probablystolen.coop.cfg" copy /Y "!GAME!\BepInEx\config\com.questinggoose.probablystolen.coop.cfg" "!KEEP!" >nul 2>&1
xcopy /E /I /Y "%SRC%\BepInEx" "!GAME!\BepInEx" >nul 2>&1
if errorlevel 1 goto COPY_FAIL
copy /Y "%SRC%\winhttp.dll" "!GAME!\" >nul 2>&1
if errorlevel 1 goto COPY_FAIL
copy /Y "%SRC%\doorstop_config.ini" "!GAME!\" >nul 2>&1
if errorlevel 1 goto COPY_FAIL

if exist "%TEMP%\coop_cfg_keep.ini" copy /Y "%TEMP%\coop_cfg_keep.ini" "!GAME!\BepInEx\config\com.questinggoose.probablystolen.coop.cfg" >nul 2>&1

echo [2/3] 校验安装文件...
if not exist "!GAME!\BepInEx\plugins\ProbablyStolenCoop.dll" goto COPY_FAIL
if not exist "!GAME!\winhttp.dll" goto COPY_FAIL
if exist "%~dp0VERSION.txt" type "%~dp0VERSION.txt"
echo [3/3] 安装完成！
echo.
echo ================================================
echo   安装成功！直接启动游戏即可。
echo.
echo   联机玩法:
echo     主菜单 - 多人联机 - 创建房间，把房间码发给朋友
echo     朋友: 多人联机 - 输入房间码 - 加入
echo   服务器已内置，无需任何设置。
echo ================================================
echo.
pause
exit /b 0

:FIND
if not "!GAME!"=="" exit /b 0
if exist "%~1:\SteamLibrary\steamapps\common\Probably Stolen Demo\Probably Stolen.exe" set "GAME=%~1:\SteamLibrary\steamapps\common\Probably Stolen Demo"
if not "!GAME!"=="" exit /b 0
if exist "%~1:\Steam\steamapps\common\Probably Stolen Demo\Probably Stolen.exe" set "GAME=%~1:\Steam\steamapps\common\Probably Stolen Demo"
if not "!GAME!"=="" exit /b 0
if exist "%~1:\Program Files (x86)\Steam\steamapps\common\Probably Stolen Demo\Probably Stolen.exe" set "GAME=%~1:\Program Files (x86)\Steam\steamapps\common\Probably Stolen Demo"
if not "!GAME!"=="" exit /b 0
if exist "%~1:\Games\Probably Stolen Demo\Probably Stolen.exe" set "GAME=%~1:\Games\Probably Stolen Demo"
if not "!GAME!"=="" exit /b 0
if exist "%~1:\SteamLibrary\steamapps\common\Probably Stolen\Probably Stolen.exe" set "GAME=%~1:\SteamLibrary\steamapps\common\Probably Stolen"
exit /b 0

:NO_PAYLOAD
echo [错误] 找不到 payload 数据文件夹，安装已取消。
echo        请先完整解压压缩包，再双击其中的 install.bat 运行。
echo.
pause
exit /b 1

:NO_GAME
echo.
echo [错误] 目录里没有找到 Probably Stolen.exe，安装已取消。
echo        请确认选的是游戏根目录，也就是能看到 Probably Stolen.exe 的那个文件夹。
echo.
pause
exit /b 1

:COPY_FAIL
echo.
echo [错误] 复制失败。可能的原因:
echo        1. 游戏正在运行，请先完全退出游戏再重试
echo        2. 权限不足，请右键 install.bat 选择以管理员身份运行
echo.
pause
exit /b 1
