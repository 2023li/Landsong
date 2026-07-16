# 建筑扩展规则

> 本文定义建筑系统的终态领域边界。具体接入步骤见《AI_添加建筑规则》，当前资产事实见《建筑说明》，正式数值源与导表规则见《建筑正式数值表与导表工具》；美术资源见《建筑Prefab与表现资源回填指南》。

## 1. 终态模型

```text
BuildingCatalog
  -> BuildingFamilyDefinition (家族静态真相)
       -> RuntimePrefab (唯一长期实例模板)
       -> ConstructionDefinition (施工回合)
       -> LevelDefinition[LV1..LVN] (升级条件、费用、等级配置)
       -> ModuleSetDefinition (稳定模块与状态)
       -> PresentationDefinition (Stage/Level/Style -> View)
```

实例身份分为四个互不混用的维度：

| 维度 | 示例 | 语义 |
| --- | --- | --- |
| `FamilyId` | `building.residential_house` | 建筑种类、任务/蓝图/目录主键 |
| `Stage` | `Construction` / `Operational` | 生命周期；施工不是等级 |
| `Level` | 1～N | 运营数值等级 |
| `StyleId` | `tree_03` | 玩家可选视觉变体，不改变玩法家族 |

一次建造只产生一个 `InstanceId`。从施工到任意等级，实例、占格、外部引用和模块状态均保持稳定。

## 2. 分层职责

### FamilyDefinition

负责可批量查询和策划配置的数据：家族身份、名称/分类/图标、固定 footprint、放置规则、施工回合、等级、升级条件/费用、模块集和表现定义引用。

不保存实例运行时进度。

### Runtime Prefab

负责稳定 Unity 载体：根节点直接挂统一且密封的 `BuildingBase`、交互/碰撞、表现控制器及空 `ViewRoot`。它不承载家族派生脚本、等级美术、等级数值或模块模板。

### Runtime Module

负责可复用能力及该能力的唯一运行时状态。ModuleSet 在建筑初始化时克隆；等级配置改变模块参数但不替换模块对象。

### 生命周期与能力接口

`BuildingBase` 以 ModuleSet 顺序分发生命周期和自动回合：初始化、注册、放置、施工开始/完成、等级应用、点击、注销和拆除。外部系统只通过 `TryGetCapability<T>` / `GetCapabilities<T>` 查询人口、生产、消费、税收、连接等能力，不允许按家族名或具体类型分支。

家族独有流程也实现为模块。模块可以只被一个家族使用；“是否复用”不是创建派生建筑类的理由。

### Presentation

只负责施工 View、可选放置预览 View、运营 Level/Style 到纯 View Prefab 的映射。施工表现必须选择一种模式：`Single` 在整个施工期保持同一个 View；`PerTurn` 按回合与可选 Style 映射 View。两种模式不互相回退，`PerTurn` 缺图时使用统一占位表现。放置预览缺失时回退当前样式 LV1，运营等级缺失时使用同样式低等级回退。

## 3. 生命周期事务

### 放置与施工

1. 菜单提交 `FamilyId + StyleId`。
2. `BuildingService` 实例化该家族唯一 Runtime Prefab 并占格。
3. 普通建造实例进入 `Construction`；固定地图模板可显式保存为运营态。
4. 每回合原子校验并扣除本施工回合费用；失败不推进。
5. 完成最后一个回合后，同一实例进入 `Operational/LV1`、应用 LV1 配置并刷新 View。

### 原地升级

1. `BuildingUpgradeService` 查询下一等级。
2. 校验目标等级已配置、科技条件与模块提供的升级条件成立、金币/物品足够。
3. 原子扣费。
4. 修改同一实例的 `CurrentLevel`。
5. 应用目标等级配置，保留并钳制模块/家族状态。
6. 按相同 `StyleId` 刷新目标 View，并向新 View 发送 `Upgraded` 入场原因。

普通升级没有 Prefab 替换和跨对象状态迁移入口。

## 4. 状态所有权

| 状态 | 唯一所有者 |
| --- | --- |
| 家族/阶段/等级/样式/施工进度 | `BuildingRuntimeIdentity` |
| 工人、补贴、吸引力 | `BM_岗位运营` |
| 生产进度与上次产出 | `BM_资源产出` |
| 加工进度及输入输出 | `BM_资源加工` |
| 作物与生长进度 | `BuildingCropGrowthModule` |
| 居民人口/荒废/税收进度 | `BM_居民运营` |
| 市场经手价值与结算 | `BM_市场资源结算` |
| 捕鱼特殊产出状态 | `BM_稀有产出` |
| 王宫固定人口 | `BM_固定人口` |
| 树木生命与采集奖励 | `BM_树木采集` |
| 范围效果定义与数值 | `BM_空间效果源` 引用的 `BuildingSpatialEffectDefinition` |
| 通用运营经验与下级经验门槛 | `BM_运营经验` |
| 仓库容量、维护结果、累计经验与满员奖励 | `BM_仓库运营` |

新增字段前先判断是否已有同义状态。禁止镜像缓存成为第二真相；只读派生值可即时计算。

## 5. 扩展规则

### 新等级

只新增 `BuildingLevelDefinition` 与必要 LevelConfiguration。等级必须连续，费用/科技条件写在目标等级，缺美术允许沿用相同样式的较低等级 View。不得新增 Runtime Prefab 或等级脚本。

### 新模块

模块是所有建筑玩法差异的唯一扩展单元，不要求跨家族复用。必须有稳定 `BuildingModuleId`；有状态则实现模块序列化；等级参数由新的 LevelConfiguration 应用；需要生命周期、自动回合、UI 或对外能力时实现相应接口。模块不能自行修改建筑等级或占格。

