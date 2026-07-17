# Landsong 项目交接总览

> 当前事实版本：2026-07-17
> 适用范围：自研代码 `Assets/Landsong/Scripts`、正式配置源 `ConfigSource`、核心 Scene/Prefab/ScriptableObject。
> 目标：让接手者先找到正确入口，再沿真实调用链修改代码、资产和数据。

## 1. 先看这里

Landsong 是一个回合制聚落建设项目。当前运行时以 `GameSystem` 为游戏级编排入口，以服务持有领域状态；地图、建筑、库存和科技已经形成稳定边界：

```text
AppBoot / SceneLoadingPipeline
            |
            v
        GameSystem -------------------- DataManager
            |                               |
            | GameSystem.Instance.Services  | GameData / AppData
            v                               v
  Turn / Building / Inventory / Technology / Quest / Policy / ...
            |
            v
  MapRuntimeHost + GridMapBehaviour + Content Scene
            |
            v
        UIPanel_Game（只展示并转发）
```

运行时领域服务统一从以下入口访问：

```csharp
GameSystem.Instance.Services
```

存档统一从以下入口发起：

```csharp
DataManager.Instance.SaveCurrentGame();
DataManager.Instance.SaveCurrentGame(GameDataSaveMode.NewSave);
```

不要在 UI 中重写领域规则，不要重新为 `GameSystem` 添加任务、科技、远征、人才等同义代理 API。

## 2. 按任务找文档

| 要做的事情 | 首要文档 | 主要代码 |
| --- | --- | --- |
| 理解启动、场景切换、服务创建和存档 | [运行时与存档](运行时与存档/README.md) | `AppSystem`、`Persistence`、`GameSystem/GameSystem.cs` |
| 修改地图、格子、地形、Overlay 或初始建筑 | [地图系统](地图系统/README.md) | `Grid`、`MapDefinition.cs`、`MapDataCatalog.cs` |
| 策划创建或验收地图 | [地图创建与校验](地图系统/地图创建与校验.md) | `Editor/MapAuthoringWindow.cs` |
| 新增建筑、等级、模块或建筑表现 | [建筑系统](建筑系统/README.md) | `BuildingSystem`、`Editor/Building*` |
| 查询现有建筑能力 | [建筑目录](建筑系统/建筑目录.md) | Family、ModuleSet、Presentation、Runtime Prefab |
| 修改建筑正式数值 | [建筑数值与导表](建筑系统/数值与导表.md) | `ConfigSource/Buildings/建筑数值策划表.xlsx` |
| 修改库存、槽位、损耗或经济预测 | [库存系统](库存系统/README.md) | `GameSystem/Inventory` |
| 修改物品与库存正式数值 | [库存数值与导表](库存系统/数值与导表.md) | `ConfigSource/库存系统/库存系统数值表.xlsx` |
| 修改科技研究、队列、解锁内容或科技 UI | [科技系统](科技系统/README.md) | `TechnologySystem`、`GamePanel_Technology*` |
| 修改任务运行时或任务 UI | [任务系统](任务系统/README.md) | `QuestSystem`、`GamePanel_Quest*` |
| 修改主线任务内容 | [主线任务](任务系统/主线任务.md) | `Assets/Landsong/Objects/SO/Quests`、`QuestCatalog.asset` |
| 修改政策 | [政策系统](政策系统/README.md) | `PolicySystem` |
| 修改回合、王朝、事件、远征、人才或继承 | [玩法系统](玩法系统/README.md) | `GameSystem`、对应 `*System` 目录 |
| 修改游戏内面板、输入或交互手势 | [UI 与输入](UI与输入/README.md) | `UI`、`InputSystem`、`CameraSystem` |
| 修改音频、设置或外部语言包 | [音频与本地化](音频与本地化/README.md) | `AudioSystem`、`GameLocalizationManager.cs` |
| 查看编码、API、数据和文档规范 | [开发规范](开发规范.md) | 全项目 |
| 查看项目级已冻结边界 | [正式架构决策](架构决策.md) | Quest、GameSystem、存档、命名空间、配置媒介和事件边界 |

## 3. 自研脚本目录职责

