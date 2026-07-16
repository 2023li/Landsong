# AI 添加建筑规则

> 适用版本：2026-07-15 建筑终态架构。本文是新增或修改建筑时的强制执行规范。

## 1. 唯一合法形态

一个建筑家族必须且只能拥有：

- 一个 `BuildingFamilyDefinition`：家族 ID、固定占地、放置费用、施工阶段、运营等级与升级条件的唯一静态真相。
- 一个 `BuildingModuleSetDefinition`：该家族全部玩法能力的模块模板；模块可以只服务一个家族。
- 一个 `BuildingPresentationDefinition`：施工、等级和样式对应的纯表现资源。
- 一个轻量 Runtime Prefab：稳定碰撞/交互、直接使用统一具体 `BuildingBase`、`BuildingPresentationController` 和空 `ViewRoot`。

`BuildingBase` 已被密封。不得创建家族行为脚本或任何替代建筑宿主；建筑之间的全部玩法差异都进入 ModuleSet。

施工、LV1～LVN 和样式都是同一个运行时实例的状态。普通升级只修改 `CurrentLevel`，不得销毁旧实例或替换 Prefab。

## 2. 先读这些入口

- `Assets/Landsong/Scripts/BuildingSystem/BuildingBase.cs`
- `Assets/Landsong/Scripts/BuildingSystem/BuildingFamilyDefinition.cs`
- `Assets/Landsong/Scripts/BuildingSystem/BuildingModuleSetDefinition.cs`
- `Assets/Landsong/Scripts/BuildingSystem/BuildingPresentationDefinition.cs`
- `Assets/Landsong/Scripts/BuildingSystem/BuildingPresentationController.cs`
- `Assets/Landsong/Scripts/BuildingSystem/BuildingUpgradeService.cs`
- `Assets/Landsong/Scripts/BuildingSystem/BuildingLevelConfigurations.cs`
- `Assets/Landsong/Scripts/Editor/BuildingArchitectureValidator.cs`
- `Document/建筑系统/建筑扩展规则README.md`
- `Document/建筑系统/建筑Prefab与表现资源回填指南.md`

正式资产目录：

- 家族：`Assets/Landsong/Objects/SO/Buildings/Families`
- 模块集：`Assets/Landsong/Objects/SO/Buildings/Modules`
- 范围效果：`Assets/Landsong/Objects/SO/Buildings/SpatialEffects`
- 表现定义：`Assets/Landsong/Objects/SO/Buildings/Presentations`
- Runtime Prefab：`Assets/Landsong/Objects/Prefabs/BuildingsRuntime`
- View Prefab：按《建筑Prefab与表现资源回填指南》建立在独立目录，不得放入 Runtime Prefab 内。

## 3. 数据放在哪里

### `BuildingDefinition`

只保存家族级、不随实例和等级变化的静态数据：

- `FamilyId`、名称、分类、图标；
- 固定 footprint、地形要求、移动阻力；
- 放置时一次性费用；
- 蓝图初始锁定、菜单排序、建造数量上限；
- 是否完成开发。

正式分类只允许：`人口`、`农业`、`工业`、`经济`、`科研`、`市政`、`军事`、`交通`、`装饰`、`奇观`。道路与运输设施必须包含 `交通` 分类，因为道路专用放置逻辑以该标记判断。不得恢复旧的居住、生产、仓储、后勤、通用、美化、奇迹、神迹、道路分类。

占地已经被产品规则冻结为全等级不变，所以占地只存在一份。禁止在等级配置或 View Prefab 中再次表达占地。

`RequiredTerrainKeys` 中的每个 Key 都要求 footprint 的每一个格子具备；采石场使用 `陆地 + 石矿`，因此 3×3 的 9 格必须全部在石矿覆盖层上。`RequiredAnyFootprintTerrainKeys` 只表达“整个 footprint 至少出现一次”的其他机制，不得用于采石场。

### `BuildingConstructionDefinition`

