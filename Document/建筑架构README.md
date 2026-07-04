# 建筑架构 README

本文档说明当前项目中的建筑架构，以及 `BuildingBase` 继承体系和 `BuildingModuleBase` 模块体系的职责边界。

结论：模块架构和当前建筑继承基类架构不冲突。它们不是两套互相替代的架构，而是分层关系：

- `BuildingBase` 和建筑子类负责“这个建筑是什么、怎么活、每回合做什么”。
- `BuildingModuleBase` 负责“这个建筑额外挂了哪些可选能力参数”。
- `BuildingService`、`TurnService`、`GameSystem` 负责跨建筑管理、回合推进和全局聚合。

## 当前核心结构

```text
BuildingCatalog
  -> BuildingBase prefab
      -> BuildingDefinition 静态建筑数据
      -> BuildingBase 通用生命周期/放置/点击/UI/模块入口
      -> 具体建筑子类 具体业务与运行时状态
      -> BuildingModuleBase[] 可选能力模块

GameSystem
  -> InventoryService
  -> DynastyService
  -> TurnService
  -> BuildingService
  -> GameEventService
```

## 职责边界

### BuildingDefinition

`BuildingDefinition` 是 prefab 上的静态配置数据。

适合放：

- 建筑 ID、显示名、分类、图标。
- 占地尺寸、地形要求、移动阻力。
- 建造成本、建造菜单显示条件、可用条件。
- 最大建造数量和数量限制分组。
- 详情面板 prefab 选择。

不适合放：

- 当前工人数、当前人口、生产进度等运行时状态。
- 每回合逻辑。
- 会随着建筑实例变化的状态。

### BuildingBase

`BuildingBase` 是所有建筑 prefab 根节点上的运行时基类，是建筑实例的统一入口。

当前负责：

- 持有 `BuildingDefinition`。
- 持有格子坐标、占地 ID、`GridMap` 引用。
- 管理初始化、注册、放置、拆除、销毁清理。
- 提供 `OnInitialized()`、`OnRegistered()`、`OnPlaced()`、`OnTurn()` 等生命周期入口。
- 统一点击和双击入口。
- 提供 `GetOverviewInfo()`、`GetRuntimeStatuses()`、`GetFunctionBlockEntries()` 等 UI 读取入口。
- 持有 `buildingModules`，并提供 `TryGetModule<T>()`、`GetModules<T>()`。
- 放置所有建筑都可能需要的极少数通用字段，例如 `IsResourceProviderPoint`、`BuildingActionPower`。

`BuildingBase` 不应该承载所有可选玩法字段。否则每个建筑都会暴露岗位、库存、税收、生产、住宅等无关字段，基类会快速膨胀。

### 具体建筑子类

具体建筑子类描述“这个等级的建筑如何运行”。

当前例子：

- `LumberCabinLV1`：岗位、招工、产出原木、岗位详情。
- `ResidentialHousingLV1`：人口、食物消耗、税收、荒废、资源连接检查。
- `PlayerHomeLV1`：王宫注册、资源点标记、通过模块提供库存格数。

适合放：

- 建筑独有的运行时状态。
- 每回合业务流程。
- 存档数据结构和恢复逻辑。
- 对库存、王朝、事件服务的业务调用。
- 自定义概览、功能块数据、侧边栏说明和运行状态。

不适合放：

- 跨建筑通用聚合逻辑。
- 与某个建筑无关的全局数值系统。
- 只是用于多个建筑复用的配置字段。

### BuildingModuleBase

`BuildingModuleBase` 是建筑上的可选能力模块。它通过 `BuildingBase` 的 `buildingModules` 列表挂在 prefab 上。

当前模块：

- `BuildingNearbyPopulationJobAttractionModule`
  - 配置 `人口搜索半径`。
  - 配置 `附近每人口就业吸引力`。
  - 由岗位建筑读取，用于计算附近人口增益。

- `BuildingInventorySlotCapacityModule`
  - 配置 `提供库存格数`。
  - 由 `GameSystem` 汇总所有建筑模块后调整库存容量。

适合放：

- 不是所有建筑都需要的能力配置。
- 多种建筑未来可能复用的字段。
- 可以被建筑子类或服务读取的只读能力数据。
- 简单的归一化和详情展示。

不适合放：

- 独立 Unity 生命周期。模块不是 `MonoBehaviour`，没有 `Start()`、`Update()`。
- 隐式修改全局服务。模块本身不应该直接扣库存、加人口、推进回合。
- 大型业务流程。业务流程应在具体建筑子类或服务中显式执行。

