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
GAME = sys.argv[1] if len(sys.argv) > 1 else r'E:\SteamLibrary\steamapps\common\Probably Stolen Demo'
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
    for f in ('ProbablyStolenCoop.dll', 'Google.Protobuf.dll'):
        shutil.copy2(os.path.join(GAME, 'BepInEx', 'plugins', f), plugins)

    print('[4/5] 写入预配置(服务器 %s:%s wss=%s)...' % (HOST, PORT, WSS))
    cfg_dir = os.path.join(be, 'config')
    os.makedirs(cfg_dir)
    with open(os.path.join(cfg_dir, 'com.questinggoose.probablystolen.coop.cfg'), 'w', encoding='utf-8') as f:
        f.write(CFG_TEMPLATE.format(host=HOST, port=PORT, wss=WSS))

    for f in ('install.bat', 'uninstall.bat'):
        shutil.copy2(os.path.join(HERE, f), stage)

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
