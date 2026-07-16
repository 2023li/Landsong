# Landsong 建筑架构审计与终态决策

> 状态：架构决策已冻结，核心重构已于 2026-07-13 落地。本文件记录“为什么这样做、最终是什么、还缺什么”，不再是待确认草案。

## 1. 结论

旧模型把“家族、施工、等级和视觉变体”混成了 Prefab/脚本身份，必须重构。最终模型为：

> 一个建筑家族一个静态定义、一个轻量 Runtime Prefab、一个 ModuleSet 和一个 Presentation；所有建筑直接使用同一个密封 `BuildingBase` 运行时类型，差异由有状态模块组合；施工、等级、样式与实例身份是状态；表现资源独立加载。

居民房从施工到 LV4 始终是同一个实例：

```text
FamilyId = building.residential_house
InstanceId = 永久不变
Construction -> Operational/LV1 -> LV2 -> LV3 -> LV4
```

普通升级只校验条件、扣费、修改等级、应用配置并刷新表现。没有等级 Prefab 替换，也没有跨对象手写状态迁移。

## 2. 旧架构的关键问题

### 高等级能力断层

旧 `PlayerHomeLV2～LV4` 与 `ResidentialHousingLV2～LV4` 不是在 LV1 能力上调整数值，而是重新实现了更薄的脚本/组件集合。升级后会丢失人口、库存、科技点、食物、税收、Resource 连接或荒废逻辑。

根因不是“忘记复制某个方法”，而是“等级被建模为另一个对象”。只要继续每级一个运行时 Prefab，这类遗漏会随新模块反复出现。

### 状态迁移不可持续

对象替换要求人工迁移人口、工人、生产进度、作物、补贴、连接、任务引用、占格和家族特殊状态。新增任何模块都可能再次遗漏。Prefab Variant 只能减少部分美术重复，无法解决运行时状态迁移。

### 数据与表现耦合

等级数值、玩法组件和 Sprite/Animator 同时存在于等级 Prefab，导致：批量平衡困难、依赖膨胀、缺美术就阻塞玩法、无法稳定导表、重复 ID 和失效字段难以静态验证。

### 身份语义混乱

旧 BuildingId 有时代表家族、有时代表施工态/等级、有时代表树木图片。任务、蓝图、科技、存档和菜单无法使用统一语义。

## 3. 已冻结的产品决策

| 决策 | 最终答案 |
| --- | --- |
| 建筑运行时模型 | 一个家族一个 Runtime Prefab，同实例跨施工和等级 |
| 脚本 | 所有普通建筑统一使用具体 `BuildingBase`；不生成家族脚本，玩法差异全部进入模块 |
| 升级 | 原地升级，状态全部保留；占地永不变化 |
| 升级支付 | 目标等级可配置科技条件与金币/物品费用；条件满足后可直接支付升级 |
| 施工 | 生命周期 Stage，不是 LV0；每个家族都有施工形态 |
| 美术不完整 | 允许；同样式低等级回退或统一占位，后续只替换 Presentation 映射 |
| 巨型 Prefab | 禁止把施工和所有等级美术作为禁用子节点塞入 Runtime Prefab |
| 树 | 一个家族的玩家可选样式，当前 8 个 StyleId |
| 仓库 | 已按终态架构实现为 `building.warehouse`；三等级共用同一运行时实例和 `warehouse.operation` 模块 |
| 医院 | 已按终态架构实现为 `building.hospital`；通用岗位、维护费、运营经验和条件化医疗范围效果组合 |
| 警局 | 已按终态架构实现为 `building.police_station`；法律解锁蓝图，通用岗位、维护费、运营经验和条件化治安范围效果组合 |
| 云中城 | 后期内容，当前移除壳资源和解锁效果 |
| 旧存档 | 不兼容，不保留旧等级 Prefab 或迁移分支 |
| 规模 | 按大规模建筑库设计，所有静态数据资产化并可验证 |

## 4. 终态资产与职责

| 层 | 责任 | 不允许 |
| --- | --- | --- |
| `BuildingFamilyDefinition` | FamilyId、固定占地、放置/施工、等级、升级条件和费用 | 实例进度、渲染对象 |
| `BuildingModuleSetDefinition` | 可复用模块模板和稳定 ModuleId | 第二套家族状态、等级 Prefab 引用 |
| Runtime Prefab | 稳定载体、交互/碰撞、空 ViewRoot、统一 `BuildingBase` | 派生建筑类型、等级美术、Animator、等级数值、内联模块 |
| Runtime Module | 一项能力的配置、状态、生命周期、存档、状态提示和 UI 条目 | 第二份同义状态、修改占地、替换运行时实例 |
| `BuildingPresentationDefinition` | 互斥的单一/逐回合施工 View 模式、放置预览 View、运营 Level/Style 到 View | 动画资源内容、玩法数值、占地、任务身份 |
| View Prefab | Sprite、Animator、粒子、音效挂点 | BuildingBase、碰撞、玩法脚本 |

## 5. 美术资源为什么不放进 Runtime Prefab

若把施工和 LV1～LVN 全塞成禁用子节点，会造成直接依赖和内存膨胀、Hierarchy 维护困难、激活状态可能误保存、多个 Animator 同驻留，并继续把数据等级与视觉等级绑死。

因此美术存放在独立 View Prefab：