| 目录 | 责任 | 关键入口 |
| --- | --- | --- |
| `AppSystem` | 启动、场景切换、应用设置、存档门面、本地化 | `AppBoot`、`AppManager`、`DataManager`、`LSScenes` |
| `AudioSystem` | BGM、环境音、SFX 通道与 Cue | `AudioPlayer` |
| `BuildingSystem` | 建筑定义、生命周期、模块、放置、升级、表现与建筑服务 | `BuildingBase`、`BuildingService` |
| `CameraSystem` | 地图视角、拖拽、缩放、地图边界 | `CameraController` |
| `Condition` | 可序列化游戏条件基类与组合条件 | `GameCondition` |
| `Debug` | 运行时调试面板与 UI/建筑诊断 | `LSDebugManager` |
| `Editor` | 建筑/地图/科技编辑器、正式数值导入与架构校验 | 对应 EditorWindow/Validator |
| `ExpeditionSystem` | 远征定义、运行时状态、结算和存档 | `ExpeditionService` |
| `GameSystem` | 游戏级服务装配、回合编排、事件消息、王朝及库存目录 | `GameSystem`、`GameServices`、`TurnService` |
| `Grid` | 地图内容绑定、坐标、占用、地形、寻路和 Overlay | `MapRuntimeHost`、`GridMapBehaviour` |
| `InheritanceSystem` | 王族角色、继承、特性与回合结算 | `RoyalInheritanceService` |
| `InputSystem` | Input System 适配、指针状态、UI 命中和相机输入阻断 | `InputController`、`InteractionConstants` |
| `Persistence` | 存档模型、磁盘仓储、索引和运行时快照 | `GameRuntimeSnapshotService`、`GameSaveRepository` |
| `PolicySystem` | 政策配置、选择、民意和存档 | `PolicyService` |
| `QuestSystem` | 任务定义、进度、提交、奖励、随机任务与存档 | `QuestService` |
| `TalentSystem` | 人才、职位、工资、升级和隐藏特性 | `TalentService` |
| `TechnologySystem` | 科技研究、队列、完成效果、解锁内容索引和全局 Buff | `TechnologyService` |
| `UI` | 面板、建筑详情、HUD 和各领域视图 | `UIPanel_Game` |
| `Visual` | 通用轻量表现组件 | `SpriteFrameAnimator` |

## 4. 当前权威数据源

不同系统不强制使用同一种配置媒介。修改前先确认该字段的所有者：

| 领域 | 权威源 | Unity 资产角色 |
| --- | --- | --- |
| 建筑正式数值 | `ConfigSource/Buildings/建筑数值策划表.xlsx`，Schema 9 | Family/模块等级配置是导入结果；结构与表现仍在 Unity |
| 物品、物品组、库存槽位类型数值 | `ConfigSource/库存系统/库存系统数值表.xlsx`，Schema 5 | Item/Group/SlotType/Catalog 是导入结果 |
| 地图 | Content Scene + `MapDefinition` + `MapCatalog` | 直接编辑 Unity 资产 |
| 科技 | `TechnologyDefinition` + `TechnologyCatalog` | 通过科技编辑器维护；没有正式 Excel 导表 |
| 政策 | `PolicyDefinition` + `PolicyCatalog` | 直接编辑 Unity 资产 |
| 任务 | `QuestDefinition` + `QuestCatalog` | `GameSystem.prefab` 只保存 Catalog 引用、起始 ID 和全局生成参数 |
| 人才、远征、继承 | 各自 Definition/Catalog 与 `GameSystem.prefab` 引用 | 当前部分内容绑定尚未完成 |
| UI、表现、音频 | Prefab、Scene、Sprite、AudioClip、Presentation | Unity 资产是唯一来源 |

生成资产不得反向覆盖正式 Excel。正式 Excel 也不得写入表现 Prefab、Animator、UI Root 映射等非数值内容。

## 5. 核心运行链

1. `Boot.unity` 中的 `AppBoot` 初始化应用、IO、存档、音频和本地化管理器。
2. `LoadScene_Start` 进入主菜单；新游戏由 `AppManager.StartNewGame` 创建 `GameData`，继续游戏由 `DataManager` 读取最近存档。
3. `LoadScene_Game` 加载 `Game.unity`，再按 `MapId` Additive 加载地图 Content Scene。
4. `MapRuntimeHost` 将 Content Scene 的 Grid/Base/Terrain 注入共享 `GridMapBehaviour`。
5. `DataManager` 通过 `GameRuntimeSnapshotService` 恢复 `GameSystem` 服务与建筑实例；新游戏只在首次地图初始化时生成初始建筑。
6. `UIPanel_Game` 绑定 `GameSystem.Instance.Services`，负责显示和转发操作。
7. `GameSystem.NextTurn()` 调用 `TurnService`：建筑回合 → 资源提供结算 → 库存自然损耗 → 科技/人才/继承/远征等跨系统结算。

