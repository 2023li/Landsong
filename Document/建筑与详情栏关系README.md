# 建筑与详情栏关系 README

本文档说明建筑脚本如何向建筑详情弹窗提供数据，以及详情弹窗如何决定显示哪些信息块。

核心规则：

```text
建筑 / 建筑模块提供事实、数值、命令和结构化说明。
详情栏 UI 负责显隐、排版、颜色、按钮交互和最终文本格式。
```

UI 不应该通过建筑类型硬编码文案，例如“如果是居民房就显示每人口消耗”。这种语义应由建筑或建筑模块通过结构化数据提供。

## 当前关系

```text
BuildingBase
├─ GetOverviewInfo()
├─ GetRuntimeStatuses()
├─ GetFunctionBlockEntries()
└─ 能力接口 / 建筑模块

Popup_BuildingDetails
├─ 标题区：名称 / 描述 / 图标
├─ BuildingDetailsBlock_Workforce：岗位块
├─ BuildingDetailsBlock_Function：功能块
├─ BuildingDetailsBlock_Level：等级块
└─ 通用侧边栏：由根弹窗统一显示
```

`Popup_BuildingDetails` 是弹窗外壳和调度者。它绑定当前 `BuildingBase`，刷新标题区，然后把建筑交给各个子块判断是否显示。

子块不应该互相持有引用。需要显示侧边栏时，子块向根弹窗提交 `BuildingDetailsSidebarRow`，由根弹窗统一创建侧边栏文本。

## 显示隐藏规则

当前详情栏不是由建筑直接开关某个 GameObject，而是由子块根据建筑能力判断。

岗位块：

```csharp
targetBuilding is IBuildingWorkforceFundingSource
```

建筑实现 `IBuildingWorkforceFundingSource` 时显示岗位块，否则隐藏。

例如 `LumberCabinLV1` 实现岗位接口，所以显示岗位块。`PlayerHomeLV1` 没有实现岗位接口，所以不显示岗位块。

功能块：

```csharp
targetBuilding.GetFunctionBlockEntries()
```

建筑返回至少一条有效 `BuildingFunctionBlockEntry` 时显示功能块，否则隐藏。

例如 `PlayerHomeLV1` 通过 `BuildingInventorySlotCapacityModule` 提供 `+5库存格`，所以显示功能块。

等级块：

```csharp
targetBuilding.TryGetModule<BuildingLevelUpgradeModule>(out _)
```

建筑拥有启用的 `BuildingLevelUpgradeModule` 时显示等级块，否则隐藏。

等级块当前显示：

```text
自动升级 Toggle
经验进度 Slider
当前经验/升级所需经验
升级按钮
升级消耗文本
```

## 职责边界

建筑和建筑模块负责：

- 当前玩法事实，例如人口、工人、产出、消耗、库存格。
- 当前状态，例如异常状态、废弃、资源不足。
- 可执行命令，例如设置补贴、招募工人。
- 功能块结构化数据，例如 `BuildingFunctionBlockEntry`。
- 结构化侧边栏行，例如 `BuildingFunctionBlockSidebarRow`。

UI 负责：

- 是否显示某个块。
- 拼接当前界面的短文本。
- 正负号、颜色、分隔符、冒号。
- hover / 按住显示侧边栏。
- 按钮、Toggle、文本控件刷新。
- prefab 上对象的显隐和排版。

不建议建筑直接持有 UI 对象，也不建议 UI 按具体建筑类型写特殊分支。

## 建筑侧 API

### `GetOverviewInfo()`

用于建筑概览短文本。当前会进入 `BuildingStatusUIFormatter.CreateDisplayData()`，并作为详情弹窗标题区描述文本的优先来源。

示例：

```csharp
public override string GetOverviewInfo()
{
    return "仓库 +5格";
}
```

适合放一行摘要，例如：

```text
人口 2/5
工人 1/3（稳定：2）
仓库 +5格
```

### `GetRuntimeStatuses()`

用于建筑当前状态。

适合放异常、警告、运行状态，例如：

```text
食物不足
无法连接资源点
消耗失败
```

### `GetFunctionBlockEntries()`

用于功能块。返回的每条 `BuildingFunctionBlockEntry` 都会参与顶部汇总，并可选提供侧边栏行。

当前分组：

```csharp
BuildingFunctionBlockGroup.资源组
BuildingFunctionBlockGroup.功能性
```

资源组显示为：

```text
资源：-3水、2水果
```

功能性显示为：

```text
功能性：+5库存格
```

## 功能块数据结构

`BuildingFunctionBlockEntry` 表示功能块顶部的一条数据。

