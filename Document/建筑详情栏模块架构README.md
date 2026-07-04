# 建筑详情栏模块架构 README

本文档用于整理 `pop_建筑详情.prefab` 和 `Popup_BuildingDetails` 的职责边界，为后续代码整理提供依据。

当前结论：已新增 `BuildingDetailsBlock_Workforce.cs`，用于把“岗位”块从 `Popup_BuildingDetails` 中拆出为独立 UI 组件。目标挂载位置是 prefab 的 `info_岗位` 对象。`Popup_BuildingDetails` 保留为弹窗外壳和模块调度者，不再直接处理岗位块的按钮、文本和业务交互。

## 当前 prefab 观察

检查对象：

```text
Assets/Landsong/Objects/Prefabs/UI/建筑详情栏/pop_建筑详情.prefab
```

当前主要结构：

```text
pop_建筑详情
├─ 标题框 / 图标 / 名称 / 描述
├─ 滚动视图父物体 / 滚动视图 / Viewport / Content
│  ├─ info_岗位
│  └─ info_功能
└─ 侧栏 / 侧栏容器 / 内容
```

`info_岗位` 当前是一个独立的视觉块，包含标题和内容区域，并已经承载岗位相关控件：

- 自动补贴 Toggle。
- 上一档 / 下一档 Button。
- 当前工人 TMP_Text。
- 当前补贴金币 TMP_Text。
- 招募工人 Button。
- 招募消耗 TMP_Text。
- 工人详情触发区域。

`info_功能` 已经在 prefab 中占位，说明详情栏未来不是单一“岗位详情”，而是多个功能块并列存在。

当前 `Popup_BuildingDetails` 的问题是：根弹窗脚本直接持有岗位块内部字段。随着 `info_功能`、产出、库存、消耗、税收、维修等块增加，根脚本会快速变成“所有详情块的总控脚本”，难以维护。

## 推荐职责拆分

### `Popup_BuildingDetails`

保留为详情弹窗外壳，只负责跨模块的公共流程：

- 显示 / 隐藏弹窗。
- 绑定当前 `BuildingBase`。
- 订阅和取消订阅 `BuildingBase.StateChanged`。
- 刷新建筑名称、描述、图标。
- 管理通用侧边栏的显示、隐藏和内容重建。
- 调度子模块：当前建筑支持某个能力时显示对应模块，否则隐藏。

不建议继续放在根脚本中的内容：

- 岗位块的具体按钮字段。
- 岗位块的具体 TMP_Text 字段。
- 岗位补贴目标档位按钮逻辑。
- 招募工人按钮逻辑。
- 岗位侧边栏内容拼装。

### `BuildingDetailsBlock_Workforce`

已新增脚本：

```text
Assets/Landsong/Scripts/UI/BuildingDetailsBlock_Workforce.cs
```

目标挂载位置是 `info_岗位`。

职责：

- 判断当前建筑是否实现 `IBuildingWorkforceFundingSource`。
- 控制 `info_岗位` 显示 / 隐藏。
- 刷新岗位块内部文本和按钮状态。
- 处理自动补贴 Toggle。
- 处理上一档 / 下一档。
- 处理招募 1 名工人按钮。
- 处理桌面 hover 和移动端按住显示岗位详情侧栏。
- 需要侧栏时，通过根弹窗提供的通用侧栏接口请求显示内容。

岗位块脚本可以持有这些字段：

```text
Toggle tgl_自动补贴满岗位
Button btn_目标稳定工人上一档
TMP_Text txt_当前工人
Button btn_目标稳定工人下一档
TMP_Text txt_当前补贴金币
Button btn_招募工人
TMP_Text txt_招募消耗
GameObject go_工人详情触发区
```

当前结构中，岗位字段已经从 `Popup_BuildingDetails` 移到 `BuildingDetailsBlock_Workforce`。根弹窗只保留 `block_岗位` 引用，并在初始化时调用 `block_岗位.Initialize(this)`。

`go_补贴栏标题`、`go_补贴栏内容` 不属于岗位块脚本字段。它们是 prefab 的静态布局对象，不需要运行时显隐或文本刷新。岗位块是否显示，直接由 `BuildingDetailsBlock_Workforce` 所在的 `info_岗位` 对象控制。

