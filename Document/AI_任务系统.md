# AI_任务系统

本文档说明 Landsong 当前任务系统的配置位置、运行时规则、UI 契约和后续扩展边界。任务规则的唯一归属是 `GameSystem`；HUD 和任务面板只负责展示与调用操作，不保存任务进度，也不自行判定完成。

## 1. 任务写在哪里

静态任务配置在以下预制体：

`Assets/Landsong/Objects/Prefabs/GameSystem.prefab`

选中该预制体根节点上的 `GameSystem` 组件，在 Odin Inspector 的 `任务` FoldoutGroup 中编辑：

| 字段 | 用途 |
| --- | --- |
| `主线任务` (`startingQuests`) | 开局可用的主线任务定义。带前置任务的主线会在前置奖励领取后自动加入。 |
| `随机任务池` (`randomQuestPool`) | 可直接随机抽取的固定任务定义。 |
| `运行时交换任务规则` (`runtimeExchangeQuestRules`) | 用物品 BaseValue 动态生成“提交物资，获得报酬”的随机交换任务。 |
| `开局随机任务数量` | 游戏初始化时生成的随机任务数。 |
| `随机任务同时存在上限` | 同时处于进行中状态的随机任务上限。 |
| `随机任务补充间隔回合` | 按回合尝试补充随机任务的间隔；0 表示不自动补充。 |

当前已配置的主线任务：

| questId | 名称 | 要求 | 奖励 | 前置 |
| --- | --- | --- | --- | --- |
| `main_build_farms_3` | 整备田垄 | 建造 3 个农田 | 500 蔬菜 | 无 |
| `main_build_residential_houses_3` | 安置新民 | 建造 3 个居民房 LV0 | 1000 金币 | `main_build_farms_3` |

不要在版本发布后随意修改已使用的 `questId`。它同时用于前置关系和存档恢复；重命名会使旧存档无法正确关联这条任务。开发期修改已有任务定义后，建议使用新游戏或清理测试存档验证。

## 2. 如何新增或修改任务

### 2.1 主线任务

在 `主线任务` 数组新增一个元素，至少填写：

1. `questId`：全局唯一、稳定的标识，例如 `main_build_lumberyard_1`。
2. `displayName`：任务标题。
3. `description`：任务详情文本。
4. `turnLimit`：期限回合数，最小为 1。
5. `objectiveType`：选择建造或提交资源。
6. 目标字段：根据任务类型配置。
7. `rewards`：领取完成奖励时发放的物品和数量。
8. `prerequisiteQuestIds`：需要在其奖励被领取后解锁的前置任务 ID；首个任务留空。

主线任务不会同时全部出现：没有前置的任务会在开局加入；后续任务在所有前置任务都进入 `RewardClaimed` 状态后加入。

### 2.2 建造任务

将 `objectiveType` 设为 `BuildBuildings`，然后配置：

| 字段 | 说明 |
| --- | --- |
| `targetBuilding` | 目标建筑的 `BuildingBase` 预制体引用。 |
| `targetBuildingCount` | 需要建造的数量。 |

例如“建造 3 个农田”使用农田预制体和数量 3。建造进度由 `GameSystem` 监听建筑服务刷新，不应在 UI 中手动累加。

当前一个 `GameQuestDefinition` 只能有一种目标类型，且建造任务只能指定一种建筑。若需要“建造 3 个居民房且建造 3 个农田”这种复合目标，需要先扩展任务数据结构和完成判定，不能仅靠 UI 增加两行文本伪造。

### 2.3 提交资源任务

将 `objectiveType` 设为 `SubmitResources`，在 `requiredResources` 数组中添加一项或多项 `ItemAmount`。

例如：

| 资源 | 数量 |
| --- | --- |
| 木材 | 60 |
| 石头 | 60 |
| 粮食 | 3000 |

每种资源都会分别保存已提交进度，所有资源均达到数量后任务完成。玩家在任务 HUD 或任务面板点击“提交资源”时，系统会尽可能提交每一种尚缺资源；库存不足的资源保留未完成进度。

