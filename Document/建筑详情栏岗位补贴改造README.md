# 建筑详情栏岗位补贴改造 README

本文档记录建筑详情栏岗位补贴改造的需求确认、实现口径和 prefab 绑定约定。当前岗位补贴核心逻辑已经开始落地，后续 prefab 调整应以本文档的字段契约为准。

相关源码：

- `Assets/Landsong/Scripts/BuildingSystem/Buildings/LumberCabinLV1.cs`
- `Assets/Landsong/Scripts/UI/Popup_BuildingDetails.cs`
- `Assets/Landsong/Scripts/BuildingSystem/BuildingJobSystem.cs`
- `Assets/Landsong/Scripts/BuildingSystem/BuildingDetailInterfaces.cs`
- `Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_BuildingDetaiPopup.cs`：已弃用，当前实现中已移除。

相关已有文档：

- `Document/建筑岗位计算公式README.md`
- `Document/建筑架构README.md`

## 已确认决策

以下决策来自 2026-07-03 的需求确认：

1. 补贴金币是每回合消耗。
2. 补贴换算为 `1 金币/回合 = 100 / 最大岗位数` 点就业吸引力。
3. 自动补贴打开时，如果库存金币不足，不支付，保持目标补贴值，并发送报错/警告。
4. 自动补贴减少属于通知，自动补贴增加属于警告；实现上拆分事件类型，方便消息栏区分。
5. 自动补贴调整在每次岗位重新计算时立即发生。
6. 招募工人文本显示为“消耗X金币 招募1名工人”，其中 X 为当前招募 1 名工人的费用。
7. 招募工人如果金币不足，或当前工人 + 1 会超过稳定人数，按钮不可用，点击逻辑也不执行。
8. 桌面端工人详情侧栏使用 hover 即显示。
9. 移动端按住显示工人详情侧栏，松开后隐藏。
10. 工人详情侧栏固定在详情面板侧边，不跟随鼠标或手指。
11. “满岗位需要的最少就业吸引力”只显示最终阈值。
12. `GamePanel_BuildingDetaiPopup.cs` 后续直接删除。
13. “上一档 / 下一档”用于选择目标稳定工人数；系统反算达到该目标所需的最小有效金币，避免补贴没有跨过阈值时白白浪费。
14. 自动补贴关闭时，如果天气、人口、全局修正等因素变化，仍然重新计算达到当前“目标稳定工人数”所需的最小金币。
15. 补贴金币只在建筑回合结算时扣一次；非回合内只更新显示、目标值和事件。
16. 如果目标补贴需要 `2` 金币/回合但库存只有 `1`，补贴完全不生效，不扣这 `1` 金币。

## 当前源码状态

### 详情弹窗

当前实际使用的详情弹窗是 `Popup_BuildingDetails`。

它现在做的事情：

- 显示建筑名称、描述、图标。
- 订阅当前建筑的 `StateChanged`，建筑状态变化时刷新弹窗。
- 主面板 `root_内容栏` 不再作为详细文本列表使用，而是放建筑详情的大模块，例如岗位模块、后续产出模块。
- 脚本只根据建筑能力接口控制模块显示/隐藏，不会清理 `root_内容栏` 里 prefab 预先摆好的模块对象。
- 如果建筑实现 `IBuildingWorkforceFundingSource`，显示岗位补贴控制区。
- “上一档 / 下一档”表示“目标稳定工人数”的档位调整，不表示金币数。
- 当前工人文本显示“工人：当前工人/最大工人（稳定：稳定数）”，例如 `工人：0/3（稳定：2）`。
- 工人详情使用通用侧边栏渲染：`go_详情侧边栏` 控制显隐，`root_详情侧边栏内容` 作为文本 item 父节点，`prefab_详情侧边栏文本` 作为动态文本行。
- “当前就业吸引力”“满岗位需要的最少就业吸引力”“影响因素”等文本行运行时生成，不再作为固定 serialized 字段绑定。

现有限制：

- `BuildingDetailRow` 只有 `Label` 和 `Value`，不能表达按钮、开关、红绿颜色、hover 详情、按住详情。
- 交互式控制区应继续走建筑能力接口，例如 `IBuildingWorkforceFundingSource`；普通详情文本继续走 `BuildingDetailRow`。

### 伐木小屋岗位系统

`LumberCabinLV1` 当前是岗位建筑样例，核心字段包括：

