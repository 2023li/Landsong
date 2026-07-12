# Landsong 科技系统开发上下文

最后更新：2026-07-11

## 当前实现结论

本轮目标已经落地：

1. `TechnologyCatalog` 中配置了一棵 5 行、22 节点的基础科技树。
2. `GamePanel_Technology` 会在运行时解析所有科技节点 ID，自动生成科技、空节点和列间隙。
3. 前置科技连线由 `TechnologyTreeConnectionGraphic` 统一绘制，支持同行和跨行依赖。
4. 科技规则仍由 `TechnologyService` 管理；面板只负责生成、显示和把点击转发给服务。

## 科技节点 ID 规则

固定格式：`TN_R_L_科技名称`

- `TN`：固定前缀。
- `R`：所在行，只允许 `1-5`。
- `L`：所在列，从 `1` 开始，不要求连续。
- 科技名称不能为空。
- 同一行、同一列只能有一个科技节点。
- 公共解析入口是 `TechnologyNodeId.TryParse(...)`，不要在新代码中再复制一份 ID 解析逻辑。
- `TechnologyEditorWindow` 保存时仍负责检查 ID 格式和唯一性。

示例：`TN_3_1_启蒙`、`TN_2_3_数学`、`TN_5_6_城垣营造`。

## 基础科技树

| 行 | 列 | 科技 | 科技点 | 前置科技 |
|---:|---:|---|---:|---|
| 1 | 2 | 咒语 | 10 | 启蒙 |
| 1 | 3 | 元素学 | 20 | 咒语、历法 |
| 1 | 4 | 魔法学 | 35 | 元素学、文字 |
| 1 | 5 | 星象术 | 50 | 魔法学、数学 |
| 2 | 2 | 文字 | 10 | 启蒙 |
| 2 | 3 | 数学 | 20 | 文字 |
| 2 | 4 | 物理学 | 35 | 数学 |
| 2 | 5 | 建筑学 | 50 | 物理学、石工术 |
| 3 | 1 | 启蒙 | 5 | 无 |
| 3 | 2 | 历法 | 10 | 启蒙 |
| 3 | 3 | 轮子 | 18 | 启蒙、石工术 |
| 3 | 4 | 航海术 | 35 | 历法、数学 |
| 3 | 5 | 制图术 | 45 | 航海术、文字 |
| 3 | 6 | 远洋帆装 | 65 | 制图术、物理学 |
| 4 | 2 | 狩猎 | 10 | 启蒙 |
| 4 | 3 | 驯养 | 15 | 狩猎 |
| 4 | 4 | 冶铁术 | 30 | 驯养、石工术 |
| 4 | 5 | 铁骑 | 50 | 冶铁术、轮子 |
| 5 | 2 | 石工术 | 10 | 启蒙 |
| 5 | 3 | 耕作 | 15 | 历法 |
| 5 | 4 | 灌溉 | 30 | 耕作、数学 |
| 5 | 6 | 城垣营造 | 65 | 建筑学、冶铁术 |

当前节点只建立研究成本、描述和依赖关系，未擅自添加建筑解锁或物品奖励。后续奖励应继续使用 `TechnologyEffect` 子类配置，不要写进 UI。

## 自动 UI 生成规则

科技面板路径：

`Assets/Landsong/Objects/Prefabs/UI/UIPanel_Game/Panel_Game.prefab/——科技面板——/Panel_FantasyMenus_Popup_05 (1)/Content/科技面板滚动视图/Viewport/Content`

`Content` 的直接子对象是 `——1行` 到 `——5行`。运行时生成流程如下：

1. 在编辑器中点击 `GamePanel_Technology.GenerateTechnologyTreeUI()`，从“编辑器预览科技目录”读取全部科技定义。
2. 解析每个 ID 的行、列，计算全树最大列数。
3. 每一行都从第 1 列遍历到最大列。
4. 当前行当前列有科技时，生成“科技节点”；没有时，生成“空节点”。
5. 除最后一列外，每两列之间都生成一个“间隙”。
6. 五行因此始终拥有一致的槽位数量，横向位置不会因某行缺少科技而错位。
7. 生成结果保存进 Prefab；运行时刷新只绑定节点文本、点击和研究状态，不重建整棵树。

当前预制体绑定：