### 2.4 奖励

`rewards` 支持多个 `ItemAmount`。任务完成后不会自动发奖，玩家必须在 HUD 或任务面板领取。任务奖励领取后任务进入 `RewardClaimed`，并从两个任务 UI 中移除；主线后继任务也在此时解锁。

`ItemDefinition` 为空或数量小于等于 0 的需求/奖励会被忽略，配置时应避免保留无效元素。

### 2.5 随机任务

固定随机任务添加到 `随机任务池`，配置方式与主线任务相同。运行时交换任务则配置到 `运行时交换任务规则`：系统会从规则候选物资中生成提交需求和奖励，依据 `ItemDefinition.BaseValue` 计算交换价值；奖励倍率约为 1.5 到 3，较高倍率的出现概率更低。

随机任务全部自动接取，没有“接受/拒绝”状态。玩家可以通过任务面板放弃未完成或已过期任务。

## 3. 运行时状态和事件规则

任务状态由 `QuestStatus` 表示：

| 状态 | 含义 | UI 行为 |
| --- | --- | --- |
| `Active` | 进行中。 | 在 HUD 和任务面板显示。 |
| `Completed` | 目标已达成，等待领取奖励。 | 在 HUD 和任务面板显示，可领取奖励。 |
| `Failed` | 超过期限仍未完成。 | 在任务面板显示，可放弃。 |
| `RewardClaimed` | 奖励已领取。 | 从 HUD 和任务面板隐藏。 |
| `Abandoned` | 玩家主动放弃。 | 从 HUD 和任务面板隐藏，不发奖励。 |

关键产品规则：

1. 新任务加入时发送事件栏消息，点击消息打开任务面板。
2. 任务完成时不发送事件栏消息。
3. 任务奖励只能由 HUD 或任务面板领取。
4. 所有任务自动接取，不存在可拒绝任务。
5. 超过 `DeadlineTurn` 的进行中任务会失败；完成后等待领奖的任务不会因期限继续失败。
6. 放弃只允许用于 `Active` 或 `Failed` 状态，不能放弃已完成待领奖的任务。

任务逻辑入口：

`Assets/Landsong/Scripts/GameSystem/GameSystem.cs`

主要接口：

| 接口 | 用途 |
| --- | --- |
| `TryAddQuestDefinition(...)` | 从静态定义加入一个任务。 |
| `TryClaimQuestRewards(...)` | 发放奖励并将任务标记为已领取。 |
| `TrySubmitQuestResources(...)` | 提交多种资源并刷新进度。 |
| `TryAbandonQuest(...)` | 放弃进行中或已失败任务。 |
| `CreateRuntimeSubmitResourcesQuest(...)` | 从代码创建一条运行时资源提交任务定义。 |
| `TryAddGameplayDebugRandomQuest(...)` | 开发调试时尝试生成一条随机任务。 |

不要让 UI 直接改写 `GameQuestState`、库存或建筑进度。任何新的任务规则应由 `GameSystem` 完成判定后发布 `QuestsChanged`，UI 订阅该通知刷新。

## 4. 存档和兼容性

任务存档由以下链路维护：

`GameSystem.CaptureQuestData()` -> `GameData.QuestData` -> `GameSystem.RestoreQuestData(...)`

相关文件：

`Assets/Landsong/Scripts/GameSystem/GameSystem.cs`

`Assets/Landsong/Scripts/AppSystem/DataManager.cs`

任务存档当前版本为 `QuestSaveData.CurrentVersion = 3`。恢复时会兼容旧版的“待接受”状态，将其迁移为 `Active`。如需更改序列化字段、状态枚举语义或目标进度结构，必须：

1. 增加存档版本或明确迁移策略。
2. 在 `RestoreQuestData(...)` 中处理旧数据。
3. 覆盖新游戏、旧存档、进行中任务、已完成待领奖任务的回归测试。

## 5. UI 契约

### 5.1 HUD

脚本：

`Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanelHUD_Quest.cs`

