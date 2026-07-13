# 建筑扩展规则

本文档约束之后新增或修改建筑时的代码边界。当前建筑系统不保留旧字段、旧存档、旧详情面板兼容；如果旧 prefab 或旧存档因为字段变更丢数据，按新规则重新配置。

涉及占地、地形、资源连接、放置 Overlay、初始建筑或空间范围效果时，同时以 [AI_地图系统.md](AI_地图系统.md) 为准。

## 核心边界

### BuildingDefinition

`BuildingDefinition` 只放静态配置：

- 稳定建筑 ID
- 显示名、图标、分类
- 占地尺寸
- 地形要求
- 移动阻力
- 建造成本
- 建造菜单显示/可用条件
- 建造数量限制
- 专用详情面板引用

不要把运行时进度、工人数量、生产冷却、库存结果、UI 临时文本放进 `BuildingDefinition`。

### BuildingBase

`BuildingBase` 是运行时建筑实例基类，只保留所有建筑共同需要的生命周期与基础能力：

- prefab 内联 `BuildingDefinition`
- 当前格子位置与占用 ID
- `GameSystem` / `GridMap` 引用
- 注册、放置、拆除、回合入口
- 点击/双击基础派发
- 建筑模块列表
- 公共运行状态
- 公共存档字段

不要因为某个具体建筑需要一个字段就加到 `BuildingBase`。优先放到具体建筑脚本或模块。

### BuildingService

`BuildingService` 是建筑运行时仓库和建筑事务入口。以下行为必须从这里进入：

- 注册建筑
- 注销建筑
- 单个建筑放置
- 批量建筑放置
- 拆除
- 替换/升级
- 建造成本检查与扣除
- 运行时建筑列表读取

不要让 UI 控制器、存档系统、具体建筑脚本直接复制放置/扣费/占格事务。

### TurnService

`TurnService` 不拥有建筑列表。它只接收 `BuildingService.Buildings` 的快照并推进回合。

建筑回合逻辑写在具体建筑的 `OnTurn()` 或模块方法里；回合调度、成功/失败统计和产出事件由 `TurnService` 处理。

### BuildingPlacementController

`BuildingPlacementController` 只负责玩家输入、拖拽、Ghost、Overlay 编排和确认/取消 UI 状态。普通建筑的结构化评估由 `BuildingPlacementEvaluator` 负责，格子视觉由 `GridOverlayService` 负责。

不要在这里新增建筑业务规则。道路路径规则放到 `BuildingRoadPlacementPlanner`，预览实例创建放到 `BuildingPlacementPreviewFactory`，资源连接使用 `BuildingResourceProviderSystem`，空间效果使用 `BuildingSpatialEffectService`，放置事务放到 `BuildingService`。

## 新增建筑流程

1. 创建或复用一个 `BuildingBase` 子类。
2. 如果只是数值差异，优先复用同一个脚本，通过 prefab 上的序列化字段配置。
3. 如果是横向能力，优先做 `BuildingModuleBase` 模块，不要开新继承层。
4. 在 prefab 的 `BuildingBase.definition` 填稳定 `buildingId`。
5. 把 prefab 加入 `BuildingCatalog`。
6. 如果有运行时数据，创建 `BuildingDataBase` 子类并添加 `BuildingDataTypeId`。
7. 实现 `CaptureBuildingData()` / `RestoreBuildingData()`。
8. 通过 `GetRuntimeStatuses()` 和 `GetFunctionBlockEntries()` 暴露 UI 所需数据。
9. 如果建筑需要 Resource 等连接，使用消费者接口或消费者模块声明，并配置 `buildingActionPower`。
10. 如果建筑提供范围效果，创建 `BuildingSpatialEffectDefinition`，通过 `BM_空间效果源` 绑定。
11. 在 Play Mode 验证完整占地、资源路径/Buff 范围和实际运行效果一致。

## 快速开始：添加农田

本节用“农田”作为新增建筑范例。本文档就是实现基准，不要求复制任何已有具体建筑的内部写法。

农田需求：

- 占地：`2 * 2`。
- 需要 `2` 名工人运作。
- 可种植不同作物。
- 种植时消耗材料。
- 作物经过一定回合后成熟。
- 成熟后可收获。
- 允许开启自动收获。
- 可铲除已种植作物，铲除不返还产出。
- 详情面板需要显示距离成熟还剩多少回合。

### 1. 代码分层

农田由两层组成：

