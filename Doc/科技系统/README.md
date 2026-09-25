# 科技

科技定义位于 `ECSContent/Definitions/Technology`，数量以当前内容集为准。拥有 `feature.Technology` 后开放科技树；建筑蓝图、Buff、功能许可均通过正式授权链消费。本页是新增科技的当前操作入口。

## 新增科技的固定步骤

1. 在上述目录使用 **Create → Landsong → Definitions → Research → Technology** 创建资产；填写 `Metadata.Id`、`Metadata.Name`、说明和图标。ID 稳定且在科技域内唯一。
2. 填写 `ResearchPointCost` 和 `Repeatable`。在 `Prerequisites.TechnologyRequirements` 选择前置科技，`Required` 为 1；不接受未注册引用、自引用、重复或循环。
3. 在 `Rewards.Items/Blueprints/Buffs/Features` 配置首次奖励；蓝图指定建筑和 `GrantedLevel`。属性修正填入当前支持的 `Effects`，不能仅添加枚举就创造新的经济或战斗效果。
4. 点击定义 Inspector 的“注册到Technology领域目录”。Inspector 显示当前正式内容集，目录来自世界模板；注册拒绝重复 ID 和错误领域类型。
5. 设置 `HasTreePosition/TreePosition`，打开 **Landsong → ECS → Technology tree authoring** 编辑关系与字段。窗口固定使用同一个正式内容集，“校验”会实际编译费用、奖励和前置关系；它不等于显示同步或实际运行验证。
6. 执行 **Landsong → ECS → Compile domain display catalogs** 和 **Landsong → 内容制作 → 校验正式制作内容**。进入具备科技许可的隔离会话，检查新节点/连线、规划/入队、扣点、首次奖励、重复规则及读档。工具链回归为 **Landsong → ECS → Verification → Content authoring workflow**；领域回归为 **Landsong → ECS → Verification → Technology**。

新增普通科技不创建 Prefab，不新增研究系统分支，也不更新历史迁移基线。修改已有玩法字段或目录顺序后，旧存档仍受内容指纹限制。

## 运行规则

科技树支持前置连线、图标、滚动/缩放、搜索、当前研究定位和详情。状态区分已完成、可研究、研究中、排队、等待前置、保留进度和等待发奖。

解锁科技后，游戏右侧任务追踪下方显示研究 HUD，取正式研究队列首项显示图标、名称、已投入点数/成本、紫色进度条及效果摘要。摘要优先展示奖励（建筑/功能许可、增益、物品），无奖励时使用科技说明；长摘要省略，点击卡片可查看完整详情。图标为空时使用“研”字占位。

HUD 取代独立“科技”按钮；点击打开科技树并定位当前项目。空队列显示“尚未选择科技”和选择入口。取消、完成、等待前置、等待发奖及载入旧节点后均从 ECS 刷新，不模拟虚假的连续研究进度。关闭科技树后返回无功能面板展开的游戏画面，研究继续沿用原结算规则。

- 加入队尾要求前置完成；“规划至此”按拓扑补齐前置，预览后替换队列。
- 排队/取消不立即扣点，取消不退已花点数，重新排队保留进度；取消前置会让依赖暂停。
- 白天结算按队列消费累计研究点，余额足够可完成多个科技，不丢余点。
- 首次完成须整份奖励成功，才记录完成与授权。满仓保留满进度等待，不半发、不重复扣点。
- 重复研究只计次数、不重复发首次奖励，必须明确再次排队。当前正式科技均未开启重复。
- 夜晚只读，队列变化使旧规划确认失效。

`TechnologyProgress.Completions` 是运行时完成次数的唯一权威。`ResearchOps.Completed`、前置条件、军事准备和情报均读取它；研究结算不再向 `Entitlement` 写入冗余科技行。上限为 `int.MaxValue - 1`，达到上限后不能再次排队。

首次完成通过 `RewardOps` 将物品、蓝图/Buff/功能许可、研究完成行和完成消息一起提交。失败时恢复相关库存、许可、完成状态和历史；已经投入的研究点与满进度仍保留。定义奖励保持先处理物品、后启用许可，各自保留配置顺序，避免本次新获得的 Buff 改变本批物品的存储选择。

玩家仍通过 `GameplayRequests.Enqueue<T>` 提交 QueueResearchRequest、CancelResearchRequest、PlanResearchRequest 等具名请求调整计划。直接调用 `ResearchOps.Command/Plan` 也检查白天、暂停、情报模式、节点准备与功能许可；内部结算另外允许 Settlement，不借用玩家编辑门禁。正式目录未获得科技许可时保持锁定；自定义目录完全没有 `feature.Technology` 定义时，保留原有默认开放策略。

存档只接受当前格式。研究记录校验定义、进度、完成次数和队列顺序；拒绝重复、非法或越界记录。科技许可行一律拒绝，不再通过许可补齐研究状态，也不重发首次奖励。

制作菜单：Landsong/ECS/Technology tree authoring；或定义 Inspector 的“打开科技关系与奖励配置”。支持关系图、搜索、字段编辑、Undo、保存及校验，不提供拖线建边。

ResearchPointCost 是研究点费用，Repeatable 控制是否可重复完成。前置使用 Prerequisites.TechnologyRequirements，次数必须为 1。完成奖励使用 Rewards 中的具名数组，蓝图填写 GrantedLevel。科技还可配置情报和受支持的军事修正，不能把科技定义作为任意经济修正来源。科技的这些配置不再暴露通用等级或 B/C 参数。HasTreePosition / TreePosition 只影响显示，不进入玩法签名。Baking 拒绝循环、缺失引用、非法成本、奖励和未支持的效果组合。

feature.Building / Inventory / Technology / Expedition 为四项功能许可；limit.* 仅是限建组，不可混用。

验证范围与本次报告按 [验证工作流](../ECS/验证工作流.md) 判断；旧报告的套件数和 PASS 不代表当前改动已经验收。
