"""解析 dump.cs → 全量 API 索引(JSON) + 分册 Markdown + 总览

产物:
  output/api_index.json       全量机器可读索引(类型 + 成员签名)
  docs/API/README.md          总览与用法
  docs/API/0-全量索引.md       所有类型一行一条(名字/种类/成员数/模块)
  docs/API/N-<模块>.md        分册(逐类列出 public 成员签名)

用法:
  python gen_api_docs.py            # 生成全部
  python gen_api_docs.py --stats    # 只看统计
"""
import collections
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
DUMP = os.environ.get('PS_DUMP') or os.path.join(HERE, 'output', 'dump.cs')
OUT_JSON = os.path.normpath(os.path.join(HERE, '..', 'docs', 'API', 'api_index.json'))
DOCS = os.path.normpath(os.path.join(HERE, '..', 'docs', 'API'))

EXCLUDE_PREFIX = (
    'UnityEngine', 'Unity.', 'System', 'Microsoft', 'Mono.', 'MonoBleedingEdge', 'TMPro',
    'Newtonsoft', 'DG.', 'Il2Cpp', 'ES3', 'ES3Internal', 'ES3Types', 'MessagePack', 'dnlib',
    'Mono.Cecil', 'XG', 'MS.Internal', 'Internal', 'AOT', 'Cpp2IL', 'Il2CppInterop',
    'BepInEx', 'HarmonyLib', 'I18N', 'Fx.', 'Code.', 'PostLateUpdate', 'EarlyUpdate',
    'PreLateUpdate', 'FixedUpdate', 'ParticleSystem', 'Enumerable', 'EqualInstruction',
    'NotEqualInstruction', 'ShortcutExtensions', 'AddressablesImpl', 'UnityTls',
    'StylePropertyAnimationSystem', 'X520', 'BattleRoyal',
)
BLACKLIST = {'AccessTools', 'Harmony', 'CodeMatcher', 'CodeInstruction', 'Transpilers',
             'PatchProcessor', 'MethodInvoker', 'HarmonyException', 'HarmonyPatch'}
NOISE = re.compile(r'<|>|U3C|DisplayClass|d__|b__|\$|^_|PrivateImplementation|__StaticArray|^<>')

MODULES = [
    ('物品与库存', ['Item', 'Inventory', 'Grid', 'Slot', 'Container', 'Stack', 'Recipe',
                    'Ingredient', 'Material', 'Loot', 'Directory', 'Tag', 'Tool', 'Scrap']),
    ('店铺与经营', ['Store', 'Shop', 'Emporium', 'Business', 'Counter', 'Display', 'Reputation',
                    'Client', 'Customer', 'Clientele', 'Trade', 'Market', 'Price', 'Bargain',
                    'Negociation', 'Exchange', 'Transaction', 'Contract', 'Loan', 'Deed']),
    ('机器与生产', ['Machine', 'Chem', 'Water', 'Plant', 'Husbandry', 'Furnace', 'Brew', 'Wine',
                    'Hydroponic', 'Lab', 'Scanner', 'Analyser', 'Finisher', 'Production', 'Fabric',
                    'Chemical', 'Liquid', 'Process']),
    ('人物与角色', ['Player', 'Character', 'Hireling', 'Mentor', 'Trader', 'Faction', 'Husband',
                    'Wife', 'Npc', 'Agent', 'Officer', 'Guard', 'Scavenger', 'Perk', 'Skill']),
    ('对话与剧情', ['Dialogue', 'Dialog', 'Story', 'Quest', 'News', 'Event', 'Bubble', 'Text',
                    'Note', 'Manual', 'Paper', 'Book', 'Handnote', 'Log', 'Hint', 'Tutorial']),
    ('界面与UI', ['UI', 'Window', 'Panel', 'Hud', 'Menu', 'Button', 'Element', 'Tooltip', 'Screen',
                  'View', 'Controller', 'Cursor', 'Icon', 'Sprite', 'Render', 'Tooltip']),
    ('存档与数据', ['Save', 'Data', 'Config', 'Load', 'Serialize', 'File', 'Mod', 'Local', 'Loc',
                    'Setting', 'Option', 'Stat', 'Record', 'History', 'Cache']),
    ('场景与房间', ['Room', 'Map', 'Scene', 'Area', 'Zone', 'Raid', 'Tile', 'Decor', 'Furniture',
                    'Emporium', 'Station']),
    ('战斗与武装', ['Combat', 'Weapon', 'Ammo', 'Damage', 'Health', 'Armor', 'Ability', 'Attack',
                    'Suppress', 'Bullet', 'Gun']),
    ('系统与工具', ['Helper', 'Utils', 'Util', 'Manager', 'Service', 'System', 'Game', 'Turn',
                    'Time', 'Day', 'Weather', 'Audio', 'Sound', 'Music', 'Factory', 'Graph']),
]


