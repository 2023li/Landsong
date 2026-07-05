# 详细创建建筑教程：捕鱼小屋

本文面向刚接手项目的开发者，目标是从零创建一个完整建筑，并说明每一步为什么这样做。

示例建筑：

- `捕鱼小屋`
  - 占地 `2 * 2`
  - 最多容纳 `3` 名工人
  - `2` 工人：每回合产出 `1` 鱼
  - `3` 工人：每回合产出 `2` 鱼
  - 工人小于基础产出需求，也就是少于 `2` 工人时显示警告
  - 每次成功产出后升级经验 `exp + 1`
  - `exp >= 20` 且科技已解锁“测试科技”时允许升级
  - 升级消耗 `30` 金币
  - 升级为 `高级捕鱼屋`

- `高级捕鱼屋`
  - 占地 `2 * 2`
  - 最多容纳 `5` 名工人
  - 不能继续升级
  - `2` 工人：每回合产出 `1` 鱼
  - `3` 工人：每回合产出 `2` 鱼
  - `5` 工人：每回合产出 `4` 鱼
  - `5` 工人时有 `1%` 几率额外捕获 `黄金鱼`

## 先做架构判断

不要为 `捕鱼小屋` 和 `高级捕鱼屋` 写两份重复脚本。

推荐做法：

- 创建一个脚本：`FishingHutBuilding`
- 普通捕鱼小屋 prefab 和高级捕鱼屋 prefab 都挂这个脚本
- 两个 prefab 用不同序列化参数表达差异
- 普通捕鱼小屋 prefab 额外配置 `BuildingLevelUpgradeModule`
- 高级捕鱼屋 prefab 不配置升级模块

这样做的理由：

- 两个建筑的核心生命周期完全相同：工人计算、按工人数产鱼、写入库存、显示状态。
- 差异只是数值：最大工人数、产出档位、是否有黄金鱼、是否能升级。
- 数值差异放 prefab 配置，逻辑差异才拆新脚本。

什么时候才写 `FishingHutLV1` / `FishingHutLV2` 两个脚本：

- 高级捕鱼屋有完全不同的回合流程。
- 高级捕鱼屋需要新的交互 UI 或独立运行时状态。
- 两者升级、存档、产出、状态规则已经无法用参数表达。

本例不满足这些条件，所以使用同一个脚本。

## 需要用到的核心 API

### BuildingDefinition

`BuildingDefinition` 是建筑 prefab 上的静态配置。这里配置：

- `buildingId`
- 显示名
- 图标
- 分类
- 占地尺寸
- 地形要求
- 移动阻力
- 建造成本
- 建造菜单显示条件和可用条件

不要把当前工人、当前经验、上回合产出这些运行时数据放进 `BuildingDefinition`。

### BuildingBase

所有建筑脚本都继承 `BuildingBase`。常用入口：

- `OnInitialized()`：建筑初始化时调用，适合归一化运行时默认值。
- `OnRegistered()`：建筑进入 `GameSystem` 管理时调用。
- `OnPlaced()`：建筑拥有格子位置后调用。
- `OnTurn()`：每回合执行建筑玩法，返回 `true` 表示本回合成功，返回 `false` 表示失败。
- `CaptureBuildingData()`：保存该建筑运行时数据。
- `RestoreBuildingData()`：读取该建筑运行时数据。
- `OnReceiveReplacementState()`：升级替换成新 prefab 时，从旧建筑接收状态。
- `GetRuntimeStatuses()`：向 UI 暴露异常状态，例如工人不足。
- `GetOverviewInfo()`：建筑列表和选中概览里的一行摘要。
- `GetFunctionBlockEntries()`：向详情面板功能区暴露结构化数据。

注意：`BuildingBase.ProcessTurn()` 会在 `OnTurn()` 成功后自动检查 `BuildingLevelUpgradeModule.TryAutoUpgrade()`，所以建筑脚本只需要在成功产出后给升级模块加经验。

### InventoryService

库存从 `GameSystem.Inventory` 进入。

常用 API：

- `TryAddItem(itemId, amount)`：尝试把物品放进库存，成功返回 `true`，库存放不下返回 `false`。
- `CanAddItem(itemId, amount)`：只检查是否能放，不实际放入。
- `TryRemoveItem(itemId, amount)`：尝试扣除物品。
- `HasItem(itemId, amount)`：检查库存数量是否足够。
- `TrySpendBuildingCosts(costs)`：按 `BuildingCost[]` 扣除建筑成本。升级模块内部会用它扣升级成本。

产出资源时优先用 `TryAddItem`，不要直接修改 `Inventory` 内部列表。

### IBuildingResourceProductionSource

建筑实现这个接口后，详情面板和回合统计可以知道它当前预计产出和上回合实际产出。

```csharp
public IReadOnlyList<BuildingResourceChange> CurrentResourceProductions { get; }
public IReadOnlyList<BuildingResourceChange> LastResourceProductions { get; }
```

`CurrentResourceProductions` 表示现在这座建筑预计能产什么。

`LastResourceProductions` 表示上一次成功回合实际产出了什么。

### IBuildingWorkforceFundingSource

建筑实现这个接口后，通用岗位详情 UI 可以显示：

- 当前工人 / 最大岗位
- 稳定工人
- 招募按钮
- 自动补贴满岗位
- 目标稳定工人
- 岗位吸引力说明

本教程的完整脚本会实现这个接口。刚接手项目时不需要先理解所有公式，只需要知道：

- `MaxWorkers` 是最大岗位。
- `CurrentWorkers` 是当前工人数。
- `StableWorkers` 是当前吸引力能稳定留下的工人数。
- `TryRecruitToFull()` 是 UI 点击“招募工人”时调用的入口。

### BuildingLevelUpgradeModule

升级不要写在捕鱼小屋脚本里，使用 `BuildingLevelUpgradeModule`。

普通捕鱼小屋 prefab 上添加这个模块，并配置：

