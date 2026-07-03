# BuildingResourceInterfaces 接口说明

本文档梳理 `Assets/Landsong/Scripts/BuildingSystem/BuildingResourceInterfaces.cs` 中定义的接口和数据结构：它们负责暴露什么数据、这些数据从哪里来、最终被谁使用。

## 核心定位

`BuildingResourceInterfaces.cs` 是建筑系统面向 UI 和通用统计逻辑的只读数据契约。

- 建筑实例负责计算和保存自己的运行数据。
- UI 只通过接口读取数据，不直接推导建筑业务规则。
- 库存扣减、库存写入、人口贡献、岗位人口等副作用不在接口里发生，而是在具体建筑的 `OnTurn()` 或交互方法中完成。
- `Current*` 一般表示当前状态下的预计值，不修改库存。
- `Last*` 一般表示上一轮实际成功发生的值，由建筑在回合结算中写入，通常会在下一次 `OnTurn()` 开始时清空。

## 总体数据流

```text
GameSystem.NextTurn()
  -> TurnService.NextTurn()
  -> BuildingBase.ProcessTurn()
  -> 具体建筑 OnTurn()
      -> 读取序列化配置、运行时字段、GameSystem.Inventory、GameSystem.Buildings、GridMap、DynastyService
      -> 成功时写入 InventoryService / DynastyService / 建筑运行时缓存
      -> 失败时写入 RuntimeStatus 相关缓存
  -> BuildingBase.NotifyStateChanged()
  -> UI 监听建筑状态变化或回合推进事件
  -> UI 通过资源接口和 BuildingBase 的状态/概览方法读取数据并刷新显示
```

当前主要消费者：

- `BuildingStatusUIFormatter`：读取运行状态和概览数值，生成 UI 展示数据。
- `BuildingDetailUIFormatter`：在建筑没有自定义 `IBuildingDetailSource` 时，使用这些接口生成通用详情分组。
- `GamePanel_BuildingStatusOverview`：建筑状态概览列表。
- `BuildingStatusMarkerManager`：异常建筑地图 Marker。
- `GamePanel_SelectedBuildingOverview`：选中建筑底部概览。
- `GamePanel_BuildingMessageBar`：点击建筑、列表项或 Marker 后的消息栏。
- `GamePanel_BuildingEventMessageList`：回合推进后，根据异常状态生成事件消息。

## 接口一览

| 接口 | 作用 | 当前实现方 | 主要去处 |
| --- | --- | --- | --- |
| `IBuildingResourceConsumptionSource` | 暴露预计消耗和上回合实际消耗 | `ResidentialHousingLV1` | 详情 UI fallback、建筑自定义详情 |
| `IBuildingTaxSource` | 暴露预计税收和上回合实际税收 | `ResidentialHousingLV1` | 详情 UI fallback、建筑自定义详情 |
| `IBuildingResourceProductionSource` | 暴露预计产出和上回合实际产出 | `LumberCabinLV1` | 详情 UI fallback、建筑自定义详情 |

## 资源连接点

资源连接点已不再通过 `BuildingResourceInterfaces.cs` 中的接口暴露，而是统一由 `BuildingBase.IsResourceProviderPoint` 提供。

职责：告诉其他建筑“我能不能被当作资源连接点”。

数据来源：

- 数据来自 `BuildingBase.isResourceProviderPoint` 序列化字段。
- `PlayerHomeLV1.prefab` 当前配置为 `isResourceProviderPoint: 1`。
- `IsResourceProviderPoint == true` 时，建筑会被视为可连接的资源点。

数据去处：

- `ResidentialHousingLV1.HasReachableResourceProvider()` 会遍历 `GameSystem.Buildings.Buildings`。
- 对每个建筑读取 `building.IsResourceProviderPoint`。
- 找到可用资源点后，再结合建筑占地、`GridMap`、路径消耗、搜索范围判断住宅是否能连接资源。
- 如果找不到可达资源点，住宅本回合不会消耗食物，会登记 `无法连接资源` 的运行状态。

