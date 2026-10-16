"""游戏 API 检索工具(读 output/api_index.json)

用法:
  python api.py cls PlayerStore          # 打印某个类的全部成员
  python api.py search inventory         # 类名/成员名模糊搜
  python api.py members TryAccept        # 只搜成员名
  python api.py mod 物品                 # 列出某分册的类型
  python api.py stat                     # 总体统计
"""
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
# 默认读仓库里的 docs/API/api_index.json;可用环境变量 PS_API_INDEX 覆盖
IDX = os.environ.get('PS_API_INDEX') or os.path.normpath(
    os.path.join(HERE, '..', 'docs', 'API', 'api_index.json'))
_D = None


def load():
    global _D
    if _D is None:
        if not os.path.isfile(IDX):
            print('缺少 %s，请先运行 python gen_api_docs.py' % IDX)
            sys.exit(1)
        _D = json.load(open(IDX, encoding='utf-8'))
    return _D


def members(t):
    return (t.get('fields', []), t.get('props', []), t.get('methods', []), t.get('enum', []))


def show(name):
    d = load()
    t = d['types'].get(name)
    if t is None:
        # 大小写不敏感兜底
        for k in d['types']:
            if k.lower() == name.lower():
                t, name = d['types'][k], k
                break
    if t is None:
        print('未找到类 %s。试试: python api.py search %s' % (name, name))
        return
    print('%s %s%s' % (t['kind'], name, (' : ' + t['base']) if t['base'] else ''))
    f, p, m, e = members(t)
    if e:
        print('\n[枚举值] ' + ', '.join(e))
    if f:
        print('\n[字段]')
        for s in f:
            print('  ' + s)
    if p:
        print('\n[属性]')
        for s in p:
            print('  ' + s)
    if m:
        print('\n[方法]')
        for s in m:
            print('  ' + s)
    if not (f or p or m or e):
        print('  (无 public 成员)')


def search(kw, only_member=False):
    d = load()
    low = kw.lower()
    hits = []
    for n, t in d['types'].items():
        if not only_member and low in n.lower():
            hits.append(('类', n, '%s %s' % (t['kind'], n)))
        f, p, m, e = members(t)
        for s in m:
            if low in s.lower():
                hits.append(('方法', n, s))
        for s in list(f) + list(p):
            if low in s.lower():
                hits.append(('成员', n, s))
    print('命中 %d 条(最多显示 120):' % len(hits))
    for kind, cls, s in hits[:120]:
        print('  [%s] %-28s %s' % (kind, cls, s[:110]))
    if len(hits) > 120:
        print('  … 还有 %d 条' % (len(hits) - 120))


def mod(kw):
    d = load()
    MODS = None
    # 复用生成时的分组:读 docs/API 的索引标题
    idx = os.path.normpath(os.path.join(HERE, '..', 'docs', 'API', '0-全量索引.md'))
    if not os.path.isfile(idx):
        print('缺少 0-全量索引.md')
        return
    cur = None
    n = 0
    for l in open(idx, encoding='utf-8'):
        if l.startswith('## '):
            cur = l[3:].strip()
            if kw and kw not in cur:
                cur = None
                continue
            if cur:
                print('\n' + cur)
        elif cur and l.startswith('- `'):
            print('  ' + l.strip())
            n += 1
    print('\n共 %d 个类型' % n)


def stat():
    d = load()
    tot_m = sum(len(t.get('methods', [])) for t in d['types'].values())
    tot_f = sum(len(t.get('fields', [])) + len(t.get('props', [])) for t in d['types'].values())
    print('游戏自有类型: %d' % d['count'])
    print('公开方法:     %d' % tot_m)
    print('公开字段/属性: %d' % tot_f)


if __name__ == '__main__':
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(0)
    cmd = sys.argv[1]
    a = sys.argv[2] if len(sys.argv) > 2 else ''
    if cmd == 'cls':
        show(a)
    elif cmd == 'search':
        search(a)
    elif cmd == 'members':
        search(a, only_member=True)
    elif cmd == 'mod':
        mod(a)
    elif cmd == 'stat':
        stat()
    else:
        print(__doc__)
