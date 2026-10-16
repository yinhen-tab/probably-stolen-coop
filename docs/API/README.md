# 游戏 API 全量文档

> 《Probably Stolen》完整 API 索引。由 `reverse/gen_api_docs.py` 从 `reverse/output/dump.cs`（Il2CppDumper）自动生成。

## 怎么用（推荐顺序）

1. **查某个类/方法** → 用检索工具（最快）

   ```bash
   python reverse/api.py search <关键字>      # 类名/成员名模糊搜
   python reverse/api.py cls <类名>          # 打印某个类的全部成员
   python reverse/api.py members <关键字>    # 只搜成员名
   ```

2. **想知道某个系统有哪些类** → 看下面的分册

3. **想找某个名字** → `0-全量索引.md` 里 Ctrl+F

4. **要程序化处理** → 读 `output/api_index.json`


> 📌 上手前先看 [`游戏API速查.md`](../游戏API速查.md) —— 那是"实战用法+坑"的手册，本目录是"全量清单"。


## 规模

| 项 | 数量 |
|---|---|
| 游戏自有类型 | 1553 |
| 公开方法 | 8372 |
| 公开字段/属性 | 5836 |

## 分册

| 分册 | 类型数 |
|---|---|
| [其他](1-其他.md) | 740 |
| [系统与工具](2-系统与工具.md) | 158 |
| [物品与库存](3-物品与库存.md) | 126 |
| [店铺与经营](4-店铺与经营.md) | 116 |
| [界面与UI](5-界面与UI.md) | 106 |
| [对话与剧情](6-对话与剧情.md) | 100 |
| [存档与数据](7-存档与数据.md) | 90 |
| [机器与生产](8-机器与生产.md) | 58 |
| [场景与房间](9-场景与房间.md) | 22 |
| [战斗与武装](10-战斗与武装.md) | 19 |
| [人物与角色](11-人物与角色.md) | 18 |

- [0-全量索引.md](0-全量索引.md) — 全部类型一行一条


## 已剔除

引擎与第三方：`UnityEngine*` `Unity.*` `System*` `Mono.*` `TMPro` `Newtonsoft` `DG.*` `ES3*`(Easy Save 3) `MessagePack` `Mono.Cecil`，以及编译器生成的 `<...>` 噪音。
