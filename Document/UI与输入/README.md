# UI 与输入

UI 使用 TMP 与 UGUI。应用由一份跨场景保留的 `UI_Root.prefab` 承载 Canvas、`UIManager`、输入事件与应用流程；Boot、Start、LoadingTransition、Game 场景不保存另一套界面。根面板按需加载，设置、存档和确认面板在主菜单与游戏中共用同一管理器实例。Game 内部结构见 [Game 场景布局](Game场景布局.md)，场景事务见 [启动与场景工作流](../ECS/启动与场景工作流.md)。

## 资源、目录与程序集

所有业务 UI 脚本放在 `Assets/Landsong/Scripts/UI/`，按根面板分为 `StartPanel`、`GamePanel`、`SettingPanel`、`SavePanel`、`LoadingPanel`、`BootPanel`、`ConfirmPanel` 与 `Common`。根组件叫 `UI_StartPanel.cs`，其新王朝弹窗叫 `UI_StartPanel_GameStartPop.cs`，同放 `UI/StartPanel/`；Game 科技控制器为 `UI/GamePanel/UI_GamePanel_Technology.cs`。类名与文件名保持一致。UI 内部服务和接口放根目录的 `Services`、`Contracts` 等子目录，不把非组件服务硬改为面板名。

预制体根目录为 `Assets/Landsong/Objects/Prefabs/UI/`：

| 位置 | 用途 |
| --- | --- |
| `Bootstrap/UI_Root.prefab` | 唯一应用 Canvas 与管理器，不是普通可关闭面板 |
| `StartPanel/UI_StartPanel.prefab` | 主菜单，拥有新王朝配置子视图 |
| `LoadingPanel/UI_LoadingPanel.prefab` | 加载阶段、取消与错误页 |
| `SettingPanel/UI_SettingPanel.prefab` | 应用共享设置 |
| `SavePanel/UI_SavePanel.prefab` | 应用共享加载、保存与存档管理 |
| `ConfirmPanel/UI_ConfirmPanel.prefab` | 应用共享确认 |
| `BootPanel/UI_BootPanel.prefab` | 开幕展示 |
| `GamePanel/UI_GamePanel.prefab` | 游戏会话 HUD 与其子视图 |

根面板旁的同名 `.asset` 是 `UIPanelAsset`，显式引用强类型根组件及其已配置的 CanvasGroup。注册表位于 `Assets/Landsong/Objects/SO/UIConfig.asset`；每个 `panelId` 必须与根组件类名一致。当前正式项经 Addressables 加载描述资产，例如地址 `UI/UI_SettingPanel`；不能把描述地址误配成原始 GameObject 地址。描述、预制体、类型与注册项必须一一对应。Game 的独立子预制体与条目放在 `GamePanel/Views/`、`GamePanel/Items/`，仍归 Game 根拥有，不登记为全局面板。

运行时按 Core、Authoring、AI、共享表现、UI、Application、Verification 分区，另有 ECS.Editor 编辑器程序集。UI 相关命名空间暂保留 `Landsong.ECS.Presentation` 以减少迁移风险：

- `Landsong.ECS.Authoring`：内容资产、烘焙和实体渲染适配。`Landsong.ECS.AI`：行为树与战术决策适配。二者单向引用 Core；UI/共享表现实际使用内容资产时直接引用 Authoring。Core 的 Simulation 与 Persistence 保持同一事务边界，不依赖 Authoring、AI、Hybrid、Entities Graphics 或 Opsive。
- `Landsong.UI`：UI 组件、会话展示状态、命令与输入适配；引用 Moyo、核心和共享表现层。
- `Landsong.Application`：应用根、场景注册和切换事务；引用 UI 与共享层，UI 不反向引用它。
- `Landsong.ECS.Presentation`：位于 `Scripts/Presentation/`，保留跨层契约、地图菜单/视听配置、世界表现和共享服务。`IApplicationUi`、存档打开请求与 `EcsSceneFlow` 给 UI 传递意图，实际场景流程由应用层实现。
- `Landsong.Verification`：开发构建/编辑器运行验证；生产逻辑不依赖验证代码。Editor 制作和静态检查工具位于 `Scripts/Editor/`。

