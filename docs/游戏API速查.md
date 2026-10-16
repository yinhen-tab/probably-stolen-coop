# 《Probably Stolen》游戏 API 速查

> 联机 Mod 逆向笔记。所有签名从 `reverse/output/dump.cs`（Il2CppDumper）核实，非凭记忆。
> 标记：✅ 可用 ｜ ⚠️ 有坑 ｜ ❌ 不可用

## 0. 入口

| 用途 | 写法 |
|---|---|
| 玩家仓库 | `PlayerStore.Instance`（静态属性） |
| 当前局 | `GameMaster.current` / `GameMaster.current.turnManager` |
| 语言 | `LocHelper.GetCurrentLocaleCode()` |

---

## 1. 物品与库存

### 1.1 查物品

```csharp
Il2CppSystem.Collections.Generic.List<GameItem> ps.FindAllItem(bool isOwn = true);  // ✅ 含容器内嵌套物品
bool ps.IsPlayerOwnThisItem(string id);
PlayerStore.EncodeItem(GameItem) -> string;   // {"saveItems":[...]} 含位置/朝向/内容
```

⚠️ `FindAllItem` 返回的是 **IL2CPP 的 List**，不是 `System.Collections.Generic.List`。

### 1.2 物品本体（`GameItem`，抽象类）

```csharp
string   identifier;        // 物品标识（如 "processed_meat"）—— 跨端一致
int      uniqueId;          // 实例 ID —— 同一份存档两端一致，可做跨端匹配键
int      unitCount;
GridShape shape;            // ⚠️ 物品自身外形，minX/minY 恒为 0
GridShape modifiedShape;    // ✅ 落位坐标在这里（minX/minY/orientation/flipped）
TagSystem state, modifiedState;
GameInventory parentInventory;   // 物品在哪个库存里
PixelWindow   contentWindow;     // 物品的内容窗
int  MaxNumRemove();
void RestoreContentWindowPosition();
```

### 1.3 运行时物品 = `GameItemElement`

```csharp
public class GameItemElement : GameItem, PixelElement   // 运行时实例就是它
void SetCenteredPosition(float x, float y);   // ✅ 控制"画在哪"——只改 modifiedShape 画面不会动
bool IsDestroyed();
```

### 1.4 库存

```csharp
// GameInventory（抽象，实现 GraphNodeStorage）
string identifier;                    // 库存自身的标识
GraphNodeStorage parent;              // 父节点 ← 容器物品（换容器时用）
List<GameItem> childItems;            // 直接子物品
SlotMarker TryInventorySlot(GameItem item, GridShape tempShape, TagSystem itemState);                       // 3 参
SlotMarker TryInventorySlot(GameItem item, int requestedNum, Vector2 center, GridShape shape, TagSystem st); // 5 参

// GameGridInventory : GameInventory, PixelElement
int widthPixels, heightPixels;        // 库存像素尺寸
GridShape inventoryShape;             // 网格形状
List<GameItem> items;
bool CheckShapePlacement(GameItem item, GridShape shape, List<GameItem> ignoredItems);
```

**格子边长 = `widthPixels / inventoryShape.width`**（实测主库存 = 288/18 = **16px**，网格 18×4）
**格子→像素中心 = `(minX + w/2) * 格边长`**

### 1.5 形状

| 类 | 可写性 |
|---|---|
| `GridShape`（接口） | `minX/minY/orientation/flipped/width/height` **只读** |
| `GridShapeBuilder : GridShape` | 同名属性 **可写**；`SetTransform(GridShape) -> GridShapeBuilder` |

⚠️ `GridShapeBuilder` **不能隐式转 `GridShape`**。
✅ 改坐标：`shape.TryCast<GridShapeBuilder>()` 后直接改字段（返回的就是物品持有的同一对象，**无需回写**）。

### 1.6 落位

```csharp
class SlotMarker { GameItem item; GameInventory inventory; GridShape itemGridShape;
                   int index; GameItem targetItem; int numTransfer; int priority; }
int TryAcceptOnce(int amount = -1);
int AcceptUnchecked();
bool IsValid();
```

⚠️ 实测：物品**已在库内**时 `TryInventorySlot` 返回**无效 slot** → 改用"改 modifiedShape + SetCenteredPosition"。

### 1.7 搬运 / 换容器 ✅