注意：这个接口本身不生产任何资源，也不修改库存。它只是资源连接判定的标记点。

## 建筑模块

`BuildingBase` 持有 `建筑模块` 列表，用来承载“不是所有建筑都需要，但需要统一查询入口”的能力配置。

当前模块：

- `BuildingNearbyPopulationJobAttractionModule`：岗位建筑使用，配置 `人口搜索半径` 和 `附近每人口就业吸引力`。
- `BuildingInventorySlotCapacityModule`：库存容量建筑使用，配置 `提供库存格数`。

当前使用：

- `LumberCabinLV1.prefab` 添加 `BuildingNearbyPopulationJobAttractionModule`，岗位系统从模块读取附近人口影响。
- `PlayerHomeLV1.prefab` 添加 `BuildingInventorySlotCapacityModule`，`GameSystem` 汇总所有已存在建筑的库存模块，计算库存总格数。

库存总格数：

```text
库存总格数 = GameSystem.基础库存格数 + Sum(所有启用库存模块.提供库存格数)
```

当目标库存格数变小时，如果将被移除的尾部格子里还有物品，`GameSystem` 会暂时保留当前库存格数，避免直接丢失物品。

## IBuildingResourceConsumptionSource

职责：让建筑把“预计每回合会消耗什么”和“上回合实际消耗了什么”暴露给 UI 或统计系统。

属性：

- `CurrentResourceConsumptions`：当前状态下预计每回合会消耗的资源。
- `LastResourceConsumptions`：上一次回合实际成功消耗的资源。

当前实现：`ResidentialHousingLV1`。

数据来源：

- `foodItemId`：住宅消耗的物品 ID，默认是 `蔬菜`。
- `currentPopulation`：当前人口。
- `GetCurrentFoodConsumptionAmount()` 返回 `Mathf.Max(0, currentPopulation)`，所以预计食物消耗量等于当前人口数。
- `isAbandoned == true` 时，预计消耗为空。

数据写入点：

- 在 `ResidentialHousingLV1.OnTurn()` 中，如果库存服务存在、食物 ID 有效、可连接资源点，并且 `inventory.TryRemoveItem(foodItemId, foodAmount)` 成功：
  - 设置 `lastTurnConsumedResources`。
  - 设置 `lastResourceConsumptions = CreateResourceChanges(foodItemId, foodAmount)`。
- 每次 `OnTurn()` 开始会通过 `ClearLastTurnState()` 把 `lastResourceConsumptions` 清空。

数据去处：

- `BuildingDetailUIFormatter.AddFallbackResourceSection()` 会把它格式化成 `预计消耗`、`上回合消耗`。
- `ResidentialHousingLV1` 自己实现了 `IBuildingDetailSource`，所以当前住宅详情面板优先使用它的自定义详情段，但底层数据来源一致。

失败处理：

- 库存不足、资源点不可达、食物 ID 无效等情况不会写入 `LastResourceConsumptions`。
- 失败会转成 `BuildingBase.GetRuntimeStatuses()` 暴露的运行状态，例如 `蔬菜不足`、`无法连接资源`、`消耗失败 1/3`。

## IBuildingTaxSource

职责：让建筑把“预计税收”和“上回合实际税收”暴露给 UI 或统计系统。

属性：

- `CurrentTaxRewards`：当前状态下预计可获得的税收奖励。
- `LastTaxRewards`：上一次回合实际成功获得的税收奖励。

当前实现：`ResidentialHousingLV1`。

数据来源：

- `taxItemId`：税收物品 ID，默认是 `金币`。
- `currentPopulation`：当前人口。
- `HasReachedMaxPopulation`：只有住宅达到最大人口且没有荒废时，才会暴露预计税收。
- 当前预计税收为 `taxItemId x currentPopulation`。

数据写入点：