- `currentWorkers`：当前工人。
- `maxWorkers`：最大岗位。
- `stableWorkers`：当前就业吸引力能够稳定支撑的工人。
- `baseJobAttraction`：基础就业吸引力。
- `nearbyPopulation` / `populationAttractionBonus`：附近人口带来的就业吸引力。
- `globalAttractionModifierTotal`：全局就业吸引力修正合计。
- `singleRecruitCost`：单人立即招工基础费用。
- `goldItemId`：当前为 `金币`。

当前摘要显示是：

```text
工人 {currentWorkers}/{stableWorkers}
```

你想要的新显示是：

```text
工人：0/3（稳定：2）
```

建议语义：

- `0` = 当前工人 `currentWorkers`
- `3` = 总岗位数 `maxWorkers`
- `2` = 当前能够稳定的工人 `stableWorkers`

所以后续应把伐木小屋摘要改为：

```text
工人 {currentWorkers}/{maxWorkers}（{stableWorkers}）
```

### 当前岗位公式

当前稳定工人由 `BuildingJobSystem` 计算：

```text
稳定工人 = Clamp(Floor(岗位吸引力 * (最大岗位 + 1) / 100), 0, 最大岗位)
```

因此“满岗位需要的最少就业吸引力”可以反推：

```text
满岗位最少就业吸引力 = 最大岗位 * 100 / (最大岗位 + 1)
```

伐木小屋 `maxWorkers = 3` 时：

```text
3 * 100 / 4 = 75
```

也就是就业吸引力达到 `75` 时，稳定工人达到 `3`。

当前立即招工费用公式是：

```text
立即招工费用 = Ceil(
    实际招工人数 * 单人招工费用 * (1 + (100 - 岗位吸引力) / 100)
)
```

这已经可以支撑“招募工人旁边显示招募 1 名工人需要消耗的金币”。

## 目标交互草案

### 详情面板主区域

建议主区域只显示大模块：

```text
建筑名称
建筑短描述 / 基础状态

[岗位模块]
[产出模块]
[库存模块]
```

其中：

- 每个模块是 prefab 里预先摆好的静态对象。
- 建筑需要该模块时，脚本把该模块设置为 active。
- 建筑不需要该模块时，脚本把该模块设置为 inactive。
- 模块内部只放玩家需要直接操作或快速扫描的摘要控件。
- 模块的具体计算说明、影响因素、阈值等详细信息放在通用侧栏的 Content 中运行时生成。
- “自动补贴满岗位”只负责按每回合金币提供就业吸引力，使稳定工人尽量达到 `maxWorkers`。
- 招募工人按钮只负责立即招募 1 名工人，不再一次性补满。
- 两者不是同一件事：补贴解决“能不能稳定留住这么多人”，招募工人解决“现在是否招 1 名工人入职”。

### 通用侧边栏

鼠标移动到“工人 0/3（2）”时，显示侧栏。

移动端按住“工人 0/3（2）”时，显示侧栏，松开后隐藏。

侧栏本身不是岗位专用控件。`Popup_BuildingDetails` 只依赖一个通用侧栏 GO、一个内容 root 和一个文本 item prefab；岗位、产出、消耗等后续说明都应复用同一套侧栏渲染。

工人行当前生成的侧栏内容：

```text
满岗位需要的最少就业吸引力：75
当前就业吸引力：70
还差：5

就业吸引力影响因素
基础吸引力：+55
附近人口：+20
连续大雨：-5
补贴就业吸引力：+0
```

显示规则建议：

- 正数用绿色。
- 负数用红色。
- 0 或普通说明用默认颜色。
- “连续大雨 -5”这类全局修正来自 `BuildingJobAttractionModifier.DisplayText` 和 `Value`。
- 补贴是就业吸引力影响因素之一，但 UI 应显示它让目标稳定工人达到哪个档位，而不是允许玩家选择无效的中间金币数。
- 如果没有全局修正，显示“无全局修正”即可，不要显示空列表。
- `Txt_当前就业吸引力` 这类字段不需要在 prefab 上单独提供；它们会作为通用文本 item 动态创建。

## 补贴系统定义

改造前源码只有“立即招工金币”，没有“岗位补贴金币”的业务实现。当前实现口径中，补贴金币是每回合运营支出，且补贴提供就业吸引力：

```text
1 金币/回合 = 100 / 最大岗位数 点就业吸引力
```

例如伐木小屋 `maxWorkers = 3`：

```text
1 金币/回合 = 33.33 点就业吸引力
```

这和当前稳定工人公式不冲突，但会替换上一版“1 金币直接多稳定 1 个工人”的设计。当前稳定工人阈值来自：

```text
稳定工人 = Floor(岗位吸引力 * (最大岗位 + 1) / 100)
```

所以每个稳定工人档位宽度实际是：

```text
100 / (最大岗位 + 1)
```