## 根面板、子视图与会话

`UIManager` 是显式挂在根 Canvas 上的普通组件。应用根调用 `Initialize()` 校验配置并保留根对象；静态登记只定位已安装的应用根，不扫描场景，也不自动补建 Canvas、EventSystem 或缺失组件。层级容器是同一 Canvas 下的 RectTransform。

全局根面板继承 `UIPanelBase`，只通过管理器打开、关闭或释放。内部可管理视图继承 `UIViewBase`，由所属视图的“直属子视图”数组显式登记；登记项必须是实际子对象，不能重复、循环或包含另一个根面板。普通控件、列表行和纯布局组件仍可以是 MonoBehaviour，不需要逐个变成根面板。

框架按配置建立所有权并递归创建子视图。随所属视图打开的子视图自动进入打开生命周期，其他子视图由主人按需调用 `OpenViewAsync` / `CloseViewAsync`。关闭根会关闭子树，释放根会逆序清理子树。根面板的 `OnOpenAsync` 每次接收新的打开上下文；重复打开已显示面板也会先清理上次打开状态，再更新上下文。订阅与资源应按其实际寿命分别放入创建/释放或打开/关闭钩子。

关闭与释放不同：Start、Boot、Loading、Setting、Save、Confirm 属于应用作用域，关闭后缓存；Game 属于独立 `UIScope`，关闭后释放。离开游戏先关闭共享模态、清除其会话回调，再解绑 Game 的 ECS、相机、输入、世界表现及暂停状态，最后结束会话作用域。结束作用域会取消未完成的 UI 操作并释放该作用域实例及加载租约。下一局创建新的 Game 根，不把上一局的 EntityManager、Entity 或玩家选择继续留在常驻 UI。

Game 功能窗口在根的显式导航注册表中各自拥有固定容器。根负责互斥、导航历史和输入门禁；各控制器负责其领域的数据展示。窗口容器的本地显隐与框架根面板的打开/关闭是两个层次，不能绕过根管理器直接激活共享 Setting、Save 或另一个 Game 根。

局部导航使用稳定枚举 `GamePanelId`，`FeaturePanels`、`NavigationButtons`、当前窗口和返回历史都保存类型化目标；中文标题不参与查找或跳转。窗口的 `RequiredFeatureId` 显式配置 `feature.*` 内容 ID，访问规则由其 `CanOpen` 核验。Unity 持久 Button 事件通过 `OpenPanelFromEvent(int)` 校验后转换为枚举，非法值或 `None` 直接报配置错误，不提供显示名到面板的兼容回落。这里的 `GamePanelId` 与 Moyo 根注册表的类名 `panelId` 是两个不同层次。

`GameUiRefreshScheduler` 归当前会话拥有，分别调度 HUD 与可见内容。回合、阶段、暂停、情报、存档等待、夜间速度、设置版本、表现事件、交互结束及显式失效请求会更新展示版本；HUD 与较重内容的默认节流周期分别是 0.2 秒、0.25 秒；状态变化可提前刷新 HUD，显式命令失效可请求立即刷新内容，版本未变且没有连续模拟时不重绘。夜间等连续阶段仍按节流更新。输入框、库存拖动/编辑和劳动力调整可暂缓内容重绘，HUD 继续运行，避免全局按住鼠标就冻结信息。

列表行用身份匹配复用，业务条目优先提供稳定领域键。`UI_GamePanel_InteractionLock` 与显式 `UI_GamePanel_RowPointerBinding` 将按下、抬起/点击分派及文本编辑归给对应行/容器；交互未完成时不重新分配该行的身份、回调和排序。结束交互递增版本，再协调列表变更。换局会重置调度器与交互/行缓存，不沿用上一局版本。

## 主菜单、共享设置与存档

