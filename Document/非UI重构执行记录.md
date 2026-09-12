# 非 UI 重构执行记录

更新日期：2026-09-12。对应 [实施前审查与分批方案](非UI架构说明与定向重构执行方案.md)。本文记录本轮实现及实际编辑器验收，不复述原始审查。

**当前状态：A–E 已完成，N01–N12 定向编辑器验证通过；最终 VerifyAll 为 40 套件、0 失败、11627 断言，四场景 SceneFlowPlay 与 HUD Panels Play 均通过。** 以下结果来自本轮 2026-09-12 的实际执行，不引用上一轮 UI 重构结果。按用户要求只做编辑器验证，包括编辑器 Play，未执行 Player 或构建测试。

## 五批执行状态

| 批次 | 实际范围 | 当前状态与依赖 |
| --- | --- | --- |
| A 约束与边界修复 | 奖励/许可统一校验、导入合法性、固定产出通配、Disabled 所有权、入夜候选协调、研究极值 | 已完成并通过定向回归。整批预检和最小回滚直接与 B 一起实现；许可反向一致性与 D 的旧研究归一化同时落地 |
| B 领域入口与奖励事务 | 具名许可/完成事实接口、全部正常发放调用方、奖励事务与溢出策略 | 已完成并通过定向回归；依赖 A 的共享约束 |
| C 效果来源与解释 | EffectQuery/EffectOps、来源与目标支持表、生产/军事/科研/情报消费迁移、通配目标和等级规则 | 已完成并通过定向回归；使用 B 的 Buff 查询和 D 的研究完成权威，不保留两套聚合入口 |
| D 科技权威与职责 | ResearchEntry 完成权威、兼容投影与导入归一化、查询/玩家修改/内部结算门禁、Sim 授权及效果职责移出 | 已完成并通过定向回归；持久布局未变，继续 v24 写入与 v22/v23 读取 |
| E 编辑器集成与记录 | 新增定向套件、现有领域回归、总入口及本记录 | 已完成；VerifyAll、四场景 SceneFlowPlay、HUD Panels Play 均通过，编辑器已恢复 Start 场景 |

实现过程中存在关联批次交叉，没有逐批运行成功后再实施下一批。最终统一编译、定向验证及完整回归已通过；上表记录最终验收状态。

## N01–N12 实施映射

N01–N12 均已通过本轮定向编辑器验证及最终 VerifyAll，实际 Play 流程结果另列于文末。