而每金币补贴吸引力是：

```text
100 / 最大岗位
```

因此每金币补贴吸引力会略大于一个稳定工人档位宽度。它不会破坏公式，但不能用“金币数 = 稳定工人数增量”直接计算，必须按目标稳定工人数对应的最低吸引力阈值反算金币。

推荐拆成这些值：

```text
基础就业吸引力 = 不含补贴的岗位吸引力
每金币补贴吸引力 = 100 / 最大岗位
目标稳定工人 = 玩家通过上一档/下一档按钮或自动补贴指定的稳定工人数
目标稳定工人所需最低吸引力 = 目标稳定工人 * 100 / (最大岗位 + 1)
目标补贴金币/回合 = Ceil(Max(0, 目标稳定工人所需最低吸引力 - 基础就业吸引力) / 每金币补贴吸引力)
实际就业吸引力 = Clamp(基础就业吸引力 + 已支付补贴金币 * 每金币补贴吸引力, 0, 100)
稳定工人 = 由实际就业吸引力套用现有稳定工人公式
```

自动补贴打开时：

```text
目标稳定工人 = 最大岗位
```

手动补贴时：

```text
上一档按钮：目标稳定工人 - 1
下一档按钮：目标稳定工人 + 1
可选范围：不含补贴时的稳定工人 到 最大岗位
```

这样 UI 不会暴露无效金币数，只会暴露有意义的稳定工人档位。金币消耗永远由当前环境反算最小有效值。

例子：伐木小屋 `maxWorkers = 3`、基础就业吸引力 `70`、目标稳定工人 `3`。

```text
满岗位最低吸引力 = 75
每金币补贴吸引力 = 33.33
目标补贴金币/回合 = Ceil((75 - 70) / 33.33) = 1
```

如果之后连续大雨从 `-5` 变成 `0`，导致基础就业吸引力从 `70` 变成 `75`：

```text
目标补贴金币/回合 = 0
```

自动补贴打开时，应自动把补贴从 `1` 调整为 `0`，并发送通知。自动补贴关闭时，如果玩家手动目标仍是 `3`，也应重算为 `0`，避免继续支付无意义金币。

如果之后出现新的负面因素，导致基础就业吸引力从 `70` 变成 `40`：

```text
目标补贴金币/回合 = Ceil((75 - 40) / 33.33) = 2
```

自动补贴打开时，应自动把补贴从 `1` 调整为 `2`，并发送警告。

如果库存金币不足：

```text
目标补贴金币/回合保持不变
本次不支付
本次补贴不生效
发送补贴金币不足警告
```

如果目标补贴需要 `2` 金币/回合但库存只有 `1`，完全不扣金币，补贴完全不生效。这样可以避免扣了不足以跨过目标档位的钱。

这里把“目标补贴”和“已支付补贴”拆开，原因是玩家可能希望维持目标稳定工人数；但库存不足的那个回合不能白拿补贴效果，也不能扣无效的部分金币。

## 自动补贴事件

当前 `GameEventCatalog` 没有补贴相关事件。已确认事件分为通知和警告；实现上建议拆分事件类型，因为当前 `GameEventMessage` 没有独立的 severity 字段。

推荐新增事件：

```text
subsidy_auto_decreased  自动补贴减少，通知
subsidy_auto_increased  自动补贴增加，警告
subsidy_gold_missing    补贴金币不足，警告
recruit_partially_done  招工未完全补满，通知
```

自动调整事件建议只在补贴值实际变化时发送：

```text
伐木小屋自动补贴从 1 调整为 0。
伐木小屋自动补贴从 1 调整为 2。
```

自动补贴增加用警告，是因为它表示当前环境变差，维持满岗位需要更多金币。

自动补贴减少用通知，是因为它表示当前环境变好，玩家的每回合支出降低。

金币不足时：

- 不扣金币。
- 不关闭自动补贴。
- 不把目标补贴值降到库存能支付的数量。
- 发送 `subsidy_gold_missing`。
- 该回合的补贴效果不生效。

## 招募工人

当前 `LumberCabinLV1.TryRecruitImmediately()` / `TryRecruitToFull()` 负责立即招募：

- 每次只招募 1 名工人。
- 会检查是否会超过稳定人数。
- 会检查可用人口。
- 会计算招工费用。
- 会扣除金币。
- 会增加当前工人。
- 会刷新岗位状态并 `NotifyStateChanged()`。

需要补上的点：

- 在 UI 上显示本次点击招募 1 名工人的预计消耗金币。
- 消耗文本固定显示为 `消耗X金币 招募1名工人`。
- 如果当前工人 + 1 会超过稳定人数，按钮 disabled。
- 如果金币不足以支付当前文本显示的费用，按钮 disabled。