- `FarmField`：具体建筑脚本，负责建筑身份、岗位、回合入口、存档入口。
- `BuildingCropGrowthModule`：种植模块，负责作物配置、种植、成熟、收获、铲除、自动收获和成熟回合显示。

不要把种植字段加到 `BuildingBase`，也不要把种植逻辑直接写死在 `FarmField`。种植是一种可复用建筑能力，直接抽成 `BuildingModuleBase` 模块。

### 2. 创建农田建筑脚本

```csharp
public sealed class FarmField : BuildingBase, IBuildingWorkforceFundingSource
{
    [SerializeField, LabelText("最大岗位"), Min(1)] private int maxWorkers = 2;
    [SerializeField, LabelText("自动补贴满岗位")] private bool autoFullWorkerSubsidyEnabled;
    [SerializeField, LabelText("目标稳定工人"), Min(0)] private int targetStableWorkers;
    [SerializeField, ReadOnly] private int currentWorkers;
    [SerializeField, ReadOnly] private int stableWorkers;

    protected override bool OnTurn()
    {
        ProcessWorkforceTurn();

        if (!TryGetModule<BuildingCropGrowthModule>(out var cropGrowth))
        {
            return true;
        }

        var succeeded = cropGrowth.ProcessTurn(this, currentWorkers >= maxWorkers, out var cropChanged);
        if (cropChanged)
        {
            NotifyStateChanged();
        }

        return succeeded;
    }
}
```

`FarmField` 可以实现 `IBuildingWorkforceFundingSource`，让岗位详情 UI 读取 `当前工人/2`、招募、目标稳定工人和自动补贴。岗位逻辑属于农田建筑本体；种植逻辑属于 `BuildingCropGrowthModule`。

`ProcessWorkforceTurn()` 表示农田自己的岗位推进入口，不是 `BuildingModuleBase` 的职责，也不是 `BuildingBase` 的通用 API。

### 3. 创建种植模块

新增一个继承 `BuildingModuleBase` 的模块类：

```csharp
[Serializable]
public sealed class BuildingCropGrowthModule : BuildingModuleBase, IBuildingCropFieldSource
{
    [SerializeField, LabelText("作物配置")]
    private CropDefinition[] crops = Array.Empty<CropDefinition>();

    [SerializeField, ReadOnly] private string plantedCropId;
    [SerializeField, ReadOnly] private int growthProgressTurns;
    [SerializeField, ReadOnly] private bool autoHarvestEnabled;

    public string PlantedCropId => plantedCropId;
    public int RemainingGrowTurns => CalculateRemainingGrowTurns();
    public bool IsMature => HasCrop && RemainingGrowTurns <= 0;
    public bool AutoHarvestEnabled => autoHarvestEnabled;
    public bool HasCrop => !string.IsNullOrWhiteSpace(plantedCropId);

    public override void Normalize()
    {
        NormalizeCropDefinitions();
        growthProgressTurns = Mathf.Max(0, growthProgressTurns);
    }

    public bool ProcessTurn(BuildingBase owner, bool hasEnoughWorkers, out bool stateChanged)
    {
        stateChanged = false;

        if (!HasCrop)
        {
            return true;
        }

        if (!hasEnoughWorkers)
        {
            return false;
        }

        growthProgressTurns++;
        stateChanged = true;

        if (IsMature && autoHarvestEnabled)
        {
            var harvested = TryHarvest(owner, out var harvestChanged);
            stateChanged |= harvestChanged;
            return harvested;
        }

        return true;
    }
}
```

`BuildingModuleBase` 不是 `MonoBehaviour`。模块通过 `BuildingBase` 上的 `buildingModules` 序列化列表挂到 prefab，不单独挂组件。

### 4. 设计作物配置

作物配置放在 `BuildingCropGrowthModule` 里：

```csharp
[Serializable]
private sealed class CropDefinition
{
    public string CropId;
    public int GrowTurns;
    public BuildingCost[] PlantCosts;
    public BuildingResourceChange[] HarvestRewards;
}
```

规则：

- `CropId` 是稳定 ID，例如 `crop.wheat`。
- `GrowTurns` 是成熟需要的回合数。
- `PlantCosts` 是种植时消耗。
- `HarvestRewards` 是收获时产出。

如果作物将来会被多个系统使用，再迁移成独立 `CropCatalog` 或 ScriptableObject。农田第一版只需要模块内配置，不需要先做全局作物系统。

### 5. 配置 BuildingDefinition 和模块