- `自动升级`: `true`
- `当前经验`: `0`
- `升级所需经验`: `20`
- `升级目标预制体`: 高级捕鱼屋 prefab
- `升级条件`: `GameCondition_TechnologyUnlocked`，引用“测试科技”
- `升级消耗`: `金币 * 30`

`BuildingLevelUpgradeModule` 的作用：

- 保存升级经验
- 判断经验是否足够
- 判断科技条件是否满足
- 判断库存是否能支付升级成本
- 支付升级成本
- 调用 `BuildingService.TryReplace(...)` 把旧建筑替换成升级目标 prefab

高级捕鱼屋不能升级，所以高级捕鱼屋 prefab 不要添加 `BuildingLevelUpgradeModule`。

### 科技点和科技解锁

本例只需要“测试科技已解锁”作为升级条件。

如果一个建筑要提供科技点，不要在建筑里直接调用 `Technology.ApplyResearchPoints(...)`。正确做法是：

1. 给建筑添加 `BuildingTechnologyPointModule`。
2. 配置 `提供科技点/回合`。
3. 回合系统会在建筑本回合成功后收集科技点。
4. `GameSystem` 会把本回合收集到的科技点应用到当前研究。

本例捕鱼小屋不提供科技点，所以不需要添加这个模块。

## 第 1 步：准备资源和科技数据

先在库存和科技数据里准备这些对象：

- 物品：`鱼`
- 物品：`黄金鱼`
- 物品：`金币`
- 科技：`测试科技`

`鱼`、`黄金鱼`、`金币` 的 `itemId` 要和脚本序列化字段一致。教程脚本默认使用中文 ID：

```text
鱼
黄金鱼
金币
```

如果项目里的物品 ID 使用英文，例如 `fish`、`gold_fish`、`gold`，就在 prefab 上把字段改成对应 ID。

## 第 2 步：创建脚本文件

创建文件：

```text
Assets/Landsong/Scripts/BuildingSystem/Buildings/FishingHutBuilding.cs
```

脚本职责：

- 接入建筑生命周期。
- 接入岗位 UI。
- 根据工人数计算产出。
- 把鱼和黄金鱼放入库存。
- 工人不足时输出运行状态。
- 成功产出后给升级模块加经验。
- 保存和恢复当前工人、岗位补贴设置等运行时数据。

## 第 3 步：创建普通捕鱼小屋 prefab

创建 `捕鱼小屋` prefab，根节点挂 `FishingHutBuilding`。

`BuildingBase.definition` 建议配置：

- `buildingId`: `building.fishing_hut`
- `displayName`: `捕鱼小屋`
- `size`: `(2, 2)`
- `movementResistance`: `0`
- `requiredTerrainKeys`: 按地图规则配置。没有特殊规则时可先填 `land`。
- `placementCosts`: 建造成本按设计配置。

`FishingHutBuilding` 字段建议配置：

- `最大岗位`: `3`
- `基础产出最低工人`: `2`
- `鱼物品ID`: `鱼`
- `金币物品ID`: `金币`
- `产出档位`:
  - `最低工人数 = 2`，`鱼产量 = 1`
  - `最低工人数 = 3`，`鱼产量 = 2`
- `启用特殊捕获`: `false`

然后在 `建筑模块` 列表里添加 `BuildingLevelUpgradeModule`：

- `自动升级`: `true`
- `升级所需经验`: `20`
- `升级目标预制体`: 高级捕鱼屋 prefab
- `升级条件`: 新增 `GameCondition_TechnologyUnlocked`，引用“测试科技”
- `升级消耗`: `金币 * 30`

## 第 4 步：创建高级捕鱼屋 prefab

复制普通捕鱼小屋 prefab 或新建 prefab，仍然挂 `FishingHutBuilding`。

`BuildingBase.definition` 建议配置：

- `buildingId`: `building.advanced_fishing_hut`
- `displayName`: `高级捕鱼屋`
- `size`: `(2, 2)`
- 其他放置规则按设计配置

`FishingHutBuilding` 字段建议配置：

- `最大岗位`: `5`
- `基础产出最低工人`: `2`
- `鱼物品ID`: `鱼`
- `黄金鱼物品ID`: `黄金鱼`
- `金币物品ID`: `金币`
- `产出档位`:
  - `最低工人数 = 2`，`鱼产量 = 1`
  - `最低工人数 = 3`，`鱼产量 = 2`
  - `最低工人数 = 5`，`鱼产量 = 4`
- `启用特殊捕获`: `true`
- `特殊捕获最低工人`: `5`
- `特殊捕获几率`: `1`
- `特殊捕获数量`: `1`

高级捕鱼屋不能升级，所以不要添加 `BuildingLevelUpgradeModule`。

## 第 5 步：加入 BuildingCatalog

把两个 prefab 都加入 `BuildingCatalog`：

- `捕鱼小屋`
- `高级捕鱼屋`

只有加入目录后，建造菜单和放置流程才能找到 prefab。

如果高级捕鱼屋只允许通过升级获得，可以通过建造菜单显示条件让它不显示，或者不在普通建造菜单里暴露入口。是否加入目录取决于当前菜单系统和升级替换查找方式；升级模块直接引用 prefab 时，不依赖玩家能在菜单里点到它。

## 第 6 步：完整脚本

下面是完整示例脚本。代码中的注释重点解释项目 API 的作用。

