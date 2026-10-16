@echo off
chcp 936 >nul 2>&1
setlocal enabledelayedexpansion
title Probably Stolen 联机 Mod 卸载
echo ================================================
echo    Probably Stolen 联机 Mod 卸载
echo ================================================
echo.

set "GAME=%~1"
if not "!GAME!"=="" goto HAVE_GAME
for %%D in (C D E F G H) do call :FIND %%D
if not "!GAME!"=="" goto HAVE_GAME
echo [注意] 没有自动找到游戏目录。
echo     请把游戏文件夹直接拖到本窗口，然后按回车。
echo.
set /p GAME=请输入游戏目录: 

:HAVE_GAME
set GAME=!GAME:"=!
if not exist "!GAME!\BepInEx\plugins\ProbablyStolenCoop.dll" goto NOT_INSTALLED
del /Q "!GAME!\BepInEx\plugins\ProbablyStolenCoop.dll" >nul 2>&1
del /Q "!GAME!\BepInEx\plugins\Google.Protobuf.dll" >nul 2>&1
del /Q "!GAME!\BepInEx\plugins\ProbablyStolenCoop.build.txt" >nul 2>&1
echo [完成] 联机插件已删除。
echo        BepInEx 运行环境保留，不影响其他 Mod。
echo        如需彻底清理，可手动删除游戏目录下的 BepInEx 与 dotnet 文件夹，
echo        以及 winhttp.dll 和 doorstop_config.ini 两个文件。
echo.
pause
exit /b 0

:FIND
if not "!GAME!"=="" exit /b 0
if exist "%~1:\SteamLibrary\steamapps\common\Probably Stolen Demo\BepInEx\plugins\ProbablyStolenCoop.dll" set "GAME=%~1:\SteamLibrary\steamapps\common\Probably Stolen Demo"
if not "!GAME!"=="" exit /b 0
if exist "%~1:\Steam\steamapps\common\Probably Stolen Demo\BepInEx\plugins\ProbablyStolenCoop.dll" set "GAME=%~1:\Steam\steamapps\common\Probably Stolen Demo"
if not "!GAME!"=="" exit /b 0
if exist "%~1:\Program Files (x86)\Steam\steamapps\common\Probably Stolen Demo\BepInEx\plugins\ProbablyStolenCoop.dll" set "GAME=%~1:\Program Files (x86)\Steam\steamapps\common\Probably Stolen Demo"
exit /b 0

:NOT_INSTALLED
echo [注意] 未在游戏目录检测到联机插件。
echo.
pause
exit /b 0