```csharp
// GraphUtils（静态）
int  TryAccept(GraphNodeStorage node, GameItem item);
int  TryAcceptAll(GraphNodeStorage node, GameItem item, int amount = -1);
int  TryAcceptOnce(GraphNodeStorage node, GameItem item, int amount = -1);
int  TryAcceptAllMid(GameGridInventory node, GameItem item, int amount = -1);
bool CanAccept(GraphNodeStorage node, GameItem item);
int  CountAll(List<GameItem> items);
void EmporiumTryAdd(GraphNodeStorage node, GameItem item);
void EmporiumTryAdd(GameItem item);                 // 放进店铺世界（兜底）
bool DestroyIfUnplaced(GameItem item, string context);

// GraphNodeStorage（接口，GameItem 与 GameInventory 都实现它）
List<GraphNodeStorage> parents, children;
bool AddParent(GraphNodeStorage node);
bool RemoveParent(GraphNodeStorage node);
SlotMarker TryFindOneValidInventorySlot(GameItem item);
bool MayHaveValidInventorySlot(GameItem item);
```

⚠️ 传参给 `GraphUtils` 需要 **`.Cast<GraphNodeStorage>()`**（`GameInventory` 不能隐式转）。
✅ 找容器物品的内部库存：遍历 `container.children`，取第一个 `TryCast<GameInventory>()`。

### 1.8 存档编解码

```csharp
// SaveManager（静态）—— 干净、可单独调用
List<SaveItemNode> EncodeNodes(GraphNodeStorage firstNode);
GameItem           DecodeNodes(List<SaveItemNode> savedItems);   // 返回 GameItem

class SaveItemNode { string identifier; int unitCount; long uuid; TagSystem itemState;
                     GridShapeBuilder itemShape, itemModifiedShape;
                     string itemType, name, shortDescription, flavorText;
                     int uniqueId; long unitBaseValue, unitValue, lateUnitValue, backupUnitValue;
                     List<string> itemTypes; List<long> childItems; List<int> childItemInventoryNode; }
class SaveState { List<SaveItemNode> saveItems; }
```

❌ **`PlayerStore.DecodeSaveItem(string JSON, GameInventory inv)` 运行时抛 `NullReferenceException`**（读档期专用）。
⚠️ 不要用「`ExpelAll()` + 逐件 `DecodeSaveItem` 还原」——还原失败等于**清空玩家库存**。

---

## 2. 界面与窗口

### 2.1 通用

```csharp
class PixelWindow : PixelElement, GraphNodeWindow, GraphNodeStorage
    PixelWindow Show(); PixelWindow Hide();
    string titleString;
    // rectTransform（经 PixelElement 拿）→ .anchoredPosition 就是窗口位置

class TreeNodeRender : MonoBehaviour   // 物品/节点的渲染节点
    PixelWindow  window;
    PixelElement element;        // ✅ 就是 GameItemElement（本身是 GameItem）
    TreeNodeRender parent;  RectTransform rectTransform;
    OnBeginDrag/OnEndDrag/OnDrop(PointerEventData)
```

### 2.2 "按物品打开"的界面（参数是 `GameItem`）

```csharp
ItemMouseDoubleClickHandler.OpenContentAction(GameItem, Vector2);   // 双击物品开内容窗
ItemMouseDoubleClickHandler.OpenExamineAction(GameItem, Vector2);

LogUIManager.Instance.OpenUI(GameItem) / CloseUI() / Toggle(GameItem)      // 录音机（回顾对话）
HandnoteUIManager.Instance.OpenPanel(GameItem) / ClosePanel()              // 便签 / 手账
```

同类还有（`OpenUI/OpenPanel(GameItem)` + `CloseUI/ClosePanel/HideUI()`）：
`ChemFinisherUIManager`、`ChemScannerUIManager.Open(GameItem, GameItem)`、`WaterScannerUIManager.Open(GameItem, GameItem)`、
`WaterStripUIManager.Open(GameItem)`、`ExchangeUIManager.OpenPanel`、`LabelerUIManager`、`LabelerBlueUIManager`、
`PlantAnalyserUIManager`、`ScratcherUIManager`、
`InspectionUIManager.OpenCigaretteConfrontPanel / OpenStampConfrontPanel / OpenInjectorConfrontPanel` + `CloseConfrontPanel()`

### 2.3 "开关式"界面（无参数）

`NotebookUIManager`、`RecorderUIManager`、`RecordUIManager`、`MapUIManager`、`CommissaryUIManager`、
`ClienteleUIManager`、`BargainUIManager`、`NegociationUIManager`、`ReputationUIManager`、`LoanPanelUIManager`、
`PerkUIController`、`InspectionUIManager`、`AdvCalendarUIManager`、`DialogSettingUIManager`、`WildUIManager.OpenUI(bool)`、
`RepublicLogoUIManager`、`MultiPageUIManager.OpenUI(int bookIndex)`