def clean_game(n):
    if not n or NOISE.search(n) or n in BLACKLIST:
        return False
    if '.' in n:
        return False
    if n.startswith(EXCLUDE_PREFIX):
        return False
    return True


TOKEN = re.compile(r'[A-Z][a-z0-9]*|[a-z0-9]+')


def score_module(name):
    """按 CamelCase 词元匹配,而非子串 —— 避免 AccessTools 被算进「物品」分册"""
    toks = [t.lower() for t in TOKEN.findall(name)]
    best, best_score = '其他', 0
    for mod, kws in MODULES:
        s = 0
        for k in kws:
            kl = k.lower()
            if kl in toks:                       # 词元完全一致
                s += 4
            elif any(t.startswith(kl) and len(kl) >= 5 for t in toks):
                s += 1                           # 长关键词前缀匹配
        if s > best_score:
            best, best_score = mod, s
    return best


DECL = re.compile(r'^(public|internal|private|protected)?\s*'
                  r'(static |abstract |sealed |partial )*(class|struct|interface|enum)\s+([\w<>]+)')


def parse():
    """只收"空命名空间"的类型 —— dump.cs 里每个类型前都有 `// Namespace:` 标注,
    游戏自己的代码全部没有命名空间(PlayerStore 等),其余是 Unity/.NET/第三方。"""
    lines = open(DUMP, encoding='utf-8', errors='ignore').read().splitlines()
    types, cur, ns = {}, None, ''
    NESTED = re.compile(r'\b(class|struct|interface|enum)\s+[\w<>]+')
    for l in lines:
        if l.startswith('// Namespace:'):
            ns = l.split(':', 1)[1].strip()
            continue
        if not l.startswith('\t'):
            m = DECL.match(l)
            if m:
                name = m.group(4)
                if ns != '' or not clean_game(name):
                    cur = None
                    continue
                # 先去掉注释再取基类(否则会抓到 "// TypeDefIndex: 797" 里的冒号)
                head = l.split('//')[0]
                base = ''
                mb = re.search(r':\s*(.+)$', head)
                if mb:
                    base = mb.group(1).strip()
                cur = name
                types[cur] = {'kind': m.group(3), 'base': base,
                              'fields': [], 'props': [], 'methods': [], 'enum': []}
            else:
                st = l.strip()
                # 类型体的 '{' 单独占一行且顶格,不能因此丢掉当前类型
                if st not in ('{', ''):
                    cur = None
            continue
        if cur is None:
            continue
        s = l.strip()
        if not s or s.startswith('//') or s.startswith('['):
            continue
        if s == '}':
            continue
        if NESTED.search(s) and not s.startswith(('public ', 'protected ')):
            continue
        if s.startswith(('public', 'protected')):
            if '(' in s:
                types[cur]['methods'].append(re.sub(r'\s*\{\s*\}\s*$', '', s))
            elif 'get;' in s or 'set;' in s or '{ get' in s:
                types[cur]['props'].append(s)
            else:
                types[cur]['fields'].append(re.sub(r'\s*//.*$', '', s))
        elif types[cur]['kind'] == 'enum' and re.match(r'^[A-Za-z_]\w*\s*=', s):
            types[cur]['enum'].append(s.rstrip(','))
    return types


def gen_readme(types, mod_counts, member_total):
    L = []
    L.append('# 游戏 API 全量文档\n')
    L.append('> 《Probably Stolen》完整 API 索引。由 `reverse/gen_api_docs.py` 从 '
             '`reverse/output/dump.cs`（Il2CppDumper）自动生成。\n')
    L.append('## 怎么用（推荐顺序）\n')
    L.append('1. **查某个类/方法** → 用检索工具（最快）\n')
    L.append('   ```bash\n'
             '   python reverse/api.py search <关键字>      # 类名/成员名模糊搜\n'
             '   python reverse/api.py cls <类名>          # 打印某个类的全部成员\n'
             '   python reverse/api.py members <关键字>    # 只搜成员名\n'
             '   ```\n')
    L.append('2. **想知道某个系统有哪些类** → 看下面的分册\n')
    L.append('3. **想找某个名字** → `0-全量索引.md` 里 Ctrl+F\n')
    L.append('4. **要程序化处理** → 读 `output/api_index.json`\n')
    L.append('\n> 📌 上手前先看 [`游戏API速查.md`](../游戏API速查.md) —— 那是"实战用法+坑"的手册，'
             '本目录是"全量清单"。\n')
    L.append('\n## 规模\n')
    L.append('| 项 | 数量 |')
    L.append('|---|---|')
    L.append('| 游戏自有类型 | %d |' % len(types))
    L.append('| 公开方法 | %d |' % member_total[0])
    L.append('| 公开字段/属性 | %d |' % member_total[1])
    L.append('\n## 分册\n')
    L.append('| 分册 | 类型数 |')
    L.append('|---|---|')
    idx = 1
    for mod, c in mod_counts.most_common():
        fn = '%d-%s.md' % (idx, mod)
        L.append('| [%s](%s) | %d |' % (mod, fn, c))
        idx += 1
    L.append('\n- [0-全量索引.md](0-全量索引.md) — 全部类型一行一条\n')
    L.append('\n## 已剔除\n')
    L.append('引擎与第三方：`UnityEngine*` `Unity.*` `System*` `Mono.*` `TMPro` `Newtonsoft` '
             '`DG.*` `ES3*`(Easy Save 3) `MessagePack` `Mono.Cecil`，以及编译器生成的 `<...>` 噪音。\n')
    return '\n'.join(L)


