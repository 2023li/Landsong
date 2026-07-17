# Landsong 任务系统

> 当前事实版本：2026-07-17。主线内容清单见 [主线任务](主线任务.md)。

## 1. 最终架构

任务系统采用“Definition/Catalog 管内容，Service 管运行时状态”的终态：

```text
QuestDefinition 资产
        |
        v
QuestCatalog（全部静态任务 + 交换任务规则）
        |
        v
GameSystem.prefab（Catalog 引用 + 起始任务 ID + 全局生成参数）
        |
        v
QuestService（状态、完成判定、随机生成、事件、存档）
        |
        v
HUD / Quest Panel（只展示并调用 Try... API）
```

唯一运行时入口：

```csharp
GameSystem.Instance.Services.Quest
```

任务内容不再内嵌到 `GameSystem.prefab`，也不允许 UI 保存第二份任务状态。

## 2. 配置位置

- 任务定义目录：`Assets/Landsong/Objects/SO/Quests`
- 唯一任务目录：`Assets/Landsong/Objects/SO/Quests/QuestCatalog.asset`
- 游戏级装配：`Assets/Landsong/Objects/Prefabs/GameSystem.prefab`
- 任务模型：`Assets/Landsong/Scripts/QuestSystem/QuestModels.cs`
- 运行时服务：`Assets/Landsong/Scripts/QuestSystem/QuestService.cs`
- 装配 partial：`Assets/Landsong/Scripts/QuestSystem/GameSystem.Quest.cs`

`QuestCatalog` 保存：

- 全部 `QuestDefinition`；
- 固定随机任务定义；
- 运行时交换任务规则。

`GameSystem.prefab` 的“任务”分组只保存：

- `任务目录`；
- `起始任务 ID`；
- 开局随机任务数量；
- 随机任务同时存在上限；
- 随机任务补充间隔回合。

当前正式起始任务 ID：

| questId | 名称 | 目标 | 奖励 | 前置 |
| --- | --- | --- | --- | --- |
| `main_build_farms_3` | 整备田垄 | 建造 3 个农田 | 500 蔬菜 | 无 |
| `main_build_residential_houses_3` | 安置新民 | 建造 3 个居民房 LV0 | 1000 金币 | `main_build_farms_3` |

## 3. 新增任务

1. 在 `Assets/Landsong/Objects/SO/Quests` 创建 `Landsong/Quest/Quest Definition`。
2. 配置全局唯一且稳定的 `questId`、显示文本、期限、任务类型、目标、奖励和前置 ID。
3. 在 `QuestCatalog.asset` 点击“从目录登记任务定义”，确认新资产进入目录且没有重复 ID 警告。
4. 若任务开局就进入主线候选链，把其 ID 加入 `GameSystem.prefab` 的“起始任务 ID”。固定随机任务只需把 `Category` 设为 `Random`，不加入起始 ID。
5. 更新 [主线任务](主线任务.md) 或对应内容清单，并完成运行时验证。

不要通过复制 `GameSystem.prefab` 数组元素新增任务。任务 ID 同时用于 Catalog 查找、前置关系和存档恢复；开发期允许不兼容旧存档，但同一内容版本内仍不得随意改 ID。

## 4. 目标与奖励

### BuildBuildings

- `targetBuilding` 引用目标家族唯一 Runtime Prefab；运行时只读取稳定 `FamilyId`。
- `targetBuildingCount` 是所需数量。
- 进度由 `QuestService` 订阅 `BuildingService` 刷新，UI 不累计数量。

### SubmitResources

- `requiredResources` 可包含多项 `ItemAmount`。
- 玩家可以分多次提交；每项提交进度独立保存。
- 所有资源达到数量后任务完成。

### 奖励

- `rewards` 支持多个 `ItemAmount`。
- 完成后不自动发奖；玩家必须调用领取操作。
- 领取成功后状态进入 `RewardClaimed`，后继主线才会解锁。

一个静态定义当前只能使用一种 `QuestObjectiveType`。复合目标必须先扩展模型、服务判定、存档与 UI，不能只增加显示文本。

## 5. 运行时规则

| 状态 | 含义 |
| --- | --- |
| `Active` | 进行中 |
| `Completed` | 已完成，等待领取奖励 |
| `Failed` | 超过期限仍未完成 |
| `RewardClaimed` | 奖励已领取 |
| `Abandoned` | 已放弃 |

关键规则：

1. 所有任务自动加入，不存在“待接受”或拒绝状态。
2. 无前置主线在初始化时加入；后继任务要求全部前置进入 `RewardClaimed`。
3. `Completed` 任务不会继续因期限失败。
4. 只有 `Active` 或 `Failed` 可以放弃。
5. 固定随机任务来自 Catalog 中 `Category=Random` 的定义；交换任务由 Catalog 规则运行时生成。

主要服务接口：

- `TryClaimRewards(...)`
- `TrySubmitResources(...)`
- `TryAbandon(...)`
- `TryAddDebugRandomQuest(...)`
- `CaptureSaveData()` / `RestoreSaveData(...)`

状态变化通过 `StateChanged` 发布；需要打开并聚焦任务时通过 `QuestRequested` 发布。

## 6. 存档策略

链路：

```text
QuestService.CaptureSaveData()
  -> GameData.QuestData
  -> QuestService.RestoreSaveData(...)
```

项目在首个公开版本前执行严格不兼容策略，当前 `GameData.CurrentDataVersion = 12`：

- 仓储层只接受完全相同的项目版本；
- `QuestSaveData` 不再维护子版本或旧状态迁移；
- `Validate()` 只清理当前结构中的空引用、重复 ID 和非法数值；
- 任务结构变化时提升项目级 `CurrentDataVersion`，不在 UI 或 Service 中增加旧结构兼容分支。

## 7. UI 契约

- HUD：`GamePanelHUD_Quest.cs`
- 任务面板：`GamePanel_Quest.cs`
- 任务项：`GamePanel_QuestItem.cs`
- 要求项：`GamePanelItem_Quest_Requirement.cs`
- 正式 UI：`Assets/Landsong/Objects/Prefabs/UI/UIPanel_Game/Panel_Game.prefab`

UI 只能读取 `QuestService`、订阅事件并调用服务事务。不得直接修改 `GameQuestState`、库存、建筑进度或任务期限。

任务事件点击由 `QuestRequested` 请求 `UIPanel_Game` 打开并聚焦；事件栏不保存当前任务选择。

## 8. 新任务类型检查清单

新增 `QuestObjectiveType` 时必须同步：

1. `GameQuestDefinition` 的有效性和规范化；
2. `QuestService` 的初始化、进度、完成、期限和操作事务；
3. 当前 `QuestSaveData` 结构，并提升项目级存档版本；
4. HUD、任务项的要求行和按钮状态；
5. 新建、完成、领奖、失败、放弃、存读档测试。

## 9. 验证

- Catalog 无空定义和重复 `questId`，Prefab 起始 ID 都能解析。
- 运行 `Landsong/Quest/Validate Catalog`，要求输出 `QUEST_ARCHITECTURE_VALIDATION_SUCCESS`。
- 开局只出现无前置主线；领取奖励后后继主线出现。
- 建造和多资源提交进度正确。
- 到期、放弃、领奖状态符合规则。
- 固定随机任务和交换任务不会生成重复 ID。
- 保存并读取后，静态任务从 Catalog 恢复，运行时生成任务从存档快照恢复。
- Runtime 与 Editor 程序集编译通过，并在 Play Mode 检查 HUD/面板绑定。