> 完整清单与钩子见 `UiWindowSync.cs`（由 `installer/gen_ui_sync.py` 生成）。
> **刻意不同步**：设置 / 暂停菜单 / 确认弹窗 / 存档界面 / Mod 界面 / 教程提示 / 游戏结束。

### 2.4 坐标换算

```csharp
RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPos, null, out Vector2 local);
```
（Overlay Canvas 相机传 `null`）

---

## 3. 电话

```csharp
PhoneUIManager.Instance
    OpenUI() / CloseUI()
    HandleCall(long number)      // ⚠️ 只做拨号校验（号码存在性/是否在店/播音），不弹面板
    StartCalling()               // ✅ 真正触发面板：→ DialoguePhoneUIManager.StartDialogue
    StopCall() / AutoCall(long) / StartPhoneDialog(long) / PressNumber(int)

PhoneBookUIManager.Instance.OpenUI() / CloseUI()

DialoguePhoneUIManager.Instance
    StartDialogue(Dialogue) / EndConversation() / InterruptDialog()
    SelectChoice(int index)      // ✅ 同步"选第几项"可保证两端分支一致
    isConversationActiveFlag     // 是否正在对话

Dialogue { string title; RichTextBuilder text; string plainText; Sprite portrait;
           int seenTimes; Dialogue nextDialogue; List<ValueTuple<GameItem,Dialogue,Action>> choices;
           bool HasChoices(); }

StorePhoneClient（静态）: GetPhoneClientByNumber(long) / IsNumberExist(long) / IsPhoneClientAtStore(long)
```

---

## 4. 店铺物件（`IClickable.OnClick`）

`StoreRecorder`（录音机）、`StoreVendingMachine`（售货机）、`StoreVendingFountain`（饮水机）、
`StorePhoneObject`（电话）、`StorePoster`（海报）、`StoreLamp`（灯）、
`StoreClienteleDisplay`（展示柜）、`StoreCreditDisplay`（信用屏）、`StoreDebugBell`（铃铛）

⚠️ `StoreRecorder.OnClick` **只生成文字**（派系名/颜色），不开窗。

---

## 5. 工具与流程

**本地化**：`LocHelper.GetLocalizedUI(key, object[])`、`GetLocalizedMainMenu(...)`、`GeneralHelper.GetDisplayNameFromID(itemID)`

**查中文名的正确姿势**（比扫汇编快几十倍）：解 Unity 本地化表
```
Probably Stolen_Data/StreamingAssets/aa/StandaloneWindows64/
    localization-string-tables-chinese(simplified)(zh)_assets_all.bundle
```
```python
import UnityPy
env = UnityPy.load(bundle); obj.get_raw_data().decode('utf-8','ignore')  # 直接搜中文
```

**逆向小工具**（都在 `reverse/`）

| 工具 | 用途 |
|---|---|
| `an.py cls <类名>` | 列类字段/方法 |
| `an.py calls <地址> [长度]` | 列函数内部调用（带方法名） |
| `an.py m <类> <方法>` | 查方法地址 |
| `disasm_probe.py` | 反汇编 |
| `scan_caller.py <方法名>` | 扫"谁调用了某方法"（慢） |
| `audit_ui.py` | 审计所有界面类 + 对比已同步情况 |
| `api_ref.py` | 按清单精确提取签名 |

---

## 6. 坑总表（踩过的）

| 坑 | 正确做法 |
|---|---|
| `shape.minX/minY` 恒为 0 | 落位坐标看 **`modifiedShape`** |
| 只改数据画面不动 | 还要调 **`GameItemElement.SetCenteredPosition`** |
| `DecodeSaveItem` 运行时 NRE | 别用；改用 `GraphUtils.TryAccept` 等 |
| `ExpelAll` + 还原失败 → 清空库存 | **永远不要**走这条 |
| `TryInventorySlot` 对库内物品返回无效 slot | 改 modifiedShape + SetCenteredPosition |
| `GridShapeBuilder` 不能隐式转 GridShape | **不要回写**，直接改字段 |
| `GraphUtils` 参数要 GraphNodeStorage | `.Cast<GraphNodeStorage>()` |
| `FindAllItem` 是 IL2CPP List | 用 `Il2CppSystem.Collections.Generic.List<T>` |
| `PlayerStore.gridInv` 不是真实物品容器 | 从物品的 `parents[0]` 反推 |
| BepInEx 不给老 cfg 补新增键 | LoadConfig 末尾 `config.Save()` |
| `RunOnMain` 队列只在进游戏后消化 | 主菜单 tick 也要 `PumpMainThread()` |
| 必须先启动 Steam | 否则 doorstop 不注入，日志都不生成 |
| 游戏单实例锁 | 改 DLL 前必须先关游戏 |