如果未来确实需要模块拥有回合行为，不要让模块私自模拟生命周期。应该在 `BuildingBase` 增加明确的模块生命周期钩子，例如 `OnBuildingRegistered`、`OnBuildingTurn`，并统一规定调用顺序。

## 模块和继承为什么不冲突

继承解决的是“建筑类型差异”：

```text
伐木小屋怎么招工、怎么产出
住宅怎么消耗食物、怎么增长人口
王宫怎么注册为核心建筑
```

模块解决的是“可选能力配置”：

```text
这个建筑是否受附近人口影响岗位吸引力
这个建筑是否提供库存格数
```

两者可以组合：

```text
LumberCabinLV1
  -> 继承 BuildingBase，拥有伐木小屋自己的回合逻辑
  -> 挂 BuildingNearbyPopulationJobAttractionModule，配置岗位人口影响参数

PlayerHomeLV1
  -> 继承 BuildingBase，拥有王宫注册逻辑
  -> 挂 BuildingInventorySlotCapacityModule，配置提供库存格数
```

冲突只会在职责混用时出现，例如：

- 建筑子类和模块都试图决定同一段回合流程。
- 模块绕过建筑子类直接改库存、人口、事件。
- 某个字段既在子类中配置，又在模块中配置，运行时不知道以谁为准。

当前实现没有这个冲突：模块只提供配置和详情段，具体业务仍由建筑子类或服务显式读取模块后执行。

## 服务边界

### BuildingService

`BuildingService` 是建筑运行时列表、放置、拆除、替换和建造可用性判断的入口。

负责：

- 通过 `TryPlace()` 实例化建筑 prefab。
- 调用 `GridMap` 占格。
- 写入 `BuildingBase.SetPlacement()`。
- 注册和注销建筑到 `TurnService`。
- 发出 `BuildingsChanged`。
- 计算建筑数量限制和可用性。

不负责：

- 具体建筑每回合产出。
- UI 展示格式。
- 直接修改库存或人口。

### TurnService

`TurnService` 持有当前运行时建筑列表，并在推进回合时调用建筑。

流程：

```text
GameSystem.NextTurn()
  -> TurnService.NextTurn() / NextTurnRoutine()
  -> BuildingBase.ProcessTurn()
  -> 具体建筑 OnTurn()
  -> BuildingBase.NotifyStateChanged()
```

`TurnService` 不理解每种建筑的细节，只关心建筑是否成功执行本回合，以及是否通过资源接口暴露了上回合产出。

### GameSystem

`GameSystem` 是场景级服务组合根。

当前负责：

- 创建 `InventoryService`、`DynastyService`、`TurnService`、`BuildingService`、`GameEventService`。
- 注册和注销建筑。
- 提供全局岗位吸引力修正接口。
- 汇总库存容量模块：

```text
库存总格数 = 基础库存格数 + Sum(所有启用 BuildingInventorySlotCapacityModule.提供库存格数)
```

`GameSystem` 做库存容量聚合是合理的，因为库存容量是跨建筑全局结果，不属于某一个建筑独自拥有。

## 接口边界

接口用于“让其他系统读取某类建筑数据”，不是用来驱动建筑生命周期。

当前接口：

- `IBuildingPopulationSource`
  - 暴露当前人口。
  - 被岗位系统统计可用人口、附近人口。

- `IBuildingJobSource`
  - 暴露岗位相关运行结果。
  - 用于岗位统计和调试。

- `IBuildingResourceConsumptionSource`
  - 暴露预计消耗和上回合消耗。

- `IBuildingTaxSource`
  - 暴露预计税收和上回合税收。

- `IBuildingResourceProductionSource`
  - 暴露预计产出和上回合产出。

接口适合横向读取。模块适合挂载可选配置。建筑子类适合执行业务。

## UI 边界

UI 不应该反推建筑业务规则。

UI 应读取：

- `BuildingBase.Definition`
- `BuildingBase.GetOverviewInfo()`
- `BuildingBase.GetRuntimeStatuses()`
- `BuildingBase.GetFunctionBlockEntries()`
- 能力接口或建筑模块暴露的只读结果

建筑详情由详情块承载：

- 岗位块读取 `IBuildingWorkforceFundingSource`。
- 功能块读取 `GetFunctionBlockEntries()`。
- 更具体的说明放入侧边栏结构化行。

不再使用 `GetDetailInfo()` 作为通用详情数据源。新增人口、税收、维护等详情时，应新增独立详情块或扩展现有功能块，而不是让 UI 读取一份通用明细表。

## 放置与注册流程

运行时建造的主流程：