推荐 UI 显示：

```text
[招募工人] 消耗12金币 招募1名工人
```

如果当前工人已经达到稳定人数，按钮不可用：

```text
[招募工人] 消耗12金币 招募1名工人
```

可用人口不足时仍然不能完成招募，点击后的消息示例：

```text
伐木小屋：可用人口不足。
```

实现时固定计划招募 1 人：

```text
计划招工人数 = 1
计划招工费用 = 招工费用公式(计划招工人数)
可点击 = 当前工人 + 1 <= 稳定人数 且 当前金币 >= 计划招工费用
```

## 推荐代码拆分

### `BuildingJobSystem`

建议新增纯计算方法：

```text
CalculateRequiredAttractionForStableWorkers(maxWorkers, targetStableWorkers)
CalculateFullWorkerRequiredAttraction(maxWorkers)
CalculateSubsidyAttractionPerGold(maxWorkers)
CalculateRequiredSubsidyGoldForTargetStableWorkers(maxWorkers, baseAttractionWithoutSubsidy, targetStableWorkers)
CalculateAttractionWithSubsidy(baseAttractionWithoutSubsidy, paidSubsidyGold, maxWorkers)
```

理由：

- 这些公式不属于 UI。
- 以后其他岗位建筑也可以复用。
- README 中的公式和代码能保持一致。

### `LumberCabinLV1`

建议新增运行时字段：

```text
autoFullWorkerSubsidyEnabled
targetStableWorkers
targetSubsidyGoldPerTurn
paidSubsidyGoldThisTurn
lastAutoSubsidyAdjustment
```

建议新增只读属性：

```text
AutoFullWorkerSubsidyEnabled
TargetStableWorkers
TargetSubsidyGoldPerTurn
PaidSubsidyGoldThisTurn
BaseJobAttractionWithoutSubsidy
SubsidyAttractionPerGold
SubsidyAttractionBonus
FullWorkerRequiredAttraction
RequiredSubsidyGoldForTargetStableWorkers
ImmediateRecruitToFullCost
```

建议新增操作方法：

```text
SetAutoFullWorkerSubsidyEnabled(bool enabled)
SetTargetStableWorkers(int value)
TryRecruitToFull()
TryPaySubsidyForCurrentTurn()
```

保存数据需要增加：

```text
autoFullWorkerSubsidyEnabled
targetStableWorkers
```

`targetSubsidyGoldPerTurn` 建议运行时重算，不作为存档主字段；真正需要保存的是玩家的目标稳定工人数和自动补贴开关。

是否保存 `lastAutoSubsidyAdjustment` 需要再定。一般它只影响本次显示和事件，不一定需要存档。

### UI 可选能力接口

因为不是所有建筑都有岗位和补贴，建议不要把补贴字段直接塞进 `BuildingBase`。

可以新增一个岗位补贴 UI 接口，例如：

```text
IBuildingWorkforceFundingSource
```

它向 UI 暴露：

```text
当前工人
最大岗位
稳定工人
满岗位所需就业吸引力
当前就业吸引力
补贴开关状态
目标稳定工人
目标补贴金币
本回合已支付补贴金币
不含补贴就业吸引力
每金币补贴就业吸引力
补贴就业吸引力
自动计算所需补贴金币
招募1名工人预计费用
招募1名工人当前是否可用
影响因素列表
开关/目标稳定工人/招募1名工人操作方法
```

`Popup_BuildingDetails` 只判断当前建筑是否实现这个接口：

- 实现了：显示岗位补贴控制区。
- 没实现：隐藏岗位补贴控制区，继续显示普通详情。

### `Popup_BuildingDetails`

建议它只负责 UI 绑定：

- 绑定自动补贴 Toggle。
- 绑定招募工人 Button。
- 绑定补贴金额文本。
- 绑定招募工人费用文本。
- 绑定工人行 PointerEnter / PointerExit。
- 绑定移动端按住。
- 根据建筑 `StateChanged` 刷新显示。

不要让弹窗自己计算岗位公式。公式应来自建筑/岗位系统。

## Prefab 修改清单

当前 prefab：

```text
Assets/Landsong/Objects/Prefabs/UI/建筑详情栏/pop_建筑详情.prefab
```

当前已有字段：

- `go_补贴栏标题`
- `go_补贴栏内容`
- `txt_建筑名称`
- `txt_建筑描述`
- `img_建筑图标`
- `btn_关闭弹窗`
- `root_内容栏`
- `prefab_标题`
- `prefab_内容文本`

