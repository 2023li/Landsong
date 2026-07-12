# Landsong 科技系统开发上下文

最后更新：2026-07-12

## 当前实现结论

本轮目标已经落地：

1. `TechnologyCatalog` 中配置了一棵 5 行 × 15 列、共 55 节点的基础科技树，预留 20 个空节点槽位。
2. `GamePanel_Technology` 通过 Odin Button 在编辑器解析所有科技节点 ID，生成科技、空节点和列间隙并写回 Prefab；运行时只绑定已生成节点。
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

当前布局参考《文明 5》科技树的组织方式：横向代表发展阶段，中央单一起点向上下多条路线展开；每列节点数量不同，分支会交叉汇合，非最终科技不保留死路。基础树使用 5 行 × 15 列，共 75 个位置，其中放置 55 个科技并保留 20 个空节点。第 `L` 列节点的所有前置科技都必须位于第 `L-1` 列，允许跨行组合，但禁止跨列跳级。

| 行 | 布局（从第 1 列到第 15 列，`空` 表示空节点） |
|---:|---|
| 1 | 空 → 灵性 → 咒语 → 元素学 → 魔法学 → 星象术 → 空 → 结界术 → 炼金术 → 召唤术 → 空 → 秘仪学 → 空 → 灵脉学 → 奥术塔 |
| 2 | 空 → 口述传统 → 文字 → 数学 → 物理学 → 空 → 建筑学 → 测量学 → 力学 → 空 → 光学 → 医学 → 博物学 → 空 → 学院制度 |
| 3 | 启蒙 → 空 → 空 → 历法 → 轮子 → 航海术 → 制图术 → 远洋帆装 → 天文导航 → 造船术 → 海贸法 → 空 → 港务学 → 空 → 大洋航路 |
| 4 | 空 → 求生术 → 狩猎 → 空 → 驯养 → 冶铁术 → 铁骑 → 阵列战术 → 空 → 弩炮术 → 铠甲术 → 城防学 → 空 → 军需学 → 联合军制 |
| 5 | 空 → 空 → 石器打制 → 石工术 → 耕作 → 灌溉 → 轮作 → 空 → 城垣营造 → 拱券术 → 水利工程 → 风车磨坊 → 庄园农学 → 空 → 粮仓制度 |

研究成本按列统一递增：`5、8、12、18、26、36、48、62、78、96、116、140、168、200、240`。每个节点资产中保存完整描述和实际前置节点；上表用于说明位置，实际路线由跨行依赖构成。

列密度为：`1、3、4、4、5、4、4、4、4、4、4、4、3、2、5`。第 1 列只有中央“启蒙”；第 2-5 列逐步展开；中段稳定保持多个可选分支；第 13-15 列逐渐汇合为终局能力。

当前节点只建立研究成本、描述和依赖关系，未擅自添加建筑解锁或物品奖励。后续奖励应继续使用 `TechnologyEffect` 子类配置，不要写进 UI。

## 自动 UI 生成规则

科技面板路径：

`Assets/Landsong/Objects/Prefabs/UI/UIPanel_Game/Panel_Game.prefab/——科技面板——/Panel_FantasyMenus_Popup_05 (1)/Content/科技面板滚动视图/Viewport/Content`

`Content` 的直接子对象是 `——1行` 到 `——5行`。生成流程如下：

1. 在编辑器中点击 `GamePanel_Technology.GenerateTechnologyTreeUI()`，从“编辑器预览科技目录”读取全部科技定义。
2. 解析每个 ID 的行、列，计算全树最大列数。
3. 每一行都从第 1 列遍历到最大列。
4. 当前行当前列有科技时，生成“科技节点”；没有时，生成“空节点”。
5. 除最后一列外，每两列之间都生成一个“间隙”。
6. 五行因此始终拥有一致的槽位数量，横向位置不会因某行缺少科技而错位。
7. 生成结果保存进 Prefab；运行时刷新只绑定节点文本、点击和研究状态，不重建整棵树。

当前预制体绑定：

- 科技节点：`Assets/Landsong/Objects/Prefabs/UI/UIPanel_Game/科技面板/新科技节点.prefab`，必须保留 `GamePanel_TechnologyNodeItem` 及其按钮、背景、文本、进度条和解锁内容根对象绑定。
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

