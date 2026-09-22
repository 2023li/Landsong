# A* Pro / ES3 基准迁移开发进度节点

> 创建日期：2026-09-21  
> 性质：临时续接文档。在本次迁移全部完成并通过验证前不得删除。

## 目标

- 导航图、路径搜索、路径跟随与多单位局部避让全面迁移到 A* Pathfinding Project Pro。
- 桥梁按普通宽地面处理，内宽不小于 3 格，不再维护单格桥对向通行仲裁。
- 持久化的序列化、文件、备份、压缩与存档槽基础设施全面迁移到 Easy Save 3。
- 保留 Landsong 必需的 ECS 状态提取、业务校验、稳定 ID 重建与失败回滚。
- 把本次结果定为新基准版本，清理文档中已弃用地图、旧导航架构和旧存档格式的过期描述。

## 已确认的边界

- A* Pro 已导入 `Packages/com.arongranberg.astar`。
- ES3 3.5.26 已启用运行时/编辑器 asmdef，当前业务存档已迁移到 ES3 v4 封装。
- A* Pro 负责导航能力；Landsong 仍负责战术目标、阵营规则和存档恢复事务。
- A* 路径、RVO 瞬时速度和导航缓存属于派生态，不写入存档；读档后从权威游戏状态重建。

## 进度

- [x] 确认迁移范围和桥梁规则。
- [x] 确认 A* Pro 与 ES3 已导入。
- [x] 创建可续接进度节点。
- [x] 盘点 A* Pro 5.4.7 的 PointGraph、ABPath、AIMovementSystemGroup、RVOSystem 和轻量 ECS RVO API。
- [x] 建立 `AstarNavigationRuntime`，将权威 `SurfaceNavNode/Edge` 转换为运行时 PointGraph；占地变化复用节点并增量更新连接，地图或节点布局变化时重建。
- [x] 生产路径搜索迁移到 ABPath，单位接入官方 ECS RVO，最终位移继续投影到分层权威地表。
- [x] 删除自研 `Avoid/Score/Separate` 和 `NarrowPassageOps`；`SurfacePathQuery` 只保留地表定位/移动约束及隔离 Editor 夹具回退。
- [x] 平桥按一般地面处理，Layer 编译、建筑放置和最终 MapAsset 烘焙均拒绝小于 3 格的平桥。
- [x] 启用 ES3 程序集定义，建立 v4 `ArchiveEnvelope/ArchivePayload` DTO、Gzip 与 SHA-256 校验。
- [x] ES3 接管主存档、官方 `.bac` 备份、槽位、指针、名称、缩略图、终局墓碑和损坏文件；界面设置使用 ES3 PlayerPrefs 后端。
- [x] 完成生成工程的核心、Presentation 和完整 Editor 编译验证。
- [x] Unity Editor 已完成脚本刷新，并通过导航、存档、恢复回滚及完整四场景 Play 回归。
- [x] 更新入口、导航、地图、存档和架构文档，删除过期地图描述。

## 刷新后的运行时修复

- 首次 Play 回归发现夜间单位生成后原地等待。原因不是 RVO 拥堵，而是 PointGraph 转换把一对双向地表边依次写成两条 `OneWay`；A* Pro 会替换同一节点对的方向标志，第二次写入覆盖第一次，最终图出现大量单向格点。
- 转换先聚合全部有向边：互为反向的边按同一节点对写入双向标志，真实单向边保留单向标志；初建批量填写官方 Connection 数组，增量变更使用 `GraphNode.Connect/Disconnect`。
- 修复后夜间实机日志只有 `Path Completed`，并通过“真实 DBP 移动完成拦截”以及巡逻、归营、访客拦截等运动断言；没有新的 `Path Failed` 或“搜索完全部可达节点仍无路径”。
- 宽桥改为普通地面节点后，低净空筛选曾把刚加入的桥面节点自身判为障碍；现已排除当前节点并补齐桥面高度层级。桥上双向/横向、桥下独立通行、损坏代价、存档恢复和拆除均通过。
- A* 移动到楼梯端点时，格定位会在到达格中心前切换到平面节点，造成约四分之一格的高度跳变；最终移动现在沿已连接的“平面端点—楼梯节点”边连续插值，专项实测最大误差约 `0.082` 格。
- 完整流程随后暴露 ES3 JSON 对 `null byte[]` 的兼容问题：尚未生成的黄昏/手动快照能写出但无法读取。v4 DTO 现在用空数组作为磁盘哨兵，读取后恢复为 `null`，不改变领域语义。
- ES3 原子写入的故障注入现在发生在创建 `.bac` 之前，失败操作不会先改变备份及存档戳；主文件、备份和界面确认仍由 ES3 文件 API 管理。
- ES3 压缩后文件长度不再适合用固定字段变更构造等长测试数据；陈旧确认夹具现在直接构造等长内容变更并保留时间戳，继续验证内容摘要而不是仅依赖时间戳或长度。

## 恢复工作时首先检查

本轮性能修复节点（2026-09-22）：

- 居民房放置后出现蓝色网格覆盖：运行时 AstarPath 默认开启 `showNavGraphs`，PointGraph 默认绘制 Gizmos，Game 视图启用 Gizmos 时显示导航连线与不可通行节点。项目创建的服务现在关闭导航调试显示，运行时图设置 `drawGizmos=false`；不影响寻路或占地规则。