农田 prefab 上的 `BuildingBase.definition` 建议配置：

- `buildingId`: `building.farm_field`
- `displayName`: `农田`
- `category`: 按当前菜单分类选择，例如生产类
- `size`: `(2, 2)`
- `requiredTerrainKeys`: 例如 `land`
- `movementResistance`: `0`，除非农田允许作为道路通行
- `placementCosts`: 建造农田本身的成本

然后在 `BuildingBase` 的 `建筑模块` 列表里添加 `BuildingCropGrowthModule`，并在模块里配置作物。

种植作物时消耗的材料不放在 `placementCosts`。`placementCosts` 只表示建造这个建筑的成本；作物种植成本属于 `BuildingCropGrowthModule` 的运行时玩法。

工人数也不放在 `BuildingDefinition`。农田的岗位数量属于 `FarmField` 的运行规则，固定配置为 `maxWorkers = 2`。

### 6. 实现种植、收获、铲除接口

种植相关接口放在 `BuildingCropGrowthModule`：

```csharp
public bool CanPlant(BuildingBase owner, string cropId);
public bool TryPlant(BuildingBase owner, string cropId, out bool stateChanged);
public bool CanHarvest();
public bool TryHarvest(BuildingBase owner, out bool stateChanged);
public bool CanClearCrop();
public bool TryClearCrop(BuildingBase owner, out bool stateChanged);
public bool TrySetAutoHarvestEnabled(bool enabled, out bool stateChanged);
```

规则：

- `TryPlant` 检查当前没有作物、作物配置存在、库存足够，并扣除 `PlantCosts`。
- `TryHarvest` 只在成熟后执行，把 `HarvestRewards` 放进库存。
- `TryClearCrop` 清空当前作物和进度，不返还产出，也不返还种植成本。
- 这些方法改变状态时设置 `stateChanged = true`。
- `BuildingCropGrowthModule` 不直接调用 `NotifyStateChanged()`；由拥有它的 `FarmField` 在模块调用结束后统一通知。

### 7. 实现回合推进

`FarmField.OnTurn()` 只做编排：

1. 推进岗位和工人状态。
2. 通过 `TryGetModule<BuildingCropGrowthModule>()` 找到种植模块。
3. 把 `currentWorkers >= maxWorkers` 传给 `cropGrowth.ProcessTurn(...)`。
4. 如果模块返回 `stateChanged = true`，由 `FarmField` 调用 `NotifyStateChanged()`。

`BuildingCropGrowthModule.ProcessTurn()` 只做种植回合逻辑：

1. 没有作物直接成功。
2. 工人不足时不推进成熟回合，返回 `false`。
3. 工人足够时增加 `growthProgressTurns`。
4. 成熟且开启自动收获时执行 `TryHarvest(owner)`。
5. 状态变化时输出 `stateChanged = true`，不直接刷新 UI，也不直接通知建筑状态。

如果自动收获因为库存满或配置错误失败，`ProcessTurn()` 返回 `false`，并由 `FarmField.GetRuntimeStatuses()` 或模块暴露的状态数据表现异常。

### 8. 暴露详情面板数据

农田需要显示成熟回合数。模块提供明确接口：

```csharp
public interface IBuildingCropFieldSource
{
    string PlantedCropId { get; }
    int RemainingGrowTurns { get; }
    bool IsMature { get; }
    bool AutoHarvestEnabled { get; }
}
```

如果 UI 需要触发操作，再定义动作接口，由 `FarmField` 实现并委托给模块：

```csharp
public interface IBuildingCropFieldActions
{
    bool TryPlant(string cropId);
    bool TryHarvest();
    bool TryClearCrop();
    bool TrySetAutoHarvestEnabled(bool enabled);
}
```

`FarmField.GetOverviewInfo()` 从模块读取摘要：

```csharp
public override string GetOverviewInfo()
{
    if (!TryGetModule<BuildingCropGrowthModule>(out var cropGrowth) || !cropGrowth.HasCrop)
    {
        return "未种植";
    }

    return cropGrowth.IsMature
        ? "可收获"
        : $"成熟剩余 {cropGrowth.RemainingGrowTurns} 回合";
}
```

`BuildingCropGrowthModule.AppendFunctionBlockEntries()` 可以追加成熟回合、当前作物、自动收获状态等功能区信息。

如果详情面板需要按钮，例如“种植”“收获”“铲除”“自动收获”，新增 `BuildingDetailsBlock_CropField`。UI block 读取 `IBuildingCropFieldSource`，按钮回调调用 `IBuildingCropFieldActions`；UI 不直接修改模块字段。