## 6. 跨系统边界

- UI 只能调用服务公开 API、订阅事件并渲染状态；不能持有第二份领域状态。
- `GameSystem` 负责服务装配、回合顺序和真正的跨领域协调；单领域规则留在对应 Service/Module。
- 建筑对外通过能力接口和模块提供人口、生产、库存槽位、范围效果等能力；外部系统不判断具体建筑类。
- 库存不识别建筑家族，只接收 `InventorySlotProvision`；建筑不保存槽位类型的固定损耗规则。
- 科技 UI 不扫描建筑或 Buff；各内容生产者主动写入 `TechnologyUnlockContentRegistry`。
- 地图提供坐标、地形、占用和 Overlay；建筑放置规则通过统一评估器查询地图，不复制 Grid 规则。
- `DataManager` 是应用/存档门面，`GameRuntimeSnapshotService` 负责领域快照；各服务只捕获和恢复自身状态。

## 7. 常用资产与入口

- 核心场景：`Assets/Landsong/Scenes/Boot.unity`、`Start.unity`、`Game.unity`、`LoadingTransition.unity`。
- 地图内容：`Assets/Landsong/Scenes/MapScenes`。
- 游戏服务配置：`Assets/Landsong/Objects/Prefabs/GameSystem.prefab`。
- 游戏 UI：`Assets/Landsong/Objects/Prefabs/UI/UIPanel_Game/Panel_Game.prefab`。
- 建筑目录：`Assets/Landsong/Objects/SO/BuildingCatalog.asset`。
- 物品目录：`Assets/Landsong/Objects/SO/ItemCatalog.asset`。
- 库存槽位目录：`Assets/Landsong/Objects/SO/InventorySlotTypeCatalog.asset`。
- 地图目录：`Assets/Landsong/Objects/SO/MapCatalog.asset`。
- 科技目录：`Assets/Landsong/Objects/SO/SO_Technology/TechnologyCatalog.asset`。
- 政策目录：`Assets/Landsong/Objects/SO/PolicyCatalog.asset`。
- 任务目录：`Assets/Landsong/Objects/SO/Quests/QuestCatalog.asset`。

## 8. 修改完成后的最低验证

| 改动 | 最低验证 |
| --- | --- |
| 普通 C# | `dotnet build Assembly-CSharp.csproj --no-restore`；涉及 Editor 时编译 `Assembly-CSharp-Editor.csproj` |
| 建筑结构/数值 | 建筑导表 Analyze + Import、`Validate Final Architecture`、Play Mode 建造/升级/存读档 |
| 库存数值 | 库存导表全表校验、Catalog 生成、自动入库/损耗/存读档 |
| 地图 | 地图编辑器校验、“从当前地图开始 Play”、Additive 加载与初始建筑 |
| 科技 | 科技编辑器依赖校验、重新生成科技树 UI、研究队列与完成效果 |
| UI Prefab | 源码编译 + Prefab 引用检查 + Game View 实测；源码编译不能证明序列化绑定正确 |
| 存档 | 新建、覆盖、另存、返回主菜单、重新加载；确认当前版本拒绝策略 |
| 文档 | 相对链接检查、过时路径搜索、事实与代码/资产/正式表复核 |

## 9. 文档维护规则

1. 每个系统目录只有一个 `README.md` 作为架构入口。
2. 子文档只按明确职责拆分，例如数值导表、内容清单、表现交付；不得复制 README 的架构说明。
3. 历史审计、实施计划和阶段性变更流水不再保留在现行文档目录；仍有价值的结论并入当前文档。
4. 代码、正式配置源、Unity 资产与校验器输出优先于文档；发现漂移时在同一任务回写文档。
5. 新增系统时同时在本页登记责任、代码入口、配置源、存档边界和验证方式。
