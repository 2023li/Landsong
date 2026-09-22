# 已退役的一次性迁移源码

此目录保存 2026-09-17 清理时的 13 个迁移器及原 meta 文本，其中包含清理前工作区已有的 ApplicationUiMigration 修改。`.cs.txt` 不在 Assets 下，不参与编译，也没有自动运行入口。

这些工具针对已退役的 UI / 场景 / 地形 schema，部分依赖也已退役；不要重新复制到 Assets 或对当前资产执行。旧版本资产需要恢复时，在独立旧版本检出中使用对应迁移与校验，再导入当前 schema 的资产。

仍使用的功能已提取：ApplicationUiAuthoring（路径与显式表现绑定）、InventoryUiAssets / BillUiAssets（路径及只读检查）、GamePreviewAuthoring（当前预览配方）、UnityEventBindingValidation、WorldPresentationValidation、BlueprintRuleNormalization。旧一键安装、覆盖、移动和迁移 Run 入口已移除。

作物旧点位已经转存为直接子对象并校验点位数；当前没有旧点位转换器。Map_Test2 / Map_Test01 的旧制图输入是现存制作数据，仅在 Editor 导入边界保留，不能在运行时当成另一套地图。
