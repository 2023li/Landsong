# 项目脚本

自研脚本统一位于 `Assets/Landsong/Scripts`，正式配置位于 `Assets/Landsong/ECSContent`，启动场景为 `Assets/Landsong/Scenes/Boot.unity`。

| 目录 / 程序集 | 职责 |
| --- | --- |
| `ECS/Simulation`、`ECS/Persistence` / Landsong.ECS | 唯一玩法状态、命令、模拟与事务；适配 A* Pro 导航和 ES3 持久化。 |
| `Content` / Landsong.Content.Runtime | 运行时显示目录、建筑视觉槽与共享选择规则；不引用 Authoring。 |
| `ECS/Authoring` / Landsong.ECS.Authoring | 内容制作、校验、Baker 和 Entities Graphics 适配。 |
| `ECS/AI` / Landsong.ECS.AI | Behavior Designer 决策适配；Opsive 类型不进入 Core。 |
| `Presentation` / Landsong.ECS.Presentation | 共享契约、场景路由、文本、声音与世界表现。 |
| `UI` / Landsong.UI | Moyo 面板与领域展示器；读取状态并发送命令。 |
| `Application` / Landsong.Application | 组合持久根，拥有场景切换、恢复和 UI 会话生命周期。 |
| `Verification` / Landsong.Verification | 仅 Editor / Development 编译的完整流程验证。 |
| `ECS/Editor`、`Editor` | 配置 Inspector、制作工具、领域验证和显式本机自动化。 |
| `MapAuthoring`、`ArtTools` | 制图中间格式、网格优化、作物点位与美术预览，不是第二套玩法模拟。 |

依赖方向：UI、Presentation、Application → Content.Runtime → ECS；Authoring → Content.Runtime / ECS。不得把 Core asmdef 移到 Scripts 根目录，也不得恢复 UI → Authoring 依赖。

场景切换只由 `EcsSceneFlow` → `GameApplicationFlow` 执行。`BeginAsync` 提供可等待的结果，`TryBegin` 明确拒绝忙碌请求，异常进入可诊断的终态。正常退出先等待应用流程与 `UIManager.ShutdownAsync`；销毁回调只同步兜底。

显示目录由 `Landsong > ECS > Compile runtime display catalog` 从 `GameContentSet.asset` 引用的各领域目录单向生成。修改图标、说明或科技树位置后重新编译；构建和架构验证拒绝过期目录。建筑预览槽保留原脚本 GUID。

作物制作只使用带 `BuildingCropPlantPoint` 的直接子对象。旧点位已经保存为当前资产并删除兼容字段。

13 个一次性迁移器已归档到 `Tools/RetiredMigrations/20260917`，不再编译或出现在自动化入口。仍需使用的路径、绑定和校验逻辑已提取为日常 Editor 工具；不要把归档复制回 Assets。不应重建自研寻路/避让或绕开 ES3 的并行存储基础设施。

验证入口与当前结果见 [验证工作流](../../../Document/ECS/验证工作流.md) 和 [2026-09-17 问题修复记录](../../../Document/ECS/问题修复记录20260917.md)。