`root_内容栏` 当前应作为“模块容器”，放 prefab 中预先摆好的大模块。脚本不会再遍历并 Destroy `root_内容栏` 的子物体，只会清理自己通过代码生成的临时 item。

当前脚本需要你在 prefab 上补齐或绑定这些 UI 节点：

- 自动补贴 Toggle：`tgl_自动补贴满岗位`。
- 目标稳定工人数上一档 Button：`btn_目标稳定工人上一档`。
- 当前工人文本：`txt_当前工人`，显示 `工人：当前工人/最大工人（稳定：稳定数）`。
- 目标稳定工人数下一档 Button：`btn_目标稳定工人下一档`。
- 当前补贴金额文本：`txt_当前补贴金币`。
- 招募工人 Button：`btn_一键补满工人`。
- 招募工人消耗金币文本：`txt_一键补满消耗`。
- 工人概览行容器：`go_工人详情触发区`。
- 通用详情侧边栏外壳 GO：`go_详情侧边栏`，只负责显示/隐藏整个侧栏。
- 通用详情侧边栏内容 root：`root_详情侧边栏内容`，只作为动态文本 item 的父对象。
- 通用详情侧边栏文本 item prefab：`prefab_详情侧边栏文本`。

`go_工人详情触发区` 必须是 Canvas 下的 UI 区域，并且它自己或子物体需要能被 UGUI raycast 命中。当前脚本会在运行时检测这个对象；如果它是 `RectTransform` 且没有任何 `raycastTarget = true` 的 `Graphic`，脚本会自动加一个透明 `Image` 作为 hover/按住命中区域。仍需注意不要被其他更上层的 Raycast Target 完全挡住。

`prefab_详情侧边栏文本` 建议至少包含一个 `TMP_Text`。如果 prefab 中有两个 `TMP_Text`，脚本会把第一个当作标签、第二个当作数值，并只给数值文本应用正负颜色。

脚本字段建议命名：

```text
tgl_自动补贴满岗位
btn_目标稳定工人上一档
txt_当前工人
btn_目标稳定工人下一档
txt_当前补贴金币
btn_一键补满工人
txt_一键补满消耗
go_工人详情触发区
go_详情侧边栏
root_详情侧边栏内容
prefab_详情侧边栏文本
```

不要再为工人侧栏绑定这些旧字段：

```text
go_工人详情侧栏
root_工人影响因素
prefab_工人影响因素行
txt_满岗位所需吸引力
txt_当前就业吸引力
txt_就业吸引力差值
```

这些内容已经改为运行时生成，之后产出详情也应复用 `go_详情侧边栏`、`root_详情侧边栏内容` 和 `prefab_详情侧边栏文本`。

目标稳定工人控制使用 `btn_目标稳定工人上一档` 和 `btn_目标稳定工人下一档`：

- Toggle 控制自动补贴。
- 自动补贴打开时，目标固定为 `maxWorkers`，并禁用上一档/下一档交互。
- 自动补贴关闭时，可选范围为“不含补贴稳定工人”到 `maxWorkers`。
- 上一档/下一档只按整数稳定工人档位调整。
- 当前工人文本显示 `工人：当前工人/最大工人（稳定：稳定数）`，稳定数会随目标补贴和岗位吸引力刷新。
- 当前补贴金币文本显示系统反算出的最小有效金币。
- 如果当前环境变化，仍然按当前目标稳定工人数重新计算金币，避免浪费。

## 确认结果归档

这些问题已在“已确认决策”中落定。后续实现时不再按开放问题处理。

实现时需要额外注意的是：`1 金币/回合 = 100 / 最大岗位数` 点就业吸引力，不等价于“直接多稳定 1 个工人”。UI 必须通过目标稳定工人数反算最低有效金币，避免玩家支付不能跨过稳定工人阈值的无效补贴。

## 后续实施顺序建议

1. 按本文“已确认决策”实现补贴金币经济定义和事件行为。
2. 在 `BuildingJobSystem` 增加纯计算 helper。
3. 在 `LumberCabinLV1` 增加补贴字段、计算、保存和事件。
4. 增加岗位补贴 UI 可选接口。
5. 改 `Popup_BuildingDetails` 绑定新控件。
6. 你手动补 prefab 节点和字段引用。
7. 直接删除 `GamePanel_BuildingDetaiPopup.cs`，并清理无引用的旧详情 item。
8. 跑 `dotnet build Assembly-CSharp.csproj --no-restore`。
9. 进 Unity 验证 prefab 绑定、hover、按住、事件消息、存档恢复。
