# 无草土地与石头地

来源：`Assets/TileWorldCreator/Tiles URP/MyTile/Mesh_无草/` 中的五份 FBX。

| 网格 | Prefab 名称 | Dual Grid 槽位 |
|---|---|---|
| 无草Cornerl | 拐角 | Corner |
| 无草InteriorCorner | 三边 | Inverted Corner |
| 无草Edge | 边 | Edge |
| 无草Fill | 中心 | Fill |
| 无草MergedCorner | 对角 | Double Interior Corner |

两套分别保存在 `MyTile/Prefabs/土地/`、`MyTile/Prefabs/石头地/`，绑定 `My土地.asset`、`My石头地.asset`。
材质分别引用 `Assets/Landsong/Art/Material/棕土色.mat` 和 `石头灰.mat`。

FBX 导入后中心为 2×2，根 Y=-0.2。Prefab 外层 Scale=1，嵌套模型 Scale=0.5，清除模型根位置偏移，使中心范围 ±0.5、顶面 Y=0.5；保留对原 FBX 的引用。五种 Y 旋转偏移沿用 `My陆地`，分别为 90、270、180、0、90 度。源 FBX 开启 Read/Write 以支持 TWC 网格合并；未重写网格顶点。

`preview.png` 是两套资源的实际 TWC 构建，覆盖五种槽位。两份 TileSet 的缩略图位于 `MyTile/Previews/`。原无草边网格顶面略低于中心，预览中可见细小接缝；资源配置保留了这一原始几何差异。

重新配置菜单：`Landsong/地图/配置无草土地与石头地`。重新生成预览：`Landsong/地图/预览无草土地与石头地`。配置操作会更新这十份 Prefab 与两份 TileSet；若手动修改了它们，请勿随意重新执行。

配置及实际构建报告：`Library/LandsongEcs/bare-ground-verification.txt`。