施工是生命周期阶段，不是 LV0。每个家族都必须有施工定义：

- `turns` 的每一项代表一个施工回合；
- 每回合分别保存非负的 `Costs` 与 `Rewards`；二者由一次库存交换事务原子结算，任一侧无法满足都不推进；
- Presentation 可选择整个施工阶段共用一个 View，或让每回合拥有独立 View；表现模式和映射不写入本数值定义，也不进入 Runtime Prefab；
- 全部回合完成后，同一实例进入 `Operational/LV1`；
- 空费用回合仍是一个有效施工回合；
- 放置费用和逐回合施工费用是两套独立费用，策划必须明确是否都需要。

### `BuildingLevelDefinition`

- 等级必须从 LV1 连续递增，不允许缺号。
- LV1 必须启用。
- `configured = false` 表示该等级内容尚未开放，升级服务不可进入该等级。
- 升入本级所需科技条件和金币/物品费用写在该级定义上。
- 数值差异通过 `BuildingLevelConfigurationBase` 子类应用到稳定模块。
- 升级保留人口、工人、生产进度、种植状态和家族状态；配置应用后只做合法范围钳制。

### `BuildingModuleSetDefinition`

能力及其运行时状态由模块唯一持有，例如：

- `workforce`：岗位；
- `production`：资源生产；
- `maintenance`：通用固定维护费；必须位于 `production` 前，支付失败时中止本回合后续自动模块；
- `operational_experience`：通用累计运营经验和升级门槛；依赖 `workforce`，有维护费时必须排在 `maintenance` 后；
- `processing`：资源加工；
- `inventory.capacity`：库存容量；
- `warehouse.operation`：仓库的动态库存、维护费、经验升级和维护失败吸引力惩罚；依赖 `workforce` 且排在其后；
- `technology.points`：科技点；
- `crop`：作物；
- `spatial_effect`：范围效果源；配置资产使用稳定 `EffectId`，负责美化、医疗、治安或生产百分比等曼哈顿范围规则；
- `population.fixed`：王宫等固定人口；
- `residential.operation`：居民人口、消费、增长、税收和荒废；
- `market.resource_accounting`：市场经手价值；
- `production.rare_bonus`：概率型稀有产出；
- `harvestable.tree`：树木采集。

模块类必须带稳定的 `[BuildingModuleId("...")]`。同一家族内 ModuleId 不得重复。需要存档的模块实现 `IBuildingModuleStateSerializer`，存档按 ModuleId 恢复，不能依赖数组下标。

ModuleSet 数组顺序就是生命周期和自动回合执行顺序，依赖必须排在使用者之前。模块通过可选生命周期接口参与初始化、注册、施工、等级、回合、点击、注销和拆除；通过能力接口供外部系统查询。禁止运行时用 `EnsureModule` 临时创造配置缺失的模块；缺模块应尽早报错。

范围效果由 `BM_空间效果源` 引用一个或多个 `BuildingSpatialEffectDefinition`。模块表达“建筑拥有范围效果能力”，效果资产表达 `EffectId + 类型 + 目标 + 生效运营等级 + 最低工人 + 半径 + 数值 + 叠加规则 + 是否影响自身占地`。结构先在 Unity 创建，策划数值写入正式 Excel 的 `模块_范围效果`。运营前、拆除中、已移除建筑、等级不符或工人未达门槛时不得提供效果；放置预览按当前等级显示可配置的覆盖范围。

医疗、治安与美化都属于格子值，`TargetFilter` 必须为 `cell`。需要“同格只取最高”的规则时使用 `HighestValue`；同一建筑可挂多条相同数值、不同门槛/半径的 Definition，较高工人档位会自然扩展覆盖范围，不会在重叠格重复叠加。

当前树木规则固定为：`beauty.tree`、`Beauty/Cell`、曼哈顿半径 1、每格 +1、排除自身占地、`HighestValue`。因为树木占地为 1×1，所以只影响正交四邻格；多棵树或雕塑覆盖同一格时，在 `HighestValue` 通道中只取最高值。