### 后续功能块

后续每个大块都按同一规则拆分：

```text
info_岗位 -> BuildingDetailsBlock_Workforce
info_功能 -> BuildingDetailsBlock_Function
info_产出 -> BuildingDetailsProductionBlock
info_库存 -> BuildingDetailsStorageBlock
info_消耗 -> BuildingDetailsConsumptionBlock
```

不是每个建筑都要显示所有块。每个块通过建筑能力接口判断是否可用。

## 建议的模块接口

Unity 不方便直接序列化接口数组，因此建议用抽象 MonoBehaviour 作为模块基类：

```csharp
public abstract class BuildingDetailsBlock : MonoBehaviour
{
    public abstract bool CanShow(BuildingBase building);
    public abstract void Bind(BuildingBase building, BuildingDetailsContext context);
    public abstract void Refresh();
    public abstract void Unbind();
}
```

`Popup_BuildingDetails` 可以在 `Awake()` 中收集子物体上的模块：

```csharp
private BuildingDetailsBlock[] blocks;

private void Awake()
{
    blocks = GetComponentsInChildren<BuildingDetailsBlock>(true);
}
```

刷新时：

```csharp
foreach (var block in blocks)
{
    if (block.CanShow(building))
    {
        block.gameObject.SetActive(true);
        block.Bind(building, context);
        block.Refresh();
    }
    else
    {
        block.Unbind();
        block.gameObject.SetActive(false);
    }
}
```

`BuildingDetailsContext` 不需要一开始设计得很大。第一阶段只暴露侧边栏能力即可：

```csharp
public sealed class BuildingDetailsContext
{
    public void ShowSidebar(IReadOnlyList<BuildingDetailsSidebarRow> rows);
    public void HideSidebar();
}
```

## 数据与字符串职责边界

### 建筑和系统负责什么

建筑、建筑模块、计算系统负责游戏语义和数值：

- 当前工人、最大工人、稳定工人。
- 当前就业吸引力。
- 各类吸引力来源。
- 目标补贴金币。
- 是否可招募。
- 招募 1 名工人的费用。
- 是否执行成功。
- 资源产出、消耗、库存容量等结构化数据。

建筑可以暴露命令：

```text
SetAutoFullWorkerSubsidyEnabled(bool enabled)
SetTargetStableWorkers(int targetStableWorkers)
TryRecruitToFull() / 后续建议改名 TryRecruitWorker()
```

建筑不应该关心：

- 文本是“工人：0/3（稳定：2）”还是“0 / 3 | 稳定 2”。
- 按钮文案是“招募”还是“招募1名工人”。
- 正负数用什么颜色。
- hover 还是按住显示侧栏。
- UI 块在 prefab 的哪个位置。

### UI 负责什么

UI 模块脚本负责把结构化数据转成当前界面的表现：

- 拼接 `工人：0/3（稳定：2）`。
- 拼接 `消耗12金币 招募1名工人`。
- 设置按钮 interactable。
- 设置 Toggle 状态。
- 决定侧边栏行的顺序、颜色和格式。
- 决定模块显示 / 隐藏。
- 处理 pointer enter / exit / down / up。

也就是说，交互块中的最终显示文本应由 UI 负责。

### `GetBaseInfo()` 和 `GetDetailInfo()` 的例外

项目里已经存在两类通用详情数据：

```text
BuildingBase.GetBaseInfo()
BuildingBase.GetDetailInfo()
```

它们用于列表、选中栏、通用详情行等“只读信息”。这里允许建筑返回已经格式化的短文本或 `BuildingDetailRow`，因为这些内容本身就是建筑提供给通用信息面板的展示资料。

但不要把它们当成交互模块的数据源。

推荐边界：

- `GetBaseInfo()`：短摘要，可以是字符串。
- `GetDetailInfo()`：只读详情，可以是 `BuildingDetailSection -> BuildingDetailRow`。
- 交互模块：读取能力接口的数值和状态，由 UI 模块自己拼接显示文本。

## 功能块数据接口

当前新增功能块脚本：

```text
Assets/Landsong/Scripts/UI/BuildingDetailsBlock_Function.cs
```