HUD 显示当前可见任务的简要信息、剩余回合、各项要求和领取入口。它使用 `GamePanelItem_Quest_Requirement` 生成要求行。HUD 只负责调用 `GameSystem` 的提交、领取和打开面板接口。

### 5.2 任务面板

脚本：

`Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Quest.cs`

任务项脚本：

`Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_QuestItem.cs`

要求项脚本：

`Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanelItem_Quest_Requirement.cs`

任务面板会过滤 `RewardClaimed` 和 `Abandoned` 状态。当前选中任务可执行提交资源、领取奖励、放弃任务。

任务项预制体：

`Assets/Landsong/Objects/Prefabs/UI/UIPanel_Game/任务面板 任务Item_new.prefab`

该预制体必须在 `GamePanel_QuestItem` 上绑定：

| 字段 | 绑定对象 |
| --- | --- |
| `任务要求容器` (`requirementRoot`) | `内容/任务要求容器`。 |
| `任务要求项预制体` (`requirementItemPrefab`) | 带有 `GamePanelItem_Quest_Requirement` 的要求项预制体。 |
| `任务奖励容器` (`rewardRoot`) | `内容/任务奖励容器`。 |
| `任务奖励文本预制体` (`rewardTextPrefab`) | 根节点带 `TMP_Text` 的奖励文本预制体。当前需要在 Unity Inspector 中由美术/UI 配置。 |
| `任务奖励布局` (`rewardLayout`) | 任务奖励容器上的 `GridLayoutGroup`。 |

奖励文本由 `ResourceRichTextFormatter` 输出，例如“金币 x10”，并使用全局资源富文本图标配置。奖励布局最多四列：`GamePanel_QuestItem` 会根据容器当前宽度动态调整单元格宽度。

`任务要求容器` 使用 `VerticalLayoutGroup` 驱动高度；要求项保持容器全宽，并将内部文本设为居中。这比使用固定左右填充更能适应不同分辨率。不要给处于 `LayoutGroup` 父节点下的列表项额外添加 `ContentSizeFitter`，否则 Unity 会提示布局冲突；应由父级 LayoutGroup 和子项 `LayoutElement` 共同决定尺寸。

### 5.3 面板入口

`Panel_Game.prefab` 中的任务面板和任务 HUD 应分别绑定 `GamePanel_Quest`、`GamePanelHUD_Quest`。新任务事件点击后通过 `GameSystem.QuestEventClicked` 打开任务面板；不要让事件栏直接维护任务选择状态。

## 6. 新增任务类型的检查清单

新增 `QuestObjectiveType` 时，必须同步修改以下位置：

1. `GameQuestDefinition` 的有效性检查和默认/规范化逻辑。
2. `GameSystem` 中任务初始进度、运行时进度刷新、完成判定、期限处理。
3. 存档数据和旧版本迁移逻辑。
4. `GamePanelHUD_Quest` 与 `GamePanel_QuestItem` 的要求行生成与操作按钮状态。
5. 至少覆盖新建任务、完成、领奖、失败、放弃、存档恢复的测试。

若新类型需要玩家交互，新增操作也应由 `GameSystem` 提供 `Try...` 接口，再由 UI 调用。不要把规则判断散落到 HUD、任务项或事件栏脚本中。

## 7. 验证清单

在 Unity 中至少验证：

1. 开局只出现无前置主线任务；前置奖励领取后，下一条主线出现。
2. 建造目标建筑会更新建造进度。
3. 多资源提交任务可分多次提交，所有资源满足后完成。
4. 完成任务不会发送事件栏消息；新任务会发送。
5. 领取奖励后任务从 HUD 和任务面板移除，奖励进入库存。
6. 到期任务显示为未完成/失败且可放弃。
7. 放弃后任务不再显示，且不会获得奖励。
8. 奖励数量超过 4 项时，任务奖励容器会自动换行。
9. 保存并读取后，进行中进度、领取状态、主线解锁状态保持一致。

代码编译可使用：

```powershell
dotnet build Assembly-CSharp.csproj --no-restore
```

该命令不能替代 Unity Editor 中的预制体引用、布局和实际运行验证。
