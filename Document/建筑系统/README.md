# Landsong 建筑系统

> 当前事实版本：2026-07-17
> 当前规模：14 个 Family、14 个 ModuleSet、14 个 Presentation、14 个唯一 Runtime Prefab。

## 1. 文档入口

| 任务 | 文档 |
| --- | --- |
| 理解终态架构、生命周期、状态与扩展边界 | 本文 |
| 查询所有正式建筑和当前内容缺口 | [建筑目录](建筑目录.md) |
| 创建新家族、选择模块、使用编辑器 | [新增建筑与编辑器](新增建筑与编辑器.md) |
| 修改 Excel、导表、ID 和数据权属 | [数值与导表](数值与导表.md) |
| 回填 View Prefab、图标和动画 | [表现资源](表现资源.md) |
| 修改占地、地形、连接、Overlay 或初始建筑 | [地图系统](../地图系统/README.md) |
| 修改槽位类型固定规则或库存自动存放 | [库存系统](../库存系统/README.md) |

## 2. 唯一合法形态

普通建筑固定为：

```text
BuildingFamilyDefinition
  + BuildingModuleSetDefinition
  + BuildingPresentationDefinition
  + lightweight Runtime Prefab
  + shared sealed BuildingBase
```

施工与 LV1～LVN 是同一实例的阶段/等级状态：

```text
Construction -> Operational/LV1 -> LV2 -> ... -> LVN
```

禁止：

- 每个等级一个 Runtime Prefab；
- `XXXLV1`、`XXXLV2`、`XXXUnderConstruction` 等家族/等级脚本；
- 升级时替换 Prefab；
- 把施工和全部等级美术塞进 Runtime Prefab 的禁用子节点；
- UI 或其他系统按具体建筑类分支；
- 用行为树替代固定生命周期和模块契约。

## 3. 分层职责

| 层 | 拥有内容 | 不拥有 |
| --- | --- | --- |
| `BuildingDefinition` | FamilyId、名称、分类、图标、占地、地形、放置费、蓝图与菜单规则 | 实例状态、等级美术 |
| `BuildingConstructionDefinition` | 逐回合施工消耗/产出 | 施工视觉对象 |
| `BuildingLevelDefinition` | Level 是否开放、升级条件/费用、LevelConfiguration | 模块运行时状态 |
| `BuildingModuleSetDefinition` | 模块类型、稳定 ModuleId、执行顺序和默认配置 | 第二套家族/等级数据 |
| Runtime Prefab | `BuildingBase`、碰撞、交互、空 ViewRoot、表现控制器 | 施工/等级 Sprite、Animator、内联玩法分支 |
| Runtime Module | 一项能力的配置、状态、生命周期、存档、能力接口和状态说明 | 修改占地、替换实例、跨领域全局真相 |
| Presentation | 放置预览、施工模式、Level/Style → View、过渡配置 | 玩法数值 |
| View Prefab | SpriteRenderer、Animator、粒子、音效挂点 | `BuildingBase`、Collider、玩法脚本 |

## 4. BuildingBase

`BuildingBase` 是密封的运行时 Facade，统一负责：

- 家族定义和稳定实例身份；
- 格子位置、占用 ID 与生命周期阶段；
- 从 ModuleSet 克隆模块；
- 初始化、注册、施工、回合、点击、升级、拆除和注销生命周期分发；
- 能力查询；
- 模块状态捕获/恢复；
- 表现控制器与 ViewRoot 协调。

家族差异不能通过继承 `BuildingBase` 实现。新增玩法能力应先判断是否可复用现有 Module；确有新状态机时新增职责单一的 Module 和必要的 LevelConfiguration。

## 5. 模块与等级配置

Module 是运行时能力与状态，LevelConfiguration 是某等级如何重配该 Module 的数据。

LevelConfiguration 通过 C# 类型明确找到目标 Module，例如生产配置取得 `BM_资源产出`；匹配不依赖可编辑字符串。`ModuleId` 用于：

- ModuleSet 内唯一性；
- 生命周期/自动回合顺序；
- 存档状态 Key；
- 编辑器依赖和架构校验。

ModuleSet 顺序就是执行顺序。常见组合：

```text
生产：workforce -> maintenance -> production
医院/警局：workforce -> maintenance -> operational_experience -> spatial_effect
仓库/粮仓：workforce -> warehouse.operation
```

依赖模块必须在消费者之前。配置引用的模块必须存在并启用；不允许运行时 `EnsureModule` 临时补齐错误资产。

## 6. 生命周期事务

### 放置与施工

1. UI/放置器创建放置请求。
2. `BuildingPlacementEvaluator` 使用完整占地、地形、数量、蓝图、连接与资源规则评估。
3. 确认后原子扣除放置费、占用格子并创建同一个 Runtime 实例。
4. 实例进入 Construction；每回合原子结算该施工回合的消耗与产出。
5. 全部施工回合完成后切换为 Operational/LV1，不替换 GameObject。

### 原地升级

