# -*- coding: utf-8 -*-
"""install.bat 分支逻辑沙盒测试(假 payload + 假游戏目录)

用法(在仓库根目录执行): python tests/test_install_bat.py
用途: 改了 install.bat 后跑一遍,确认版本保护 / 配置保留 / 各种报错分支都正常
"""
import os
import tempfile

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

"""install.bat 沙盒冒烟测试:用最小假 payload + 假游戏目录真跑一遍 cmd，验证语法与分支逻辑。"""
import os
import shutil
import subprocess
import sys

ROOT = os.path.join(tempfile.gettempdir(), 'ps_install_smoke')
SRC_BAT = os.path.join(REPO, 'installer', 'install.bat')


def w(p, data, enc='gbk', crlf=True):
    os.makedirs(os.path.dirname(p), exist_ok=True)
    if isinstance(data, str):
        if crlf:
            data = data.replace('\r\n', '\n').replace('\r', '\n').replace('\n', '\r\n')
        data = data.encode(enc)
    open(p, 'wb').write(data)


def build(pkg_build, inst_build):
    if os.path.isdir(ROOT):
        shutil.rmtree(ROOT)
    pl = os.path.join(ROOT, 'payload')
    gm = os.path.join(ROOT, 'game')
    # ---- 假 payload ----
    w(os.path.join(pl, 'winhttp.dll'), b'FAKE-DLL')
    w(os.path.join(pl, 'doorstop_config.ini'), b'[General]\r\nenabled=true\r\n')
    w(os.path.join(pl, 'build.txt'), pkg_build)
    w(os.path.join(ROOT, 'VERSION.txt'), '================================================\r\n  版本: %s\r\n  含: 用房主的档 / 容器同步 / 物品位置同步\r\n================================================\r\n' % pkg_build)
    w(os.path.join(pl, 'BepInEx', 'plugins', 'ProbablyStolenCoop.dll'), b'FAKE-PLUGIN-' + pkg_build.encode())
    w(os.path.join(pl, 'BepInEx', 'plugins', 'Google.Protobuf.dll'), b'FAKE-PROTOBUF')
    w(os.path.join(pl, 'BepInEx', 'config', 'com.questinggoose.probablystolen.coop.cfg'), '[设置]\r\n玩家昵称 = 包里的默认昵称\r\n')
    w(os.path.join(pl, 'dotnet', 'coreclr.dll'), b'x')      # xcopy 不建空目录,放个占位文件
    # ---- 假游戏目录(已装过一个版本) ----
    w(os.path.join(gm, 'Probably Stolen.exe'), b'FAKE-EXE')
    w(os.path.join(gm, 'BepInEx', 'config', 'com.questinggoose.probablystolen.coop.cfg'), '[设置]\r\n玩家昵称 = 老王的昵称\r\n')
    if inst_build:
        w(os.path.join(gm, 'BepInEx', 'plugins', 'ProbablyStolenCoop.build.txt'), inst_build)
        w(os.path.join(gm, 'BepInEx', 'plugins', 'ProbablyStolenCoop.dll'), b'FAKE-OLD-PLUGIN-' + inst_build.encode())
        w(os.path.join(gm, 'winhttp.dll'), b'FAKE-OLD-WINHTTP')
    # ---- 把被测脚本放到沙盒根 ----
    shutil.copy2(SRC_BAT, os.path.join(ROOT, 'install.bat'))
    return ROOT, gm


def run(root, gm, answer=''):
    bat = os.path.join(root, 'install.bat')
    payload = ((answer or 'n') + '\r\n' + '\r\n').encode('gbk')
    r = subprocess.run(['cmd', '/c', bat, gm], input=payload,
                       capture_output=True, cwd=root, timeout=120)
    out = r.stdout.decode('gbk', 'replace')
    return out, r.returncode


def show(title, out):
    print('=' * 60)
    print(title)
    print('=' * 60)
    for l in out.splitlines():
        if l.strip():
            print('   ' + l)


def check(cond, msg):
    print(('   [OK] ' if cond else '   [!!] ') + msg)
    return cond


ok_all = True

# ---------- 场景 1: 包比已装的旧 → 应拦截(输入 n 取消) ----------
root, gm = build('202610160900', '202610161000')
out, _ = run(root, gm, 'n')
show('场景1  包(0900) 比 已装(1000) 旧 → 输入 n', out)
ok_all &= check('这个包是旧的' in out, '出现了版本告警')
ok_all &= check('已取消' in out, '输入 n 后取消了安装')
dll = open(os.path.join(gm, 'BepInEx', 'plugins', 'ProbablyStolenCoop.dll'), 'rb').read()
ok_all &= check(dll == b'FAKE-OLD-PLUGIN-202610161000', '插件未被覆盖(仍是原有内容)')
ok_all &= check('Traceback' not in out and '不是内部或外部命令' not in out, '没有任何 cmd 语法报错')

# ---------- 场景 2: 包比已装的旧 → 强制继续(输入 y) ----------
root, gm = build('202610160900', '202610161000')
out, _ = run(root, gm, 'y')
show('场景2  包(0900) 旧 → 输入 y 强制安装', out)
ok_all &= check('安装成功' in out, '走到了安装成功')
dll = open(os.path.join(gm, 'BepInEx', 'plugins', 'ProbablyStolenCoop.dll'), 'rb').read()
ok_all &= check(dll == b'FAKE-PLUGIN-202610160900', '插件被替换成包内的版本')
ok_all &= check('不是内部或外部命令' not in out, '无 cmd 语法报错')

# ---------- 场景 3: 全新包(比已装新) → 直接装,且配置保留不同步的昵称 ----------
root, gm = build('202610162000', '202610161000')
out, _ = run(root, gm)
show('场景3  包(2000) 比 已装(1000) 新 → 直接安装', out)
ok_all &= check('这个包是旧的' not in out, '没有误报旧包告警')
ok_all &= check('安装成功' in out, '安装成功')
cfg = open(os.path.join(gm, 'BepInEx', 'config', 'com.questinggoose.probablystolen.coop.cfg'), 'rb').read().decode('gbk')
ok_all &= check('老王的昵称' in cfg, '玩家原配置被保留(昵称未重置)')
stamp = open(os.path.join(gm, 'BepInEx', 'plugins', 'ProbablyStolenCoop.build.txt'), 'rb').read().decode('gbk').strip()
ok_all &= check(stamp == '202610162000', '构建戳已更新为包内版本(%s)' % stamp)
ok_all &= check('不是内部或外部命令' not in out, '无 cmd 语法报错')

# ---------- 场景 4: 游戏目录里没有 exe → 应报 NO_GAME ----------
root, gm = build('202610162000', '')
os.remove(os.path.join(gm, 'Probably Stolen.exe'))
out, _ = run(root, gm)
show('场景4  目录里没有 Probably Stolen.exe', out)
ok_all &= check('没有找到 Probably Stolen.exe' in out, '正确报错并取消')
ok_all &= check('不是内部或外部命令' not in out, '无 cmd 语法报错')

# ---------- 场景 5: 缺 payload → 应报 NO_PAYLOAD ----------
root, gm = build('202610162000', '')
shutil.rmtree(os.path.join(root, 'payload'))
out, _ = run(root, gm)
show('场景5  缺少 payload 文件夹', out)
ok_all &= check('找不到 payload' in out, '正确报错并取消')

print()
print('#' * 60)
print('#  总结: ' + ('全部通过 ✓' if ok_all else '**有失败项,见上面 [!!]**'))
print('#' * 60)
sys.exit(0 if ok_all else 1)
