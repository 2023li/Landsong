# Landsong 科技系统

最后更新：2026-07-17

## 当前实现结论

当前实现：

1. `TechnologyCatalog` 中配置了一棵 5 行 × 18 列、共 56 节点的基础科技树；当前 Definition/Catalog 是运行时真相，完整内容清单见 [科技树内容](科技树内容.md)。
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

示例：`TN_3_1_启蒙`、`TN_1_6_数学`、`TN_5_17_军工理论`。

## 基础科技树

当前布局以 `TechnologyCatalog` 登记的 Definition 为准，参考《文明 5》科技树的组织方式：横向代表发展阶段，中央单一起点向上下多条路线展开；每列节点数量不同，分支会交叉汇合。基础树使用 5 行 × 18 列，共 90 个位置，其中放置 56 个科技并保留 34 个空节点。前置科技可以跨过空列并跨行组合，但不能跳过前置科技所在行中已经存在的中间节点；每条依赖必须连接该行在目标科技左侧最近的可见节点。

| 行 | 布局（从第 1 列到第 18 列，`空` 表示空节点） |
|---:|---|
| 1 | 空 → 文字 → 空 → 哲学 → 法律 → 数学 → 空 → 物理学 → 力学 → 空 → 光学 → 天文学 → 空 → 材料学 → 空 → 空 → 科学理论 → 空 |
| 2 | 空 → 农业 → 捕鱼 → 历法 → 空 → 医学 → 货币 → 教育制度 → 空 → 经济理论 → 空 → 化学 → 生物学 → 空 → 银行学 → 工业化 → 生态工程 → 空 |
| 3 | 启蒙 → 占星术 → 空 → 咒语 → 神学 → 空 → 元素学 → 空 → 通灵术 → 空 → 炼金术 → 空 → 附魔术 → 空 → 考古学 → 符文学 → 魔导理论 → 未来 |
| 4 | 空 → 木工术 → 石工术 → 空 → 轮子 → 空 → 零件 → 建筑学 → 空 → 工程学 → 导航术 → 空 → 空 → 蒸汽动力 → 信息化 → 航空 → 机械化 → 空 |
| 5 | 空 → 狩猎 → 采矿 → 驯养 → 空 → 青铜 → 阵列作战 → 空 → 铁器 → 火药 → 空 → 战术学 → 协调作战 → 空 → 体系战争 → 空 → 军工理论 → 空 |

研究成本按列统一递增：`5、8、12、18、26、36、48、62、78、96、116、140、168、200、240、285、335、500`。每个节点资产中保存完整描述和实际前置节点；上表用于说明位置，实际路线由跨行依赖构成。“未来”依赖第 17 列的全部 5 个节点，作为整棵树的终局汇合点。

列密度为：`1、5、3、4、3、3、4、3、3、3、3、3、3、2、4、3、5、1`。第 1 列只有中央“启蒙”；第 2 列展开 5 条基础路线；中段保持稀疏的跨行汇合；第 17 列形成 5 个终局理论，第 18 列收束为“未来”。

完整的 56 节点成本、前置关系和玩家可见描述记录在 [科技树内容](科技树内容.md)。一次性奖励继续使用 `TechnologyEffect`；建筑升级、全局 Buff 等持续规则由实际规则资产引用所需科技，科技 UI 通过解锁内容索引反向汇总，不在科技节点重复配置。

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

科技树滚动区采用单一布局驱动，组件职责固定如下：

- `科技面板滚动视图` 只负责 `ScrollRect` 与横向滚动条。
- `Viewport` 必须四向拉伸填满滚动区，并保留 `Image + RectMask2D` 负责裁剪。
- `Content` 负责整棵树的尺寸与五行排列，保留横向 `ContentSizeFitter (Preferred Size)` 和 `VerticalLayoutGroup`。
- `——1行` 到 `——5行` 只保留各自的 `HorizontalLayoutGroup`，禁止再添加 `ContentSizeFitter`；行的位置和尺寸由 `Content` 的 `VerticalLayoutGroup` 驱动。
- `GamePanel_Technology` 只触发行与 `Content` 的布局重建，不再手动计算行坐标、行高度或 `Content` 尺寸。

