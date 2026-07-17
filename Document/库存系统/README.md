# Landsong 库存系统

> 当前事实版本：2026-07-17。正式数值与导入流程见 [数值与导表](数值与导表.md)。
> 当前 GameData 版本：12；库存正式工作簿 SchemaVersion：5。
> 旧库存存档：不兼容、不迁移

## 1. 最终架构结论

库存系统保留“槽位 + 单槽堆叠上限”模型，所有运行时槽位由建筑提供，不再由 `GameSystem` 赠送免费槽位。

建筑系统与库存系统通过 `InventorySlotType` 建立稳定契约：

- 建筑系统决定建筑提供多少槽位、每个槽位是什么类型；
- 库存系统决定该槽位类型的固定数值规则；
- 建筑运行状态只可以提供临时运行时倍率；
- UI 根据槽位类型使用代码中的固定视觉映射；
- 库存系统不读取建筑家族、等级或建筑数值表；
- 建筑系统不保存冻库、粮库等槽位类型的固定损耗数值。

这一边界用于避免同一个规则同时散落在建筑等级配置、库存表和 UI 资产中。

## 2. 数据权属

| 数据 | 权威系统 | 正式来源 |
| --- | --- | --- |
| 建筑提供的槽位数量 | 建筑系统 | 建筑数值表 |
| 建筑提供的槽位类型 | 建筑系统 | 建筑数值表 |
| 槽位类型基础损耗倍率 | 库存系统 | 库存数值表 |
| 槽位类型对物品组的损耗倍率 | 库存系统 | 库存数值表 |
| 自动入库选择规则 | 库存系统 | `Inventory` 代码；按目标物品有效损耗率统一排序 |
| 物品基础损耗率 | 库存系统 | 库存数值表 |
| 物品与物品组定义 | 库存系统 | 库存数值表 |
| 缺工、维护失败等临时损耗倍率 | 建筑运行状态 | 建筑模块代码和运行时状态 |
| 科技产生的全局损耗倍率 | 科技系统 | 建筑正式工作簿中的科技全局 Buff 表 |
| 槽位颜色、边框、覆盖效果 | UI | `GamePanel_InventorySlot` 代码 |
| 住宅人口、饮食需求与生活质量参数 | 建筑系统 | 建筑数值表 |

关键规则：

> “冻库格使所有食物的损耗率变为基础结果的 80%”属于库存系统；“某建筑提供 3 个冻库格”属于建筑系统。

## 3. 核心运行时模型

### 3.1 槽位身份

每个槽位使用以下稳定身份：

```text
StorageSlotId = ProviderBuildingInstanceId + ":" + LocalSlotId
```

建筑升级不会替换建筑实例，因此已有槽位身份可以跨等级、存档和读档保持稳定。

### 3.2 建筑提供契约

建筑模块通过 `IBuildingInventorySlotProvider` 返回 `InventorySlotProvision`。Provision 只包含：

- 来源建筑实例 ID；
- 来源家族 ID 和显示名；
- 建筑内本地槽位 ID；
- `InventorySlotType`；
- 可选运行时损耗倍率。

Provision 不包含：

- 固定基础损耗倍率；
- 物品组修正；
- 自动存放优先级；库存系统不再维护该独立字段；
- UI Prefab、Sprite 或表现资产。

这些固定规则统一由 `InventorySlotTypeCatalog` 解析。

### 3.3 槽位类型

当前稳定类型：

| 枚举 | 用途 |
| --- | --- |
| `简陋库存` | 基础仓储；也是运行时和 UI 未命中时的默认类型 |
| `普通库存` | 标准仓储 |
| `高级库存` | 高级仓储 |
| `冻库` | 食物专业保存槽位 |
| `粮库` | 粮食专业槽位 |

枚举成员直接使用中文，不再保留 `Default` 或建筑专属类型。`InventorySlotType` 是跨建筑、库存和 UI 的稳定代码契约；新增类型必须同时修改正式工作簿、生成资产、Catalog、建筑可选项和 UI 映射。

## 4. 损耗计算

单槽最终损耗率：

```text
最终损耗率
= 物品基础损耗率
× 槽位类型基础倍率
× 所有命中的物品组倍率
× 建筑运行时倍率
× 科技全局倍率
```

单槽实际损耗：

```text
floor(槽内数量 × 最终损耗率)
```

