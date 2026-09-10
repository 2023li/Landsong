# 项目脚本

原 `Assets/Landsong/ECS` 已整体移入 `Assets/Landsong/Scripts/ECS`。玩法、UI、制作工具和验证代码统一收在本目录，按职责分层，不恢复旧 GameObject 玩法架构。正式配置仍在 `Assets/Landsong/ECSContent`；启动仍为 `Assets/Landsong/Scenes/Boot.unity`。

| 目录 | 职责 |
| --- | --- |
| `ECS/Simulation` | ECS 玩法数据、命令处理、建造、经济、昼夜与战斗。 |
| `ECS/AI` | DBP 原生 Entity Task 与战术逻辑。 |
| `ECS/Authoring` | 原生定义、地图配置、Baker 与视觉烘焙。 |
| `ECS/Persistence` | 快照、存档与昼夜节点。 |
| `ECS/Presentation` | UGUI、四场景流程、输入与表现；只读状态并发送命令。 |
| `ECS/Editor` | ECS 配置 Inspector 与原生内容检查。 |
| `MapAuthoring` | TWC 烘焙中间格式、坐标/占地检查、ECS 初始建筑编辑预览。没有地图运行宿主、寻路服务或玩法单例。 |
| `ArtTools` | 实际制作场景仍使用的建筑网格优化、命名映射、作物点位/阶段预览；不读取王朝或建筑运行状态。 |
| `Editor` | TWC 烘焙/地形导入、美术工具 Inspector、网格工具、ECS 验证和显式本地自动化入口。 |

制图与美术工具中的 MonoBehaviour 是制作接口，不是另一套模拟；UGUI 和场景入口中的 MonoBehaviour 是表现适配层。`GridMapDefinition` 只作为 TWC 中间数据；正式游戏读取经过 Baking 的 ECS `GridData`。作物 GO 预览、局部旋转不等于 ECS 作物生长动画或单位动画已接入。

目录整合保留全部脚本与 meta，ECS 的三个 asmdef、程序集名称和命名空间不变。不要把 ECS 根 asmdef 移到 Scripts 根目录，否则会改变制作工具的编译边界。菜单中的 `Landsong/ECS` 是菜单分类，不是旧文件路径。

已删除旧启动管理器、GameSystem/Services、建筑模块、库存、昼夜战斗、任务/科技/人才/王室等旧服务、旧 UI、本地化运行桥、ES3 业务存档层和一次性迁移脚本。不要重建这些兼容层。

详细删除范围、备份和工作流变化见 `Document/ECS/Scripts清理记录.md`。菜单 `Landsong > ECS > Verify architecture cleanup` 检查统一脚本目录及程序集归属、旧类型缺席、正式场景/保留资产无 Missing Script，以及 TWC 和美术绑定完整。