### 9. 实现存档

模块运行时数据通过拥有它的建筑数据保存。模块自己提供轻量数据结构：

```csharp
[Serializable]
public sealed class BuildingCropGrowthModuleData
{
    public string PlantedCropId;
    public int GrowthProgressTurns;
    public bool AutoHarvestEnabled;
}
```

模块提供显式读写方法：

```csharp
public BuildingCropGrowthModuleData CaptureData()
{
    return new BuildingCropGrowthModuleData
    {
        PlantedCropId = plantedCropId,
        GrowthProgressTurns = growthProgressTurns,
        AutoHarvestEnabled = autoHarvestEnabled
    };
}

public void RestoreData(BuildingCropGrowthModuleData data)
{
    if (data == null)
    {
        plantedCropId = string.Empty;
        growthProgressTurns = 0;
        autoHarvestEnabled = false;
        return;
    }

    plantedCropId = data.PlantedCropId;
    growthProgressTurns = data.GrowthProgressTurns;
    autoHarvestEnabled = data.AutoHarvestEnabled;
    Normalize();
}
```

农田建筑数据保留稳定类型 ID，并包含模块数据：

```csharp
[Serializable]
[BuildingDataTypeId("building.farm_field")]
private sealed class FarmFieldData : BuildingDataBase
{
    public int CurrentWorkers;
    public bool AutoFullWorkerSubsidyEnabled;
    public int TargetStableWorkers;
    public BuildingCropGrowthModuleData CropGrowth;
}
```

`FarmField.CaptureBuildingData()` / `RestoreBuildingData()` 负责调用模块：

```csharp
protected override BuildingDataBase CaptureBuildingData()
{
    TryGetModule<BuildingCropGrowthModule>(out var cropGrowth);

    return new FarmFieldData
    {
        CurrentWorkers = currentWorkers,
        AutoFullWorkerSubsidyEnabled = autoFullWorkerSubsidyEnabled,
        TargetStableWorkers = targetStableWorkers,
        CropGrowth = cropGrowth == null ? null : cropGrowth.CaptureData()
    };
}

protected override void RestoreBuildingData(BuildingDataBase data)
{
    if (data is not FarmFieldData farmData)
    {
        return;
    }

    currentWorkers = farmData.CurrentWorkers;
    autoFullWorkerSubsidyEnabled = farmData.AutoFullWorkerSubsidyEnabled;
    targetStableWorkers = farmData.TargetStableWorkers;

    if (TryGetModule<BuildingCropGrowthModule>(out var cropGrowth))
    {
        cropGrowth.RestoreData(farmData.CropGrowth);
    }
}
```

不要用 C# 类名或命名空间当建筑存档类型，不要保留旧存档兼容。模块数据不单独注册 `BuildingDataTypeId`，除非建筑存档系统未来明确支持模块独立存档。

### 10. 实现前需要确认的设计细节

真正开始写农田代码前，需要确定：

- 第一批作物有哪些，例如小麦、蔬菜、药草。
- 每种作物的种植材料是什么。
- 每种作物成熟需要几回合。
- 每种作物收获产出是什么。
- 自动收获失败时是否保留成熟作物。
- 收获后是否自动继续种同一种作物，还是只收获并清空。
- 铲除是否允许在成熟后执行。
- 作物是否需要显示图标，图标来自农田配置还是物品目录。

未确认前，代码可以先把这些都做成 prefab 上的序列化配置，不写死到脚本常量里。

## 建筑 ID 规则

`BuildingDefinition.BuildingId` 是存档和目录查找的主键。

规则：

- 一旦对外使用，不随显示名改动。
- 不使用临时中文展示名当 ID。
- 推荐格式：`building.farm_field`、`building.residential_house.lv1`。
- 同一建筑不同等级是否共用脚本，不影响 ID 唯一性。

## 运行时数据存档规则

有运行时状态的建筑必须提供稳定类型 ID：

```csharp
[Serializable]
[BuildingDataTypeId("building.example")]
private sealed class ExampleBuildingData : BuildingDataBase
{
    public int Progress;
}
```

规则：

- 不使用 `AssemblyQualifiedName`。
- 不依赖 C# 类名或命名空间作为存档类型。
- 不保留旧存档兼容。
- 字段改名会造成旧数据丢失，按新数据结构处理。
- 只有需要跨存档保留的数据才写入 data。

