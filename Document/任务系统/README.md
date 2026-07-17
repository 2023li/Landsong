# Landsong 任务系统

> 当前事实版本：2026-07-17。正式主线内容见 [主线任务](主线任务.md)。

## 1. 架构

```text
QuestDefinition 资产
        |
        v
QuestCatalog（全部静态任务与随机交换规则）
        |
        v
GameSystem.prefab（Catalog、起始任务 ID、随机任务参数）
        |
        v
QuestService（状态、进度、完成、奖励、前置、存档）
        |
        v
HUD / Quest Panel（只展示并调用 Try... API）
```

唯一公开入口：

```csharp
GameSystem.Instance.Services.Quest
```

任务规则属于 `QuestService`。UI 不累计任务进度，不直接修改库存、建筑、科技或功能解锁状态。

## 2. 配置位置

- 任务定义：`Assets/Landsong/Objects/SO/Quests`
- 任务目录：`Assets/Landsong/Objects/SO/Quests/QuestCatalog.asset`
- 游戏装配：`Assets/Landsong/Objects/Prefabs/GameSystem.prefab`
- 模型：`Assets/Landsong/Scripts/QuestSystem/QuestModels.cs`
- 服务：`Assets/Landsong/Scripts/QuestSystem/QuestService.cs`
- 装配：`Assets/Landsong/Scripts/QuestSystem/GameSystem.Quest.cs`
- 功能解锁：`Assets/Landsong/Scripts/GameSystem/GameFeatureUnlockService.cs`

当前 `GameSystem.prefab` 登记全部 6 个主线 ID。初始化时只有无前置的 `main_camera_survey` 加入；领取奖励后才逐项解锁后续任务。

## 3. 目标类型

| 类型 | 规则来源 | 完成条件 |
| --- | --- | --- |
| `BuildBuildings` | `BuildingService.Buildings` | 指定建筑家族达到数量 |
| `SubmitResources` | `InventoryService` + 任务提交进度 | 每种资源累计提交完成 |
| `MoveCamera` | `CameraController.AnyManualCameraPanPerformed` | 玩家真实手动平移过视野 |
| `CollectResources` | `InventoryService` | 每种资源当前持有量达到要求，不扣除 |
| `PlantCrops` | 建筑 `StateChanged` + `IBuildingCropFieldSource` | 指定家族中已播种建筑达到数量 |
| `SelectTechnology` | `TechnologyService.StateChanged` | 当前研究科技不为空 |

`requiredResources` 同时用于 `SubmitResources` 和 `CollectResources`。前者读取已提交量，后者读取当前库存量；UI 必须分别显示“提交”和“收集”。

## 4. 奖励与解锁

- `rewards` 支持多个 `ItemAmount`。
- `unlockedFeatures` 支持 `Building`、`Inventory`、`Technology`、`Expedition`、`Inheritance`、`Congress`。
- 完成任务不会自动发奖；玩家领取成功后，状态才进入 `RewardClaimed`。
- 物品奖励先作为一次库存事务发放；成功后再应用系统解锁，避免领取一半成功。
- 物品奖励受当前库存容量约束；容量不足时领取会显示明确失败消息，任务保持已完成状态，玩家调整库存后可以再次领取。
- 后续主线要求全部前置任务均为 `RewardClaimed`。

功能解锁由 `GameSystem.Instance.Services.Features` 统一管理。HUD 负责显隐，`UIPanel_Game` 负责入口校验。读取存档时不保存平行的解锁列表，而是根据已领取任务重新应用解锁奖励。

远征、继承和国会还必须在领域服务与回合结算层校验解锁状态，不能只依赖按钮显隐。`Congress` 当前对应 `PolicyService`；项目尚无独立国会面板。

## 5. 运行时与存档

任务状态：`Active`、`Completed`、`Failed`、`RewardClaimed`、`Abandoned`。

```text
QuestService.CaptureSaveData()
  -> GameData.QuestData
  -> QuestService.RestoreSaveData(...)
```

- 相机任务完成后保存任务状态，不保存相机事件次数。
- 收集任务从库存即时重建进度。
- 播种任务从建筑模块存档恢复后的 `HasCrop` 状态重建进度。
- 科技选择任务从科技服务恢复后的当前研究重建进度。
- 功能解锁由已领取任务奖励重建。

任务期限使用明确语义：`turnLimit = 0` 表示不限时，正数表示限时回合数。不限时任务不参与超时失败判定，HUD 与任务详情的期限位置只显示 `∞`，不附加“剩余”或“回合”等文字。

当前项目采用严格数据版本策略，`GameData.CurrentDataVersion = 16`。版本 15 及更早存档不会继续加载，不在 UI 或服务中堆叠旧结构兼容分支。

## 6. 新增任务检查清单

1. 创建独立 `QuestDefinition`，使用稳定且唯一的 `questId`。
2. 注册到 `QuestCatalog.asset`。
3. 若属于主线候选链，将 ID 加入 `GameSystem.prefab` 的起始任务 ID 列表，并配置前置 ID。
4. 新目标类型必须同步模型有效性、服务进度与完成判定、事件订阅、保存/恢复语义及两处任务 UI。
5. 新奖励类型必须保证一次领取不会产生部分成功，并定义恢复后的重建方式。
6. 更新 [主线任务](主线任务.md)，运行 Catalog 验证、编译和 Play Mode 测试。

## 7. 验证

- 运行 `Landsong/Quest/Validate Catalog`，输出应包含 `QUEST_ARCHITECTURE_VALIDATION_SUCCESS`。
- 新游戏只出现 `main_camera_survey`；领取后按 1→6 顺序推进。
- 相机自动聚焦和缩放不误完成任务 1。
- 任务 2 不扣除库存；持有量下降时未完成进度同步下降。
- 第 3 个农田建成、第 3 座农田播种、第 3 座居民房建成和选择研究科技时立即完成对应任务。
- 未解锁时 HUD 不显示建造/库存/科技入口，快捷键也不能绕过；读档后入口与已领取奖励一致。
- Runtime 与 Editor 程序集编译通过，并在 Unity Play Mode 检查任务文案、奖励行、按钮显隐与存读档。