- 在 `ProcessSuccessfulConsumption()` 中，住宅满人口后推进 `taxConsumptionProgress`。
- 达到 `taxIntervalTurns` 后调用 `TryProvideTax()`。
- `inventory.TryAddItem(taxItemId, taxAmount)` 成功时：
  - 重置税收进度。
  - 设置 `lastTurnProvidedTax`。
  - 设置 `lastTaxRewards = CreateResourceChanges(taxItemId, taxAmount)`。
- 每次 `OnTurn()` 开始会通过 `ClearLastTurnState()` 把 `lastTaxRewards` 清空。

数据去处：

- `BuildingDetailUIFormatter.AddFallbackResourceSection()` 会把它格式化成 `预计税收`、`上回合税收`。
- 当前住宅详情面板优先使用自定义详情段，显示 `税收物品`、`税收进度`、`当前预计税收`、`上回合实际税收`。

失败处理：

- 税收物品 ID 无效或库存写入失败时，不写入 `LastTaxRewards`。
- 失败会写入运行状态缓存，例如 `税收配置异常`、`税收存入失败`。

## IBuildingResourceProductionSource

职责：让建筑把“预计生产什么”和“上回合实际生产了什么”暴露给 UI 或统计系统。

属性：

- `CurrentResourceProductions`：当前状态下预计每回合会生产的资源。
- `LastResourceProductions`：上一次回合实际成功生产的资源。

当前实现：`LumberCabinLV1`。

数据来源：

- `woodItemId`：产出物品 ID，默认是 `原木`。
- `currentWorkers`：当前工人数量。
- `minimumWorkersForProduction`：开始生产所需最低工人数。
- `fullProductionWorkers`、`baseProductionAmount`、`fullProductionAmount`：决定基础产量或满额产量。
- `GetCurrentWoodProductionAmount()` 在工人不足时返回 `0`，工人达到满额阈值时返回 `fullProductionAmount`，否则返回 `baseProductionAmount`。

数据写入点：

- `LumberCabinLV1.OnTurn()` 会先计算岗位状态、处理招工或离职，再调用 `TryProduceWood()`。
- `inventory.TryAddItem(woodItemId, productionAmount)` 成功时：
  - 设置 `lastProducedWood`。
  - 设置 `lastResourceProductions = CreateResourceChanges(woodItemId, productionAmount)`。
- 每次 `OnTurn()` 开始会通过 `ClearLastTurnState()` 把 `lastResourceProductions` 清空。

数据去处：

- `BuildingDetailUIFormatter.AddFallbackResourceSection()` 会把它格式化成 `预计产出`、`上回合产出`。
- 当前伐木屋详情面板优先使用自定义详情段，显示 `产出物品`、`当前预计产出`、`上回合产出`、生产工人阈值等。

失败处理：

- 工人不足、原木 ID 无效、库存写入失败时，不写入 `LastResourceProductions`。
- 失败会转成运行状态，例如 `工人不足`、`原木配置异常`、`原木存入失败`。

## 建筑运行状态

建筑运行状态已不再通过 `BuildingResourceInterfaces.cs` 中的接口暴露，而是统一由 `BuildingBase.GetRuntimeStatuses()` 虚方法提供。

职责：暴露建筑当前需要显示的异常或提示状态。

属性：

- `GetRuntimeStatuses()`：建筑当前状态列表。空列表表示 UI 可以视为正常。

当前实现：

- `ResidentialHousingLV1`
- `LumberCabinLV1`

住宅状态来源：

- `isAbandoned` -> `荒废`
- `lastTurnPopulationDecayed` -> `人口衰减`
- `consecutiveConsumptionFailures` -> `消耗失败 progress/target`
- `lastAbnormalStatusId`、`lastAbnormalStatusText` -> 最近一次异常

伐木屋状态来源：

- `currentWorkers < minimumWorkersForProduction` -> `工人不足`
- `currentWorkers < stableWorkers` -> `缺工`
- `lastTurnNoAvailablePopulation` -> `可用人口不足`
- `lastAbnormalStatusId`、`lastAbnormalStatusText` -> 最近一次异常

数据去处：

