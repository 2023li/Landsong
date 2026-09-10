# Landsong 当前基准

基准日期：2026-09-10。本文档只描述当前实现、制作入口和已知限制。当前阶段任务已收口，不包含后续内容扩充或性能发布计划。

Unity **6000.3.5f2**；Entities / Entities Graphics **1.4.2**；Behavior Designer Pro **3.3.2**。游戏使用 ECS 权威状态，界面使用 **UGUI + TMP**。存档为 **快照 v22 / 王朝封装 v3**，包含人物性别、王室登基历史、赐婚待办、远征愿望、建筑补贴预算、肖像 DNA、丽质一次性状态、士兵个人生命周期和任务承接容器/槽号。

## 从哪里开始

打开 `Assets/Landsong/Scenes/Boot.unity`，进入 Play：开幕展示 → Start 主菜单 → LoadingTransition → Game。日常流程验证使用 **Map_Test2**；Map_Test01 不承担新手流程验收。

| 路径 | 用途 |
| --- | --- |
| Assets/Landsong/Scripts/ECS | 数据、模拟、AI、存档、表现及原生编辑器 |
| Assets/Landsong/Scripts/MapAuthoring | TWC 编辑数据、坐标和初始建筑预览 |
| Assets/Landsong/Scripts/ArtTools | 建筑模型导出与农田点位/阶段制作 |
| Assets/Landsong/Scripts/Editor | 地图、美术、语言工具与测试 |
| Assets/Landsong/ECSContent/Definitions | 185 项独立内容定义，其中 29 类建筑 |
| Assets/Landsong/ECSContent/GameCatalog.asset | 定义注册、规则及初始王朝参数 |
| Assets/Landsong/ECSContent/Prefabs | 建筑分级/皮肤与单位的 Authoring Prefab |
| Assets/Landsong/ECSContent/Resources/LandsongPresentation.asset | 动态模型、画像、音频和语言运行表 |
| Assets/Landsong/ECSContent/Presentation | 纯表现模型、动画、粒子与材质 |
| Assets/Landsong/Art/Portraits/Parts | 肖像导入窗口生成的部件 PNG，按类别 / 稳定标识归档 |
| Assets/Landsong/Scenes | 四个正式场景；MapScenes 为制图源，EntityMaps 为地图 SubScene |

UI 基础结构位于四个场景各自的 `ECS UI` 对象下，**目前没有独立的整套 GameUI Prefab**。动态布局位于 Scripts/ECS/Presentation。Input System 自动生成代码随输入资产保存，不手工维护。

## 文档导航

- [架构](架构决策.md)、[开发规范](开发规范.md)、[内容配置](ECS/内容与规则.md)
- [启动与场景](ECS/启动与场景工作流.md)、[地图制作](地图系统/README.md)
- [建筑玩法](建筑系统/README.md)、[建筑模型制作](ECS/建筑玩法与制作工作流.md)、[经济与岗位](建筑系统/经济与岗位.md)、[库存](库存系统/README.md)
- [昼夜与战斗](昼夜与战斗系统/README.md)、[士兵与英雄](昼夜与战斗系统/士兵与英雄.md)、[AI 与飞行物](昼夜与战斗系统/AI与飞行物.md)、[情报](昼夜与战斗系统/情报.md)、[平安互动与战报](昼夜与战斗系统/平安互动与战报.md)
- [科技](科技系统/README.md)、[任务](任务系统/README.md)、[王室与人才](玩法系统/README.md)、[远征](玩法系统/远征.md)、[政策](政策系统/README.md)
- [UI 与输入](UI与输入/README.md)、[运行与存档](运行时与存档/README.md)、[表现、音频与本地化](音频与本地化/README.md)
- [验证工作流](ECS/验证工作流.md)、[当前限制](ECS/当前限制.md)

配置数值以当前资产及 Baking 校验为准。文档中的默认数值是可编辑基准，不代表最终平衡或正式美术已完成。

肖像系统的已确认规则、程序化占位和美术接入见 [肖像系统初稿（现行实现规格）](肖像系统初稿.md)。美术使用 Unity 菜单 **Landsong → ECS → Portraits → Import parts** 导入，按需添加一个或多个程序层，窗口检查尺寸、图层合法性和适用性别，成功后自动注册。