每个槽位分别计算和向下取整，不按同种物品总量合并计算。

科技全局倍率由 `TechnologyGlobalBuffService` 在结算时动态查询。未解锁相关科技时倍率为 `1.00`；科技完成后立即参与之后的实际损耗和经济预测，不复制到物品、槽位类型或建筑等级配置中。

多个全局损耗 Buff 使用乘法叠加。例如两个科技分别降低 10% 和 20%，最终科技倍率为：

```text
0.90 × 0.80 = 0.72
```

“降低 10%”表示将当前损耗结果乘 `0.90`，不是从损耗率中直接减去 10 个百分点。

### 冻库示例

正式规则：

```text
冻库 基础倍率 = 1.00
冻库 对 food 物品组倍率 = 0.80
蔬菜基础损耗率 = 0.05
建筑运行正常倍率 = 1.00
未解锁损耗科技时的全局倍率 = 1.00

最终损耗率 = 0.05 × 1.00 × 0.80 × 1.00 × 1.00 = 0.04
```

因此冻库格中所有属于 `food` 或其子组的物品，其损耗率都是普通结果的 80%。

物品组支持父子关系。`food.vegetable` 的父组是 `food`，蔬菜、胡萝卜和白菜命中 `food.vegetable` 时也会命中 `food` 的冻库规则。

### 轮子科技示例

`TN_4_5_轮子` 激活 `buff.inventory.wheel_preservation`，使所有物资的库存自然损耗结果乘 `0.90`。

简陋库存中 100 个基础损耗率为 5% 的蔬菜：

```text
最终损耗率 = 0.05 × 1.00 × 1.00 × 1.00 × 0.90 = 0.045
实际损耗 = floor(100 × 0.045) = 4
```

这里的“所有物资”只影响库存系统的自然损耗，不影响建筑施工消耗、维护费、生产投入或玩家主动消费。

## 5. 建筑运行异常与物理槽位

物理槽位和建筑正常运营状态是两件事。

仓储建筑（当前为仓库与粮仓）已经建成后：

- 基础槽位始终存在；
- 工人不足不会移除已有槽位；
- 维护费不足不会移除已有槽位；
- 缺工时使用建筑模块配置的临时损耗倍率；
- 维护失败时叠加另一层临时损耗倍率；
- 维护失败仍可降低本建筑岗位吸引力；
- 已经解锁的奖励槽位作为仓库实例状态保存。

这可以避免工人数或维护状态波动时，装有物品的槽位突然消失。

当前仓库临时倍率：

| 状态 | 倍率 |
| --- | --- |
| 正常 | `1.00` |
| 工人不足 | `1.25` |
| 维护失败 | `1.50` |
| 同时缺工且维护失败 | `1.25 × 1.50` |

这些倍率属于建筑运行状态，不进入库存正式数值表。若以后需要策划化，应进入建筑表，而不是槽位类型表。

当前粮仓由建筑系统的 `building.granary` 提供槽位：LV1/LV2/LV3 分别提供 2/4/6 个 `粮库`，LV3 达到 5 工人后永久增加 1 个。库存系统只接收这些槽位的来源、类型、数量与运行时倍率；`粮库` 对食物的固定损耗规则仍由库存数值表统一维护。

## 6. 自动存放

自动存放不再使用独立的类型优先级。每次 `Inventory.Add` 都把“已有同物品且未满的槽位”和“空槽位”合并为同一候选集合，并按以下顺序处理：

1. 目标物品在该槽位中的有效损耗率从低到高；
2. 有效损耗率相同时，先合并已有同物品堆叠，减少槽位碎片；
3. 仍相同时按稳定 `StorageSlotId` 排序，保证结果可复现。

有效损耗率用于比较的部分为：

```text
物品基础损耗率 × 槽位类型基础倍率 × 命中的物品组倍率 × 建筑运行时倍率
```

科技全局倍率对同一次入库的所有候选槽位相同，不改变相对顺序，因此不参与候选排序。已满槽位、装有其他物品的槽位和无效槽位不会进入候选集合。

当前食物在建筑正常运行时的正式倍率顺序为：`粮库 0.60 < 高级库存 0.70 < 冻库 0.80 < 普通库存 0.85 < 简陋库存 1.00`。因此有粮库时食物优先进入粮库；粮库不存在或已满时自然回退到下一个实际损耗更低的可用槽位。非食物不命中粮库和冻库的 `food` 修正，只按各类型基础倍率比较。