def gen_index(types):
    L = ['# 全量类型索引\n', '格式：`类型名` · 种类 · 成员数 → 所属分册\n']
    by_mod = collections.defaultdict(list)
    for n, t in types.items():
        by_mod[score_module(n)].append(n)
    for mod in sorted(by_mod):
        L.append('\n## %s（%d）\n' % (mod, len(by_mod[mod])))
        for n in sorted(by_mod[mod], key=str.lower):
            t = types[n]
            cnt = len(t['methods']) + len(t['fields']) + len(t['props']) + len(t['enum'])
            L.append('- `%s` · %s · %d' % (n, t['kind'], cnt))
    return '\n'.join(L)


def gen_module_doc(mod, names, types):
    L = ['# %s\n' % mod, '> 共 %d 个类型。仅列 `public` / `protected` 成员。\n' % len(names)]
    for n in sorted(names, key=str.lower):
        t = types[n]
        cnt = len(t['methods']) + len(t['fields']) + len(t['props']) + len(t['enum'])
        if cnt == 0:
            continue
        L.append('\n## %s\n' % n)
        head = '%s %s' % (t['kind'], n)
        if t['base']:
            head += ' : ' + t['base']
        L.append('`%s`\n' % head)
        if t['enum']:
            L.append('**枚举值**：' + ', '.join(t['enum'][:60]) + '\n')
        for label, key in (('字段/属性', 'fields'), ('属性', 'props'), ('方法', 'methods')):
            if t[key]:
                L.append('**%s**\n' % label)
                L.append('```csharp')
                for s in t[key][:120]:
                    L.append(s)
                if len(t[key]) > 120:
                    L.append('// … 其余 %d 项见 api_index.json' % (len(t[key]) - 120))
                L.append('```\n')
    return '\n'.join(L)


def main():
    if '--stats' in sys.argv:
        pass
    types = parse()
    member_total = [0, 0]
    for t in types.values():
        member_total[0] += len(t['methods'])
        member_total[1] += len(t['fields']) + len(t['props'])
    mods = collections.defaultdict(list)
    for n in types:
        mods[score_module(n)].append(n)
    mod_counts = collections.Counter({k: len(v) for k, v in mods.items()})
    print('游戏自有类型 %d | 公开方法 %d | 公开字段/属性 %d' % (len(types), *member_total))
    for m, c in mod_counts.most_common():
        print('  %-12s %4d' % (m, c))
    if '--stats' in sys.argv:
        return
    os.makedirs(DOCS, exist_ok=True)
    for old in os.listdir(DOCS):          # 清掉上一次生成的分册,避免编号漂移后残留
        if old.endswith('.md'):
            try:
                os.remove(os.path.join(DOCS, old))
            except Exception:
                pass
    with open(OUT_JSON, 'w', encoding='utf-8') as f:
        json.dump({'count': len(types), 'types': types}, f, ensure_ascii=False, indent=0)
    print('api_index.json  %.1f MB' % (os.path.getsize(OUT_JSON) / 1048576))
    open(os.path.join(DOCS, 'README.md'), 'w', encoding='utf-8').write(
        gen_readme(types, mod_counts, member_total))
    open(os.path.join(DOCS, '0-全量索引.md'), 'w', encoding='utf-8').write(gen_index(types))
    idx = 1
    for mod, c in mod_counts.most_common():
        txt = gen_module_doc(mod, mods[mod], types)
        open(os.path.join(DOCS, '%d-%s.md' % (idx, mod)), 'w', encoding='utf-8').write(txt)
        print('  %d-%s.md  %d KB' % (idx, mod, len(txt) // 1024))
        idx += 1
    print('完成 →', DOCS)


main()