| 编号 | 当前实现 | 代码入口 / 已验证重点 |
| --- | --- | --- |
| N01 固定产出通配 | 固定产出单独传入物品与建筑；建筑为空编译为通配目标 | `EffectOps.FlatProduction`、`EffectQuery.TargetBuildingDefinition`、`ContentModuleCompiler.Modifiers`；全建筑与指定建筑都能正确贡献 |
| N02 配置与消费链 | 通用效果共用来源枚举、支持表与匹配逻辑；补政策、人才、岗位、特性及相应科技来源；任职效果另限制来源/时机/目标缩放 | `EffectRules.SupportsOwner/SupportsTarget/SupportsTalentEffect`、`ValidateTalentEffectScope`、`EffectOps` 及领域调用方；生效条件和编译拒绝边界 |
| N03 奖励约束 | 所有作者奖励来源共用类型/数值检查；人才许可限制目标、时机、缩放并检查等级范围 | `RewardAuthoringValidation`、`TalentGrantAuthoringValidation`、`EntitlementRules`；零值、负数、上限、未注册引用和来源组合 |
| N04 许可导入 | 在破坏正式状态前检查定义、重复、类型、正等级和领域上限 | `EntitlementStore.ValidateImport`、`SnapshotCodec.NormalizeResearch/ValidateRestore`；重复 Buff 和非法许可拒绝且原会话不变 |
| N05 科技双权威 | `ResearchEntry.Completions` 是完成次数权威；科技许可仅兼容投影；支持合法旧缺行归一化 | `ResearchOps.Completed/NormalizeImportedState/ValidateState`；不重发奖、冲突拒绝、次数上限、查询只读 |
| N06 通用授权含义 | 分离 `BlueprintOps`、`PermanentBuffOps`、`FeatureOps`、`ProgressionFacts`，内部存储仍共用原表 | `EntitlementOperations`、`FeatureOperations`；最高蓝图覆盖低级、重复发放不降级、完成事实不冒充普通奖励 |
| N07 等级/目标/解释 | 明确领域、目标、固定/比例单位和贡献说明；非建筑情报使用真实等级 | `EffectQuery/EffectQuote`、`EffectRules.LevelMatches`、`IntelOps`；说明与数值一致、情报等级和军事累积/冻结语义 |
| N08 整批奖励 | 预检全部条目后提交库存、许可、研究、Session、事件、战报、历史和夜间提交状态 | `RewardOps`、`RewardTransaction`；容量失败、提交中间异常、历史原位合并、夜奖幂等与待存放 |
| N09 通用工具职责 | 授权/前置由领域接口承担，效果聚合移至 EffectOps，Sim 不再承载 Grant/HasGrant/Modifier | `SimulationAccess`、`EntitlementOperations`、`RewardOperations`、`EffectOperations`；不保留旧入口双轨演化 |
| N10 服务门禁 | 玩家研究修改检查阶段、暂停、情报、节点准备和功能许可；内部结算单独允许 Settlement | `ResearchOps.Editable/Command/Plan/Settle`；直接调用也不能绕过门禁，自定义目录许可策略保持 |
| N11 Disabled 所有权 | 清理和释放审计包括 Disabled 运行实体；Disabled Session 仍可作为恢复中的合法 owner | `SimulationLifetimeSystem.Cleanup/IsReleased`、应用释放审计；孤儿清理、同步恢复隔离、再次进入 |
| N12 入夜事务前副作用 | 先捕获正式快照，再在候选 root 协调失效任务容器 | `NightEntryOps.Begin`；取消/异常不留下扣罚或销毁，成功只提交一次，确认指纹仍有效 |

## 保持的规则与事务边界

- 建筑保存最高许可，建造检查一级，升级检查当前等级加一。没有改为独立逐级卡片，也没有拆成新的状态仓库。
- Buff 等级为正整数，不臆造建筑式上限；同定义取最高许可。功能仅一级，`limit.*` 不得作为开关发放。任务/远征完成标记为一级；普通奖励仅授予 Building/Buff/Feature。
- 定义奖励仍先物品、后许可，各遍保留作者顺序。开局/夜间保留原混合顺序；远征物品倍率保留原 float 乘法后取整，避免报价与实发不一致。
- 普通完整奖励容量不足整批拒绝；夜间进入待存放并只提交一次；人才每回合物品保留只存可容纳数量。人才显式许可必须是合法正等级，零产物可无收益。
- 研究只在首次完成发首奖；奖励不足保留已投入点数和满进度，后续队列暂停。完成次数最多 `int.MaxValue - 1`，不能继续排队。正式目录按功能解锁；自定义目录完全不声明 `feature.Technology` 时仍默认开放。
- RewardTransaction 覆盖本批根状态和资源副作用，完整恢复历史行以撤销消息合并。它不承担实体创建/销毁或 I/O；初始化建筑、任务后续衔接等仍是既有领域流程，不宣称整个 GameLoop.Initialize 成为一个奖励事务。
- 经济仍按稳定建筑身份顺序结算；范围效果保留求和/同类最高/组内最高，军事数值仍按黄昏冻结。查询与解释不改变状态或 RNG，不增加跨会话效果缓存。

## 效果与人才内容支持