Unity 会对“父节点使用 `LayoutGroup`、直接子节点又使用 `ContentSizeFitter`”的组合报告布局冲突。因此以后调整科技树布局时，应修改 `Content` 的纵向布局参数、行的横向布局参数或节点模板的尺寸，不要把 `ContentSizeFitter` 加回五个行节点。

当前预制体绑定：

- 科技节点：`Assets/Landsong/Objects/Prefabs/UI/UIPanel_Game/科技面板/科技节点.prefab`，必须保留 `GamePanel_TechnologyNodeItem` 及其按钮、背景、文本、进度条、解锁内容根对象和解锁内容项 Prefab 绑定。
- 科技解锁内容项：`Assets/Landsong/Objects/Prefabs/UI/UIPanel_Game/科技面板/科技解锁内容项.prefab`。图标尺寸由根 `RectTransform` 控制；有图标时的角标字号在“图标角标”中调整，无图标时的文字字号在“无图标文字”中调整。根组件必须显式引用图标 Image 和两套 TMP 文本；缺少任一引用都属于配置错误。运行时代码只实例化该 Prefab 并填充数据，不按名称搜索子节点，也不会临时创建 Image/TMP 对象。
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

科技详情栏中的科技描述和“解锁与完成效果”使用两个独立的、预先存在于 `Panel_Game.prefab` 中的 TMP 文本。`GamePanel_Technology.detailUnlockEffectsLabel` 必须显式绑定到 `底栏/txt_解锁与完成效果`；运行时只收集注册表内容并更新该字段的文本，不允许创建 TMP 对象、复制文本模板或把解锁内容拼接进科技描述字段。需要调整字体、字号、颜色、换行和占用高度时，直接修改这个 Prefab 节点。

科技节点 UI 使用 `TechnologyUnlockContentRegistry` 汇总两类内容。注册表是被动容器，UI 只读，不主动搜索各领域资产：

- 一次性研究完成效果：由 `TechnologyUnlockContentRegistry.InjectCompletionEffects(...)` 在注册表重建时统一注入。`TechnologyEffect.TryGetPresentation(...)` 只提供图标、名称、类型和数量，UI 不执行效果。
- 持续规则解锁：实现 `ITechnologyUnlockContentProducer` 的领域目录通过 `ReplaceSource(...)` 主动注入。当前 `BuildingCatalog` 注入蓝图和等级升级；`TechnologyGlobalBuffCatalog` 注入全局 Buff。其他系统以后按同一接口注入，不把扫描逻辑写进科技节点。

节点的“解锁内容”区域最多显示 5 个位置：建筑或建筑升级优先显示建筑图标，升级内容叠加 `LVx`；全局 Buff 使用 Buff Definition 自己的图标；无图标时才显示文字占位。超过容量时最后一格显示 `+N`，选中科技后的详情说明会列出全部内容。注册表按来源原子替换，一次重建后由全部节点共享。节点实例化统一的“科技解锁内容项” Prefab；悬停提示框是科技节点 Prefab 中预先配置并显式引用的固定子对象，不允许运行时自行生成。缺少解锁项 Prefab、提示根节点或提示文本时，节点会报告配置错误并停止运行。旧版本误保存在面板中的动态解锁项会在刷新时按专用容器统一清理，避免重复显示。

建筑解锁只使用一套蓝图状态：`BuildingBlueprintService` 是唯一运行时真相并负责存档。科技解锁建筑时，在 Family 的 `AutomaticBlueprintUnlockCondition` 配置科技并启用 `blueprintInitiallyLocked`；`GameSystem` 在服务建立、科技完成和恢复时统一协调条件并授予蓝图。科技 SO 不再配置 `TechnologyEffect_UnlockBuildingBlueprint`。雕塑的石工术、采石场的采矿都遵循这一规则。

`TechnologyGlobalBuffCatalog` 既是解锁内容生产者，也是运行时 Buff 真相。当前包含：