```csharp
using System;
using System.Collections.Generic;
using Landsong.BuildingSystem;
using Landsong.InventorySystem;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

public sealed class FishingHutBuilding :
    BuildingBase,
    IBuildingWorkforceFundingSource,
    IBuildingResourceProductionSource
{
    private const string DefaultFishItemId = "鱼";
    private const string DefaultGoldFishItemId = "黄金鱼";
    private const string DefaultGoldItemId = "金币";

    private const string StatusMissingInventory = BuildingRuntimeStatusCatalog.BS_库存缺失;
    private const string StatusInsufficientWorkers = BuildingRuntimeStatusCatalog.BS_工人不足;
    private const string StatusWorkerShortage = BuildingRuntimeStatusCatalog.BS_缺工;
    private const string StatusRecruitGoldMissing = BuildingRuntimeStatusCatalog.BS_招工金币不足;
    private const string StatusSubsidyGoldMissing = BuildingRuntimeStatusCatalog.BS_补贴金币不足;

    private const string StatusInvalidFishItem = "invalid_fish_item";
    private const string StatusFishStorageFailed = "fish_storage_failed";
    private const string StatusInvalidSpecialCatchItem = "invalid_special_catch_item";
    private const string StatusSpecialCatchStorageFailed = "special_catch_storage_failed";

    private static readonly IReadOnlyList<BuildingJobAttractionModifier> EmptyAttractionModifiers =
        Array.Empty<BuildingJobAttractionModifier>();

    [Serializable]
    private struct WorkerProductionTier
    {
        [SerializeField, FormerlySerializedAs("minimumWorkers")]
        [LabelText("最低工人数"), HideLabel, Min(1), TableColumnWidth(110, Resizable = false)]
        private int 最低工人数;

        [SerializeField, FormerlySerializedAs("fishAmount")]
        [LabelText("鱼产量"), HideLabel, Min(0), TableColumnWidth(80, Resizable = false)]
        private int 鱼产量;

        public WorkerProductionTier(int minimumWorkers, int fishAmount)
        {
            最低工人数 = minimumWorkers;
            鱼产量 = fishAmount;
        }

        public int MinimumWorkers => Mathf.Max(1, 最低工人数);
        public int FishAmount => Mathf.Max(0, 鱼产量);
        public bool IsValid => FishAmount > 0;

        public WorkerProductionTier Normalize(int maxWorkers)
        {
            return new WorkerProductionTier(
                Mathf.Clamp(最低工人数, 1, Mathf.Max(1, maxWorkers)),
                Mathf.Max(0, 鱼产量));
        }
    }

    [TitleGroup("资源")]
    [SerializeField, LabelText("鱼物品ID")]
    private string fishItemId = DefaultFishItemId;

    [TitleGroup("资源")]
    [SerializeField, LabelText("黄金鱼物品ID")]
    private string goldFishItemId = DefaultGoldFishItemId;

    [TitleGroup("资源")]
    [SerializeField, LabelText("金币物品ID")]
    private string goldItemId = DefaultGoldItemId;

    [TitleGroup("岗位")]
    [SerializeField, LabelText("最大岗位"), Min(1)]
    private int maxWorkers = 3;

    [TitleGroup("岗位")]
    [SerializeField, LabelText("建造完成初始工人"), Min(0)]
    private int initialWorkersOnPlaced;

    [TitleGroup("岗位")]
    [SerializeField, LabelText("基础吸引力"), Min(0f)]
    private float baseJobAttraction = 55f;

    [TitleGroup("岗位")]
    [SerializeField, LabelText("单人招工费用"), Min(0)]
    private int singleRecruitCost = 10;

    [TitleGroup("岗位补贴")]
    [SerializeField, LabelText("自动补贴满岗位")]
    private bool autoFullWorkerSubsidyEnabled;

    [TitleGroup("岗位补贴")]
    [SerializeField, LabelText("目标稳定工人"), Min(0)]
    private int targetStableWorkers;

    [TitleGroup("产出")]
    [SerializeField, LabelText("基础产出最低工人"), Min(1)]
    private int minimumWorkersForProduction = 2;

    [TitleGroup("产出")]
    [SerializeField, LabelText("工人数产量表")]
    [TableList(AlwaysExpanded = true, DrawScrollView = false)]
    private WorkerProductionTier[] productionTiers =
    {
        new WorkerProductionTier(2, 1),
        new WorkerProductionTier(3, 2)
    };

    [TitleGroup("特殊捕获")]
    [SerializeField, LabelText("启用特殊捕获")]
    private bool enableSpecialCatch;

    [TitleGroup("特殊捕获")]
    [SerializeField, LabelText("特殊捕获最低工人"), Min(1)]
    private int specialCatchMinimumWorkers = 5;

    [TitleGroup("特殊捕获")]
    [SerializeField, LabelText("特殊捕获几率%"), Range(0f, 100f)]
    private float specialCatchChancePercent = 1f;

    [TitleGroup("特殊捕获")]
    [SerializeField, LabelText("特殊捕获数量"), Min(0)]
    private int specialCatchAmount = 1;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly]
    private int currentWorkers;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly]
    private int stableWorkers;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly]
    private int stableWorkersWithoutSubsidy;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly]
    private float rawJobAttraction;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly]
    private float jobAttraction;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly]
    private float jobAttractionWithoutSubsidy;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly]
    private int targetSubsidyGoldPerTurn;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly]
    private int paidSubsidyGoldThisTurn;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly]
    private int lastProducedFish;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly]
    private int lastProducedGoldFish;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly]
    private bool initialWorkersGranted;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly]
    private bool lastTurnRecruitedWorker;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly]
    private bool lastTurnSubsidyGoldMissing;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly]
    private bool lastTurnNoAvailablePopulation;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly]
    private bool lastTurnCaughtSpecial;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly]
    private string lastAbnormalStatusId = string.Empty;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly]
    private string lastAbnormalStatusText = string.Empty;

    private IReadOnlyList<BuildingResourceChange> lastResourceProductions = EmptyResourceChanges;
    private readonly List<BuildingWorkforceAttractionFactor> workforceAttractionFactors =
        new List<BuildingWorkforceAttractionFactor>();

    public int CurrentWorkers => currentWorkers;
    public int MaxWorkers => maxWorkers;
    public int StableWorkers => stableWorkers;
    public float RawJobAttraction => rawJobAttraction;
    public float JobAttraction => jobAttraction;

    public bool AutoFullWorkerSubsidyEnabled => autoFullWorkerSubsidyEnabled;
    public int TargetStableWorkers => targetStableWorkers;
    public int TargetSubsidyGoldPerTurn => targetSubsidyGoldPerTurn;
    public int PaidSubsidyGoldThisTurn => paidSubsidyGoldThisTurn;
    public int MissingWorkersToFull => Mathf.Max(0, maxWorkers - currentWorkers);
    public int RecruitToFullWorkerCount => CalculateRecruitToFullWorkerCount();
    public int RecruitToFullCost => CalculateImmediateRecruitCost(RecruitToFullWorkerCount);
    public bool CanRecruitToFull => RecruitToFullWorkerCount > 0 && CanPayGold(RecruitToFullCost);
    public float JobAttractionWithoutSubsidy => jobAttractionWithoutSubsidy;
    public float SubsidyAttractionPerGold => BuildingJobSystem.CalculateSubsidyAttractionPerGold(maxWorkers);
    public float SubsidyAttractionBonus => Mathf.Max(0, paidSubsidyGoldThisTurn) * SubsidyAttractionPerGold;
    public float TargetSubsidyAttractionBonus => Mathf.Max(0, targetSubsidyGoldPerTurn) * SubsidyAttractionPerGold;
    public float PreviewJobAttractionWithTargetSubsidy =>
        BuildingJobSystem.CalculateAttractionWithSubsidy(
            jobAttractionWithoutSubsidy,
            targetSubsidyGoldPerTurn,
            maxWorkers);
    public float FullWorkerRequiredAttraction => BuildingJobSystem.CalculateFullWorkerRequiredAttraction(maxWorkers);
    public float JobAttractionGapToFullWorkers =>
        Mathf.Max(0f, FullWorkerRequiredAttraction - jobAttractionWithoutSubsidy);
    public IReadOnlyList<BuildingWorkforceAttractionFactor> WorkforceAttractionFactors =>
        workforceAttractionFactors;

    public IReadOnlyList<BuildingResourceChange> CurrentResourceProductions =>
        CreateResourceChangeList(fishItemId, GetProductionAmountForWorkers(currentWorkers));
    public IReadOnlyList<BuildingResourceChange> LastResourceProductions => lastResourceProductions;

    protected override void Awake()
    {
        base.Awake();
        NormalizeConfiguration();
        RecalculateWorkforce();
    }

    protected override void OnInitialized()
    {
        NormalizeConfiguration();
        RecalculateWorkforce();
    }

    protected override void OnRegistered()
    {
        RecalculateWorkforce();
        RefreshDynastyEmployedPopulation();
    }

    protected override void OnPlaced()
    {
        TryGrantInitialWorkersOnPlaced();
        RefreshDynastyEmployedPopulation();
    }

    protected override bool OnTurn()
    {
        NormalizeConfiguration();
        ClearLastTurnState();

        RecalculateWorkforceWithoutSubsidy();
        TryPaySubsidyForCurrentTurn();
        RecalculateWorkforce();
        ProcessWorkerTurn();

        if (currentWorkers < minimumWorkersForProduction)
        {
            SetLastAbnormalStatus(StatusInsufficientWorkers, "工人不足");
            RefreshDynastyEmployedPopulation();
            return false;
        }

        var inventory = GameSystem == null ? null : GameSystem.Inventory;
        if (!TryProduceFish(inventory))
        {
            RefreshDynastyEmployedPopulation();
            return false;
        }

        AddUpgradeExperience();
        RefreshDynastyEmployedPopulation();
        return true;
    }

    protected override BuildingDataBase CaptureBuildingData()
    {
        return new FishingHutData
        {
            CurrentWorkers = currentWorkers,
            AutoFullWorkerSubsidyEnabled = autoFullWorkerSubsidyEnabled,
            TargetStableWorkers = targetStableWorkers,
            InitialWorkersGranted = initialWorkersGranted,
            LastTurnSubsidyGoldMissing = lastTurnSubsidyGoldMissing,
            LastTurnNoAvailablePopulation = lastTurnNoAvailablePopulation,
            LastTurnCaughtSpecial = lastTurnCaughtSpecial,
            LastAbnormalStatusId = lastAbnormalStatusId,
            LastAbnormalStatusText = lastAbnormalStatusText
        };
    }

    protected override void RestoreBuildingData(BuildingDataBase data)
    {
        if (data is not FishingHutData fishingData)
        {
            return;
        }

        currentWorkers = fishingData.CurrentWorkers;
        autoFullWorkerSubsidyEnabled = fishingData.AutoFullWorkerSubsidyEnabled;
        targetStableWorkers = fishingData.TargetStableWorkers;
        initialWorkersGranted = fishingData.InitialWorkersGranted;
        lastTurnSubsidyGoldMissing = fishingData.LastTurnSubsidyGoldMissing;
        lastTurnNoAvailablePopulation = fishingData.LastTurnNoAvailablePopulation;
        lastTurnCaughtSpecial = fishingData.LastTurnCaughtSpecial;
        lastAbnormalStatusId = fishingData.LastAbnormalStatusId;
        lastAbnormalStatusText = fishingData.LastAbnormalStatusText;

        NormalizeConfiguration();
        RecalculateWorkforce();
        RefreshDynastyEmployedPopulation();
    }

    protected override void OnReceiveReplacementState(BuildingBase sourceBuilding)
    {
        if (sourceBuilding is not FishingHutBuilding sourceFishingHut)
        {
            return;
        }

        currentWorkers = Mathf.Clamp(sourceFishingHut.currentWorkers, 0, maxWorkers);
        autoFullWorkerSubsidyEnabled = sourceFishingHut.autoFullWorkerSubsidyEnabled;
        targetStableWorkers = Mathf.Clamp(sourceFishingHut.targetStableWorkers, 0, maxWorkers);
        initialWorkersGranted = true;

        NormalizeConfiguration();
        RecalculateWorkforce();
        RefreshDynastyEmployedPopulation();
    }

    protected override void OnUnregistered()
    {
        currentWorkers = 0;
        RefreshDynastyEmployedPopulation();
    }

    public override string GetOverviewInfo()
    {
        var currentProduction = GetProductionAmountForWorkers(currentWorkers);
        return $"工人 {currentWorkers}/{maxWorkers}，鱼 +{currentProduction}/回合";
    }

    public override IReadOnlyList<BuildingFunctionBlockEntry> GetFunctionBlockEntries()
    {
        List<BuildingFunctionBlockEntry> entries = null;
        var currentProduction = GetProductionAmountForWorkers(currentWorkers);
        if (currentProduction > 0)
        {
            AddFunctionBlockEntry(
                ref entries,
                new BuildingFunctionBlockEntry(
                    BuildingFunctionBlockGroup.资源组,
                    fishItemId,
                    currentProduction,
                    new BuildingFunctionBlockSidebarRow(
                        "当前工人产出",
                        $"{currentWorkers}工人 = {currentProduction}{fishItemId}")));
        }

        AddFunctionBlockEntry(
            ref entries,
            new BuildingFunctionBlockEntry(
                BuildingFunctionBlockGroup.功能性,
                "最低生产工人",
                minimumWorkersForProduction));

        if (enableSpecialCatch && specialCatchChancePercent > 0f && specialCatchAmount > 0)
        {
            AddFunctionBlockEntry(
                ref entries,
                new BuildingFunctionBlockEntry(
                    BuildingFunctionBlockGroup.功能性,
                    goldFishItemId,
                    specialCatchAmount,
                    new BuildingFunctionBlockSidebarRow(
                        "特殊捕获",
                        $"{specialCatchMinimumWorkers}工人时 {specialCatchChancePercent:0.##}%")));
        }

        AppendBuildingModuleFunctionBlockEntries(ref entries);
        return entries ?? EmptyFunctionBlockEntries;
    }

    public override IReadOnlyList<BuildingRuntimeStatus> GetRuntimeStatuses()
    {
        List<BuildingRuntimeStatus> statuses = null;

        AppendRuntimeStatus(
            ref statuses,
            currentWorkers < minimumWorkersForProduction
                ? new BuildingRuntimeStatus(
                    StatusInsufficientWorkers,
                    "工人不足",
                    currentWorkers,
                    minimumWorkersForProduction)
                : default);

        AppendRuntimeStatus(
            ref statuses,
            currentWorkers >= minimumWorkersForProduction && currentWorkers < stableWorkers
                ? new BuildingRuntimeStatus(
                    StatusWorkerShortage,
                    "缺工",
                    currentWorkers,
                    stableWorkers)
                : default);

        AppendRuntimeStatus(
            ref statuses,
            ShouldAddLastAbnormalStatus()
                ? new BuildingRuntimeStatus(lastAbnormalStatusId, lastAbnormalStatusText)
                : default);

        AppendCommonRuntimeStatuses(ref statuses);
        return statuses ?? EmptyRuntimeStatuses;
    }

    public void SetAutoFullWorkerSubsidyEnabled(bool enabled)
    {
        autoFullWorkerSubsidyEnabled = enabled;
        if (autoFullWorkerSubsidyEnabled)
        {
            targetStableWorkers = maxWorkers;
        }

        RecalculateWorkforceWithoutSubsidy();
        NotifyStateChanged();
    }

    public void SetTargetStableWorkers(int targetStableWorkers)
    {
        autoFullWorkerSubsidyEnabled = false;
        this.targetStableWorkers = Mathf.Clamp(targetStableWorkers, stableWorkersWithoutSubsidy, maxWorkers);
        RecalculateWorkforceWithoutSubsidy();
        NotifyStateChanged();
    }

    public bool TryRecruitToFull()
    {
        var recruitCount = RecruitToFullWorkerCount;
        if (recruitCount <= 0)
        {
            return false;
        }

        var recruitCost = CalculateImmediateRecruitCost(recruitCount);
        if (!TryPayGold(recruitCost))
        {
            SetLastAbnormalStatus(StatusRecruitGoldMissing, "招工金币不足");
            NotifyStateChanged();
            return false;
        }

        currentWorkers = Mathf.Clamp(currentWorkers + recruitCount, 0, maxWorkers);
        lastTurnRecruitedWorker = true;
        RecalculateWorkforce();
        RefreshDynastyEmployedPopulation();
        NotifyStateChanged();
        return true;
    }

    private void TryGrantInitialWorkersOnPlaced()
    {
        if (initialWorkersGranted)
        {
            return;
        }

        initialWorkersGranted = true;
        var workersToGrant = Mathf.Clamp(initialWorkersOnPlaced, 0, maxWorkers - currentWorkers);
        if (workersToGrant <= 0)
        {
            return;
        }

        var availablePopulation = GetAvailablePopulation();
        if (availablePopulation <= 0)
        {
            lastTurnNoAvailablePopulation = true;
            return;
        }

        workersToGrant = Mathf.Min(workersToGrant, availablePopulation);
        currentWorkers = Mathf.Clamp(currentWorkers + workersToGrant, 0, maxWorkers);
        lastTurnRecruitedWorker = workersToGrant > 0;
        RecalculateWorkforce();
    }

    private void ProcessWorkerTurn()
    {
        if (currentWorkers < stableWorkers)
        {
            if (GetAvailablePopulation() <= 0)
            {
                lastTurnNoAvailablePopulation = true;
                return;
            }

            currentWorkers = Mathf.Min(maxWorkers, currentWorkers + 1);
            lastTurnRecruitedWorker = true;
            RecalculateWorkforce();
            return;
        }

        if (currentWorkers > stableWorkers)
        {
            currentWorkers = Mathf.Max(0, currentWorkers - 1);
            RecalculateWorkforce();
        }
    }

    private void RecalculateWorkforceWithoutSubsidy()
    {
        var calculation = CalculateJob(0);
        stableWorkersWithoutSubsidy = calculation.StableWorkers;
        jobAttractionWithoutSubsidy = calculation.Attraction;

        NormalizeTargetStableWorkers();
        targetSubsidyGoldPerTurn =
            BuildingJobSystem.CalculateRequiredSubsidyGoldForTargetStableWorkers(
                maxWorkers,
                jobAttractionWithoutSubsidy,
                targetStableWorkers);
    }

    private void RecalculateWorkforce()
    {
        RecalculateWorkforceWithoutSubsidy();
        var calculation = CalculateJob(paidSubsidyGoldThisTurn);
        stableWorkers = calculation.StableWorkers;
        rawJobAttraction = calculation.RawAttraction;
        jobAttraction = calculation.Attraction;
        BuildWorkforceAttractionFactors();
    }

    private BuildingJobCalculation CalculateJob(int paidSubsidyGold)
    {
        return BuildingJobSystem.Calculate(
            new BuildingJobCalculationInput(
                maxWorkers,
                currentWorkers,
                baseJobAttraction,
                0,
                0,
                0f,
                EmptyAttractionModifiers,
                Mathf.Max(0, paidSubsidyGold) * SubsidyAttractionPerGold,
                singleRecruitCost));
    }

    private void NormalizeTargetStableWorkers()
    {
        targetStableWorkers = autoFullWorkerSubsidyEnabled
            ? maxWorkers
            : Mathf.Clamp(targetStableWorkers, stableWorkersWithoutSubsidy, maxWorkers);
    }

    private void TryPaySubsidyForCurrentTurn()
    {
        paidSubsidyGoldThisTurn = 0;
        if (targetSubsidyGoldPerTurn <= 0)
        {
            return;
        }

        if (TryPayGold(targetSubsidyGoldPerTurn))
        {
            paidSubsidyGoldThisTurn = targetSubsidyGoldPerTurn;
            return;
        }

        lastTurnSubsidyGoldMissing = true;
        SetLastAbnormalStatus(StatusSubsidyGoldMissing, "补贴金币不足");
    }

    private bool TryProduceFish(InventoryService inventory)
    {
        if (inventory == null)
        {
            SetLastAbnormalStatus(StatusMissingInventory, "库存服务缺失");
            return false;
        }

        if (!HasUsableItemId(fishItemId))
        {
            SetLastAbnormalStatus(StatusInvalidFishItem, "鱼物品配置异常");
            return false;
        }

        var fishAmount = GetProductionAmountForWorkers(currentWorkers);
        if (fishAmount <= 0)
        {
            SetLastAbnormalStatus(StatusInsufficientWorkers, "工人不足");
            return false;
        }

        if (!inventory.TryAddItem(fishItemId, fishAmount))
        {
            SetLastAbnormalStatus(StatusFishStorageFailed, "鱼存入失败");
            return false;
        }

        lastProducedFish = fishAmount;

        if (ShouldCatchSpecialFish())
        {
            TryProduceSpecialCatch(inventory);
        }

        lastResourceProductions = CreateResourceChangeList(
            fishItemId,
            lastProducedFish,
            goldFishItemId,
            lastProducedGoldFish);

        return true;
    }

    private bool ShouldCatchSpecialFish()
    {
        return enableSpecialCatch
               && currentWorkers >= specialCatchMinimumWorkers
               && specialCatchAmount > 0
               && specialCatchChancePercent > 0f
               && UnityEngine.Random.value < Mathf.Clamp01(specialCatchChancePercent / 100f);
    }

    private void TryProduceSpecialCatch(InventoryService inventory)
    {
        if (!HasUsableItemId(goldFishItemId))
        {
            SetLastAbnormalStatus(StatusInvalidSpecialCatchItem, "黄金鱼物品配置异常");
            return;
        }

        if (!inventory.TryAddItem(goldFishItemId, specialCatchAmount))
        {
            SetLastAbnormalStatus(StatusSpecialCatchStorageFailed, "黄金鱼存入失败");
            return;
        }

        lastProducedGoldFish = specialCatchAmount;
        lastTurnCaughtSpecial = true;
    }

    private void AddUpgradeExperience()
    {
        if (TryGetModule<BuildingLevelUpgradeModule>(out var levelModule))
        {
            levelModule.AddExperience(1);
        }
    }

    private int GetProductionAmountForWorkers(int workers)
    {
        if (productionTiers == null || productionTiers.Length == 0)
        {
            return 0;
        }

        var selectedMinimumWorkers = 0;
        var selectedAmount = 0;
        workers = Mathf.Max(0, workers);
        for (var i = 0; i < productionTiers.Length; i++)
        {
            var tier = productionTiers[i].Normalize(maxWorkers);
            if (!tier.IsValid || workers < tier.MinimumWorkers)
            {
                continue;
            }

            if (tier.MinimumWorkers > selectedMinimumWorkers)
            {
                selectedMinimumWorkers = tier.MinimumWorkers;
                selectedAmount = tier.FishAmount;
                continue;
            }

            if (tier.MinimumWorkers == selectedMinimumWorkers && tier.FishAmount > selectedAmount)
            {
                selectedAmount = tier.FishAmount;
            }
        }

        return selectedAmount;
    }

    private int CalculateRecruitToFullWorkerCount()
    {
        if (currentWorkers >= maxWorkers || stableWorkers < maxWorkers)
        {
            return 0;
        }

        return Mathf.Max(0, maxWorkers - currentWorkers);
    }

    private int CalculateImmediateRecruitCost(int recruitCount)
    {
        return Mathf.Max(0, recruitCount) * Mathf.Max(0, singleRecruitCost);
    }

    private bool CanPayGold(int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        var inventory = GameSystem == null ? null : GameSystem.Inventory;
        return inventory != null
               && HasUsableItemId(goldItemId)
               && inventory.HasItem(goldItemId, amount);
    }

    private bool TryPayGold(int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        var inventory = GameSystem == null ? null : GameSystem.Inventory;
        return inventory != null
               && HasUsableItemId(goldItemId)
               && inventory.TryRemoveItem(goldItemId, amount);
    }

    private int GetAvailablePopulation()
    {
        var buildings = GameSystem == null || GameSystem.Buildings == null
            ? null
            : GameSystem.Buildings.Buildings;
        return BuildingJobSystem.GetAvailablePopulation(GameSystem, buildings);
    }

    private void RefreshDynastyEmployedPopulation()
    {
        if (GameSystem == null || GameSystem.Dynasty == null || GameSystem.Buildings == null)
        {
            return;
        }

        GameSystem.Dynasty.SetEmployedPopulation(
            BuildingJobSystem.CountCurrentWorkers(GameSystem.Buildings.Buildings));
    }

    private void BuildWorkforceAttractionFactors()
    {
        workforceAttractionFactors.Clear();
        AddWorkforceAttractionFactor("基础吸引力", baseJobAttraction);

        if (paidSubsidyGoldThisTurn > 0)
        {
            AddWorkforceAttractionFactor("本回合补贴", SubsidyAttractionBonus);
        }
    }

    private void AddWorkforceAttractionFactor(string label, float value)
    {
        var factor = new BuildingWorkforceAttractionFactor(label, value);
        if (factor.IsValid)
        {
            workforceAttractionFactors.Add(factor);
        }
    }

    private void ClearLastTurnState()
    {
        paidSubsidyGoldThisTurn = 0;
        lastProducedFish = 0;
        lastProducedGoldFish = 0;
        lastTurnRecruitedWorker = false;
        lastTurnSubsidyGoldMissing = false;
        lastTurnNoAvailablePopulation = false;
        lastTurnCaughtSpecial = false;
        lastResourceProductions = EmptyResourceChanges;
        lastAbnormalStatusId = string.Empty;
        lastAbnormalStatusText = string.Empty;
    }

    private bool ShouldAddLastAbnormalStatus()
    {
        return !string.IsNullOrWhiteSpace(lastAbnormalStatusId)
               && !string.Equals(lastAbnormalStatusId, StatusInsufficientWorkers, StringComparison.Ordinal)
               && !string.Equals(lastAbnormalStatusId, StatusWorkerShortage, StringComparison.Ordinal);
    }

    private void SetLastAbnormalStatus(string statusId, string statusText)
    {
        lastAbnormalStatusId = string.IsNullOrWhiteSpace(statusId) ? string.Empty : statusId.Trim();
        lastAbnormalStatusText = string.IsNullOrWhiteSpace(statusText) ? lastAbnormalStatusId : statusText.Trim();
    }

    private void NormalizeConfiguration()
    {
        maxWorkers = Mathf.Max(1, maxWorkers);
        initialWorkersOnPlaced = Mathf.Clamp(initialWorkersOnPlaced, 0, maxWorkers);
        currentWorkers = Mathf.Clamp(currentWorkers, 0, maxWorkers);
        stableWorkers = Mathf.Clamp(stableWorkers, 0, maxWorkers);
        stableWorkersWithoutSubsidy = Mathf.Clamp(stableWorkersWithoutSubsidy, 0, maxWorkers);
        baseJobAttraction = Mathf.Max(0f, baseJobAttraction);
        singleRecruitCost = Mathf.Max(0, singleRecruitCost);
        minimumWorkersForProduction = Mathf.Clamp(minimumWorkersForProduction, 1, maxWorkers);
        specialCatchMinimumWorkers = Mathf.Clamp(specialCatchMinimumWorkers, 1, maxWorkers);
        specialCatchChancePercent = Mathf.Clamp(specialCatchChancePercent, 0f, 100f);
        specialCatchAmount = Mathf.Max(0, specialCatchAmount);
        targetStableWorkers = Mathf.Clamp(targetStableWorkers, 0, maxWorkers);
        fishItemId = NormalizeItemId(fishItemId, DefaultFishItemId);
        goldFishItemId = NormalizeItemId(goldFishItemId, DefaultGoldFishItemId);
        goldItemId = NormalizeItemId(goldItemId, DefaultGoldItemId);

        if (productionTiers == null || productionTiers.Length == 0)
        {
            productionTiers = new[]
            {
                new WorkerProductionTier(2, 1),
                new WorkerProductionTier(Mathf.Min(3, maxWorkers), 2)
            };
        }

        for (var i = 0; i < productionTiers.Length; i++)
        {
            productionTiers[i] = productionTiers[i].Normalize(maxWorkers);
        }
    }

    private static IReadOnlyList<BuildingResourceChange> CreateResourceChangeList(string itemId, int amount)
    {
        var change = new BuildingResourceChange(itemId, amount);
        return change.IsValid ? new[] { change } : EmptyResourceChanges;
    }

    private static IReadOnlyList<BuildingResourceChange> CreateResourceChangeList(
        string fishItemId,
        int fishAmount,
        string specialItemId,
        int specialAmount)
    {
        List<BuildingResourceChange> changes = null;
        AddResourceChange(ref changes, fishItemId, fishAmount);
        AddResourceChange(ref changes, specialItemId, specialAmount);
        return changes == null ? EmptyResourceChanges : changes.ToArray();
    }

    private static void AddResourceChange(ref List<BuildingResourceChange> changes, string itemId, int amount)
    {
        var change = new BuildingResourceChange(itemId, amount);
        if (!change.IsValid)
        {
            return;
        }

        changes ??= new List<BuildingResourceChange>();
        changes.Add(change);
    }

    [Serializable]
    [BuildingDataTypeId("building.fishing_hut")]
    private sealed class FishingHutData : BuildingDataBase
    {
        public int CurrentWorkers;
        public bool AutoFullWorkerSubsidyEnabled;
        public int TargetStableWorkers;
        public bool InitialWorkersGranted;
        public bool LastTurnSubsidyGoldMissing;
        public bool LastTurnNoAvailablePopulation;
        public bool LastTurnCaughtSpecial;
        public string LastAbnormalStatusId;
        public string LastAbnormalStatusText;
    }
}
```