```text
BuildingViews/<family>/Construction.prefab
BuildingViews/<family>/LV01.prefab
BuildingViews/<family>/LV02.prefab
BuildingViews/Tree/tree_01/LV01.prefab
```

Presentation 可直接引用或 Addressable 引用。LV3 没有美术时可以继续显示相同 Style 的 LV1/LV2，而 UI 和玩法仍是 LV3。补齐资源只新增映射，不改实例、数据和存档。

## 6. 已完成的代码与资产改造

- 新增家族、施工、等级、身份、表现与升级领域模型。
- `BuildingBase` 改为从 FamilyDefinition 读取静态数据；施工和等级由运行时身份保存。
- 普通升级的 Prefab 替换 API、旧 `BM_等级升级`、旧 `BM_施工材料消耗` 和施工脚本已删除。
- 模块只从独立 ModuleSet 克隆；模块状态按稳定 ModuleId 保存，不再按数组下标。
- 生产、岗位、作物等状态只有一个所有者；伐木、捕鱼和农田已改用稳定模块。
- 王宫、居民房、市场、农田、捕鱼、伐木、道路、树木、仓库、雕塑、采石场、医院和警局都使用统一运行时与模块组合，没有家族专属 `BuildingBase` 派生类。
- 王宫人口、居民运营、市场结算、捕鱼稀有产出、树木采集均成为独立状态模块；高等级只重配同一模块，不会再缺失低等级能力。
- 仓库通过 `workforce + warehouse.operation` 组合实现最低运营人数、动态库存、固定维护费、累计经验、升级门槛和满员奖励；外部库存系统只查询容量能力接口。
- 医院通过 `workforce + maintenance + operational_experience + spatial_effect` 组合实现岗位、维护、累计经验和医疗覆盖；医疗 Definition 可按精确运营等级与最低工人数生效，不需要医院脚本。
- 警局复用同一模块组合实现岗位、维护、累计经验和治安覆盖；治安是公共空间效果类型，不需要警局脚本。
- 外部系统通过 `TryGetCapability<T>` 查询人口、资源消费、税收、连接、作物等能力，不再判断具体建筑类。
- `BuildingBase` 已设为 `sealed`，旧派生钩子和家族自定义存档入口已删除；编译器会阻止重新创建具体建筑类。
- 建造菜单和放置请求支持 StyleId；树的八个样式由一个家族呈现。
- 任务、科技、场景初始模板和默认主城 ID 已迁移到新 Runtime Prefab/FamilyId。
- 当前共有 12 个 Family、12 个 ModuleSet、12 个 Presentation 和 12 个 Runtime Prefab。
- 删除旧等级 Prefab 目录、旧等级/施工/云中城脚本和失效科技解锁引用。
- 新增终态架构校验器；当前目录包含 12 家族和 12 个唯一 Runtime Prefab，提交前仍必须在 Unity 执行最终架构校验。

## 7. 当前不是架构问题的内容缺口

以下不会再通过创建等级 Prefab 解决：

- 表现回填提醒按模式输出：单一模式只检查一个施工 View；逐回合模式按家族、样式和施工回合精确列出缺口。两种模式不混合回退，树木和雕塑的 Style LV1 View 已配置。
- 王宫和居民房 LV2～LV4、捕鱼 LV2 尚未设计，因此保持 `configured=false`。
- 农田作物数组为空；捕鱼特殊鱼关闭；部分建筑缺图标/成本。
- 医院与警局的施工、LV1～LV3 运营表现和家族图标待回填；云中城仍需未来按完整新家族流程实现，不能恢复旧壳资源。

具体数值见正式 Excel 与《建筑正式数值表与导表工具》，具体美术项见《建筑Prefab与表现资源回填指南》。

## 8. 最终执行标准

1. 一个 FamilyId 只对应一个 Runtime Prefab；其根组件必须直接是统一 `BuildingBase`，禁止普通建筑派生类。
2. 施工不是 LV0；等级从 LV1 连续递增。
3. 普通等级升级不替换实例，完整保留运行时状态和外部引用。
4. footprint 只在 FamilyDefinition 配置，全生命周期不可变化。
5. 所有模块有稳定 ID，同一状态只有一个所有者。
6. Runtime Prefab 的 ViewRoot 为空，不含 Renderer/Animator 等等级美术。
7. View 缺失时允许回退；有 StyleId 时不得切到其他样式。
8. 树种/皮肤不制造新家族；只有玩法规则真的不同才创建新家族。
9. 存档不保存等级 Prefab 或 View 引用；不兼容旧建筑存档。
10. 任何数值新增/修改必须先写正式 Excel 并通过原子导表，再通过 `Landsong/Building/Validate Final Architecture`。

## 9. 关于建筑行为树的最终决定

行为树不作为建筑核心架构。绝大多数建筑是确定性的“生命周期 + 回合结算 + 数值配置”，使用行为树会把顺序、依赖和存档状态转成图资产，增加策划调试与版本合并成本，却不能替代 Family、Level、Module、Presentation 或存档契约。

若后期出现会自主选择目标、长时间执行计划、根据世界状态切换策略的特殊建筑，可新增一个可选“行为树驱动模块”，在模块内部接入自研节点或 Opsive Behavior Designer Pro。行为树只能编排已注册能力，不得直接持有人口、库存、等级、占地等领域真相，也不得成为所有建筑的默认依赖。
