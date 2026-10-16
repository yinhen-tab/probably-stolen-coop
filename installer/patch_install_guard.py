"""给 install.bat 加"版本保护":拦住旧包覆盖已安装的新版本

install.bat 是 GBK 编码,必须用 gbk 读写(不能用 UTF-8 工具直接改)。
锚点全用 ASCII 行,避免中文匹配出错。
"""
import os

HERE = os.path.dirname(os.path.abspath(__file__))
P = os.path.join(HERE, 'install.bat')
src = open(P, 'rb').read().decode('gbk')

if 'PKGBUILD' in src:
    print('[=] 已打过补丁,跳过')
    raise SystemExit(0)

# ---- 1) 在"游戏目录校验通过"之后插入版本保护 ----
ANCHOR_A = 'if not exist "!GAME!\\Probably Stolen.exe" goto NO_GAME'
GUARD = ANCHOR_A + '''

rem --- 版本保护:拦住"旧包覆盖已装的新版本" ---
set "PKGBUILD="
if exist "%~dp0payload\\build.txt" set /p PKGBUILD=<"%~dp0payload\\build.txt"
set "INSTBUILD="
if exist "!GAME!\\BepInEx\\plugins\\ProbablyStolenCoop.build.txt" set /p INSTBUILD=<"!GAME!\\BepInEx\\plugins\\ProbablyStolenCoop.build.txt"
if not "!PKGBUILD!"=="" if not "!INSTBUILD!"=="" if "!PKGBUILD!" LSS "!INSTBUILD!" (
  echo.
  echo [警告] 这个包是旧的^(构建 !PKGBUILD!^)
  echo        你已安装的插件更新^(构建 !INSTBUILD!^)
  echo        继续安装会把插件降级,通常不是你想要的。
  echo.
  set /p GO=仍要继续请输入 Y 再回车: 
  if /i not "!GO!"=="Y" goto ABORT_OLD
)'''
assert ANCHOR_A in src, '未找到锚点 A'
src = src.replace(ANCHOR_A, GUARD, 1)

# ---- 2) 复制成功后记录构建戳 ----
ANCHOR_B = 'echo [2/3]'
assert ANCHOR_B in src, '未找到锚点 B'
STAMP = ('if not "!PKGBUILD!"=="" >"!GAME!\\BepInEx\\plugins\\ProbablyStolenCoop.build.txt" '
         'echo !PKGBUILD!\n\n' + ANCHOR_B)
src = src.replace(ANCHOR_B, STAMP, 1)

# ---- 3) 加取消分支 ----
src = src.rstrip() + '''

:ABORT_OLD
echo.
echo [已取消] 未安装旧包,当前插件保持不变。
echo          请改用最新的一键安装包。
echo.
pause
exit /b 1
'''

open(P, 'wb').write(src.encode('gbk'))
print('[ok] install.bat 已加版本保护(GBK 原样写回)')
back = open(P, 'rb').read().decode('gbk')
for k in ('PKGBUILD', 'INSTBUILD', 'ABORT_OLD', 'probablystolencoop.build.txt'.upper()):
    print('  含 %-16s %s' % (k, k in back))