## 第 7 步：理解脚本的回合流程

`OnTurn()` 的顺序是有意安排的：

1. `NormalizeConfiguration()`  
   清理非法配置，防止 inspector 填出负数或超过最大岗位。

2. `ClearLastTurnState()`  
   清理上一回合产出、警告和特殊捕获记录。

3. `RecalculateWorkforceWithoutSubsidy()`  
   先计算不补贴时能稳定多少工人，并计算本回合为了目标稳定工人需要付多少补贴。

4. `TryPaySubsidyForCurrentTurn()`  
   通过 `GameSystem.Inventory.TryRemoveItem(goldItemId, amount)` 扣金币。扣不到时不直接崩溃，而是记录“补贴金币不足”状态。

5. `RecalculateWorkforce()`  
   用实际支付的补贴重新计算稳定工人。

6. `ProcessWorkerTurn()`  
   当前工人少于稳定工人时尝试增加 1 个工人；当前工人大于稳定工人时减少 1 个工人。

7. 检查 `currentWorkers < minimumWorkersForProduction`  
   捕鱼小屋基础产出需要 2 工人。少于 2 人时返回 `false`，并通过 `GetRuntimeStatuses()` 显示警告。

8. `TryProduceFish(...)`  
   用 `GameSystem.Inventory.TryAddItem(...)` 把鱼放进库存。高级捕鱼屋满足 5 工人时额外尝试 1% 黄金鱼。