- 用户 Profiler 显示放置建筑一帧约 2286ms，其中导航 Work Items 约 2159ms。确认普通占地变化会整图重建，且 `HashSet<ulong>` 的默认哈希对 `(source << 32) | target` 做高低位异或，规则网格的相邻边大量碰撞，产生近二次复杂度。
- 边键改用 `math.hash(uint2)` 比较器；普通占地更新保留 PointGraph/PointNode，只更新通行、代价和变更的连接；初建连接数组一次分配，并启用官方 PointGraph KD-tree 查询。
- 地表权威缓存目前仍随占地变化全量生成，桥梁/楼梯新增或删除导致节点布局变化时仍重建 A* 图；本次不承诺所有建筑操作均落在 16ms 内。
- 160×160 独立编辑器夹具：节点复用但未修哈希时初建 6469ms / 更新 8931ms；修正哈希后初建 137.40ms / 更新 192.65ms。该对比不是用户截图场景的直接前后采样。
- 本轮 `TerrainConnectionVerification` 51 条通过，覆盖 25600 格性能门槛、节点身份复用、放置绕行、拆除直达、桥上/桥下/楼梯实际运动与连续高度。Editor 工程编译成功。`SceneFlowSoldierPlay` 于 2026-09-22 14:32:54 PASS，确认真实准备阶段巡逻和实体归营；结果见 `Library/LandsongEcs/soldier-night-play-verification.txt`。

地图场景瘦身节点（2026-09-22，已落地）：

- 决策范围：暂不支持自定义地图或独立地图编辑器；TWC 源数据继续提交，运行地形 Mesh 生成到 Git 忽略的 `Assets/LandsongGenerated/GameMaps`。
- 已加入 `RuntimeMapArtifacts`：稳定 GUID 外置 Mesh、剥离源场景生成表现、输入哈希清单、显式生成/干净重建入口、Play 前自动补齐和 Player Build 前补齐。
- MapAsset、逻辑网格和轻量 Entity Scene 暂时继续提交，以保留现有 MapId/SubScene GUID 和运行加载流程；运行 Mesh 不提交。
- Map01 已完成迁移：源场景由 `156.77 MiB` 降为 `1.88 MiB`，Entity Scene 由 `120.02 MiB` 降为 `2.87 MiB`；两者均不再包含内嵌 Mesh。运行目录生成 `1042` 个 Mesh，约 `114.11 MiB`，由 `.gitignore` 排除。
- 清空整个生成目录后只重建 Mesh，Entity Scene 的 SHA-256 保持 `2C80D86635D156F58489C0D0E8172C0117FEBCD7DFCD64E5F4D04D42E52133B8`，证明干净重建没有改写已提交的轻量 Entity Scene。
- `VerifyMapAuthoring` 36 条断言通过；干净重建后的 `MapLoadingPlay` 连续两次加载/释放通过，共 6 条断言。新增 `RuntimeMapArtifactVerification` 防止场景重新内嵌 Mesh、生成目录误入 Git或 Entity Scene 丢失外置引用。

1. `git status --short`，不覆盖用户并行修改。
2. `Packages/com.arongranberg.astar/package.json` 和插件 asmdef/API。
3. `Assets/Landsong/Scripts/ECS/Simulation/NavigationSystem.cs`、`SurfaceNavigationGraph.cs`、`SurfacePathQuery.cs`。
4. `Assets/Landsong/Scripts/ECS/Persistence` 及 `PersistenceContractVerification.cs`。
5. Unity 刷新完成后优先运行 `Landsong/ECS/Verification/Terrain connections`、存档两个专项入口和 `Verification/Run all`。

## 验证记录

- `dotnet build Landsong.ECS.csproj --no-restore -v:minimal`：0 警告，0 错误。
- `dotnet build Landsong.ECS.Presentation.csproj --no-restore -v:minimal`：0 警告，0 错误。
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:minimal`：0 错误；6 条既有过时 API 警告来自 `TwcLabConnection`、ES3 `Light.cookieSize` 和 ES3 Editor Postprocessor。
- Unity Tundra 脚本编译：成功；`compilationFailed=False`。
- `VerifyPersistenceContracts`：成功；持久化协议 241 条、存档应用服务 52 条断言通过，包含 ES3 可选节点、`.bac` 主备恢复、槽位、陈旧确认与会话权限。
- `TerrainConnectionVerification`：44 条断言通过；覆盖宽桥、桥下、楼梯双向及连续高度、A* 实际移动、至少 3 格宽规则、保存恢复与拆除。
- `SceneFlowPlay`：2026-09-21 21:22 最终完整 PASS；覆盖四场景、夜间真实导航与拦截、巡逻/归营、昼夜、实时回滚、冷存档恢复、快速继续、损坏存档和取消清理。
- 完整 Play 日志中未出现新的 `Path Failed`；A* 请求均为 `Path Completed`。
- `RuntimeMapArtifactVerification`：2026-09-22 PASS，11 条断言；生成目录及根 `.meta` 均被 Git 忽略，正式源/Entity Scene 无内嵌 Mesh，外置引用和 GUID 完整。
- 缺失本地生成清单后从 Boot 启动，Play 防护自动取消首次切换、只重建运行 Mesh 并继续；`MapLoadingPlay` 连续两次加载/释放 Map_Map01，6 条断言通过。
- `VerifyAll`：2026-09-22 16:37 最终 PASS；55 个套件、0 失败、14969 条断言。
