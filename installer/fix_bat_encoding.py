"""把本目录下的 .bat 统一成 GBK + CRLF。

为什么需要它:编辑器/工具写出来的 .bat 常常是纯 LF,而 cmd.exe 解析
`if (...) (...)` 这类括号块时遇到裸 LF 会错乱,报出一堆
"'xxx' 不是内部或外部命令" —— 这个坑真实踩过(见 2026-10-16)。

用法:
    python fix_bat_encoding.py            # 修复本目录全部 .bat
    python fix_bat_encoding.py install.bat
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))


def fix(path):
    raw = open(path, 'rb').read()
    if raw.startswith(b'\xef\xbb\xbf'):          # 去 BOM
        raw = raw[3:]
    try:
        text = raw.decode('utf-8')
        src = 'utf-8'
    except UnicodeDecodeError:
        text = raw.decode('gbk')
        src = 'gbk'
    text = text.replace('\r\n', '\n').replace('\r', '\n')
    if not text.endswith('\n'):
        text += '\n'
    data = text.replace('\n', '\r\n').encode('gbk')
    open(path, 'wb').write(data)

    b = open(path, 'rb').read()
    crlf, lf = b.count(b'\r\n'), b.count(b'\n')
    flag = 'OK' if crlf == lf else '**仍有裸 LF**'
    print('  %-18s 源编码=%-6s 行数=%-4d CRLF=%d  %s' % (os.path.basename(path), src, crlf, crlf, flag))
    return crlf == lf


def main():
    names = sys.argv[1:] or [f for f in sorted(os.listdir(HERE)) if f.lower().endswith('.bat')]
    if not names:
        print('(没有 .bat 文件)')
        return 0
    ok = True
    for n in names:
        p = n if os.path.isabs(n) else os.path.join(HERE, n)
        if not os.path.isfile(p):
            print('  (跳过,不存在: %s)' % n)
            continue
        ok = fix(p) and ok
    print('结果: ' + ('全部已统一为 GBK + CRLF' if ok else '**有文件未修好**'))
    return 0 if ok else 1


if __name__ == '__main__':
    sys.exit(main())
