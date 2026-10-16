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

for %%D in (C D E F G H I J) do call :FIND %%D
if not "!GAME!"=="" goto HAVE_GAME

echo [注意] 没有自动找到游戏目录。
echo        请把游戏文件夹直接拖到本窗口，然后按回车。
echo.
set /p GAME=请输入游戏目录: 
goto HAVE_GAME

:HAVE_GAME
set GAME=!GAME:"=!
if not exist "!GAME!\Probably Stolen.exe" goto NO_GAME

rem ---------- 版本保护:拦住"旧包覆盖已装的新版本" ----------
set "PKGBUILD="
if exist "%~dp0payload\build.txt" for /f "usebackq delims=" %%A in ("%~dp0payload\build.txt") do set "PKGBUILD=%%A"
set "INSTBUILD="
if exist "!GAME!\BepInEx\plugins\ProbablyStolenCoop.build.txt" for /f "usebackq delims=" %%A in ("!GAME!\BepInEx\plugins\ProbablyStolenCoop.build.txt") do set "INSTBUILD=%%A"
if "!PKGBUILD!"=="" goto SKIP_GUARD
if "!INSTBUILD!"=="" goto SKIP_GUARD
if "!PKGBUILD!" GEQ "!INSTBUILD!" goto SKIP_GUARD

echo.
echo [警告] 这个包是旧的，构建号 !PKGBUILD!
echo        你机器上已装的插件更新，构建号 !INSTBUILD!
echo        继续安装会把插件降级，通常不是你想要的。
echo.
set /p GO=仍要继续请输入 Y 再回车: 
if /i not "!GO!"=="Y" goto ABORT_OLD
:SKIP_GUARD

echo.
echo [信息] 游戏目录: !GAME!
echo [1/3] 正在复制运行环境，约 150MB，请稍候...
xcopy /E /I /Y "%SRC%\dotnet" "!GAME!\dotnet" >nul 2>&1
if errorlevel 1 goto COPY_FAIL

rem ---------- 保留玩家已有配置；新增配置项由插件启动时自动补全 ----------
set "KEEP=%TEMP%\coop_cfg_keep.ini"
if not exist "!GAME!\BepInEx\config\com.questinggoose.probablystolen.coop.cfg" goto NO_KEEP
copy /Y "!GAME!\BepInEx\config\com.questinggoose.probablystolen.coop.cfg" "!KEEP!" >nul 2>&1
:NO_KEEP

xcopy /E /I /Y "%SRC%\BepInEx" "!GAME!\BepInEx" >nul 2>&1
if errorlevel 1 goto COPY_FAIL
copy /Y "%SRC%\winhttp.dll" "!GAME!\winhttp.dll" >nul 2>&1
if errorlevel 1 goto COPY_FAIL
copy /Y "%SRC%\doorstop_config.ini" "!GAME!\doorstop_config.ini" >nul 2>&1
if errorlevel 1 goto COPY_FAIL

if not exist "%KEEP%" goto NO_RESTORE
copy /Y "%KEEP%" "!GAME!\BepInEx\config\com.questinggoose.probablystolen.coop.cfg" >nul 2>&1
:NO_RESTORE

if "!PKGBUILD!"=="" goto SKIP_STAMP
>"!GAME!\BepInEx\plugins\ProbablyStolenCoop.build.txt" echo !PKGBUILD!
:SKIP_STAMP

echo [2/3] 校验安装文件...
if not exist "!GAME!\BepInEx\plugins\ProbablyStolenCoop.dll" goto COPY_FAIL
if not exist "!GAME!\BepInEx\plugins\Google.Protobuf.dll" goto COPY_FAIL
if not exist "!GAME!\winhttp.dll" goto COPY_FAIL
if not exist "!GAME!\dotnet\coreclr.dll" goto COPY_FAIL

echo [3/3] 安装完成！
echo.
echo ================================================
if not exist "%~dp0VERSION.txt" goto NO_VERSION
type "%~dp0VERSION.txt"
:NO_VERSION
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

:ABORT_OLD
echo.
echo [已取消] 未安装旧包，当前插件保持不变。
echo          请改用最新的一键安装包。
echo.
pause
exit /b 1