主菜单依次提供继续、新王朝、加载、设置、退出。继续只在最近王朝或有效备份可读时展示。“开始新王朝”打开 `UI_StartPanel_GameStartPop`，填写名称、选择地图并查看说明；点击建立才提交场景请求。难度目前仍是界面草稿，未定义数值效果；特殊规则占位不等同于已实现玩法。Esc 先收起展开的下拉框，再关闭配置子视图，Start 根本身不因返回键关闭。

`ApplicationUiRoot` 实现共享导航接口。主菜单与 `UI_GamePanel_PausePop` 只调用接口打开 `UI_SettingPanel`、`UI_SavePanel` 或 `UI_ConfirmPanel`，不持有各自的设置/存档副本，也不各建一层共享面板遮罩。暂停页是 Game 内部窗口；关闭时恢复打开前的暂停状态，存档操作仍需满足当前会话的有效性与阶段约束。

设置每次打开读取新的偏好草稿。应用、恢复默认、按键重绑与显示变更确认均由一份 `UI_SettingPanel` 管理。显示变更需在 15 秒内再次确认才保存，超时或关闭恢复原显示；关闭还清理未完成重绑。共享面板缓存只保留 UI 结构，不代表草稿跨打开自动提交。

存档每次通过 `ArchiveOpenRequest` 接收 Load / Save / Manage 模式、存储服务、当前王朝、有效性检查与操作委托。`ArchiveApplicationService` 承担存档应用操作，`UI_SavePanel` 负责列表、详情与反馈；行模板为 `UI_SavePanel_ArchiveRow`。关闭清空选中记录、行回调、缩略图及打开上下文，防止常驻实例持有旧游戏会话。局部返回先从详情回列表，再关闭根。白天保存、手动槽、备份及王朝恢复规则见 [运行时与存档](../运行时与存档/README.md)。

## 编辑示例与美术制作

打开正式预制体即可看到已序列化的代表性文字和模板示例。示例来自 `Assets/Landsong/Editor/UI/Profiles/` 下的 `UIPreviewProfile`，覆盖菜单、设置、存档、HUD、科技、人才、王室、建筑详情、赐婚和士兵详情；塑容窗口另有独立预览状态。不依赖正在运行的 ECS、真实玩家存档或当前随机结果。

可持续刷新的配方使用 `UIPreviewRecipe`，同样只存放在 Editor 目录。配方显式绑定目标 UIViewBase、数据资产、文字目标与运行初始文字，以及列表容器、真实行模板、文字列和可选布局。“刷新预制体示例内容”会更新真实序列化文字并创建示例克隆。科技与人才配方使用显式网格参数（列数、尺寸、间距、边距）；默认布局保持模板行为，网格只改示例克隆，不改运行模板或容器尺寸。

制作流程：

1. 在目标根目录创建或编辑预制体，先完成固定层级、控件和禁用行模板；每个暴露到检查器的项目字段添加中文 Odin `LabelText`。
2. 为根绑定 CanvasGroup，为需要生命周期的子视图配置归属；补齐所有 TMP、按钮、滚动区、图标、输入焦点与肖像引用。
3. 在 Editor 目录通过 `Landsong/UI/编辑预览数据`、`编辑预览绑定配方` 创建资产，绑定目标与字段；编辑示例数据后刷新配方。现有 Game 配方也可用菜单 `Landsong/UI/刷新游戏预览配方与示例` 重新绑定和刷新。
4. 在 Prefab Mode 检查内容、空态、布局和缩放，保存预制体与配方。当前正在编辑的 Prefab Stage 会记录 Undo 并标脏，不直接覆盖未保存内容；未打开的目标预制体则加载、保存并卸载。
5. 运行配置与预览检查，再验证实际打开、关闭、重复打开、场景切换和输入行为。编辑示例截图不能代替运行验证。

示例克隆统一标记 `EditorOnly`，由 `UIPreviewOnly` 保存清单，并在所属视图“编辑示例清理配置”中显式引用。运行创建钩子之前禁用示例、恢复真实初始文本；Player 构建移除 EditorOnly 示例。真实控件和真实行模板不能标为 EditorOnly。刷新时只删除已登记的示例，不扫描并删除任意子对象；重复刷新不累积节点。不要把 Profile / Recipe 反向引用到运行时 UI，也不要依靠一轮运行后的场景状态充当预览资源。

