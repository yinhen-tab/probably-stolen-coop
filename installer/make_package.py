"""生成 "Probably Stolen 联机一键安装" zip 包。

从本机(已装好 Mod 的)游戏目录收集 BepInEx 运行环境 + 插件,
写入预配置(服务器地址), 与 install.bat / uninstall.bat 一起打包为 zip。

用法:
    python make_package.py [游戏目录] [输出目录]

环境变量(可选):
    COOP_PACK_HOST  服务器域名(默认 ws.yinhen.top)
    COOP_PACK_PORT  服务器端口(默认 443)
    COOP_PACK_WSS   加密连接 true/false(默认 true)

产物: <输出目录>/ProbablyStolen联机一键安装.zip
"""
import os
import shutil
import sys
import zipfile

HERE = os.path.dirname(os.path.abspath(__file__))
# 游戏目录:可用环境变量 PS_GAME 覆盖;否则自动在常见位置里找
CANDIDATES = [
    os.environ.get('PS_GAME', ''),
    'F:////SteamLibrary////steamapps////common////Probably Stolen Demo',
    'E:////SteamLibrary////steamapps////common////Probably Stolen Demo',
    'C:////SteamLibrary////steamapps////common////Probably Stolen Demo',
]
GAME = next((c for c in CANDIDATES if c and os.path.isfile(os.path.join(c, 'Probably Stolen.exe'))),
            CANDIDATES[1])

OUT = sys.argv[2] if len(sys.argv) > 2 else os.path.join(HERE, '..', 'dist')
HOST = os.environ.get('COOP_PACK_HOST', 'ws.yinhen.top')
PORT = os.environ.get('COOP_PACK_PORT', '443')
WSS = os.environ.get('COOP_PACK_WSS', 'true')

CFG_TEMPLATE = '''## Settings file was created by plugin ProbablyStolenCoop v0.1.0
## Plugin GUID: com.questinggoose.probablystolen.coop

[联机]

## 是否允许联机功能(点击主菜单"多人联机"后才连接服务器)
# Setting type: Boolean
# Default value: true
启用联机 = true

## 权威服务器域名(公网如 ws.yinhen.top)
# Setting type: String
# Default value: 127.0.0.1
服务器地址 = {host}

## 权威服务器端口(WSS 公网通常填 443)
# Setting type: Int32
# Default value: 27500
服务器端口 = {port}

## false=ws(本地/直连); true=wss(公网 CDN/HTTPS 转发,如 EdgeOne)
# Setting type: Boolean
# Default value: false
加密连接(wss) = {wss}

## 显示给队友的名字
# Setting type: String
# Default value: 玩家
玩家昵称 = 玩家

## true=队友触发的音效在你这边也回放(音画一致)
# Setting type: Boolean
# Default value: true
同步音效 = true

## true=顾客进店/挑选/成交事件同步,保证同一顾客两端结果一致
# Setting type: Boolean
# Default value: true
同步顾客行为 = true

## false(默认)=你打电话时队友只是观看,不产生任何效果;true=队友端也真实生效
# Setting type: Boolean
# Default value: false
通话效果对对方生效 = false

## 安静=只留警告与错误; 正常=连接/房间/存档/错误+同步结果; 详细=再含逐条流水(排查问题用)
# Setting type: String
# Default value: 正常
日志详细程度 = 正常

## 每秒上报多少次队友光标(5~60;默认 30 已足够,配合插值观感平滑;调高只增加带宽)
# Setting type: Int32
# Default value: 30
光标发送频率 = 30

## true(默认)=加入房间后直接载入房主的存档(世界状态完全一致);false=各自用自己的存档
# Setting type: Boolean
# Default value: true
使用房主的存档 = true

[测试]

## 诊断用:自动从主菜单开局并推进回合(正式联机请保持 false)
# Setting type: Boolean
# Default value: false
自动开局与回合测试 = false
'''