## 模块规则

适合做模块的能力：

- 库存容量
- 科技点
- 等级升级
- 附近人口吸引力
- 种植、成熟、收获
- 未来可复用的生产、维护、加成、范围效果
- 资源加工、施工材料消耗和连接消费者能力
- 产量 Buff、美化等空间效果源

模块应该满足：

- 只保存该能力自己的配置和运行时状态。
- 不是 `MonoBehaviour`，不直接挂到 GameObject。
- 通过 `BuildingBase` 的 `buildingModules` 序列化列表挂到建筑 prefab。
- 通过 `AppendFunctionBlockEntries()` 暴露功能区 UI。
- 如果需要回合推进，由拥有它的 `BuildingBase` 子类在 `OnTurn()` 中显式调用模块方法。
- 如果需要存档，实现 `IBuildingModuleStateSerializer`；`BuildingBase` 会统一捕获和恢复，不要再在具体建筑数据中重复保存同一模块状态。
- 不直接调用 `NotifyStateChanged()`；模块返回状态变化结果，由拥有它的建筑脚本负责通知。
- 不直接处理输入。
- 不直接实例化 UI。
- 不直接遍历全局场景，除非能力本身需要且没有更合适服务。

创建模块的流程：

1. 新建 `[Serializable] public sealed class XxxModule : BuildingModuleBase`。
2. 把模块配置和该能力的运行时状态放进模块类。
3. 覆写 `Normalize()`，清理非法配置和运行时值。
4. 覆写 `AppendFunctionBlockEntries(BuildingBase building, ref List<BuildingFunctionBlockEntry> entries)`，暴露功能区信息。
5. 为建筑脚本提供明确方法，例如 `ProcessTurn(...)`、`TryDoSomething(...)`、`CaptureData()`、`RestoreData(...)`。
6. 在建筑 prefab 的 `建筑模块` 列表里添加该模块。
7. 在拥有模块的建筑脚本里用 `TryGetModule<XxxModule>(out var module)` 读取并调用它。

连接与空间效果模块还要遵循：

- 实现 `IBuildingConnectionConsumerModule` 的启用模块会自动参与 `BuildingBase.RequiresConnectionType(...)`；不要再增加重复的 Prefab 布尔字段。
- `buildingActionPower` 是连接寻路预算，不是消费者开关。
- `BM_施工材料消耗` 和 `BM_资源加工` 已自动声明 `Resource` 连接。
- `BM_空间效果源` 只引用 `BuildingSpatialEffectDefinition`；预览和运行时结算共用 Definition，不在模块或 UI 中复制半径/数值。

如果能力有独立配置、独立运行时状态、独立 UI 表达，或者未来明显会被多个建筑复用，直接做 `BuildingModuleBase`。只有纯粹的一次性小差异才放在具体建筑脚本。

## 回合规则

具体建筑的 `OnTurn()` 只做建筑自己的玩法处理，并返回成功/失败。

推荐顺序：

1. 归一化配置。
2. 清理上一回合临时状态。
3. 检查依赖服务。
4. 扣运营消耗。
5. 更新建筑内部状态。
6. 记录实际产出。
7. 调用 `NotifyStateChanged()`。

不要在 `OnTurn()` 里直接刷新 UI。UI 通过建筑状态、服务事件或 `StateChanged` 响应。

## UI 暴露规则

建筑面板只读取结构化数据：

- `GetRuntimeStatuses()`
- `GetOverviewInfo()`
- `GetFunctionBlockEntries()`
- 明确接口，例如 `IBuildingWorkforceFundingSource`

不要新增旧式 `BuildingDetailSection` / `BuildingDetailRow` 路线。当前详情面板以 `Popup_BuildingDetails` 的岗位、功能、等级 block 为准。

如果新增一种详情模块：

1. 定义清晰接口。
2. 写独立 `BuildingDetailsBlock_*`。
3. `Popup_BuildingDetails` 只负责发现和绑定 block。
4. 具体建筑只暴露数据，不操作 UI。

## 放置和道路规则

普通建筑放置必须走：

```csharp
BuildingService.TryPlace(BuildingPlacementRequest request, out BuildingBase building)
```

批量道路放置必须走：

```csharp
BuildingRoadPlacementPlanner
BuildingService.TryPlaceBatch(...)
```

不要在 UI 控制器里重复扣建造成本、重复占格、重复回滚。

普通建筑放置评估必须保持以下边界：