## 引用检查与输入边界

运行时禁止用 Unity 的 Find、GetComponent 系列查找固定 UI 引用，也禁止缺引用后 AddComponent 或新建替代控件。模板使用强类型引用，数量可变时只复制已配置模板；改名、换层级或换皮肤后由资源配置检查报告断链。Editor 的资产查询、配置检查和预览映射，以及 ECS 的实体查询/组件操作，不属于运行时固定 Unity 引用修补。

七个业务根 Prefab 都采用全屏拉伸锚点、零位置/尺寸差、单位缩放，不保留旧 Canvas 的屏幕尺寸。Game 的 `FeatureRoot` 在参考画布中预留 56 单位底栏；普通功能窗在此区域内排布，HUD 和模态层有各自归属。预览渲染会校验原始配置，不替错误根布局归零；实际 Play 还比较已加载根与 Canvas 的世界角点。有效布局组不控制子控件宽度时，参与布局的 Button/Input/Dropdown 必须有正的宽度；拉伸锚点不能作为例外，因为布局组仍会驱动锚点。赐婚与塑容底部操作采用布局组控制的等宽按钮。

非输入 TMP 使用 `UI_Common_TextBinding.Target`，按钮使用 `UI_Common_Click.Target`；输入框通过 `UI_Common_InputFocusBinding` 登记焦点，正文保持玩家输入。肖像通过 `UI_Common_PortraitImageBinding` 显式引用 Image 和 PortraitCache；士兵详情与所属 Game 的条目共享该缓存，关闭详情时调用 `Unbind()` 释放租约、人物身份与世界引用。共同的语言、音效与世界表现服务不遍历场景补挂组件。

Esc 由应用根每帧统一路由到管理器最上层焦点面板。管理器先询问 `TryHandleBackAsync`，已消费则停止，再判断配置是否允许关闭。因此 Start / Game 即使不可被返回关闭，也能处理各自局部弹窗。共享模态打开时阻断底层游戏输入；Game 内部按其路由优先处理士兵详情、塑容、人物请求、赐婚等，再处理暂停。普通功能窗口仍有明确关闭与返回入口。

Game 根显式创建 `GameUiInputPolicy`，统一采集共享模态及士兵详情、塑容、人物请求、婚姻、暂停和建筑确认的当前所有权。每次输入使用一份快照；导航、世界输入、私有模态入口与命令提交共用规则。叠加模态取命令许可的交集，保留士兵改名、暂停/存档与情报镜头操作的例外，返回优先级由同一规则表定义。策略不另存一份容易失步的模态开关。

镜头、建造、英雄与库存拖动由 Game 输入适配器处理。UI 起点认领拖动，输入框聚焦时阻断对应快捷键，游戏内模态和情报模式继续执行各自门禁。UI 只保存展示/输入状态并提交命令；费用、权限、阶段和实体有效性由 ECS 再次检查。

配置入口为 `UiConfigurationVerification.Run()`；其中 `UiRuntimeCallVerification` 读取当前已编译 IL 的实际方法调用，覆盖别名、泛型、异步与 lambda，补充源码规则对禁止查找/修补 API 的检查。检查范围为自有 UI、Application、共享表现与实际使用的 Moyo UI 辅助代码；不遍历第三方实现，也不声称能分析任意动态反射。运行前须确认编译完成。预览制作回归为 `UIPreviewVerification.Run()`，框架生命周期回归为 `UIFrameworkVerification.RunAsync()`。程序集检查与运行验证流程见 [验证工作流](../ECS/验证工作流.md)；本轮实际执行结果以 [重构执行记录](../重构执行记录.md) 为准。功能规则继续分别维护在 [建筑系统](../建筑系统/README.md)、[科技系统](../科技系统/README.md)、[任务系统](../任务系统/README.md) 等领域文档中。