- `buff.quarry.masonry`：由 `TN_4_3_石工术` 激活，对所有采石场的有效石头生产固定 +1；生产模块通过 `TechnologyGlobalBuffService` 查询实际加成。
- `buff.inventory.wheel_preservation`：由 `TN_4_5_轮子` 激活，使所有物资的库存自然损耗结果乘 `0.90`；`Inventory` 在每次实际损耗和模拟预测时动态查询该倍率。

多个同类全局倍率效果按乘法叠加。科技 UI 通过 Buff Definition 的图标和 `Describe()` 文案显示解锁内容；库存系统不读取科技树，也不把效果复制到物品、槽位类型或建筑等级。

## HUD 科技条与系统解锁

HUD 科技条由挂载在 `Panel_Game.prefab/———Hud———/科技条` 根对象上的 `GamePanel_HUD_TechnologyBar` 独立管理。该组件负责科技按钮转发、`TechnologyService` 状态订阅、当前研究名称/图标/进度显示，以及根据 `GameFeatureUnlockService` 控制整条科技栏的可见性；`GamePanel_HUD` 不再持有科技栏引用或科技状态订阅。

科技系统未解锁时，科技条通过根对象的 `CanvasGroup` 设为透明、不可交互且不接收 Raycast，同时清空名称和进度文本，因此 HUD 不显示“未选择科技”。根对象自身保持激活，以便继续监听 `GameFeatureUnlockService.StateChanged`；任务奖励在运行中解锁科技后，科技条应立即出现，无需重开面板。

“未选择研发节点”属于科技系统解锁后的回合规则。科技未解锁时，`GameSystem.ShouldCancelNextTurnForMissingResearch()` 必须直接放行，不取消本次下回合，也不向事件栏发送缺少研发项目的警告；解锁后仍沿用原有的首次警告并取消、再次点击继续规则。

## 主要代码职责

- `TechnologyDefinition.cs`：科技数据，以及公共节点 ID 解析规则。
- `TechnologyCatalog.cs`：科技定义目录与 ID 索引。
- `TechnologyService.cs`：当前研究、队列、解锁、重复研究和研究进度规则。
- `GamePanel_HUD_TechnologyBar.cs`：HUD 科技条的功能解锁显隐、当前研究显示和科技面板入口。
- `GamePanel_Technology.cs`：Odin 编辑器生成入口、运行时节点绑定、详情显示和点击转发。
- `GamePanel_TechnologyNodeItem.cs`：单个科技节点的视觉状态。
- `TechnologyTreeConnectionGraphic.cs`：依赖连线网格绘制。
- `TechnologyEditorWindow.cs`：编辑科技数据、依赖和编辑器图位置；编辑器图位置不参与运行时 UI 布局。

## 后续开发检查清单

1. 新节点 ID 必须满足 `TN_行_列_名称`，行号限定 1-5。
2. 前置科技必须位于目标左侧；允许跨过空列，但不能跳过前置科技所在行中的已有中间节点；第 1 列不得配置前置科技。
3. 保存前确认没有位置冲突、重复 ID、自己依赖自己或依赖环。
4. 新节点资产放入 `Assets/Landsong/Objects/SO/SO_Technology/TechnologyDef`，并确保进入 `TechnologyCatalog`。
5. 修改科技数据后，用 `GenerateTechnologyTreeUI()` 重新生成并保存 `Panel_Game.prefab`。
6. 研究完成世界效果继续扩展 `TechnologyEffect`，由 `GameSystem` 执行，不让 UI 修改库存、建筑或其他玩法状态。
7. 修改 UI 模板后验证按钮 TargetGraphic、文字引用、遮罩和 Raycast 链。
8. CLI 编译只能证明 C# 引用成立；自动布局、遮罩、连线层级和滚动范围仍需在 Unity Game 视图运行确认。
9. 科技未解锁时验证科技条完全隐藏、事件栏不显示研发缺失警告且回合正常推进；领取任务 5 奖励后验证科技条立即出现、按钮可用，未选择研究时才显示“未选择科技”。
