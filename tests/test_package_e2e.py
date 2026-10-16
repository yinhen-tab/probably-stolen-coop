# -*- coding: utf-8 -*-
"""真实安装包端到端测试(解压 zip → 对假游戏目录跑 install.bat / uninstall.bat)

用法(在仓库根目录执行): python tests/test_package_e2e.py
用途: 打完包后跑一遍,确认"解压即装"在别的机器上也能跑通,且不会动玩家的配置与存档
"""
import os
import tempfile

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

"""真实安装包端到端测试:解压 dist 里的 zip,对"假游戏目录"跑它自带的 install.bat。"""
import hashlib
import os
import shutil
import subprocess
import sys
import zipfile

ZIP = os.path.join(REPO, 'dist', 'ProbablyStolen联机一键安装.zip')
WORK = os.path.join(tempfile.gettempdir(), 'ps_pkg_e2e')
EXTRACT = os.path.join(WORK, 'pkg')
FAKEGAME = os.path.join(WORK, 'game')

ok_all = True


def check(cond, msg):
    global ok_all
    print(('   [OK] ' if cond else '   [!!] ') + msg)
    ok_all = ok_all and bool(cond)


def w(path, data):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    if isinstance(data, str):
        data = data.encode('gbk')
    open(path, 'wb').write(data)


if os.path.isdir(WORK):
    shutil.rmtree(WORK)
os.makedirs(EXTRACT)

print('=' * 62)
print('步骤 1  解压真实安装包')
print('=' * 62)
z = zipfile.ZipFile(ZIP)
z.extractall(EXTRACT)
names = z.namelist()
pkg_dll = z.read('payload/BepInEx/plugins/ProbablyStolenCoop.dll')
print('   包内顶层: %s' % [n for n in names if n.count('/') == 0][:8])
print('   插件 DLL: %d 字节 md5=%s' % (len(pkg_dll), hashlib.md5(pkg_dll).hexdigest()[:12]))
print('   构建戳  : %s' % z.read('payload/build.txt').decode('gbk').strip())

raw = open(os.path.join(EXTRACT, 'install.bat'), 'rb').read()
check(raw.count(b'\r\n') == raw.count(b'\n'), '解压出的 install.bat 全是 CRLF（CRLF=%d）' % raw.count(b'\r\n'))
try:
    raw.decode('gbk')
    check(True, 'install.bat 是 GBK 编码（中文不乱码）')
except UnicodeDecodeError as e:
    check(False, 'install.bat GBK 解码失败: %s' % e)

print()
print('=' * 62)
print('步骤 2  造假游戏目录（一个已装旧版的游戏）')
print('=' * 62)
w(os.path.join(FAKEGAME, 'Probably Stolen.exe'), 'FAKE-EXE')
w(os.path.join(FAKEGAME, 'BepInEx', 'config', 'com.questinggoose.probablystolen.coop.cfg'),
  '[设置]\r\n玩家昵称 = 老王的昵称\r\n服务器地址 = ws.yinhen.top\r\n')
w(os.path.join(FAKEGAME, 'BepInEx', 'plugins', 'ProbablyStolenCoop.dll'), 'OLD-PLUGIN')
w(os.path.join(FAKEGAME, 'BepInEx', 'plugins', 'ProbablyStolenCoop.build.txt'), '202601010000')
w(os.path.join(FAKEGAME, 'BepInEx', 'plugins', 'coop_savebackup', 'save_1.es3'), 'SAVE-DATA')  # 玩家存档(不能被删)
print('   已建: Probably Stolen.exe / 旧插件 / 旧构建戳 202601010000 / 玩家配置 / 存档备份')

print()
print('=' * 62)
print('步骤 3  运行包内 install.bat（指向假游戏目录）')
print('=' * 62)
r = subprocess.run(['cmd', '/c', os.path.join(EXTRACT, 'install.bat'), FAKEGAME],
                   input=b'\r\n\r\n', capture_output=True, cwd=EXTRACT, timeout=600)
out = r.stdout.decode('gbk', 'replace')
for line in out.splitlines():
    if line.strip():
        print('   | ' + line)

print()
print('=' * 62)
print('步骤 4  校验安装结果')
print('=' * 62)
check('不是内部或外部命令' not in out, '没有任何 cmd 语法报错')
check('系统找不到指定的路径' not in out, '没有"找不到指定的路径"')
check('安装成功' in out, '打印了"安装成功"')
check('这个包是旧的' not in out, '没有误报旧包（包比已装的新）')

g_dll = os.path.join(FAKEGAME, 'BepInEx', 'plugins', 'ProbablyStolenCoop.dll')
check(os.path.isfile(g_dll) and open(g_dll, 'rb').read() == pkg_dll, '插件 DLL 已装且与包内一致')
stamp = open(os.path.join(FAKEGAME, 'BepInEx', 'plugins', 'ProbablyStolenCoop.build.txt'), 'rb').read().decode('gbk').strip()
check(stamp == z.read('payload/build.txt').decode('gbk').strip(), '构建戳已更新为包内版本（%s）' % stamp)
cfg = open(os.path.join(FAKEGAME, 'BepInEx', 'config', 'com.questinggoose.probablystolen.coop.cfg'), 'rb').read().decode('gbk')
check('老王的昵称' in cfg, '玩家配置被保留（昵称没被重置）')
check(os.path.isfile(os.path.join(FAKEGAME, 'BepInEx', 'plugins', 'coop_savebackup', 'save_1.es3')),
      '玩家存档备份没被动过')
check(os.path.isfile(os.path.join(FAKEGAME, 'dotnet', 'coreclr.dll')), 'dotnet 运行时已复制')
check(os.path.isfile(os.path.join(FAKEGAME, 'winhttp.dll')), 'winhttp.dll 已复制')
check(os.path.isfile(os.path.join(FAKEGAME, 'Google.Protobuf.dll') if False else
                     os.path.join(FAKEGAME, 'BepInEx', 'plugins', 'Google.Protobuf.dll')),
      'Google.Protobuf.dll 已复制')
check('版本:' in out, '安装完打印了版本信息')

print()
print('=' * 62)
print('步骤 5  卸载脚本冒烟（uninstall.bat）')
print('=' * 62)
r2 = subprocess.run(['cmd', '/c', os.path.join(EXTRACT, 'uninstall.bat'), FAKEGAME],
                    input=b'\r\n', capture_output=True, cwd=EXTRACT, timeout=120)
out2 = r2.stdout.decode('gbk', 'replace')
for line in out2.splitlines():
    if line.strip():
        print('   | ' + line)
check('不是内部或外部命令' not in out2, '卸载脚本无语法报错')
check('已删除' in out2, '卸载脚本正常执行')
check(not os.path.isfile(g_dll), '插件 DLL 已被卸载删除')
check(not os.path.isfile(os.path.join(FAKEGAME, 'BepInEx', 'plugins', 'ProbablyStolenCoop.build.txt')),
      '构建戳已一并清除（下次装任何版本都不会被误拦）')
check(os.path.isfile(os.path.join(FAKEGAME, 'BepInEx', 'plugins', 'coop_savebackup', 'save_1.es3')),
      '卸载没有碰玩家的存档备份')

print()
print('#' * 62)
print('#  端到端结果: ' + ('全部通过 ✓' if ok_all else '**有失败项**'))
print('#' * 62)
sys.exit(0 if ok_all else 1)