- 科技节点：`Assets/Landsong/Objects/Prefabs/UI/UIPanel_Game/科技节点_启蒙.prefab`，必须保留 `GamePanel_TechnologyNodeItem` 及其按钮、文本、状态根对象绑定。
- 空节点：`Assets/Landsong/Objects/Prefabs/UI/UIPanel_Game/科技面板/空节点.prefab`，只占布局位置，不应挂 `GamePanel_TechnologyNodeItem`。
- 列间隙：`Assets/Landsong/Objects/Prefabs/UI/UIPanel_Game/科技面板/间隙.prefab`。

如果以后更换科技节点视觉，只替换 `GamePanel_Technology.nodePrefab`，不要在生成器中按子节点名称查找并临时拼装按钮。

### 编辑器预览工作流

自动布局不仅能在运行时生成，也可以直接写入当前 Prefab 编辑界面作为预览：

1. 在 Prefab Mode 打开 `Panel_Game.prefab`。
2. 选中挂有 `GamePanel_Technology` 的 `——科技面板——`。
3. 确认“编辑器预览科技目录”指向 `TechnologyCatalog.asset`。
4. 点击 Inspector 中的“按节点 ID 生成科技树 UI”。
5. 脚本会立即重建 1-5 行的科技、空节点、间隙和依赖连线；生成操作支持 Undo。
6. 修改节点 ID、前置关系或三个模板后，再点一次生成按钮即可刷新效果。
7. “清除编辑器预览”只清空五行中的生成内容，不删除五个行根对象。

编辑器预览会保留 Prefab 实例连接，连线引用也会序列化，因此保存并重新打开 Prefab 后仍能看到预览。公开方法 `GamePanel_Technology.GenerateTechnologyTreeUI()` 在编辑状态读取“编辑器预览科技目录”，运行状态读取 `TechnologyService.Catalog`，可供运行时调试手动调用；正常运行时不会自动重建 UI，只扫描并绑定 Prefab 中已经生成的节点。

## 连线方案

`TechnologyTreeConnectionGraphic` 是 `Content` 下运行时创建的“科技连线”图层：

- 图层设置为最底层 sibling，线显示在节点后方。
- `LayoutElement.ignoreLayout = true`，不会被五行布局当成额外槽位。
- `raycastTarget = false`，不会拦截科技按钮点击。
- 每条线从前置节点右边缘连接到目标节点左边缘。
- 同行依赖绘制直线；跨行依赖绘制水平—垂直—水平的折线。
- 线宽和颜色由 `GamePanel_Technology` 的序列化字段控制。
- 连线数据直接读取 `TechnologyDefinition.Prerequisites`，不维护第二份手工连线表。

因此，新增或调整前置科技后，只要目录和节点 ID 有效，UI 与连线会在下次运行时自动反映变化。

## 主要代码职责

- `TechnologyDefinition.cs`：科技数据，以及公共节点 ID 解析规则。
- `TechnologyCatalog.cs`：科技定义目录与 ID 索引。
- `TechnologyService.cs`：当前研究、队列、解锁、重复研究和研究进度规则。
- `GamePanel_Technology.cs`：运行时树布局、节点绑定、详情显示和点击转发。
- `GamePanel_TechnologyNodeItem.cs`：单个科技节点的视觉状态。
- `TechnologyTreeConnectionGraphic.cs`：依赖连线网格绘制。
- `TechnologyEditorWindow.cs`：编辑科技数据、依赖和编辑器图位置；编辑器图位置不参与运行时 UI 布局。

## 后续开发检查清单

1. 新节点 ID 必须满足 `TN_行_列_名称`，行号限定 1-5。
2. 保存前确认没有位置冲突、重复 ID、自己依赖自己或依赖环。
3. 新节点资产放入 `Assets/Landsong/Objects/SO/Technology`，并确保进入 `TechnologyCatalog`。
4. 研究完成世界效果继续扩展 `TechnologyEffect`，由 `GameSystem` 执行，不让 UI 修改库存、建筑或其他玩法状态。
5. 修改 UI 模板后验证按钮 TargetGraphic、文字引用、遮罩和 Raycast 链。
6. CLI 编译只能证明 C# 引用成立；自动布局、遮罩、连线层级和滚动范围仍需在 Unity Game 视图运行确认。
