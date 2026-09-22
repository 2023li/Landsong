# My斜坡：突出式斜坡

斜坡占用上层平台外侧一排格子，不占平台逻辑格。每块深 1 格，高差 1，主坡面 45°；组合为 `左端 + 中段×k + 右端`，k≥1。中段可重复，实际宽度受直边长度限制。

## 已配置的资源

- TileSet：`Assets/TileWorldCreator/Tiles URP/MyTile/My斜坡.asset`。
- 模型：同目录 `Mesh_Slope/Slope_Left.fbx`、`Slope_Middle.fbx`、`Slope_Right.fbx`。
- Prefab：同目录 `Prefabs/斜坡/`，含静态 `斜坡拼接预览.prefab`。
- 可编辑 Blender：本目录 `Slope_Modules.blend`。
- `unity_assembly_preview.png`、`unity_slope_detail.png` 为真正经过 TWC Blueprint → Tiles Build → Landsong Slope 的 Unity 材质预览；`slope_assembly_preview.png` 为 Blender 三格、六格组合预览。

模型使用原 `中心.prefab`、`边.prefab` 的网格改造，沿用原草地和岩壁材质。`land_reference_meshes.json` 包含 Unity 实际 Prefab 变换，避免误用 FBX 原始 Scale=100。中段由原中心草面倾斜；端帽由原边缘草沿与岩壁转到侧面，再按上坡方向剪切，保留原拓扑和 UV。Blender 仅近似展示材质颜色。

Blend 中 `EXPORT_MODULES` 是原点重合的三个导出模块，默认隐藏；取消隐藏即可单独编辑。`ORIGINAL_LAND_REFERENCE` 保存原网格，`ASSEMBLY_PREVIEW_NOT_EXPORTED` 为不参与 FBX 导出的组合预览。

## 绘制与构建

1. 打开当前地图，菜单 `Landsong/地图/配置突出式斜坡 My斜坡`。它为已有 Layer1 及更高层创建空的 `L<n>_斜坡` Blueprint、对应 `LV<n>_斜坡` Build，并绑定规则和 My斜坡。新增 Layer 后再执行一次此菜单。不会替你选择坡口位置。
2. 在 `L<n>_斜坡` 中，沿高地平台**外侧**画一排连续格子，至少三格，深度只有一格。平台陆地 Blueprint 保持原样。
3. 点击 TWC 顶部绿色按钮完整构建全部 Build Layers；坡向、左右端和中段自动确定。修改平台后也完整构建，以更新坡口视觉。不要给斜坡使用普通 Dual Grid Tiles Build Layer；已配置的 `Landsong Slope` 构建层负责它。
4. 正常烘焙地图，自动写入 n−1 ↔ n 逻辑连接，无需再手动添加同一条连接。

不合法的绘制会报错：上层必须 n≥1；平台直边至少六格；坡宽≥3；两端至少各留一个角格；每格只朝向一个上层可通行平台；斜坡占地和下口必须是 n−1 层可通行地表；不得覆盖高层平台、转角或地图边缘入口掩码。当前一格 45° 模型要求 Cell Size=1。

地形与逻辑高度仅来自 Blueprint 和 Layer。`My斜坡` 只供视觉构建选择三种 Prefab，Dual Grid 不参与逻辑。斜坡是独立连接：低地与平台仍为固定 Layer 高度，高差在中间一格内完成。

坡口视觉会在生成网格的副本上展平原草沿，并降低遮挡斜面的岩壁。它不改原始陆地 FBX，也不移除平台逻辑格；移除斜坡后再次构建可恢复原网格。

## 道路与导航

斜坡占地只允许现有 `BuildingCategory.Road` 的 1×1 普通道路，禁止其他建筑、桥梁/台阶建筑及跨格道路；道路仍遵守地形要求、资源条件和 XZ 占用互斥。道路外观按坡向倾斜并保持一格水平投影；逻辑落点、鼠标取格与单位连续移动使用 Layer 推导的斜面。道路正的移动代价仍生效。

## 模型对齐约定

现有中心 Prefab 的 XZ 范围为 ±0.5，视觉顶面 Y=0.5。斜坡标准上坡方向为 +Z，根原点放在所属上层 n 的外侧格中心：

| 接口 | 模型局部坐标 | 放入 Layer n 后 |
|---|---|---|
| 上口 | Z=+0.5，Y=+0.5 | 视觉高度 n+0.5 |
| 下口 | Z=−0.5，Y=−0.5 | 视觉高度 n−0.5 |
| 主坡面 | Y=Z | 一格深、高差一 |
| 中段左右接口 | X=±0.5 | 相邻根中心间距一 |

端帽外侧保留原草沿的圆弧，靠近坡顶逐渐展平并外扩为草面肩部，后缘接平平台，遮住两侧接头的岩柱顶部；肩部最多超出逻辑格 0.03 个单位。岩柱可略超出逻辑占地并伸入地下。根 Scale=(1,1,1)，其他方向仅绕 Y 转 90° 的整数倍。不要移动拼接线或上下口；加宽请复制中段。

## 再生成与验证

原陆地修改后，可在 Unity 执行 `Landsong/地图/导出现有陆地斜坡建模参考`。随后在项目根运行：

```powershell
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' --background --factory-startup --python ArtSource/MyTileSlope/build_slopes.py
```

会重新生成 Blend、FBX 和 Blender 预览，手工修改请先另存。Unity 执行 `Landsong/地图/配置突出式斜坡 My斜坡` 重新导入、检查三种 Prefab 并绑定配置；`Landsong/地图/生成突出斜坡 TWC 实际预览` 更新静态预览 Prefab 和截图。逻辑检查菜单 `Landsong/ECS/Verification/Protruding slopes`。

验证报告位于 `Library/LandsongEcs/`：`slope-assets-verification.txt`、`protruding-slope-verification.txt`、`protruding-slope-twc.txt`。