当前雕塑规则固定为：`building.sculpture + beauty.sculpture`，参数同样是 `Beauty/Cell`、半径 1、每格 +1、排除自身占地、`HighestValue`。`sculpture_goddess` 与 `sculpture_deer` 是同一家族的表现 Style，不允许为两种雕塑复制玩法家族或模块数据。

### 模块与能力接口

外部系统使用 `BuildingBase.TryGetCapability<T>` 或 `GetCapabilities<T>`，不能判断 `PlayerHome`、`Market` 等具体类型。即使某段流程当前只有一个家族使用，也写成模块，例如：

- 居民房的人口增长、食物失败、税收与荒废；
- 市场的资源经手价值结算；
- 捕鱼小屋的特殊鱼概率。

不得新增 `XXX.cs`、`XXXLV2.cs` 或 `XXXUnderConstruction.cs`。若新需求不能放入现有模块，就新增一个职责单一的模块与必要 LevelConfiguration。

模块和 LevelConfiguration 暴露给策划的物品字段统一使用 `ItemDefinition` 资产引用，不使用可编辑字符串 ItemId。只有库存 API、资源变化 DTO、字典键与存档数据使用由该资产派生出的 `ItemDefinition.ItemId`。

## 4. Runtime Prefab 规范

根节点必须包含：

- 恰好一个统一 `BuildingBase`，组件的运行时类型必须等于 `typeof(BuildingBase)`；
- `BuildingPresentationController`；
- 一个空的 `ViewRoot`（可带 `BuildingView`，但不能预埋等级美术）。

Runtime Prefab 中禁止：

- 施工、LV1～LVN 的禁用美术子节点；
- `SpriteRenderer`、`Animator` 或等级动画资源；
- 另一个 `BuildingBase`；
- 内联等级数值或模块模板；
- 用 Prefab Variant 表示领域等级。

Runtime Prefab 负责稳定身份、交互和运行时挂点；纯美术 View Prefab 负责渲染和动画。两者不可混合。

## 5. 表现、缺资源与替换

`BuildingPresentationDefinition` 可以直接引用 View Prefab，也可以引用 Addressable。当前解析规则是：

1. 施工态先读取 `ConstructionViewMode`：`Single` 始终读取一个 `ConstructionView`；`PerTurn` 按当前回合和 StyleId 查 `ConstructionViewMappings`，未命中只回退同回合空 StyleId 映射，再缺失就显示统一占位表现。两种模式不互相回退。
2. 放置预览优先查 `PlacementPreviewView`；未配置时按当前 `StyleId` 回退到 LV1 运营 View。
3. 运营态按相同 `StyleId` 查不高于当前等级的最高映射；因此 LV3 没美术时可安全沿用 LV1/LV2。
4. 有样式的建筑绝不静默切到另一个样式。
5. 无样式建筑查不到等级映射时使用 `DefaultOperationalView`。
6. 仍缺失时使用统一占位表现，玩法等级和数值不受影响。

运营 `ViewMapping` 是派生结构，不是人工数组：等级轴来自 Excel 导入的全部 Family Levels，样式轴来自 Presentation Styles；无 Style 时每级一个默认槽位，有 Style 时每个 `Style × Level` 一个槽位。编辑器中只允许填写槽位 View，禁止手动增删或修改 Level/StyleId。新增等级自动补空槽位；删除等级或 Style 时，仍含 View 的待删除槽位必须阻止操作，禁止静默丢失引用。

升级时表现控制器立即切换到目标等级 View，再向 `BuildingView` 发送 `Upgraded` 入场原因；施工完成使用 `ConstructionCompleted`，读档和普通刷新使用 `Normal`。动画、特效和音效由目标 View 自己持有，禁止创建独立 Transition Prefab。后续补齐美术时只新增或替换映射，不改存档、Runtime Prefab 和玩法数据。