9. `AddUpgradeExperience()`  
   成功产出后，调用 `BuildingLevelUpgradeModule.AddExperience(1)`。如果普通捕鱼小屋经验到 20，且“测试科技”已解锁，且库存有 30 金币，`BuildingBase.ProcessTurn()` 会在 `OnTurn()` 成功后自动执行升级。

## 第 8 步：为什么升级不写在 OnTurn 里

不要在 `FishingHutBuilding.OnTurn()` 里直接写：

```csharp
if (exp >= 20) { 替换成高级捕鱼屋; }
```

原因：

- 项目已经有 `BuildingLevelUpgradeModule` 管理升级。
- 升级模块已经知道如何检查经验、科技条件、消耗、目标 prefab。
- `BuildingBase.ProcessTurn()` 已经在成功回合后自动尝试升级。
- 手写升级会重复扣金币、重复替换、绕过统一存档和 UI。

捕鱼小屋脚本只负责：

```csharp
if (TryGetModule<BuildingLevelUpgradeModule>(out var levelModule))
{
    levelModule.AddExperience(1);
}
```

剩下交给升级模块。

## 第 9 步：如何增加库存、消耗库存、增加科技点

### 增加库存

产出鱼时使用：

```csharp
GameSystem.Inventory.TryAddItem("鱼", 1);
```

