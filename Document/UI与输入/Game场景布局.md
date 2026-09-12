# Game 场景 UI 布局

`Assets/Landsong/Scenes/Game.unity` 是世界场景壳，保存 `ApplicationSceneEntry`、`EcsGameHost`、相机、光源及地图 SubScene 引用。Game 不持有 Canvas、EventSystem 或另一套设置/存档面板。进入有效游戏会话时，应用流程从注册表加载 `Assets/Landsong/Objects/Prefabs/UI/GamePanel/UI_GamePanel.prefab`，将它挂到常驻 `UI_Root` 的 HUD 层。

## 所有权与预制体层级

以下是职责示意，不要求 Transform 使用这些中文名称；实际引用均通过检查器保存。

```text
UI_Root：Canvas / UIManager / ApplicationUiRoot / GameApplicationFlow
├─ HUD 层
│  └─ UI_GamePanel：一局游戏的根面板
│     ├─ HUD、功能导航、操作反馈、历史与阶段交互
│     ├─ 建筑目录、建筑详情、放置提示
│     ├─ 科技、任务、人才、王室、政策等功能视图
│     ├─ 经济、库存、驻军、情报、战报等独立窗口
│     ├─ Game 私有模态：暂停、士兵详情、赐婚、请求、塑容
│     └─ 各控件的禁用真实模板及 EditorOnly 示例
├─ Popup 层：共享 UI_SettingPanel / UI_SavePanel / UI_ConfirmPanel
└─ Blocker 层：共享 UI_LoadingPanel
```

只有 `UI_GamePanel` 登记为 Game 根面板。控制器按实际层级进入根的“直属子视图”树，统一创建、绑定所有者和释放；HUD 与历史等需要随根打开的视图显式配置该标志。局部功能窗口与列表也保存自己的固定容器，由 Game 根的导航协调器互斥显隐，不另建全局堆栈项。列表行、网格节点、纯布局组件不因为可复用就成为根面板。

Game 根配置为 Session 作用域、关闭后释放，打开时必须传入当前游戏 `UIScope`。应用流程在地图与模拟准备完成后调用 `BindSession`，传入 EntityManager、会话根、世界相机、共享 Scaler 和 EventSystem；视图不会自行查找这些对象。解绑时清理选择、导航历史、输入拖动、建造放置、稳定行、肖像/家谱缓存、请求和私有窗口，再结束作用域。相同的 Game 预制体可以用于下一局，但上一局运行实例不跨局复用。

## 目录与职责

脚本位于 `Assets/Landsong/Scripts/UI/GamePanel/`。预制体位于 `Assets/Landsong/Objects/Prefabs/UI/GamePanel/`，根以外的独立视图在 `Views/`，列表/卡片模板在 `Items/`。

| 组件或目录 | 职责 |
| --- | --- |
| `UI_GamePanel` | 显式引用、会话注入、功能导航与互斥、统一刷新、局部返回路由 |
| `UI_GamePanel_Hud` / `UI_GamePanel_History` / `UI_GamePanel_Phase` | HUD、历史过滤与反馈、阶段相关窗口 |
| `UI_GamePanel_Building` / `UI_GamePanel_Technology` / `UI_GamePanel_Quest` | 建筑、科技、任务展示和用户意图 |
| `UI_GamePanel_Court` / `UI_GamePanel_Talent` / `UI_GamePanel_Policy` | 王室、人才、政策的独立控制器 |
| `UI_GamePanel_Marriage` / `UI_GamePanel_PersonRequests` / `UI_GamePanel_Portrait` / `UI_GamePanel_Soldier` | 赐婚、人物待办、塑容、士兵详情等私有交互 |
| `UI_GamePanel_Economy` / `Inventory` / `Garrison` / `Intelligence` / `BattleReport` | 分别拥有内容、筛选、选择和滚动位置的功能窗口；文件均保留完整 `UI_GamePanel_` 前缀 |
| `UI_GamePanel_List` | 通用窗口绑定与渲染接口，不包含按玩法功能分类的长分支 |
| `UI_GamePanel_RowRenderer` / `UI_GamePanel_Row` 及行变体 | 稳定条目复用、文字/图标/按钮绑定、局部输入与选择 |
| `Services/GameUiSession` / `GameUiCommandWriter` | 只在有效会话内提供展示状态与命令提交 |
| `Services/GameUiInputPolicy` / `GameUiRefreshScheduler` | 统一输入所有权与命令权限、会话展示版本及分层刷新 |
| `UI_GamePanel_InteractionLock` / `UI_GamePanel_RowPointerBinding` | 显式认领行/容器交互，保护点击与编辑期间的身份和回调 |
| `Contracts/` | `GamePanelId` 及 `IGameUiNavigation`、`IGameUiPanelActions`、`IGameFeatureRenderer` 边界 |
| `Interaction/UI_GamePanel_WorldInteraction` | 将世界指针、相机和选择输入适配为 UI/命令意图；它依赖 UI，因而留在 UI 程序集 |

`FeaturePanels` 显式登记各独立窗口，`PanelId` 必须是唯一、非 `None` 的 `GamePanelId`。`NavigationButtons` 的目标也使用该枚举，中文显示名只负责展示。功能许可由窗口的 `RequiredFeatureId`、`AllowMissingFeature`、`AllowLockedOpen` 明确配置，不按面板中文名推断玩法权限。