树木是 `building.tree` 的八个可选样式 `tree_01`～`tree_08`，不是八个建筑家族。玩家在建造菜单选择样式，放置请求把 `StyleId` 写入实例。

## 6. 新增建筑步骤

标准入口是 Unity 菜单 `Landsong/Building/建筑编辑器`，详细字段和失败恢复见 [建筑编辑器窗口规划与使用.md](建筑编辑器窗口规划与使用.md)。除修复/迁移任务外，不再手工拼装整套新家族资产。

1. 在窗口定义稳定 `FamilyId`，格式为 `building.<snake_case>`；上线后不得因改名或升级而改变。
2. 选择基础、岗位生产或岗位维护生产模板；模板只决定初始 ModuleSet，不决定运行时类型。
3. 配置固定占地、放置费用、施工回合、初始等级，明确选择单一或逐回合施工视图模式，并填写可缺省的 Presentation View。
4. 先执行“校验创建参数”，再执行“创建完整建筑资产组”。窗口一次性创建 ModuleSet、Family、Presentation、轻量 Runtime Prefab 并登记标准 Catalog；不会生成脚本，也没有等待编译续跑阶段。
5. 在项目根目录的正式 Excel `ConfigSource/Buildings/建筑数值策划表.xlsx` 增加公共数据、连续等级、费用和所需配置专表行；未完成高等级填写“开放=否”。
6. 科技、任务和蓝图引用 FamilyId/唯一 Runtime Prefab，不引用等级 Prefab。
7. 执行 `Landsong/Building/建筑数值导表工具` 的全表校验和导入；未知 FamilyId、缺模块或任何数值错误都必须先修复。
8. 更新美术回填清单，并执行窗口“架构校验”或 Unity 菜单 `Landsong/Building/Validate Final Architecture`；美术缺失警告按回填计划处理。

## 7. 跨系统与存档

- 蓝图解锁单位是 FamilyId。
- 任务默认按 FamilyId 统计；等级条件额外比较 `CurrentLevel`。
- 科技可以作为“升入某级”的条件，也可以解锁蓝图。
- 存档身份是 `FamilyId + InstanceId + Stage + Level + StyleId + ConstructionProgress`。
- 建筑核心身份使用固定字段；玩法状态全部按稳定 ModuleId 保存。
- 不保存 View Prefab、等级 Prefab 或模块数组下标。
- 本轮重构不兼容旧建筑存档，不得重新引入兼容分支。

## 8. 绝对禁止

- 每级一个 Runtime Prefab、BuildingId 或脚本。
- 普通 LV 升级调用 Prefab 替换。
- 把施工视为 LV0。
- 把全部美术塞进一个巨型 Prefab 后用显隐切换。
- 高等级仅复制部分低等级能力。
- 在多个模块或 `BuildingBase` 中保存同义状态。
- 创建普通建筑的 `BuildingBase` 派生类，或用具体类名驱动跨系统逻辑。
- 用树种、皮肤或朝向制造新的建筑家族 ID。
- 在 View Prefab 中挂 `BuildingBase`、碰撞或玩法脚本。
- 绕开 `BuildingService` 直接占格/注册，或绕开 `BuildingUpgradeService` 修改等级。

## 9. 提交前检查

- 一个家族、一个 ModuleSet、一个 Presentation、一个 Runtime Prefab；不生成家族脚本。
- Runtime Prefab 只有一个直接类型为 `BuildingBase` 的根组件，家族双向引用正确，`ViewRoot` 为空。
- footprint 只在家族定义中出现且全等级不变。
- LV1 可用，等级连续，未完成等级关闭。
- 升级条件和费用表示“升入目标等级”。
- 状态只有一个所有者，所有可持久模块都有稳定 ID。
- 缺美术时回退正确，不影响真实等级和功能。
- 正式 Excel 全表导入通过；建筑校验器零错误；不得让导入字段在 Excel 与 SO 中分叉。