完整制作约束见 [内容功能配置](ECS/内容功能配置.md#通用效果支持边界)。当前通用 Modifiers 支持 Buff/政策/人才/岗位/王室特性的已消费经济与军事效果；科技只支持军事和情报；情报来源限定建筑、Buff、政策、科技。无执行意义的来源/目标组合由共享支持表拒绝。

固定产出必须指定物品，可不指定建筑；允许负固定修正，最终产量最低为零。科研、民意、阴谋风险为全局；军事的建筑目标仅支持防御结算消费的护甲和减伤。情报使用“通用等级零＋当前等级”，政策和不可重复科技仅允许通用或一级，重复科技允许按完成次数配置多级；军事保留“不高于当前等级”的规则。普通无逐级字段的 Buff 不按许可等级自动叠乘。

人才/岗位通用效果受雇佣、任职资格、岗位适配和工资状态控制。“人物与任职”的任职效果另有明确矩阵：Talent/TalentSlot 被动只支持生产百分比、攻击百分比、民意；每回合只支持 Talent 自身物品、科研点、内容许可，TalentSlot 全部每回合项拒绝。

任职效果当前共用一个 Subject 作为目标和缩放来源：每百份物品必须选 Item，运营建筑计数只能选 Building 或空；被动还必须满足接收目标类型。攻击按物品计数、生产按指定建筑计数等冲突组合拒绝；全局生产按全部运营建筑计数可用。每回合物品必选 Item；科研固定/等级/人口缩放须留空，只有物品/建筑计数缩放可指定来源。详细制作例子见 [人才与岗位的任职效果](ECS/内容功能配置.md#人才与岗位的任职效果)。

人才内容许可仅人才自身每回合发放，必须指定 Building/Buff/Feature，且仅允许固定/人才等级缩放；岗位每回合许可、被动许可及库存/人口/建筑数缩放许可均拒绝。此处不引入临时 Buff 实例、撤销、持续时间或新的岗位回合奖励玩法。

周期收益校验同时覆盖 PeriodicItems 和每回合物品/科研任职效果：允许零，拒绝成长范围内的负收益及静态可知溢出；固定/等级缩放检查端点和二次内部极值，数量缩放检查成长系数非负且有限，实际数量由运行时 checked 保护。被动负修正不受这项收入限制。对应反例已在本轮 `RewardAuthoringVerification` 通过。

## 存档与内容兼容

继续使用现有 Entitlement/ResearchEntry 布局和 v24 写入协议，不重写 v22/v23 冻结读取器或王朝 v3 封装。合法旧节点仅有科技许可、缺少研究行时，导入补完成事实；已有行冲突或投影不一致会被拒绝，不自动取较大值，不重放奖励。归一化先于一致性验证及候选重建，且不修改调用方快照数组。

本轮没有主动改写正式内容资产、内容 ID、GUID、任务 Key 或配置对照基准。不能为消除新检查失败批量更新 `ContentCompilationBaseline.json` 或旧协议金样本。旧签名未存储的信息仍不能追溯补验；本轮没有扩大兼容承诺。

入夜使用专用内部候选恢复入口，仅暂缓可由任务协调处理的失效容器绑定，重复占槽及其他不变量仍须通过。候选协调后立即执行完整快照校验，再进入结算；正式 Decode/Restore 和普通候选恢复保持严格校验。该处理增加一次候选快照复验，确认指纹仍来自未修改的正式白天状态。

## 实际验证结果

最终请求：`nonui-all-3fb297c0e5b4435388757888716eedfe`。开始于 `2026-09-12T13:12:48.4872669+08:00`，结束于 `2026-09-12T13:13:42.4883676+08:00`。结果为 **PASS：40 套件、0 失败、11627 断言**；完整明细见 [regressions.txt](../Library/LandsongEcs/regressions.txt)。

| 检查 | 本轮覆盖目的 | 状态 / 结果 |
| --- | --- | --- |
| `RewardAuthoringVerification.Run` | 全部奖励来源、类型/等级/零值、人才许可范围及周期产物 | PASS，180 断言 |
| `EntitlementRewardVerification.Run` | 混合奖励预检/回滚、容量、顺序、倍率、许可导入、夜奖幂等 | PASS，37 断言 |
| `EffectVerification.Run` | 来源支持、任职效果来源/时机枚举组合及目标缩放反例、通配目标、岗位/政策条件、等级情报、冻结、解释与只读 | PASS，85 断言 |
| `SessionBoundaryVerification.Run` | Disabled 所有权与候选事务中的任务协调 | PASS，89 断言 |
| `ResearchAuthorityVerification.Run` | 研究门禁、旧缺行归一化、冲突/极值、查询只读、首奖失败注入、兼容节点恢复 | PASS，89 断言 |
| `TechnologyVerification.Run` | 科技配置、队列与正式地图科技路径 | PASS，125 断言 |
| `PersistenceContractVerification.Run` | 持久协议、签名、兼容与恢复边界 | PASS，105 断言 |
| 正式 native content、`ContentCompilationVerification.Run` | 当前内容通过新约束，编译结果与原基准一致 | PASS；内容编译套件 379 断言 |
| `ProjectVerification` / VerifyAll | 上述检查及现有适用领域回归 | PASS，40 套件 / 11627 断言 / 0 失败 |

五个新增定向套件合计 **480 断言**，包含在 11627 总数内，不另行累加。正式内容资产、内容编译基准及旧协议金样本未为消除失败而改写。验证针对当前工作区最终编译程序集，Unity 为 6000.3.5f2；程序集编译时间与 SHA256 见 [本轮验证清单](../Library/LandsongEcs/nonui-verification-manifest.json)。[最终编辑器日志片段](../Library/LandsongEcs/nonui-final-editor-log.txt) 从最终 VerifyAll 开始记录，包含两项 Play 检查。

真实 v23/v3 金样本兼容由现有 PersistenceContract 套件验证；新增研究缺行边界中的 v22/v23 数据使用相应冻结写入路径生成，不冒充历史金样本，也没有通过篡改版本头伪造旧格式。

## 实施中发现的问题与复验

- 初始集成发现新 `.meta` 的非法 33 位 GUID，以及测试中不合法的 DynamicBuffer 写法，修正后重新编译进入验证。
- 物品倍率必须保留原 `math.floor(quantity * multiplier)` 精度语义；提前改用 double 乘法或不同的取整路径会改变远征实发数量。定义奖励继续先物品后许可，避免新损耗 Buff 改变同批落格；奖励回滚补全历史行以恢复消息合并计数。
- 入夜候选恢复原先调用严格 Decode，会先拒绝本应由协调处理的失效任务容器。改为专用内部入口仅延后该项检查，候选协调后立即严格复验；没有放宽正常存档 Decode/Restore 或重复占槽等约束。
- 首轮总回归为 39 通过、1 失败：旧 CoreRules 断言仍假设零级授权自动补成一级。将其改为验证零级明确拒绝、显式一级成功；最终 40 套件全部通过。
- 补测并通过每回合物品/科研负收益与静态溢出、负固定修正后的最终产量最低零、不可重复科技不可达情报等级等边界；被动负修正继续允许。

## 编辑器 Play 与收尾

| 检查 | 当前状态 |
| --- | --- |
| SceneFlowPlay | PASS，`2026-09-12T13:15:56.2621231+08:00`；[实际日志](../Library/LandsongEcs/scene-flow-verification.txt) |
| HUD Panels Play | PASS，`2026-09-12T13:17:32.2798478+08:00`；[实际日志](../Library/LandsongEcs/hud-panels-verification.txt) |

SceneFlowPlay 覆盖四个正式场景和 Map_Test2 完整流程，包括昼夜/实际 DBP、科技与任务领奖、政策/人才与远征、库存/经济预演、读档故障回滚、冷存档恢复、重复进入、无效地图/损坏节点/取消及退出清理。HUD Panels 单独检查研究卡片、功能面板关闭、王室/委托/肖像，以及科技规划取消、奖励蓝图跳转和原始白天状态恢复。

两项 Play 完成后，编辑器检查为 `playing=False; changingPlayMode=False; paused=False; sceneFlowTest=False`，当前场景为 `Assets/Landsong/Scenes/Start.unity`，`dirty=False`。本轮一次性请求文件已清理；验证日志保留在 Library。本次约定范围内没有未闭环修复项；没有执行 Player 或构建测试，也未进行性能达标评估。