```csharp
new BuildingFunctionBlockEntry(
    BuildingFunctionBlockGroup.功能性,
    "库存格",
    5)
```

含义：

```text
分组：功能性
显示名：库存格
数值：5
```

顶部显示：

```text
功能性：+5库存格
```

`BuildingFunctionBlockSidebarRow` 表示鼠标移上功能块后，侧边栏中的一行结构化说明。

```csharp
new BuildingFunctionBlockSidebarRow(
    "仓库容量",
    "+5格",
    5,
    true)
```

含义：

```text
Label：仓库容量
Value：+5格
SignedValue：5
HasSignedValue：true
```

`SignedValue` 和 `HasSignedValue` 只用于 UI 判断颜色。正数可以显示为增益色，负数可以显示为消耗色。

如果 `BuildingFunctionBlockEntry` 没有提供侧边栏行，功能块会自动 fallback，侧边栏显示汇总后的 `+/-数量名称`。

## 模块 vs 建筑重写

优先使用模块的情况：

- 能力是通用的。
- 多种建筑都可能拥有。
- 数值适合放在检查器中配置。

例如库存格：

```text
BuildingInventorySlotCapacityModule
```

它负责提供：

```text
功能性：+x库存格
```

例如等级升级：

```text
BuildingLevelUpgradeModule
```

它负责提供：

```text
自动升级开关
当前经验
升级所需经验
升级目标预制体
升级消耗
```

`BuildingDetailsBlock_Level` 只读取这个模块并刷新 Toggle、Slider、经验文本、升级按钮和升级消耗文本。升级要替换成哪个 prefab、当前经验是多少、升级消耗什么资源，不由 UI 决定。

建筑每回合 `OnTurn()` 成功后，`BuildingBase.ProcessTurn()` 会统一调用等级模块的自动升级检查。具体建筑如果需要增加经验，应在自己的业务逻辑中获取 `BuildingLevelUpgradeModule` 并调用 `AddExperience()`。

手动点击升级按钮时，等级模块会先检查：

```text
经验是否已满
升级目标 prefab 是否有效
当前格子是否可以替换成目标建筑
库存是否足够支付升级消耗
```

检查通过后，模块会扣除升级消耗，再通过 `BuildingService.TryReplace()` 替换建筑。

优先由具体建筑重写的情况：

- 信息只属于该建筑。
- 侧边栏文本需要体现该建筑的特殊语义。
- 数据来自该建筑自己的运行时字段。

例如 `ResidentialHousingLV1`：

```text
顶部：资源：-x蔬菜
侧边栏：每人口消耗：x蔬菜
```

这不是 UI 判断“居民房”后拼出来的，而是 `ResidentialHousingLV1.GetFunctionBlockEntries()` 提供的结构化行。

## 当前例子

### `PlayerHomeLV1`

`PlayerHomeLV1` 在脚本中保证自己拥有 `BuildingInventorySlotCapacityModule`，并把库存格设置为 `5`。

当前效果：

```text
概览：仓库 +5格
功能块：功能性：+5库存格
```

因为它没有实现 `IBuildingWorkforceFundingSource`，所以不显示岗位块。

如果库存模块没有提供自定义 `SidebarRows`，功能块侧边栏会 fallback 显示：

```text
+5库存格
```

如果希望所有库存模块都显示更明确的侧边栏，应改 `BuildingInventorySlotCapacityModule.AppendFunctionBlockEntries()`，而不是在 UI 中判断 `PlayerHomeLV1`。

### `ResidentialHousingLV1`

当前效果：

```text
概览：人口 当前/上限
功能块：资源：-x蔬菜
侧边栏：每人口消耗：x蔬菜
```

该建筑自己知道“每人口消耗”的语义，所以由建筑提供侧边栏行。

### `LumberCabinLV1`

`LumberCabinLV1` 实现 `IBuildingWorkforceFundingSource`，所以显示岗位块。

它也实现资源产出接口，基础资源产出可通过 `BuildingBase.GetFunctionBlockEntries()` 默认进入功能块。

## HelloWorld 最小示例

下面是一个教学用建筑，展示建筑如何控制详情弹窗显示自定义信息。

重点：

- 建筑不直接操作 UI。
- 建筑只提供结构化数据。
- 详情栏负责显示。

