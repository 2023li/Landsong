# 建筑 Prefab 与表现资源回填指南

> 回答“施工、LV1～LVN 的美术素材不放进巨型 Runtime Prefab 后，应放在哪里、如何替换”。

## 1. 唯一存放位置

建筑纯表现 Prefab 统一放在：

`Assets/Landsong/Objects/Prefabs/BuildingViews`

推荐层级：

```text
BuildingViews/
  PlayerHome/
    Construction.prefab
    LV01.prefab
    LV02.prefab
  ResidentialHouse/
    Construction.prefab
    LV01.prefab
  Tree/
    Construction.prefab
    tree_01/LV01.prefab
    tree_02/LV01.prefab
    ...
```

Runtime Prefab 保持在 `Assets/Landsong/Objects/Prefabs/BuildingsRuntime`。两个目录不能混用：

- `BuildingsRuntime`：玩法实例、交互、占格、空 `ViewRoot`。
- `BuildingViews`：Sprite、模型、Animator、粒子、灯光和纯表现挂点。

把 Runtime Prefab 作为初始建筑放进地图 Content Scene 后，编辑态看不到建筑美术是预期行为。`MapContentAuthoring` 会在 Scene 视图显示建筑名称、阶段/等级、`StyleId`、未吸附警告和占地框，不会为了编辑预览创建隐藏 View 实例；进入 Play Mode 后才由 Presentation 加载真实表现。

原始 PSD、Aseprite、序列帧、材质和音频可以继续按项目美术源文件规范存放；进入建筑表现系统的组合入口必须是 `BuildingViews` 下的 View Prefab。

## 2. View Prefab 允许和禁止的内容

允许：

- `SpriteRenderer` / Mesh Renderer；
- `Animator`、AnimationClip、材质；
- 粒子、灯光、纯表现音效挂点；
- 为排序、翻转、阴影或动画服务的纯表现组件。

禁止：

- `BuildingBase` 或任何具体建筑脚本；
- `BuildingPresentationController`；
- Collider / Collider2D、占格、寻路或点击判定；
- 工人、产量、人口、费用、科技条件等玩法数据；
- 自己注册到 GameSystem、推进回合或修改建筑等级的脚本。

View Prefab 的根原点应与 Runtime Prefab 的 `ViewRoot` 原点一致。升级前后尽量保持脚底/基座锚点一致，避免换图跳动。

## 3. 绑定入口

每个家族的 `BuildingPresentationDefinition` 是唯一绑定入口，资产位于：

`Assets/Landsong/Objects/SO/Buildings/Presentations`

可使用两种引用：

- `Direct Prefab`：适合当前本地资源和快速制作。
- `Addressable View Prefab`：适合后期大量家族/等级、分包和按需加载。

同一个映射只选一种。Addressable 路径建议使用稳定地址，例如：

```text
building-view/player_home/construction
building-view/player_home/placement-preview
building-view/player_home/lv01
building-view/tree/tree_03/lv01
```

## 4. 映射与回退规则

### 放置预览

`PlacementPreviewView` 是可选的独立纯 View Prefab，只替换 Runtime Prefab 的 `ViewRoot` 内容，不是另一套 Runtime/Ghost Prefab。配置后所有样式优先使用它；留空时按玩家当前选择的 `StyleId` 回退到 LV1 运营 View，因此树木等视觉变体仍能显示正确样式。

### 施工

施工表现先读取 `ConstructionViewMode`，两种模式互斥：

- `Single`（单一施工视图）：整个施工阶段始终使用 `ConstructionView`，不读取逐回合映射。适合只有一套施工美术或美术尚未完整交付的建筑。
- `PerTurn`（逐回合施工视图）：按 `当前施工回合 + StyleId` 解析 `ConstructionViewMappings`，先匹配相同回合和 StyleId，再匹配相同回合、空 StyleId 的通用映射；仍未命中则显示统一占位表现，不读取 `ConstructionView`。

`ConstructionProgress` 是已完成回合数，因此新放置建筑显示第 1 回合，成功结算后切换到第 2 回合。映射回合从 1 开始且不能超过 Family 的施工总回合数。不同树种等视觉样式可使用样式专属映射；空 StyleId 映射可覆盖该回合的所有样式。全部施工 Prefab 都必须是独立纯 View，不得塞回 Runtime Prefab。

### 运营等级

按 `Stage=Operational + Level + StyleId` 查找：

1. 优先使用相同 StyleId、等级等于当前等级的映射。
2. 若不存在，使用相同 StyleId 中低于当前等级且最接近的映射。
3. 无样式家族仍无映射时，使用 `DefaultOperationalView`。
4. 有 StyleId 的家族绝不静默切到别的样式。
5. 最后仍无资源时显示统一占位表现。

因此策划可以先完成数值 LV1～LV4，但美术暂时只交付 LV1。玩家升到 LV4 时真实数值仍为 LV4，外观安全沿用 LV1；以后添加 LV3 映射即可自动替换。

运营 ViewMapping 在建筑编辑器中不是可自由增删的数组，而是固定槽位矩阵：

- 运营等级轴来自正式 Excel 的 `运营等级` 表，导表后写入 Family Levels；
- 视觉样式轴来自 Unity Presentation 的 Styles；
- 无 Style 时槽位数为 `等级数`，有 Style 时槽位数为 `等级数 × Style 数`；
- 每个槽位的 Level 与 StyleId 只读，只允许回填 Direct/Addressable View；
- 新增等级会自动补空槽位，删除等级或 Style 会移除对应空槽位；
- 待移除槽位仍配置 View 时同步会被阻止，必须先撤销或明确清空该 View，系统不会静默删除美术引用。