若以后需要“某类物品禁止进入某种槽位”，应新增库存系统的槽位接纳规则，而不是恢复一个与损耗率无关的优先级，也不能让建筑模块判断具体物品。

## 7. 视觉表现

库存槽位视觉不进入 Excel，也不再使用 `InventorySlotPresentationDefinition`。

`GamePanel_Inventory` 始终实例化统一的 `仓库格子预制体`。该 Prefab 必须预先挂载 `GamePanel_InventorySlot`，不允许运行时临时添加组件。`GamePanel_InventorySlot.ApplySlotTypePresentation` 根据 `InventorySlotType` 设置：

- 背景色；
- 内容色；
- 覆盖层色；
- 已存在覆盖 Sprite 的启用状态。
- Inspector 中配置的槽位类型 Root 子节点。

`槽位类型 Root 映射` 的匹配顺序是“精确 `InventorySlotType` -> `简陋库存` 回退”。系统先关闭全部已登记 Root，再只启用命中的 Root。每种类型只能配置一次，且必须有唯一的 `简陋库存` 映射。Root 必须是槽位 Prefab 的非空直属子节点，所有类型 Root 互为同级，不能引用槽位根自身。物品图标和数量文本应放在这些 Root 外部，由不同类型共用。

这样不同格子的视觉仍然由 UI Prefab 中的类型映射决定，但库存数值表不会持有 GameObject、Prefab 或 Sprite 引用。新增槽位类型美术时，在统一槽位 Prefab 中增加视觉 Root 并补充映射，不得把表现引用塞入建筑等级或槽位数值定义。

## 8. 物品与物品组

`ItemDefinition` 是物品的正式 Unity 定义，核心字段包括：

- 稳定 `ItemId`；
- 显示名、描述、图标；
- 堆叠规则；
- 基础价值；
- 每回合基础损耗率；
- 所属 `ItemGroupDefinition`；
- 食物属性。

策划配置中的物品引用使用 `ItemDefinition`。只有库存 API、运行时 DTO、字典键和存档保存从资产派生出的 `ItemId`。

`ItemGroupDefinition` 用于：

- 冻库等槽位类型规则；
- 住宅的分类消费；
- 未来药材、木材、矿物等替代需求。

库存系统数值表只定义组本身和物品归属；具体建筑消费哪个组由建筑数值表配置。

## 9. 正式数值源与导入顺序

库存正式表：

```text
ConfigSource/库存系统/库存系统数值表.xlsx
```

建筑正式表：

```text
ConfigSource/Buildings/建筑数值策划表.xlsx
```

推荐导入顺序：

1. 先导入库存表，创建或更新物品、物品组、槽位类型和两个 Catalog。
2. 再导入建筑表，解析建筑使用的 `InventorySlotType`、住宅引用的物品组和科技全局库存 Buff。
3. 执行 `Landsong/Building/Validate Final Architecture`。

库存导表器不读取或写入任何 `BuildingFamilyDefinition`。建筑导表器只引用库存已生成的 `ItemDefinition` 和 `ItemGroupDefinition`。

## 10. 存档规则

存档保存：

- 稳定槽位 ID；
- 来源建筑实例 ID；
- 建筑内本地槽位 ID；
- 槽内物品 ID 和数量。

槽位类型和固定数值规则由当前建筑配置与 `InventorySlotTypeCatalog` 重新解析，不保存表现资源引用。

来源建筑的槽位中仍有物品时，正常拆除会被拒绝。玩家必须先清空对应槽位。

## 11. 槽位类型变更标准

当前可供建筑使用的正式集合为 `简陋库存 / 普通库存 / 高级库存 / 冻库 / 粮库`。建筑升级只改变类型和数量，不生成建筑专属枚举。

未来确实出现无法由这五种规则表达的新库存领域能力时，必须先做架构评审，再一次性修改枚举、库存表、建筑表、类型资产、Catalog、UI Root 映射及验证用例。禁止为每个建筑等级复制相同的槽位固定数值，也禁止让库存导表器按 `FamilyId + Level` 修改建筑资产。

## 12. 概览面板与经济预测

### 12.1 UI 所有权

游戏内概览统一使用以下层级：

```text
UIPanel_Game
└─ GamePanel_Overview
   ├─ GamePanel_EconomyForecast
   └─ GamePanel_BuildingStatusOverview
```