模块模板和 LevelConfiguration 中所有策划可选物品必须直接引用 `ItemDefinition`，禁止暴露可手填的字符串 ItemId。模块执行库存操作、生成资源变化事件或写入存档时，再读取 `ItemDefinition.ItemId`；运行时 DTO 与存档仍只保存 ItemId。

无运行时进度的范围规则使用 `BM_空间效果源 + BuildingSpatialEffectDefinition`：ModuleSet 负责声明能力和引用关系，效果资产保存稳定 `EffectId`、效果种类、目标过滤、精确生效运营等级、最低工人、曼哈顿半径、数值、叠加规则和是否影响自身占地。策划数值由正式 Excel 的 `模块_范围效果` 单向导入，不把树木、美化、医疗、治安或邻接判断硬编码进家族类。

跨家族可复用的累计升级进度使用 `BM_运营经验 + BuildingOperationalExperienceLevelConfiguration`。经验模块必须排在岗位和维护费之后；维护支付失败会停止后续自动模块，因此经验只在本回合维护成功且工人数达到本级门槛时增长。经验属于实例状态，升级和存档都保留。

LevelConfiguration 与模块通过代码类型关联：配置的 `Apply(BuildingBase)` 调用 `GetRequiredModule<TModule>()`，再把本等级参数写入该运行时模块。`ConfigurationId` 只标识等级配置并检查重复，不负责运行时查找；`ModuleId` 负责模块唯一性、执行顺序、存档和编辑期依赖校验。新增配置时必须同步声明其目标模块依赖，不能依赖两个字符串恰好同名来完成绑定。

ModuleSet 顺序就是执行顺序。依赖模块必须排在使用者之前，例如岗位必须位于生产、作物、市场结算、运营经验和仓库运营之前，维护费必须位于运营经验或生产之前。等级配置引用的目标模块必须存在且启用，架构校验器会阻止缺失依赖。需要经验、声望等实例条件的模块实现 `IBuildingUpgradeRequirementSource`，由统一 `BuildingUpgradeService` 查询；模块不得直接修改等级。

### 新样式

样式只改变表现。先在 Presentation 声明 StyleId，编辑器会按 `全部运营等级 × Styles` 自动生成固定 ViewMapping 槽位，再逐槽回填 View；不得手工增删映射或修改其 Level/StyleId。样式不得改变 footprint、模块、费用、任务身份或升级树；若这些规则不同，应评估是否是真正的新家族。

### 新动画

动画、特效和音效全部放在目标 View 内。玩法先提交等级状态并立即换到目标 View；`BuildingView` 再按 `Normal`、`ConstructionCompleted` 或 `Upgraded` 入场原因播放对应动画。放置预览直接选择 Presentation 的独立预览 View，不再使用动画键表达。禁止创建独立 Transition Prefab，动画中断时最终 View 仍必须与真实状态一致。

### 新建筑家族

通过建筑编辑器一次创建 Family、空或非空 ModuleSet、Presentation、统一 Runtime Prefab，并加入 Catalog。创建后直接在 ModuleSet 组合模块和在 Level 中填写配置；不编写或生成建筑脚本。禁止复制某等级 Prefab 作为接入方式。

## 6. Prefab Variant 边界

Variant 只允许用于纯 View Prefab 的视觉继承，例如共享骨架、材质或动画控制器。禁止用于表达 LV1/LV2 领域等级、施工状态或模块能力。运行时实例永远来自家族唯一 Runtime Prefab。

## 7. 存档契约

实例存档保存 `FamilyId`、`InstanceId`、`Stage`、`Level`、`StyleId`、施工进度、家族状态类型/JSON 和按 ModuleId 保存的模块状态。读取时先按 FamilyId 创建唯一 Runtime Prefab，再恢复身份和状态，最后应用等级配置并刷新表现。

本架构明确不兼容重构前“等级 Prefab ID”存档。不得为了旧存档恢复等级 Prefab、模块下标或替换升级逻辑。

## 8. 静态门禁

`Landsong/Building/Validate Final Architecture` 必须验证：

- Catalog、FamilyId 和 Runtime Prefab 的一对一关系；所有 Runtime Prefab 必须直接使用统一 `BuildingBase`；
- Family、ModuleSet、Presentation 引用完整；
- 等级从 1 连续、LV1 可用、配置 ID 不重复；
- ModuleId 非空且家族内唯一；模块依赖顺序和 LevelConfiguration 的目标模块完整；
- 范围效果引用非空、EffectId 在家族内唯一、生效等级与最低工人合法、数值与目标类型合法；
- 模块与等级配置中的物品均为有效 `ItemDefinition`，并具有非空稳定 ItemId；
- Runtime Prefab 的根组件、空 ViewRoot 和无内嵌 Renderer/Animator；
- View Prefab 不含 `BuildingBase`/表现控制器/Collider；
- 旧等级 Prefab 目录和旧等级/施工脚本不存在；
- 未入 Catalog 的家族和未被家族引用的 Runtime Prefab 不存在。

缺少 View 资源是回填警告而非结构错误；其他错误不得带入主线。

## 9. 行为树边界

普通建筑禁止用行为树替代 ModuleSet、Level 或生命周期。只有需要自主决策、目标选择和长时计划的特殊建筑，才允许在一个可选模块内部使用行为树；节点调用能力接口，领域状态仍由现有模块持有。是否使用 Opsive 只影响该可选适配模块，不得让全建筑系统依赖第三方行为树运行时。