空槽位是合法的美术缺口，不代表数值等级缺失，也不应复制低等级 Prefab 伪装为“已回填”。运行时低等级回退负责在资源未交付期间保持可用。

## 5. 动画资源

### 建筑自身循环动画

放在对应 View Prefab 的 Animator 中。例如烟囱、树叶、农田作物摆动。它与玩法等级绑定只通过 ViewMapping，不在建筑脚本里按名称寻找 Animator。

### 施工完成和升级入场动画

系统会先切换到真实目标 View，再向 `BuildingView` 发送入场原因。统一动画键/Animator Trigger 为：

| 入场原因 | 动画键/Trigger | 触发场景 |
| --- | --- | --- |
| `Normal` | `default` | 初次显示、读档、普通刷新 |
| `ConstructionCompleted` | `construction_complete` | 施工完成后进入 LV1 |
| `Upgraded` | `upgrade` | 真实升级后进入目标等级 |

静态 View 不要求任何额外组件。使用 `SpriteFrameAnimator` 时按上表建立动画键；使用 Unity `Animator` 时可以建立同名 Trigger，没有同名 Trigger 时 Animator 仍按自己的默认状态运行。升级专用动画不得仅依赖 `OnEnable`，否则读档和重新加载也会错误播放升级。

升级特效和音效应放进目标 View，可由 Animator、粒子 `Play On Awake` 或动画事件驱动。禁止创建独立 Transition Prefab；最终显示始终由真实 Stage/Level/Style 解析，动画不能成为玩法状态真相。

## 6. 美术交付与替换流程

1. 在 `BuildingViews/<Family>/...` 创建纯 View Prefab。
2. 对齐 `ViewRoot` 原点、排序层、像素密度和默认朝向。
3. 确认 Prefab 不含建筑脚本和 Collider。
4. 在对应 Presentation 中选择单一或逐回合施工视图模式，并配置该模式对应的 Direct/Addressable 引用；再回填固定运营映射槽位。
5. 无样式建筑可先填 `DefaultOperationalView`；运营等级槽位由 Excel 等级自动生成，不手动增加 ViewMapping。
6. 树木必须逐个 StyleId 绑定，不得只填默认图让八个按钮显示同一棵树。
7. 逐回合推进施工，并在施工中途存档/读档，检查当前回合、完工和目标等级换图。
8. 执行 `Landsong/Building/Validate Final Architecture`；结构零错误后提交。

替换已有美术时只替换 View Prefab 内容或 Presentation 引用。不得修改 FamilyId、Runtime Prefab、模块、存档数据或任务引用。

## 7. 当前回填清单

| 家族 | 施工 View | 运营 View | 图标/样式 | 优先级 |
| --- | --- | --- | --- | --- |
| 王宫 | 缺 | 默认 LV1 已配置 | 家族图标已有 | P0 |
| 居民房 | 已配置 | 默认 LV1 已配置 | 家族图标已有 | - |
| 伐木小屋 | 缺 | 默认 LV1 缺；LV2 可先回退 LV1 | 家族图标已有 | P0 |
| 捕鱼小屋 | 缺 | 默认 LV1 缺 | 家族图标缺 | P1 |
| 农田 | 缺 | 默认 LV1 缺 | 家族图标缺 | P1 |
| 市场 | 缺 | 默认 LV1 缺 | 当前临时复用伐木图标 | P1 |
| 仓库 | 已配置 | LV1～LV3 已配置 | 家族图标缺 | P1 |
| 泥路 | 缺 | 默认 LV1 缺 | 家族图标已有 | P1 |
| 树木 | 缺 | `tree_01`～`tree_08` 的 LV1 均已配置 | 8 个样式图标均已配置 | P1 |
| 雕塑 | 缺 | `sculpture_goddess`、`sculpture_deer` 的 LV1 均已配置 | 2 个样式图标均已配置 | P1 |
| 采石场 | 缺 | 默认 LV1 缺；LV2/LV3 可先回退 LV1 | 当前临时使用石头图标 | P1 |
| 医院 | 第 1、2 施工回合均缺 | 默认 LV1 缺；LV2/LV3 可先回退 LV1 | 家族图标缺 | P1 |
| 警局 | 第 1、2 施工回合均缺 | 默认 LV1 缺；LV2/LV3 可先回退 LV1 | 家族图标缺 | P1 |

校验器按模式检查施工表现：单一模式缺图只产生一条家族级提醒；逐回合模式按“家族/样式/施工回合”列出缺口。美术不完整时应明确使用单一模式，不能依赖逐回合模式回退到单一字段。树木与雕塑的 Style LV1 View 已完成，不计入运营缺口。

## 8. 验收清单

- Runtime Prefab 的 `ViewRoot` 在资产中为空。
- View Prefab 单独打开即可预览，且不含玩法/碰撞组件。
- 施工态不会提前显示运营图。
- 完工后同一实例切到 LV1 图。
- 高等级无图时只回退同 Style 的低等级，不改变 UI 等级。
- 快速连续升级或加载延迟不会让旧等级 View 覆盖新等级。
- 树木菜单八个样式与实际落地样式一致，读档后 StyleId 不变。
- 雕塑菜单显示女神与神鹿两个样式；二者放置后保持同一 `building.sculpture` 家族和各自 StyleId。
- 替换美术不改变建筑数值、任务统计、占地和存档身份。