功能块的数据入口是建筑侧的结构化接口：

```text
BuildingBase.GetFunctionBlockEntries()
```

单条数据使用 `BuildingFunctionBlockEntry` 表示：

- `Group`：显示在哪个分组，当前有 `Resource` 和 `Functionality`。
- `DisplayName`：资源名或功能名，例如 `水`、`水果`、`库存格`。
- `Amount`：正负数值。资源消耗传负数，资源产出传正数，功能增益传正数。
- `SidebarRows`：结构化侧边栏追溯行，可为空。

UI 负责把这些结构化数据拼接成当前界面文本：

```text
资源：-3水、2水果
功能性：+4库存格
```

资源汇总规则：

- 顶部资源行按 `Group + DisplayName` 汇总。
- 资源行正数默认不显示 `+`，负数显示 `-`。
- 功能性行正数显示 `+`。
- 鼠标 hover 或移动端按住 `info_功能` 时显示侧边栏。
- 如果条目提供了 `SidebarRows`，侧边栏优先显示这些结构化追溯行。
- 如果条目没有 `SidebarRows`，侧边栏显示汇总后的 `+/-数量名称`。

例如 `ResidentialHousingLV1` 当前提供：

```text
资源：-x蔬菜
```

侧边栏显示：

```text
每人口消耗 x蔬菜
```

这不是 UI 判断居民房后拼出来的专用句子，而是住宅建筑提供的结构化行：

```text
Label = 每人口消耗
Value = x蔬菜
SignedValue = -x
```

维护消耗也可以用同一套接口：

```text
顶部：
资源：-1金币

侧边栏：
Label = 维护消耗
Value = 每回合 -1金币
SignedValue = -1
```

库存格这种通用能力由建筑模块提供。`BuildingInventorySlotCapacityModule` 会向功能块追加：

```text
功能性：+x库存格
```

后续如果某个建筑有特殊功能，不需要让 UI 判断建筑类型；建筑只要重写 `GetFunctionBlockEntries()` 或通过建筑模块追加 `BuildingFunctionBlockEntry` 即可。建筑仍然只提供事实，不直接拼接顶部 UI 文案。

## 当前接口整理建议

`IBuildingWorkforceFundingSource` 当前仍有一些历史命名：

```text
RecruitToFullWorkerCount
RecruitToFullCost
CanRecruitToFull
TryRecruitToFull()
```

但当前需求已经改为“每次招募 1 名工人，不能超过稳定人数”。后续代码整理建议改名为：

```text
RecruitWorkerCount 或 RecruitWorkerStepCount
RecruitWorkerCost
CanRecruitWorker
TryRecruitWorker()
```

如果不想一次性改 prefab 和所有调用，可以分两步：

1. 先新增新命名属性 / 方法，让旧命名暂时转发到新命名。
2. 更新 UI 和建筑实现后，删除旧命名。

如果用户明确不需要兼容旧版本，则可以直接改名并修复引用。

## 建议整理顺序

1. 新增 `BuildingDetailsBlock` 和 `BuildingDetailsContext`。
2. 新增 `BuildingDetailsBlock_Workforce`，挂到 `info_岗位`。
3. 把 `Popup_BuildingDetails` 中的岗位字段和岗位方法迁移到 `BuildingDetailsWorkforceBlock`。
4. `Popup_BuildingDetails` 只保留弹窗外壳、建筑绑定、公共侧边栏、模块调度。
5. 将 `IBuildingWorkforceFundingSource` 中的招募相关命名从 `RecruitToFull` 语义整理为单次招募语义。
6. 编译通过后，再考虑是否清理 prefab 字段命名。

## 当前阶段不建议做的事

- 不建议把所有功能块继续写进 `Popup_BuildingDetails`。
- 不建议让建筑直接返回按钮文案或完整 UI 句子。
- 不建议为了一个岗位块设计复杂的插件系统。
- 不建议把通用侧边栏复制到每个块里；侧边栏应由根弹窗统一拥有，子块只提交要显示的行数据。
- 不建议用字符串判断建筑类型；应使用能力接口或模块接口判断。

## 一句话规则

建筑提供“事实和命令”，UI 模块负责“表达和交互”。
