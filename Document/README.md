# Landsong 当前基准

更新日期：2026-09-12。本文档描述当前代码与资源组织。本轮非 UI 定向重构已完成，编辑器回归为 40 套件 / 11627 断言通过，四场景和研究 HUD 两项实际 Play 检查均通过，详见 [非 UI 重构执行记录](非UI重构执行记录.md)；此前 UI 重构结果单独保留在 [重构执行记录](重构执行记录.md)。

Unity **6000.3.5f2**；Entities / Entities Graphics **1.4.2**；Behavior Designer Pro **3.3.2**。游戏使用 ECS 权威状态，界面使用 **Moyo + UGUI + TMP**。新快照写入 **v24**，王朝封装保持 **v3**；旧 v22/v23 按冻结布局和各自的原内容签名读取，不因 UI 重构一律废弃。格式、签名与恢复规则见 [运行与存档](运行时与存档/README.md)。

## 从哪里开始

打开 `Assets/Landsong/Scenes/Boot.unity`，进入 Play：开幕展示 → Start 主菜单 → LoadingTransition → Game。日常流程验证使用 **Map_Test2**；Map_Test01 不承担新手流程验收。

| 路径 | 用途 |
| --- | --- |
| Assets/Landsong/Scripts/ECS | 模拟与存档共同组成运行核心；Authoring、第三方 AI 适配和原生编辑器各自编译 |
| Assets/Landsong/Scripts/Presentation | 应用接口、存档交互服务、设置、音频、本地化和世界外观 |
| Assets/Landsong/Scripts/UI | 按 StartPanel、GamePanel、SettingPanel 等根面板组织的 UI 组件 |
| Assets/Landsong/Scripts/Application | 持久 UI 根、场景入口、地图宿主与加载/恢复流程 |
| Assets/Landsong/Scripts/Verification/Tests | 编辑器和 Development Player 共用的完整流程验证 |
| Assets/Moyo | 通用面板加载、缓存、作用域、显示层、生命周期与预览框架 |
| Assets/Landsong/Scripts/MapAuthoring | TWC 编辑数据、坐标和初始建筑预览 |
| Assets/Landsong/Scripts/ArtTools | 建筑模型导出与农田点位/阶段制作 |
| Assets/Landsong/Scripts/Editor | 地图、美术、语言工具与测试 |
| Assets/Landsong/ECSContent/Definitions | 185 项独立内容定义，其中 29 类建筑 |
| Assets/Landsong/ECSContent/GameCatalog.asset | 定义注册、规则及初始王朝参数 |
| Assets/Landsong/ECSContent/Prefabs | 建筑分级/皮肤与单位的 Authoring Prefab |
| Assets/Landsong/ECSContent/Resources/LandsongPresentation.asset | 动态模型、画像、音频和语言运行表 |
| Assets/Landsong/ECSContent/Presentation | 纯表现模型、动画、粒子与材质 |
| Assets/Landsong/Objects/Prefabs/UI | 与脚本根面板目录对应的 UI 预制体及 Bootstrap/UI_Root.prefab |
| Assets/Landsong/Art/Portraits/Parts | 肖像导入窗口生成的部件 PNG，按类别 / 稳定标识归档 |
| Assets/Landsong/Scenes | 四个正式场景；MapScenes 为制图源，EntityMaps 为地图 SubScene |

四个入口场景通过显式 ApplicationSceneEntry 引用同一个 `Bootstrap/UI_Root.prefab`，运行时保留一个根 Canvas、UIManager 和 EventSystem。Start、Game、Loading、Setting、Save、Confirm 等根面板按需加载；设置与存档浏览共用同一套面板，游戏 HUD 及其功能子面板归属于游戏会话。检查器引用缺失是配置错误，由编辑器制作和校验修正。Input System 自动生成代码随输入资产保存，不手工维护。

## 文档导航

- [非 UI 重构执行记录](非UI重构执行记录.md)：A–E 实施结果、N01–N12 定向验证、完整编辑器回归及实际 Play 验收。
- [非 UI 架构说明与定向重构执行方案](非UI架构说明与定向重构执行方案.md)：2026-09-12 实施前静态审查、问题证据和原定批次；当前状态以执行记录为准。
- [架构](架构决策.md)、[开发规范](开发规范.md)、[内容配置](ECS/内容与规则.md)、[建筑模块配置](ECS/建筑模块配置.md)、[内容功能配置](ECS/内容功能配置.md)
- [启动与场景](ECS/启动与场景工作流.md)、[地图制作](地图系统/README.md)
- [建筑玩法](建筑系统/README.md)、[建筑模型制作](ECS/建筑玩法与制作工作流.md)、[经济与岗位](建筑系统/经济与岗位.md)、[库存](库存系统/README.md)
- [昼夜与战斗](昼夜与战斗系统/README.md)、[士兵与英雄](昼夜与战斗系统/士兵与英雄.md)、[AI 与飞行物](昼夜与战斗系统/AI与飞行物.md)、[情报](昼夜与战斗系统/情报.md)、[平安互动与战报](昼夜与战斗系统/平安互动与战报.md)
- [科技](科技系统/README.md)、[任务](任务系统/README.md)、[王室与人才](玩法系统/README.md)、[远征](玩法系统/远征.md)、[政策](政策系统/README.md)
- [UI 与输入](UI与输入/README.md)、[运行与存档](运行时与存档/README.md)、[表现、音频与本地化](音频与本地化/README.md)
- [验证工作流](ECS/验证工作流.md)、[当前限制](ECS/当前限制.md)

配置数值以当前资产及 Baking 校验为准。文档中的默认数值是可编辑基准，不代表最终平衡或正式美术已完成。

肖像的玩法与遗传规则见 [王室与人才](玩法系统/README.md)，持久身份见 [运行与存档](运行时与存档/README.md)。美术使用 Unity 菜单 **Landsong → ECS → Portraits → Import parts** 导入，按需添加一个或多个程序层；单前发、单后发均合法。窗口检查尺寸、图层合法性和适用性别，成功后自动注册。导入及其验证必须等待脚本编译和资源导入完成。
