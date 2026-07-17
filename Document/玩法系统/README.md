# 回合与玩法系统

## 1. 总体关系

`GameSystem` 负责玩法服务的装配与跨系统顺序，各领域 Service 负责自己的状态和事务。UI 通过 `GameSystem.Instance.Services` 调用它们。

```text
GameSystem.NextTurn
  -> TurnService
       -> 建筑回合与资源归因
       -> 库存自然损耗
  -> 科技点结算
  -> 人才工资/效果/特性
  -> 王族年龄/出生/继承/特性
  -> 远征到期与补贴
  -> 王宫存在性与游戏结束
```

任务和政策拥有独立文档：[任务系统](../任务系统/README.md)、[政策系统](../政策系统/README.md)。

## 2. TurnService

`TurnService` 是建筑回合处理和资源归因的统一入口。同步和分帧版本必须保持相同语义：

1. `BeforeTurnAdvanced`。
2. 建立建筑快照并开始资源提供回合。
3. 逐建筑执行 `ProcessTurn`，收集消费、产出和科技点。
4. 完成资源提供结算。
5. 逐槽处理库存自然损耗。
6. 更新当前回合并触发 `TurnAdvanced`。

需要统计“本回合某建筑提供/消费了什么”的功能必须订阅 `TurnService` 事件或使用已有归因接口，不能在 UI 中扫描库存差值。

## 3. DynastyService

王朝服务统一维护王朝名、阶段、基础人口、建筑人口、就业人口和王宫状态。建筑通过人口/岗位能力向服务贡献，不直接修改 UI 数字。

失去王宫的游戏结束判定由 `GameSystem` 在回合结束后执行。地图初始王宫、读档王宫和玩家建造王宫都必须走统一建筑注册流程。

## 4. GameEventService

`GameEventService` 保存给玩家显示的事件消息以及事件类型接收偏好。它不是逻辑 EventBus：

- 领域逻辑使用 Service 的强类型事件。
- `GameSystem` 将重要跨系统结果格式化为 `GameEventMessage`。
- UI 订阅消息集合变化并展示，不从消息文本反推游戏状态。

## 5. ExpeditionService

负责目的地可用性、队伍人口、开始远征、到期结算、奖励领取、补贴不足惩罚和存档。`GameSystem` 在回合结束后调用结算，并把已分配人口同步到 `DynastyService`。

当前 `GameSystem.prefab` 的 `expeditionDestinationCatalog` 仍为空，正式远征内容尚未完成绑定。代码可用不等于功能已完成验收。

## 6. TalentService

负责人才候选刷新、招募、职位分配、工资、经验升级、隐藏特性与回合效果。岗位对职业的接纳规则属于 `TalentSlotDefinition`；UI 只显示 Service 返回的结果。

当前 `GameSystem.prefab` 的 `talentCatalog`、人才金币物品和起始职位仍为空，属于内容/绑定缺口。

## 7. RoyalInheritanceService

负责国王、王后、王子、年龄、寿命、出生、退位/继承、先天与后天特性及其回合效果。使用稳定 CharacterId/TraitId 存档，不保存 Definition 对象。

当前 `GameSystem.prefab` 的 `royalTraitCatalog` 为空；配置结构存在，但正式特性内容尚未接入。

## 8. 扩展原则

- 新玩法优先新增独立 Service 与 Definition/Catalog，不把状态直接堆到 UI 或 `GameSystem` 字段。
- 只有需要固定跨系统顺序的步骤进入 `GameSystem.HandleTurnAdvanced`。
- 玩法结果通过结构化 Result 返回，消息文案由编排层或 UI 格式化。
- 需要建筑能力时使用能力接口，不检查 FamilyId 或具体建筑类。
- 需要库存事务时调用 `InventoryService` 的原子 API，不先查后改造成竞态式两步逻辑。
- 每个 Service 都要明确事件、存档、恢复、配置源和无内容时的行为。

## 9. 关键代码

- `Assets/Landsong/Scripts/GameSystem/GameSystem.cs`
- `Assets/Landsong/Scripts/GameSystem/GameSystem.Turn.cs`
- `Assets/Landsong/Scripts/GameSystem/GameSystem.Expedition.cs`
- `Assets/Landsong/Scripts/GameSystem/GameSystem.Talent.cs`
- `Assets/Landsong/Scripts/GameSystem/GameSystem.Inheritance.cs`
- `Assets/Landsong/Scripts/GameSystem/GameServices.cs`
- `Assets/Landsong/Scripts/GameSystem/TurnService.cs`
- `Assets/Landsong/Scripts/GameSystem/DynastyService.cs`
- `Assets/Landsong/Scripts/GameSystem/GameEventService.cs`
- `Assets/Landsong/Scripts/ExpeditionSystem`
- `Assets/Landsong/Scripts/TalentSystem`
- `Assets/Landsong/Scripts/InheritanceSystem`

## 10. 验证清单

- 同步与分帧回合产生相同结果。
- 回合推进过程中不能再次推进回合。
- 建筑消费/生产、损耗和跨系统结算顺序明确且可重复。
- 服务的 StateChanged 与具体结果事件不会重复触发 UI 操作。
- 保存/读取后随机结果之外的状态完全一致。
- 未绑定 Catalog 的系统明确显示不可用，不产生空引用异常。