- `BuildingStatusUIFormatter.GetRuntimeStatuses()` 调用 `BuildingBase.GetRuntimeStatuses()`。
- `BuildingStatusUIFormatter.CreateDisplayData()` 把状态转成 `StatusText` 和 `HasAbnormalStatus`。
- `GamePanel_BuildingStatusOverview` 用它排序和显示建筑状态。
- `BuildingStatusMarkerManager` 只给 `HasAbnormalStatus == true` 的建筑创建地图 Marker。
- `GamePanel_BuildingMessageBar` 用它生成点击后的短文本。
- `GamePanel_BuildingEventMessageList` 在 `TurnAdvanced` 后读取状态并生成事件消息。
- `BuildingDetailUIFormatter` fallback 会把状态写入 `运行状态` 分组。

注意：`GetRuntimeStatuses()` 返回的是 UI 状态，不等同于回合成功或失败。具体建筑的 `OnTurn()` 返回值仍由建筑自己的业务判断决定。

## 建筑概览文本

建筑概览文本已不再通过 `BuildingResourceInterfaces.cs` 中的接口暴露，而是统一由 `BuildingBase.GetsOveriewMessage()` 抽象方法提供。

职责：返回一条适合列表、底部栏和消息栏快速展示的短文本。

属性：

- `GetsOveriewMessage()`：完整概览短文本，例如 `人口 4/5`、`工人 2/3`。

当前实现：

- `ResidentialHousingLV1`
  - `GetsOveriewMessage()`: `人口 {currentPopulation}/{maxPopulationContribution}`
- `LumberCabinLV1`
  - `GetsOveriewMessage()`: `工人 {currentWorkers}/{stableWorkers}`
- 其他建筑
  - 当前返回空字符串，保持原先没有概览接口时的展示行为。

数据去处：

- `BuildingStatusUIFormatter.CreateDisplayData()` 读取并放进 `BuildingStatusDisplayData`。
- `GamePanel_BuildingStatusOverviewItem` 显示在建筑状态列表行。
- `GamePanel_SelectedBuildingOverview` 显示在选中建筑底部栏。
- `GamePanel_BuildingMessageBar` 拼进点击后的 `DetailText`。
- `BuildingDetailUIFormatter` fallback 会把它加入 `基础信息` 分组。

注意：这是展示摘要，不应该承载复杂业务对象。复杂详情应放在 `IBuildingDetailSource`。

## BuildingRuntimeStatus

`BuildingRuntimeStatus` 是 `BuildingBase.GetRuntimeStatuses()` 返回的单条状态数据。

字段含义：

- `StatusId`：稳定 ID，用于程序判断、去重和未来扩展。
- `DisplayName`：面向玩家显示的文本。
- `Progress`：可选进度值，例如连续失败次数、当前人口、当前工人数。
- `Target`：可选目标值，例如失败阈值、人口上限、稳定工人数。
- `EventMessage`：事件消息专用短文本。为空时，事件列表会用建筑名和 `DisplayName` 自动拼接。
- `IsValid`：`StatusId` 非空时为有效状态。

格式化规则：

- `BuildingStatusUIFormatter` 在 `Target > 0` 时显示 `DisplayName Progress/Target`。
- `GamePanel_BuildingEventMessageList` 优先使用 `EventMessage`。
- 没有 `EventMessage` 时，事件列表使用 `建筑名 + DisplayName + ！`。

构造器会裁剪空白字符串，并把负数进度或目标归零。

## BuildingResourceChange

`BuildingResourceChange` 表示一次资源变化。

字段含义：

- `ItemId`：库存系统中的物品 ID。
- `Amount`：资源数量。
- `IsValid`：`ItemId` 非空且 `Amount > 0`。

格式化规则：

- `BuildingDetailUIFormatter.FormatResourceChanges()` 会把有效项显示为 `ItemId xAmount`。
- 没有有效项时显示 `无`。

构造器会裁剪物品 ID，并把负数数量归零。

## 当前建筑数据来源与去处