- 完整 `BuildingDefinition.Size` footprint 的边界、全局可建造、地形和占用决定红/绿与 `CanConfirm`。
- Resource 可达范围、可用提供点、最终提供点/路径和 Buff 范围是附加信息，不阻止放置。
- 项目没有建筑旋转语义；根 Transform 始终使用单位旋转。
- Base Tilemap 决定地图边界，Terrain Layers 决定静态地形标签，运行时占用由 `GridMapBehaviour` 字典决定。
- 不要让建筑、UI 或新效果直接写某个共享 Tilemap。通过 `GridOverlayService.AcquireOwner(...)` 获取 owner handle，结束时 Dispose，只清理自己的提交。

## 资源连接规则

- 当前内置连接类型为 `BuildingConnectionTypes.Resource`；接口预留其他稳定类型 ID。
- 建筑本体实现 `IBuildingConnectionConsumer`，或启用模块实现 `IBuildingConnectionConsumerModule`，才能成为消费者。
- 提供点使用 `isResourceProviderPoint`，需要动态可用性时实现 `IBuildingResourceProviderOperationalState`。
- 运行时消费前调用 `BuildingResourceProviderSystem.TrySelectProvider(...)`；不要因为放置预览找到过提供点就缓存并永久复用。
- Resource 表示可以访问全局库存，不是提供点自己的局部库存。
- 找不到提供点时允许放置，但依赖连接的回合逻辑必须失败且不得扣库存/推进进度。

## 空间效果规则

- 范围效果使用 `BuildingSpatialEffectDefinition` + `BM_空间效果源`。
- 当前使用完整来源 footprint 的曼哈顿范围，忽略障碍，只裁剪到 Base 地图边界。
- `ProductionPercent` 根据 Definition 的叠加规则影响目标；`Beauty` 按格取最高值，多格建筑取占地平均值向下取整。
- 新增效果类型必须同时补齐预览、目标过滤、运行时结算和 UI 描述，不能只画 Overlay。

## 升级/替换规则

建筑升级走：

```csharp
BuildingService.TryReplace(sourceBuilding, targetPrefab, out replacement)
```

规则：

- 升级目标是 prefab。
- 升级条件和成本由模块或具体建筑判断。
- 替换后的状态迁移写在 `OnReceiveReplacementState()`。
- 不要手动 `Destroy` 旧建筑再手动实例化新建筑。

## 禁止事项

- 不要让 `TurnService` 保存建筑列表。
- 不要让 `BuildingPlacementController` 承担业务规则。
- 不要把单个建筑字段放进 `BuildingBase`。
- 不要把 UI 文本当成建筑数据源。
- 不要新增旧存档兼容逻辑。
- 不要用 C# 类型全名作为存档类型。
- 不要绕过 `BuildingService` 直接占格/扣费/替换。
- 不要把资源连接结果并入通用放置合法性。
- 不要只配置 `buildingActionPower` 却遗漏消费者接口/模块。
- 不要直接清空共享 Overlay Tilemap 或清理其他 owner 的格子。
- 不要为建筑根对象增加旋转放置或旋转存档字段。

## 判断某段逻辑应该放哪里

- 这是所有建筑共同生命周期吗？放 `BuildingBase`。
- 这是建筑实例集合或放置事务吗？放 `BuildingService`。
- 这是回合调度吗？放 `TurnService`。
- 这是玩家拖拽、点击确认、预览 UI 吗？放 `BuildingPlacementController`。
- 这是道路路径选择吗？放 `BuildingRoadPlacementPlanner`。
- 这是可复用建筑能力吗？做 `BuildingModuleBase`。
- 这是某个建筑独有玩法吗？放具体建筑脚本。
- 这是 UI 展示组件吗？放 `BuildingDetailsBlock_*` 或 UI 脚本。
- 这是地图静态内容或地形标签吗？放 `MapContentAuthoring` 的 Base/Terrain Tilemap。
- 这是放置阶段的纯评估吗？放 `BuildingPlacementEvaluator` 或对应领域查询。
- 这是格子视觉吗？定义 `GridOverlayChannelDefinition`，通过 `GridOverlayService` 提交。

## 变更记录

### 2026-07-13

- 同步地图与建筑放置重构后的职责边界。
- 补充 Resource 消费者/提供点、空间效果、完整 footprint 和 owner 化 Overlay 规则。
- 修正模块存档说明：实现 `IBuildingModuleStateSerializer` 后由 `BuildingBase` 统一保存与恢复。