```csharp
using System.Collections.Generic;
using Landsong.BuildingSystem;

public sealed class HelloWorldBuilding : BuildingBase
{
    public override string GetOverviewInfo()
    {
        return "HelloWorld 概览文本";
    }

    public override IReadOnlyList<BuildingFunctionBlockEntry> GetFunctionBlockEntries()
    {
        return new[]
        {
            // 第一条：显示在“功能性”分组。
            // 最终顶部文本会汇总成：功能性：+1测试功能
            new BuildingFunctionBlockEntry(
                BuildingFunctionBlockGroup.功能性, // 参数1：分组。功能性 = 显示在“功能性：...”这一行。
                "测试功能",                        // 参数2：显示名称。这里会和数量拼成“+1测试功能”。
                1,                                 // 参数3：数量。正数代表增加，功能性分组会显示 + 号。
                new[]
                {
                    new BuildingFunctionBlockSidebarRow(
                        "来源",                    // 侧边栏标签。
                        "HelloWorld 建筑"),         // 侧边栏内容。
                    new BuildingFunctionBlockSidebarRow(
                        "说明",
                        "这是自定义侧边栏文本")
                }),

            // 第二条：显示在“资源”分组。
            // 最终顶部文本会汇总成：资源：2苹果
            new BuildingFunctionBlockEntry(
                BuildingFunctionBlockGroup.资源组,  // 参数1：分组。资源组 = 显示在“资源：...”这一行。
                "苹果",                            // 参数2：显示名称。这里会和数量拼成“2苹果”。
                2,                                  // 参数3：数量。正数代表获得资源，资源组正数默认不显示 + 号。
                new[]
                {
                    new BuildingFunctionBlockSidebarRow(
                        "资源来源",
                        "HelloWorld 建筑"),
                    new BuildingFunctionBlockSidebarRow(
                        "每回合获得",
                        "+2苹果",
                        2,
                        true)
                })
        };
    }

    protected override void OnRegistered()
    {
    }

    protected override void OnPlaced()
    {
    }

    protected override bool OnTurn()
    {
        return true;
    }

    protected override void OnDemolished()
    {
    }
}
```

这个例子的当前可见效果：

```text
标题区描述：HelloWorld 概览文本
功能块：功能性：+1测试功能
资源块：资源：2苹果
侧边栏：
来源：HelloWorld 建筑
说明：这是自定义侧边栏文本
资源来源：HelloWorld 建筑
每回合获得：+2苹果
```

这个例子没有 `GetDetailInfo()`。当前详情栏的详细内容由“岗位块”“功能块”和侧边栏承载；如果要继续增加详细内容，应优先新增详情块或为现有功能项增加 `BuildingFunctionBlockSidebarRow`。

## 给侧边栏多加一行

如果只想给某条功能项添加一行说明，在 `BuildingFunctionBlockEntry` 的最后一个参数里追加 `BuildingFunctionBlockSidebarRow`。

例如：

```csharp
new BuildingFunctionBlockEntry(
    BuildingFunctionBlockGroup.功能性,
    "库存格",
    5,
    new[]
    {
        new BuildingFunctionBlockSidebarRow("仓库容量", "+5格", 5, true),
        new BuildingFunctionBlockSidebarRow("来源", "玩家住所"),
        new BuildingFunctionBlockSidebarRow("说明", "提供基础物资存放空间")
    })
```

侧边栏显示：

```text
仓库容量：+5格
来源：玩家住所
说明：提供基础物资存放空间
```

## 新增详情块流程

如果以后要新增人口块、税收块、维护块，建议按以下流程：

1. 新建独立 UI 脚本，例如 `BuildingDetailsBlock_Population`。
2. 挂到 prefab 中对应的 `info_人口` 对象。
3. 在脚本里实现 `CanShow(BuildingBase building)`。
4. 能力来自建筑接口或模块，不要在 UI 中写建筑类型判断。
5. 需要侧边栏时，通过 `Popup_BuildingDetails.ShowDetailSidebar()` 显示。
6. 根弹窗只负责解析、初始化、调度子块。

不要把所有字段和逻辑继续塞回 `Popup_BuildingDetails`。根弹窗应保持薄，只做公共生命周期和侧边栏调度。

## 常见错误

不要让 UI 判断具体建筑：

```csharp
if (building is ResidentialHousingLV1)
{
    text = "每人口消耗";
}
```

应该由建筑提供：

```csharp
new BuildingFunctionBlockSidebarRow("每人口消耗", "1蔬菜", -1, true)
```

不要让建筑直接操作详情栏控件：

```csharp
popup.txt_说明.text = "...";
```

应该由建筑返回结构化数据，详情栏自己显示。

不要把通用能力写成具体建筑特例。

例如库存格是通用能力，应优先放在 `BuildingInventorySlotCapacityModule`；只有“玩家住所来源说明”这种建筑语义，才适合由 `PlayerHomeLV1` 自己提供。

## 一句话总结

建筑决定“有什么”，UI 决定“怎么显示”。