1. `BuildingUpgradeService` 读取目标等级。
2. 同时校验目标 Level、科技/条件、模块升级条件和费用。
3. 全部满足后原子扣费并修改 `runtimeIdentity.Level`。
4. 按目标 LevelConfiguration 重配现有 Module。
5. 模块状态保留，只对超过新合法范围的值做钳制。
6. Presentation 异步切换 View，玩法等级不依赖美术是否存在。

占地永不随等级变化。需要不同占地或完全不同交互状态机的内容应建成不同家族或独立系统。

## 7. 状态所有权

| 状态 | 所有者 |
| --- | --- |
| FamilyId、固定占地、成本、等级结构 | FamilyDefinition / 正式 Excel |
| InstanceId、Stage、Level、StyleId、施工进度 | `BuildingRuntimeIdentity` |
| 工人、岗位吸引力、补贴 | `BM_岗位运营` |
| 生产周期与进度 | 生产 Module |
| 作物、种植、生长、收获 | `BuildingCropGrowthModule` |
| 仓库经验、维护失败、奖励槽位 | `BM_仓库运营` |
| 通用运营经验 | `BM_运营经验` |
| 居民人口、饮食、生活质量、税收、荒废 | 住宅 Module |
| 范围效果定义 | `BuildingSpatialEffectDefinition` |
| 范围效果当前贡献 | 空间效果 Module + 地图查询 |
| View 与动画 | Presentation / View Prefab |

同一状态不得同时存在于家族脚本、模块、UI 和存档多个结构中。

## 8. 能力接口

外部系统使用 `BuildingBase.TryGetCapability<T>` / `GetCapabilities<T>`，或建筑系统提供的公共 Utility。常见能力包括：

- 人口与岗位；
- 资源生产/消费/提供点；
- 库存槽位 Provision；
- 科技点；
- 维护费与运营经验；
- 范围效果；
- 作物与只读预测。

库存、任务、UI、地图和回合系统不得判断 `PlayerHome`、`Warehouse` 等具体类型；仓库与粮仓共享同一模块正是此规则的标准案例。

## 9. 科技与蓝图

- 蓝图科技条件唯一存放在 `BuildingDefinition.AutomaticBlueprintUnlockCondition`。
- 等级科技条件唯一存放在目标 `BuildingLevelDefinition.UpgradeCondition`。
- `BuildingBlueprintService` 是蓝图运行时真相并负责存档。
- `BuildingCatalog` 主动把科技 → 蓝图/等级内容注入 `TechnologyUnlockContentRegistry`。
- 科技 SO 不再保存重复的“解锁建筑蓝图”完成效果。

## 10. 表现与 Style

Style 是同一家族内玩家可选择的表现变体：共享占地、费用、施工、等级、模块和存档规则，只改变图标与 View 映射。树木和雕塑使用 Style；不同 Style 不会被当成独立建筑计数。

没有美术资源时允许：

- 使用统一占位 View；
- 同一 Style 的高等级回退到已有低等级 View；
- 后续只替换 Presentation 引用，不改 Family、Module、存档或 Runtime Prefab。

放置预览是独立字段；施工可选择单一视图或逐回合视图，两种模式不混用回退。详见 [表现资源](表现资源.md)。

## 11. 存档

建筑存档保存：

- 稳定 FamilyId 和 InstanceId；
- Stage、Level、StyleId、施工进度；
- 格子位置与占用身份；
- 按稳定 ModuleId 保存的模块状态。

恢复时由 `BuildingCatalog` 找到唯一 Runtime Prefab，再按 ModuleId 恢复状态。不能依赖 ModuleSet 数组下标，也不能保存 View Prefab 或 Animator 状态作为玩法真相。

## 12. 行为树边界

建筑不使用一棵独立行为树替代建筑类。原因：

- 当前建造、施工、升级、回合和存档顺序是固定事务，不需要自由图调度；
- 行为树会引入第二套状态/生命周期/存档语义；
- 大多数建筑差异是参数和可复用模块组合；
- 编辑器校验、导表和预测器更适合明确类型契约。

未来若出现复杂自治单位式建筑，可在一个专用 Module 内使用行为树实现局部决策；行为树仍不能拥有 Family、Stage、Level、占地、升级或通用存档。

## 13. 权威顺序

发生冲突时：

1. 当前 C# 领域契约。
2. 正式 Excel 的导入范围字段。
3. Unity 中的 ModuleSet 结构、Presentation、Runtime/View Prefab 和非 Excel 字段。
4. 导表生成的 Family/LevelConfiguration/模块数值资产。
5. 文档。

## 14. 验证

1. `Landsong/Building/建筑数值导表工具`：Analyze 后导入，最终应无错误、无待同步差异。
2. 编译 Runtime 与 Editor 程序集。
3. `Landsong/Building/Validate Final Architecture`：结构错误必须为零；缺 View 可作为明确的美术提醒。
4. Play Mode 验证放置、逐回合施工、升级、拆除、主要 Module 回合行为和存读档。
5. 新建筑同步更新 [建筑目录](建筑目录.md)、正式 Excel 和必要表现清单。