`UIPanel_Game` 只负责打开或关闭整个概览。`GamePanel_Overview` 是唯一的子面板总控，负责：

- 绑定建筑概览/经济概览两个 Toggle 和同一个 `ToggleGroup`；
- 保证任意时刻只激活一个子面板；
- 记忆上次页签或使用配置的默认页签；
- 处理概览关闭按钮；
- 在切页时刷新当前子面板。

`GamePanel_BuildingStatusOverview` 和 `GamePanel_EconomyForecast` 只负责内容，不保存父面板开关按钮，也不互相切换。

### 12.2 经济预测范围

`EconomyForecastService.ForecastTurns` 当前支持 `1～10` 回合，面板默认显示未来 5 回合。预测在 `Inventory.CreateSimulation()` 创建的副本上执行，不会修改真实库存或建筑状态。

每个预测回合遵循实际结算顺序：

1. 按 `BuildingService.Buildings` 的稳定顺序模拟建筑；
2. 所有建筑消费完成后结算市场经手价值；
3. 最后逐库存格计算自然损耗。

当前纳入预测的内容：

- 施工每回合成本、奖励、资源连接、阻塞和预计竣工；
- 周期生产和加工进度，例如每 3 回合产出；
- 农田成熟倒计时、手动收获提示、自动收获成本及产出范围；
- 居民分类饮食、人口变化、税收、饮食多样性和生活质量；
- 市场按预计经手资源基础价值结算金币；
- 稀有产出和随机收获的最小/最大范围；
- 每回合资源消耗、最小/最大产出、逐格损耗、缺口、入库溢出和期末数量。

随机产出采用“最小值推进库存、最大值只显示潜在范围”的保守模型。未来工人数、建筑连接、科技和玩家操作默认保持当前状态；手动收获只显示计划事件，不擅自计入库存。预测期内刚竣工建筑的新运营能力从下一次重新预测开始计算，面板会明确显示这些假设和限制。

### 12.3 经济子面板字段契约

`GamePanel_EconomyForecast` 由 Inspector 显式绑定五个文本区域：

- `summaryText`：预测回合数、库存格占用和最早风险；
- `resourceTimelineText`：每种资源的当前数量与 T+1～T+N 时间线；
- `scheduledEventsText`：施工、生产、加工、收获、人口、税收和市场事件；
- `residentialForecastText`：居民人口、饮食满足、种类、饮食分和生活质量；
- `warningText`：阻塞原因、模型假设和未展开说明。

字段为空时对应区块安全跳过；正式 Prefab 应全部绑定。经济子面板激活期间监听库存、建筑集合、建筑状态和回合推进；回合结算过程中抑制中间刷新，统一在 `TurnAdvanced` 后重算。

## 13. 主要代码位置

- 类型契约：`Assets/Landsong/Scripts/GameSystem/Inventory/InventoryModels.cs`
- 类型定义：`Assets/Landsong/Scripts/GameSystem/Inventory/InventorySlotTypeDefinition.cs`
- 类型目录：`Assets/Landsong/Scripts/GameSystem/Inventory/InventorySlotTypeCatalog.cs`
- 库存与损耗：`Assets/Landsong/Scripts/GameSystem/Inventory/Inventory.cs`
- 科技全局 Buff：`Assets/Landsong/Scripts/TechnologySystem/TechnologyGlobalBuffDefinition.cs`
- 全局 Buff 目录与查询：`Assets/Landsong/Scripts/TechnologySystem/TechnologyGlobalBuffCatalog.cs`
- 建筑库存模块：`Assets/Landsong/Scripts/BuildingSystem/BuildingModules.cs`
- 仓库运营模块：`Assets/Landsong/Scripts/BuildingSystem/BuildingWarehouseModule.cs`
- 槽位 UI：`Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_InventorySlot.cs`
- 概览总控：`Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Overview.cs`
- 经济预测领域服务：`Assets/Landsong/Scripts/GameSystem/Inventory/EconomyForecastService.cs`
- 经济预测子面板：`Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_EconomyForecast.cs`
- 建筑概览子面板：`Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_BuildingStatusOverview.cs`
- 库存导表：`Assets/Landsong/Scripts/Editor/InventoryNumericImport/`
- 建筑导表：`Assets/Landsong/Scripts/Editor/BuildingNumericImport/`