`TryAddItem` 会检查库存容量。返回 `false` 表示放不下，建筑应该记录异常状态。

### 消耗库存

如果建筑需要运营消耗，使用：

```csharp
GameSystem.Inventory.TryRemoveItem("金币", 10);
```

如果消耗是一组 `BuildingCost[]`，使用：

```csharp
GameSystem.Inventory.TrySpendBuildingCosts(costs);
```

升级模块内部就是通过 `TrySpendBuildingCosts` 扣 `金币 * 30`。

### 暴露产出给 UI

实现 `IBuildingResourceProductionSource`：

```csharp
public IReadOnlyList<BuildingResourceChange> CurrentResourceProductions { get; }
public IReadOnlyList<BuildingResourceChange> LastResourceProductions { get; }
```

如果建筑不覆写 `GetFunctionBlockEntries()`，`BuildingBase` 的默认实现会读取这些产出。示例脚本覆写了 `GetFunctionBlockEntries()`，所以它在方法里手动加入鱼产出、最低工人和黄金鱼概率。

### 增加科技点

如果未来做“研究所”这类建筑，不要手动调用科技服务。给建筑添加模块：

```csharp
var module = EnsureBuildingModule<BuildingTechnologyPointModule>();
module.SetProvidedTechnologyPointsPerTurn(1);
```