| 建筑 | 入口 | 数据来源 | 数据去处 |
| --- | --- | --- | --- |
| `PlayerHomeLV1` | `BuildingBase.IsResourceProviderPoint` | `isResourceProviderPoint` 序列化字段 | 住宅资源连接判定 |
| `PlayerHomeLV1` | `BuildingInventorySlotCapacityModule` | `providedSlotCount` 模块字段 | `GameSystem` 汇总后调整库存格数 |
| `ResidentialHousingLV1` | `IBuildingResourceConsumptionSource` | `foodItemId`、`currentPopulation`、荒废状态、库存扣减结果 | 库存 `TryRemoveItem`、详情 UI、失败状态 |
| `ResidentialHousingLV1` | `IBuildingTaxSource` | `taxItemId`、`currentPopulation`、满人口状态、税收进度、库存写入结果 | 库存 `TryAddItem`、详情 UI、失败状态 |
| `ResidentialHousingLV1` | `BuildingBase.GetRuntimeStatuses()` | 荒废、人口衰减、连续消耗失败、最近异常 | 状态概览、Marker、消息栏、事件消息、详情 UI |
| `ResidentialHousingLV1` | `BuildingBase.GetsOveriewMessage()` | `currentPopulation`、`maxPopulationContribution` | 状态概览、选中概览、消息栏、详情 UI |
| `LumberCabinLV1` | `IBuildingResourceProductionSource` | `woodItemId`、工人数量、生产阈值、库存写入结果 | 库存 `TryAddItem`、详情 UI、失败状态 |
| `LumberCabinLV1` | `BuildingBase.GetRuntimeStatuses()` | 工人数量、稳定工人、可用人口、最近异常 | 状态概览、Marker、消息栏、事件消息、详情 UI |
| `LumberCabinLV1` | `BuildingBase.GetsOveriewMessage()` | 当前工人、稳定工人 | 状态概览、选中概览、消息栏、详情 UI |

## 与详情接口的关系

`BuildingResourceInterfaces.cs` 中的资源接口，以及 `BuildingBase.GetRuntimeStatuses()` 的状态列表、`BuildingBase.GetsOveriewMessage()` 的概览文本，可以被 `BuildingDetailUIFormatter` 用于通用 fallback 详情。

但如果建筑实现了 `IBuildingDetailSource`，并且返回了有效 `DetailSections`，详情弹窗会优先使用建筑自己的详情数据。当前 `ResidentialHousingLV1` 和 `LumberCabinLV1` 都是这种模式。

因此：

- 资源接口仍然有价值，因为它们给通用 UI 和未来统计系统提供统一入口。
- 自定义详情可以显示更细的信息，例如成长进度、税收进度、岗位调试值。
- 不要让 UI 反向计算业务数据；建筑应该直接暴露 UI 需要的只读结果。

## 新建筑接入建议

添加新建筑时，按以下规则选择入口：

- 只是作为资源连接点：在建筑 prefab 上勾选 `isResourceProviderPoint`。
- 会消耗库存资源：实现 `IBuildingResourceConsumptionSource`。
- 会发放税收或奖励：实现 `IBuildingTaxSource`。
- 会生产库存资源：实现 `IBuildingResourceProductionSource`。
- 有异常状态需要 UI 显示：重写 `BuildingBase.GetRuntimeStatuses()`。
- 需要在列表或底部栏显示一个摘要值：重写 `BuildingBase.GetsOveriewMessage()`。

实现规则：

- `Current*` 只做预计展示，不要在 getter 中修改库存或运行时状态。
- `Last*` 只在实际成功扣减或写入后更新。
- 回合开始时清空上一回合缓存，避免 UI 显示过期成功结果。
- 失败时写入运行状态，而不是伪造资源变化。
- 非回合交互改变展示数据时，调用 `NotifyStateChanged()`。
- 物品 ID 应与 `InventoryService` 使用的 `itemId` 保持一致。
- 复杂详情放入 `IBuildingDetailSource`，不要把大量业务文本塞进 `GetsOveriewMessage()`。
