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
building-view/player_home/lv01
building-view/tree/tree_03/lv01
```

## 4. 映射与回退规则

### 施工

所有样式默认共用 `ConstructionView`。如果某家族未来确需“样式不同的施工图”，应扩展 Presentation 映射能力，不得把多个施工图塞回 Runtime Prefab。

`ConstructionView` 缺失时，运行时显示统一占位表现。

### 运营等级

按 `Stage=Operational + Level + StyleId` 查找：

1. 优先使用相同 StyleId、等级等于当前等级的映射。
2. 若不存在，使用相同 StyleId 中低于当前等级且最接近的映射。
3. 无样式家族仍无映射时，使用 `DefaultOperationalView`。
4. 有 StyleId 的家族绝不静默切到别的样式。
5. 最后仍无资源时显示统一占位表现。

因此策划可以先完成数值 LV1～LV4，但美术暂时只交付 LV1。玩家升到 LV4 时真实数值仍为 LV4，外观安全沿用 LV1；以后添加 LV3 映射即可自动替换。

## 5. 动画资源

### 建筑自身循环动画

放在对应 View Prefab 的 Animator 中。例如烟囱、树叶、农田作物摆动。它与玩法等级绑定只通过 ViewMapping，不在建筑脚本里按名称寻找 Animator。

### 施工完成和升级过渡

在 Presentation 的 `ConstructionCompleteTransition` / `DefaultUpgradeTransition` 配置：

- 持续时间；
- 换 View 的归一化时点；
- 是否允许跳过；
- 过渡特效 Prefab；
- 音效。

即使过渡资源缺失或被跳过，最终显示必须由真实 Stage/Level/Style 重新解析，动画不能成为玩法状态真相。

## 6. 美术交付与替换流程

1. 在 `BuildingViews/<Family>/...` 创建纯 View Prefab。
2. 对齐 `ViewRoot` 原点、排序层、像素密度和默认朝向。
3. 确认 Prefab 不含建筑脚本和 Collider。
4. 在对应 Presentation 中配置 Direct 或 Addressable 引用。
5. 无样式建筑可先填 `DefaultOperationalView`；有多个美术等级时再加 ViewMapping。
6. 树木必须逐个 StyleId 绑定，不得只填默认图让八个按钮显示同一棵树。
7. 进入施工、完工、目标等级和读档场景检查换图。
8. 执行 `Landsong/Building/Validate Final Architecture`；结构零错误后提交。

替换已有美术时只替换 View Prefab 内容或 Presentation 引用。不得修改 FamilyId、Runtime Prefab、模块、存档数据或任务引用。

## 7. 当前回填清单（23 项）

| 家族 | 施工 View | 运营 View | 图标/样式 | 优先级 |
| --- | --- | --- | --- | --- |
| 王宫 | 缺 | 默认 LV1 缺 | 家族图标已有 | P0 |
| 居民房 | 缺 | 默认 LV1 缺 | 家族图标已有 | P0 |
| 伐木小屋 | 缺 | 默认 LV1 缺；LV2 可先回退 LV1 | 家族图标已有 | P0 |
| 捕鱼小屋 | 缺 | 默认 LV1 缺 | 家族图标缺 | P1 |
| 农田 | 缺 | 默认 LV1 缺 | 家族图标缺 | P1 |
| 市场 | 缺 | 默认 LV1 缺 | 当前临时复用伐木图标 | P1 |
| 泥路 | 缺 | 默认 LV1 缺 | 家族图标已有 | P1 |
| 树木 | 缺 | `tree_01`～`tree_08` 各缺 LV1，共 8 项 | 8 个样式图标均缺 | P0 |

计数口径：8 个施工 + 7 个无样式家族 LV1 + 8 个树样式 LV1 = 23 个 View 回填提醒。

## 8. 验收清单

- Runtime Prefab 的 `ViewRoot` 在资产中为空。
- View Prefab 单独打开即可预览，且不含玩法/碰撞组件。
- 施工态不会提前显示运营图。
- 完工后同一实例切到 LV1 图。
- 高等级无图时只回退同 Style 的低等级，不改变 UI 等级。
- 快速连续升级或加载延迟不会让旧等级 View 覆盖新等级。
- 树木菜单八个样式与实际落地样式一致，读档后 StyleId 不变。
- 替换美术不改变建筑数值、任务统计、占地和存档身份。