作用：

- `BuildingTechnologyPointModule` 暴露每回合科技点。
- `TurnService` 在建筑成功处理回合后收集科技点。
- `GameSystem` 在回合结束时把收集到的科技点应用到当前研究。

## 第 10 步：存档规则

本脚本的运行时数据类是：

```csharp
[Serializable]
[BuildingDataTypeId("building.fishing_hut")]
private sealed class FishingHutData : BuildingDataBase
{
    public int CurrentWorkers;
    public bool AutoFullWorkerSubsidyEnabled;
    public int TargetStableWorkers;
    public bool InitialWorkersGranted;
}
```

普通捕鱼小屋和高级捕鱼屋共用同一个脚本、同一个数据形状，所以可以共用这个数据类型 ID。

建筑身份不是靠 `BuildingDataTypeId` 区分，而是靠 prefab 上的 `BuildingDefinition.BuildingId`：

- 普通捕鱼小屋：`building.fishing_hut`
- 高级捕鱼屋：`building.advanced_fishing_hut`

升级经验不需要写进 `FishingHutData`。因为 `BuildingBase.CaptureSaveData()` 会自动捕获 `BuildingLevelUpgradeModule` 的经验和自动升级开关。

## 最终检查清单

普通捕鱼小屋 prefab：

- 挂 `FishingHutBuilding`
- `BuildingDefinition.size = (2, 2)`
- `buildingId = building.fishing_hut`
- `maxWorkers = 3`
- 产出档位：`2 -> 1`，`3 -> 2`
- `enableSpecialCatch = false`
- 添加 `BuildingLevelUpgradeModule`
- 升级经验 `20`
- 升级目标是高级捕鱼屋 prefab
- 升级条件引用“测试科技”
- 升级消耗 `金币 * 30`

高级捕鱼屋 prefab：

- 挂 `FishingHutBuilding`
- `BuildingDefinition.size = (2, 2)`
- `buildingId = building.advanced_fishing_hut`
- `maxWorkers = 5`
- 产出档位：`2 -> 1`，`3 -> 2`，`5 -> 4`
- `enableSpecialCatch = true`
- `specialCatchMinimumWorkers = 5`
- `specialCatchChancePercent = 1`
- `specialCatchAmount = 1`
- 不添加 `BuildingLevelUpgradeModule`

目录和数据：

- `鱼`、`黄金鱼`、`金币` 存在于物品目录
- “测试科技”存在于科技目录
- 两个 prefab 按需求加入 `BuildingCatalog`