通用窗口的序列化 `Presenter` 必须实现 `IGameFeatureRenderer`；绑定通过 `BindFeature` 注入会话、命令、导航和行服务，刷新由窗口的 `Render()` 分派给自己的实现。经济、库存等专用窗口可以实现自己的渲染逻辑。根不再通过逐项分支承接所有功能的绘制代码，窗口也不互相借用内容节点。

视图预制体按根组件命名，例如 `Views/UI_GamePanel_Hud.prefab`、`UI_GamePanel_Technology.prefab`、`UI_GamePanel_Court.prefab`。同一通用列表组件的用途变体使用 `UI_GamePanel_List_History.prefab` 等明确后缀。行资产如 `Items/UI_GamePanel_Row.prefab`、`UI_GamePanel_InventoryGridRow.prefab`、`UI_GamePanel_QuantityRow_Quest.prefab`。这些资产的根对象名与文件用途一致，原有 GUID 保留。

世界呈现的 `WorldPresentationView`、`PresentationActor`、`PresentationEffect` 和 `PresentationRuntime` 位于 `Scripts/Presentation/`，不伪装成 UI 面板。视听服务由应用根提供；世界模板与模型绑定由世界表现自己维护。

## 固定布局与可变条目

HUD 提供状态、阶段、功能导航、研究卡、任务追踪、英雄/人物请求入口和操作反馈。研究 HUD 读取 ECS 的研究状态，点击后打开科技；UI 不推进研究数值。操作反馈显示最近消息，完整记录由历史窗口承载。

建筑区域由目录栏与详情组成。目录分类/建筑卡使用显式模板，悬浮说明只刷新内容。详情名称输入、图标、经验、岗位、种植、驻军与其他模块均在预制体内制作；主滚动区与底部操作区分开，工人侧栏通过配置引用共享。放置提示整体面板在放置/移动期间显示，取消或结束会话清理，不只隐藏文字而留下背景。具体规则见 [建筑系统](../建筑系统/README.md)。

经济、库存、驻军、情报、战报各有独立窗口；驻军分别保存已分配/待分配两组滚动容器。科技、任务、家谱/人才/政策图保存各自图形容器和节点模板。固定标题、关闭按钮与返回入口放在滚动内容之外。王室自己的事务容器不借用其他功能窗口，普通关闭不应被定时刷新重新打开。

暂停、士兵详情、赐婚、请求和塑容属于 Game 私有交互，由局部路由阻断底层输入。暂停入口通过应用接口打开共享设置与存档，没有嵌套的第二套设置页面。返回处理与完整输入约束见 [UI 与输入](README.md)。

数量变化只实例化已配置的强类型模板。通用行及库存格、驻军组、士兵卡、人物卡、工人档位等变体保存自己的 TMP、Image、按钮与悬浮组件引用；复制时连同绑定一起复制。固定层级调整不通过运行时找名、找组件、补建控件或挪用备用窗口解决。

可见内容按会话展示版本刷新，连续模拟阶段再按节流推进；HUD 与较重列表/图形刷新分开。行按稳定身份协调增删，当前正在点击、拖动或编辑的行/容器由交互锁保留；锁的控件绑定必须随正式模板保存，不能运行时扫描补挂。配置及复用规则见 [UI 与输入](README.md)。Game 命令经过同一 `GameUiCommandWriter`，包括建筑放置提交；`GameUiInputPolicy` 集中决定不同模态的导航、世界输入和命令许可，具体费用/阶段仍由 ECS 核验。

## 可编辑预览与配置验收

常用 Game 预览配方位于 `Assets/Landsong/Editor/UI/Profiles/`：

| 配方 | 目标示例 |
| --- | --- |
| `GameHudRecipe.asset` | HUD 状态与入口示例 |
| `TechnologyRecipe.asset` | 科技标题与研究节点网格 |
| `TalentRecipe.asset` | 人才标题与人物节点网格 |
| `BuildingDetailsRecipe.asset` | 建筑名称、说明和详情示例 |
| `RoyalDetailsRecipe.asset` | 王室人物、影响力、人物详情和可滚动说明 |
| `MarriageRecipe.asset` | 赐婚双方资料、提示和三个等宽操作 |
| `SoldierDetailsRecipe.asset` | 士兵姓名、年龄、属性和能力；独立 EditorOnly 肖像示例随所属视图清理 |

通过配方检查器“刷新预制体示例内容”更新；整体重新绑定可运行 `Landsong/UI/刷新游戏预览配方与示例`。Profile 的示例内容可以由美术修改，正常刷新不重置已编辑的数据。网格配方显式保存列数、卡片尺寸、间距和边距，重复刷新保持排布；列表未启用网格时沿用模板布局。

示例是实际序列化到预制体的文本与 EditorOnly 克隆。对应 `UIPreviewOnly` 必须被所属 UIViewBase 显式登记，运行创建前恢复实际初始文字并禁用示例；正式模板继续供真实会话使用。不得拿真实存档或跑完一局后残留的实体当示例来源。预览制作细节见 [UI 与输入](README.md#编辑示例与美术制作)。

修改布局后先保存资产并运行 UI 配置检查，核验根/子归属、CanvasGroup、描述地址、模板、中文 Odin 标签、文本/点击/输入/肖像绑定与 PreviewOnly 清理配置。随后实际打开功能窗口，检查关闭和返回、输入聚焦、共享模态、缩放、连续进入两局及退出后的会话清理。预览验证与运行验证分别报告，实际结果记录在 [重构执行记录](../重构执行记录.md)。