因此，新增或调整前置科技后，需要在 `GamePanel_Technology` 上再次点击生成按钮，把最新节点和连线写回 Prefab；运行时只绑定已生成内容。

## 科技节点 UI 状态与完成效果

科技树不增加时代背景列或阶段标题。节点本身承担信息表达，运行时由 `GamePanel_TechnologyNodeItem` 统一解析为以下视觉状态：`Invalid`、`Preview`、`Locked`、`Available`、`CurrentResearch`、`Queued`、`Completed`、`Repeatable`。不同状态使用不同底色和短状态文本；锁定节点仍保持可点击，让面板可以把所需的前置路径加入研究队列。

当前研究节点显示数值进度和底部只读 `Slider`；Slider 的最小值为 0、最大值为研究所需科技点、当前值为已投入科技点。队列节点显示从 1 开始的队列序号。面板打开或当前研究项目改变时，`GamePanel_Technology` 会等待一帧完成布局，然后只在节点超出 Viewport 时移动 ScrollRect，使当前研究节点回到可见区域，研究点变化不会反复抢夺玩家的滚动位置。

`TechnologyEffect` 继续负责实际玩法效果，同时通过 `TryGetPresentation(...)` 提供只读展示数据：图标、名称、类型和数量。节点 UI 不判断具体效果子类，也不执行效果；它只读取 `TechnologyDefinition.CompletionEffects` 并在“解锁内容”区域生成最多 5 个图标。目前物品奖励和建筑蓝图解锁已经实现该展示契约，后续新增效果子类时应同时补充展示数据。

建筑解锁只使用一套蓝图状态：`BuildingBlueprintService` 是唯一运行时真相并负责存档；所有建造菜单建筑都必须拥有蓝图才可用，不再存在 `BuildingDefinition.availableCondition`。`TechnologyEffect_UnlockBuildingBlueprint` 只是科技完成时向该服务授予蓝图的命令，不单独保存状态。科技解锁建筑时，将建筑配置为 `blueprintInitiallyLocked`，并在对应科技的 `CompletionEffects` 中配置蓝图效果；不要再用 `visibleCondition` 重复检查同一个科技。旧存档恢复后会补齐初始蓝图及已完成科技授予的蓝图，但不会重放物品等奖励。

## 主要代码职责

- `TechnologyDefinition.cs`：科技数据，以及公共节点 ID 解析规则。
- `TechnologyCatalog.cs`：科技定义目录与 ID 索引。
- `TechnologyService.cs`：当前研究、队列、解锁、重复研究和研究进度规则。
- `GamePanel_Technology.cs`：Odin 编辑器生成入口、运行时节点绑定、详情显示和点击转发。
- `GamePanel_TechnologyNodeItem.cs`：单个科技节点的视觉状态。
- `TechnologyTreeConnectionGraphic.cs`：依赖连线网格绘制。
- `TechnologyEditorWindow.cs`：编辑科技数据、依赖和编辑器图位置；编辑器图位置不参与运行时 UI 布局。

## 后续开发检查清单

1. 新节点 ID 必须满足 `TN_行_列_名称`，行号限定 1-5。
2. 第 `L` 列的所有前置科技必须位于第 `L-1` 列；第 1 列不得配置前置科技。
3. 保存前确认没有位置冲突、重复 ID、自己依赖自己或依赖环。
4. 新节点资产放入 `Assets/Landsong/Objects/SO/Technology`，并确保进入 `TechnologyCatalog`。
5. 修改科技数据后，用 `GenerateTechnologyTreeUI()` 重新生成并保存 `Panel_Game.prefab`。
6. 研究完成世界效果继续扩展 `TechnologyEffect`，由 `GameSystem` 执行，不让 UI 修改库存、建筑或其他玩法状态。
7. 修改 UI 模板后验证按钮 TargetGraphic、文字引用、遮罩和 Raycast 链。
8. CLI 编译只能证明 C# 引用成立；自动布局、遮罩、连线层级和滚动范围仍需在 Unity Game 视图运行确认。