```text
GamePanel_Building / 建造按钮
  -> BuildingPlacementController
  -> BuildingService.TryPlace(prefab, gridMap, origin, parent, out building)
      -> GridMap.CanOccupy()
      -> GridMap.TryOccupy()
      -> Instantiate(prefab)
      -> building.SetPlacement(origin, occupancyId, gridMap)
      -> building.gameObject.SetActive(true)
  -> BuildingBase.Start()
      -> GameSystem.RegisterBuilding(this)
      -> BuildingService.RegisterBuilding()
      -> building.Initialize()
      -> OnInitialized()
      -> OnRegistered()
      -> OnPlaced()
```

场景初始建筑可能通过 `InitialBuildingPlacement` 先写入占格和 `SetPlacement()`，再走 `BuildingBase.Start()` 注册。无论入口如何，真实运行都应回到 `BuildingBase` 和 `GameSystem.RegisterBuilding()`。

## 新能力放在哪里

| 需求 | 推荐位置 | 原因 |
| --- | --- | --- |
| 所有建筑都需要的身份、生命周期、放置、点击、UI 基础入口 | `BuildingBase` | 统一入口，避免重复 |
| 某个建筑等级独有的每回合行为和状态 | 具体建筑子类 | 行为和状态属于该建筑 |
| 多个建筑可复用、但不是所有建筑都需要的检查器配置 | `BuildingModuleBase` 派生模块 | 可选组合，避免基类膨胀 |
| 多种建筑都要被 UI 或统计系统横向读取的一类结果 | 接口 | 只读契约，低耦合 |
| 跨建筑聚合、全局计算、列表管理、放置管理 | 服务或静态系统 | 不属于单个建筑实例 |
| 纯公式、无 Unity 状态的计算 | 静态系统类 | 易复用、易测试 |

当前例子：

- 附近每人口提供就业吸引力：模块。
- 提供库存格数：模块，聚合在 `GameSystem`。
- 建筑升级经验、升级消耗和自动升级：模块，显示在 `BuildingDetailsBlock_Level`，由 `BuildingBase.ProcessTurn()` 在回合成功后统一尝试自动升级。
- 资源连接点：当前在 `BuildingBase`，因为它是简单、高频、基础的建筑标记，并且已有 prefab 字段需要稳定。
- 伐木小屋生产原木：`LumberCabinLV1` 子类。
- 住宅消耗食物和增长人口：`ResidentialHousingLV1` 子类。
- 岗位公式：`BuildingJobSystem` 静态系统。
- 建筑可建造数量限制：`BuildingService` / `BuildingAvailabilityEvaluator`。

## 新建筑接入建议

1. 创建建筑 prefab，并挂具体 `BuildingBase` 子类。
2. 在 prefab 上填写 `BuildingDefinition`。
3. 如果只是调整可选能力参数，优先添加建筑模块。
4. 如果需要每回合行为，在建筑子类中实现 `OnTurn()`。
5. 如果需要 UI 展示通用横向数据，实现对应只读接口。
6. 如果需要详情栏，优先接入现有详情块；新增详情类型时创建独立详情块。
7. 如果涉及跨建筑聚合，把聚合放到服务或静态系统中，不要放到 UI。
8. 最后把 prefab 加入 `BuildingCatalog`。

## 模块使用约定

新增模块时遵守以下规则：

- 模块类继承 `BuildingModuleBase`。
- 标记 `[Serializable]`。
- 字段用 `[SerializeField]`，通过只读属性向外暴露。
- 在 `Normalize()` 中归一化检查器输入。
- 如果需要显示到功能块，实现 `AppendFunctionBlockEntries()`。
- 如果需要专门交互 UI，新增独立详情块。
- 如果模块有运行时状态，例如当前经验，后续需要明确纳入建筑存档或模块存档。
- 模块不直接调用 `InventoryService`、`DynastyService`、`TurnService`。
- 模块不保存运行时流程状态，除非未来明确引入模块生命周期和存档约定。

## 当前架构判断

当前项目仍然是“继承式建筑运行时架构”，模块只是挂在 `BuildingBase` 上的轻量组合层。

这个方向适合当前阶段：

- 建筑数量还不大，具体建筑子类能清晰表达业务。
- 已有 UI、放置、回合、存档都围绕 `BuildingBase`。
- 模块可以解决可选字段越来越多导致的基类膨胀。
- 服务层可以继续承接跨建筑聚合，避免 UI 或单个建筑承担全局责任。

需要警惕的是：如果之后模块越来越像“行为组件”，就必须补齐明确的模块生命周期、执行顺序和存档机制。否则模块只应保持为可选配置和只读能力数据。
