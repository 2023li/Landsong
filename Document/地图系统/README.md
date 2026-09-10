# 地图制作

继续使用 TileWorldCreator v4 制作地形。TWC 是编辑工具，运行时以 MapAsset / Entity SubScene 为准。

1. 在 Assets/Landsong/Scenes/MapScenes 打开制图源场景，编辑 TWC 生成配置与地形。
2. 通过 TileWorldCreatorMapBaker 烘焙 GridMapDefinition，并绑定 MapContentAuthoring 的 Grid、逻辑网格、可视根和 TargetMap。
3. 初始建筑用 InitialBuildingPreview 指向正式 GameDefinitionAsset，配置等级、位置、朝向；使用 Inspector 校验和吸附。
4. 用 EcsMapIncrementalImport 的显式增量导入更新对应 MapAsset / EntityMaps SubScene。先预览/验证，失败不覆盖目标。
5. 新地图另注册菜单和 EcsGameHost 引用，见 [场景工作流](../ECS/启动与场景工作流.md)。

网格为等尺寸 XZ 平面；原点、格尺寸、高度和地形标签一致。建筑占地要存在、平整、可建且无重叠；不要靠场景模型位置猜运行格坐标。

增量导入按格坐标保留已有 ECS 配置，例如 BlocksProjectile；新格默认不拦截弹体。通行、建造、弹体阻挡各自独立。初始建筑和特殊配置先校验，不能用重新生成目录覆盖手动编辑内容。

地图入口带与缓冲区禁建，出生、可达目标和情报共用同一空间规则。没有合法点取消该股入侵/出勤并提示，不强制跨区生成。

当前有两张逻辑地图。Map_Test2 用于完整流程验收；Map_Test01 可用于隔离数据/结构检查，不承担新手流程验收。源地图、美术试作场景和正式运行地图不要仅凭名称含 Test 就当作临时文件删除。
