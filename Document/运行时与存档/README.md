# 运行时与存档系统

## 1. 责任边界

| 层 | 责任 | 不负责 |
| --- | --- | --- |
| `AppBoot` | 初始化常驻管理器并进入 Start Scene | 游戏领域状态 |
| `AppManager` | 新游戏/继续游戏/退出的应用级命令 | 具体存档序列化 |
| `DataManager` | AppData、存档索引、当前 GameData、保存/加载门面和恢复事件 | 单个领域的保存结构 |
| `GameSaveRepository` | 磁盘目录、ES3 读写、备份和版本拒绝 | 运行时对象重建 |
| `GameRuntimeSnapshotService` | 汇总各服务快照、恢复服务和建筑运行时对象 | UI 显示 |
| `GameSystem` | 服务装配、跨领域恢复协调和游戏运行 | 文件系统路径 |
| `LSScenes` | Loading UI、场景切换、地图加载和恢复时序 | 领域规则 |

## 2. 启动与场景流

```text
Boot.unity
  AppBoot.InitializeManagers
      -> AppManager
      -> IOManager.Initialize
      -> DataManager.Initialize
      -> AudioPlayer.Initialize
      -> GameLocalizationManager.Initialize
  LoadScene_Start
      -> Start.unity + UIPanel_MainMenu

新游戏/继续游戏
  AppManager
      -> DataManager 创建或加载 GameData
      -> LoadScene_Game
      -> Game.unity
      -> 按 GameData.MapId 加载 Content Scene
      -> MapRuntimeHost.TryBind
      -> DataManager.RestoreCurrentGameDataToRuntimeRoutine
      -> 新游戏首次创建初始建筑并立即保存
      -> UIPanel_Game
```

Content Scene 由 `MapContentSceneLoader` Additive 加载。返回主菜单时必须先卸载 Content Scene，并关闭游戏 UI，不能直接加载 Start 而留下地图或订阅者。

## 3. GameSystem 与 GameServices

`GameSystem` 是唯一游戏级 Facade。领域访问统一使用 `GameSystem.Instance.Services`；`GameServices` 暴露 Service 与 Catalog，但不复制状态。

当前主要服务：库存、经济预测、回合、王朝、建筑、游戏事件、科技、科技解锁内容、全局 Buff、政策、任务、远征、人才、继承、建筑蓝图与建筑选择。

`GameSystem` 保持一个 Unity 组件和一个 Facade，但物理代码按职责拆为 `GameSystem.Turn.cs`、`Technology.cs`、`Expedition.cs`、`Talent.cs`、`Inheritance.cs`、`Blueprints.cs` 以及 `QuestSystem/GameSystem.Quest.cs`。这些 partial 文件只负责编排，不形成新的状态所有者；文件责任表见 [架构决策](../架构决策.md)。

创建新服务时：

1. Service 自己持有规则和运行时状态。
2. `GameSystem` 只装配依赖、订阅跨领域事件、决定回合/恢复顺序。
3. 在 `GameServices` 增加唯一只读入口。
4. 为 Service 增加 Capture/Restore 数据，并接入 `GameRuntimeSnapshotService`。
5. UI 从 Services 取服务，不能 `new` 或保存第二份状态。

## 4. 存档模型

- `AppData`：语言、音量、显示偏好等应用级设置。
- `GameDataMeta`：存档列表显示和索引信息。
- `GameData`：当前游戏完整快照，`CurrentDataVersion = 12`。
- 各领域 `*SaveData`：库存、建筑、科技、任务、政策、远征、人才、继承、蓝图等子快照。
- `BuildingSoftReferenceSaveData`：加载聚焦等轻量建筑引用，不直接保存 Unity 对象。

运行时引用统一恢复为稳定 ID：MapId、FamilyId、InstanceId、ItemId、TechnologyId、QuestId 等。不要把 Scene 对象、Prefab 引用或 Service 实例写入 GameData。

## 5. 保存流程

```text
DataManager.SaveCurrentGame
  -> GameRuntimeSnapshotService.Capture(GameData)
  -> 更新 GameDataMeta
  -> GameSaveRepository.SaveGame / SaveIndex
  -> DataManager.OnGameDataSave
```

覆盖保存沿用当前 SaveGuid；`GameDataSaveMode.NewSave` 创建新的存档槽。改变存档名称通过 `SetCurrentGameSaveName`，不要直接改索引列表。

## 6. 恢复流程

```text
Load Game Scene + bind map
  -> OnRuntimeDataRestoreStarted
  -> Inventory/Services 进入恢复态
  -> 恢复回合与各领域状态
  -> 分帧创建建筑并恢复模块状态
  -> 重建库存槽位与建筑能力引用
  -> Reconcile 蓝图/科技自动条件
  -> OnRuntimeDataRestoreCompleted
```

恢复中不要提前触发依赖完整世界状态的 UI 刷新。需要监听恢复的面板应在完成事件后重新取服务状态。

## 7. 版本与兼容

`GameSaveRepository` 只接受与 `GameData.CurrentDataVersion` 完全相同的存档，并在扫描索引时排除其他版本。首个公开版本前采用严格不兼容策略：结构变化提升项目级版本，子系统不保存独立版本，也不增加局部兼容别名。详见 [架构决策](../架构决策.md)。

## 8. 关键代码

- `Assets/Landsong/Scripts/AppSystem/AppBoot.cs`
- `Assets/Landsong/Scripts/AppSystem/AppManager.cs`
- `Assets/Landsong/Scripts/AppSystem/DataManager.cs`
- `Assets/Landsong/Scripts/AppSystem/IOManager.cs`
- `Assets/Landsong/Scripts/AppSystem/LSScenes.cs`
- `Assets/Landsong/Scripts/GameSystem/GameSystem.cs`
- `Assets/Landsong/Scripts/GameSystem/GameServices.cs`
- `Assets/Landsong/Scripts/Persistence/GameRuntimeSnapshotService.cs`
- `Assets/Landsong/Scripts/Persistence/GameSaveRepository.cs`
- `Assets/Landsong/Scripts/Persistence/GameSaveIndexService.cs`
- `Assets/Landsong/Scripts/Persistence/SaveDataModels.cs`

## 9. 验证清单

- Boot → Start → New Game → Game 的完整流程可用。
- Continue Game 正确加载最近有效版本存档。
- Content Scene 加载失败会回到主菜单，不留下半初始化存档。
- 新游戏初始建筑只创建一次，首次快照保存失败会回滚。
- 覆盖保存、另存、删除、备份和存档索引一致。
- 读档后回合、建筑模块、库存槽位、科技、任务和其他服务状态一致。
- 返回主菜单后地图 Content Scene 和游戏 UI 均已卸载。