def main():
    if not os.path.isfile(os.path.join(GAME, 'Probably Stolen.exe')):
        print('[x] 游戏目录无效:', GAME)
        sys.exit(1)

    stage = os.path.join(OUT, '_stage')
    if os.path.exists(stage):
        shutil.rmtree(stage)
    payload = os.path.join(stage, 'payload')
    os.makedirs(payload)

    print('[1/5] 复制根文件...')
    for f in ('winhttp.dll', 'doorstop_config.ini'):
        shutil.copy2(os.path.join(GAME, f), payload)

    print('[2/5] 复制 dotnet 运行时(约 67MB)...')
    shutil.copytree(os.path.join(GAME, 'dotnet'), os.path.join(payload, 'dotnet'))

    print('[3/5] 复制 BepInEx core + 插件...')
    be = os.path.join(payload, 'BepInEx')
    os.makedirs(be)
    shutil.copytree(os.path.join(GAME, 'BepInEx', 'core'), os.path.join(be, 'core'))
    plugins = os.path.join(be, 'plugins')
    os.makedirs(plugins)
    # 插件 DLL 一律取"编译产物"(bin/Release),不取游戏目录 ——
    # 否则一旦游戏里被旧包覆盖过,打出来的包就是旧 DLL(踩过:朋友拿到旧版)
    built = os.path.normpath(os.path.join(HERE, '..', 'bin', 'Release', 'net6.0'))
    for f in ('ProbablyStolenCoop.dll', 'Google.Protobuf.dll'):
        src = os.path.join(built, f)
        if not os.path.isfile(src):
            src = os.path.join(GAME, 'BepInEx', 'plugins', f)
            print('      (警告: %s 不在编译产物里,回退到游戏目录)' % f)
        shutil.copy2(src, plugins)
    try:
        _dll = os.path.join(plugins, 'ProbablyStolenCoop.dll')
        import hashlib as _h
        print('      插件 DLL: %d 字节 md5=%s' % (
            os.path.getsize(_dll), _h.md5(open(_dll, 'rb').read()).hexdigest()[:12]))
    except Exception:
        pass

    # Unity 基础库(必需! 缺它 BepInEx 首次启动会联网下载, 国内网络极易失败导致模组加载不了)
    print('[3.5/5] 复制 Unity 基础库...')
    ul_src = os.path.join(GAME, 'BepInEx', 'unity-libs')
    ul_dst = os.path.join(be, 'unity-libs')
    os.makedirs(ul_dst, exist_ok=True)
    n_ul = 0
    for f in os.listdir(ul_src):
        sp = os.path.join(ul_src, f)
        if os.path.isfile(sp) and f.lower().endswith('.zip'):
            shutil.copy2(sp, ul_dst)
            n_ul += 1
            print('      + %s (%.2f MB)' % (f, os.path.getsize(sp) / 1024 / 1024))
    if n_ul == 0:
        print('      !! 警告: 未在 BepInEx/unity-libs 找到 zip 文件')

    print('[4/5] 写入预配置(服务器 %s:%s wss=%s)...' % (HOST, PORT, WSS))
    cfg_dir = os.path.join(be, 'config')
    os.makedirs(cfg_dir)
    with open(os.path.join(cfg_dir, 'com.questinggoose.probablystolen.coop.cfg'), 'w', encoding='utf-8') as f:
        f.write(CFG_TEMPLATE.format(host=HOST, port=PORT, wss=WSS))

    # BepInEx 主配置: 关闭调试控制台窗口(玩家环境不需要黑窗)
    import re as _re
    bep_src = os.path.join(GAME, 'BepInEx', 'config', 'BepInEx.cfg')
    if os.path.isfile(bep_src):
        txt = open(bep_src, encoding='utf-8').read()

        def _fix(seg):
            return _re.sub(r'(?m)^(Enabled\s*=\s*)true', r'\1false', seg.group(0), count=1)

        txt = _re.sub(r'\[Logging\.Console\][\s\S]*?(?=\n\[|$)', _fix, txt, count=1)
        # 日志优化:不再把 Unity 自身日志并入 BepInEx(字体警告等噪音与额外开销)
        txt = _re.sub(r'(?m)^UnityLogListening\s*=\s*true', 'UnityLogListening = false', txt)
        with open(os.path.join(cfg_dir, 'BepInEx.cfg'), 'w', encoding='utf-8') as f:
            f.write(txt)
        print('      BepInEx.cfg 已写入(关闭控制台窗口 + 不并入 Unity 日志)')
    else:
        with open(os.path.join(cfg_dir, 'BepInEx.cfg'), 'w', encoding='utf-8') as f:
            f.write('[Logging]\n\nUnityLogListening = false\n\n[Logging.Console]\n\n## Enables showing a console for log output.\n# Setting type: Boolean\n# Default value: true\nEnabled = false\n')

    for f in ('install.bat', 'uninstall.bat'):
        shutil.copy2(os.path.join(HERE, f), stage)

    # 版本标记:装完由 install.bat 打印,方便确认"朋友装的是不是新包"
    try:
        import datetime
        with open(os.path.join(stage, 'VERSION.txt'), 'w', encoding='gbk') as f:
            f.write('================================================\n')
            f.write('  版本: %s\n' % datetime.datetime.now().strftime('%Y-%m-%d %H:%M'))
            f.write('  含: 用房主的档 / 容器同步 / 物品位置同步 / 掉线重连 / 音效与顾客行为\n')
            f.write('================================================\n')
    except Exception as e:
        print('      (VERSION.txt 写入失败: %s)' % e)

    print('[5/5] 打包 zip...')
    os.makedirs(OUT, exist_ok=True)
    zip_path = os.path.join(OUT, 'ProbablyStolen联机一键安装.zip')
    with zipfile.ZipFile(zip_path, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as z:
        for root, dirs, files in os.walk(stage):
            for f in files:
                p = os.path.join(root, f)
                z.write(p, os.path.relpath(p, stage))
    shutil.rmtree(stage)
    size_mb = os.path.getsize(zip_path) / 1024 / 1024
    print('完成: %s (%.1f MB)' % (zip_path, size_mb))
    print('给朋友: 解压后双击 install.bat 即可。')


if __name__ == '__main__':
    main()
