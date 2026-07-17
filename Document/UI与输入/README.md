# UI 与输入系统

## 1. 总体边界

UI 是领域服务的消费者：从 `GameSystem.Instance.Services` 读取状态、订阅事件、渲染 Prefab，并把点击转发给 Service。面板不得复制库存、科技、任务、建筑或其他领域规则。

`InputController` 统一读取 Unity Input System、识别主指针/多点触控、判断 UI 命中并管理相机输入阻断。业务组件不直接同时维护另一套鼠标与触摸入口。

## 2. UIPanel_Game

正式 Prefab：`Assets/Landsong/Objects/Prefabs/UI/UIPanel_Game/Panel_Game.prefab`。

`UIPanel_Game` 是游戏内面板总控，负责：

- HUD；
- 库存；
- 建筑菜单与放置控件；
- 建筑/经济概览；
- 科技；
- 任务；
- 远征；
- 人才；
- 继承；
- 暂停；
- 建筑选择、详情和事件消息。

同一时刻的主面板显隐由 `UIPanel_Game` 或明确的子总控管理。子列表项不直接关闭其他领域面板。

## 3. Inspector/Prefab 契约

- TMP、Image、Button、Toggle、Root 和列表项 Prefab 必须通过 `[SerializeField]` 显式绑定。
- 缺少必填引用应尽早报告配置错误；不要用 `transform.Find`、按名称查找或运行时创建占位控件掩盖问题。
- 运行时动态实例化仅用于列表项、节点或浮字等明确的重复内容，模板必须是正式 Prefab。
- UI 视觉数据留在 Prefab；数值规则留在领域配置。
- 修改 Prefab 后检查 `m_TargetGraphic`、`raycastTarget`、CanvasGroup、Mask 和布局组件，不把“看得见”当成“可点击”。

## 4. InputController

主要能力：

- `TryGetPrimaryPointerState`：统一鼠标/触摸主指针；
- `TryGetTwoTouchPositions`：双指缩放；
- `TryGetScrollDelta`：滚轮/触控缩放输入；
- `IsPointerOverUi`：EventSystem Raycast；
- `SetCameraInputBlocked(owner, blocked)`：按所有者阻断相机输入；
- `OpenBuildingPanelRequested`、`OpenInventoryPanelRequested`、`BackRequested`：Input Action 事件。

阻断相机时必须使用稳定 owner，并在禁用/销毁时解除。不要用一个全局 bool 让多个面板互相覆盖状态。

## 5. 交互常量

全局交互阈值位于 `Assets/Landsong/Scripts/InputSystem/InteractionConstants.cs`：

- `DoubleClickIntervalSeconds`：建筑双击判定；
- `LongPressDurationSeconds`：触屏长按显示详情；
- `ClickMovementTolerancePixels`：世界点击允许的最大指针移动距离，选择与拆除共用。

这些值是输入语义，不属于某个建筑或某个 UI Prefab。新增相同语义必须复用常量。淡入淡出、点击缩放、浮字间隔等表现时长仍可由组件序列化配置。

## 6. 建筑选择与放置

- `BuildingSelectionController` 管理世界点击、双击、选择态、操作条、详情请求和选择 Overlay。
- `BuildingPlacementController` 管理待放置建筑、指针跟随、合法性评估、放置确认与范围预览。
- `BuildingPointerHitUtility` 负责从世界命中解析建筑。
- UI 发起放置请求后只显示/取消/确认，不自行占用格子或扣费。

双击由选择控制器按“同一建筑 + 未超过统一间隔”判定，再通过 `BuildingBase.DispatchPointerClick` 分发普通点击与双击模块事件。

## 7. 领域面板规则

- 库存：`GamePanel_Inventory` 绑定 `InventoryService`；槽位项只负责显示、拖放和请求移动/丢弃。
- 科技：`GamePanel_Technology` 只绑定已生成节点、读取 `TechnologyService` 和解锁内容注册表。
- 任务：`GamePanel_Quest` 读取 `QuestService`，提交与领奖必须调用服务事务。
- 概览：`GamePanel_Overview` 是建筑/经济页签总控；经济预测读取 `EconomyForecastService`，不修改真实库存。
- 建筑详情：模块通过功能块接口提供结构化内容，详情面板不判断具体建筑家族。

## 8. 命名空间现状

新 UI 代码必须使用 `Landsong.UISystem`。部分旧面板仍处于全局命名空间；批量改名会影响 Unity 序列化类型，因此正式规则是冻结旧序列化全名，只在专门迁移任务中处理。详见 [架构决策](../架构决策.md)。

## 9. 验证清单

- 鼠标、单指、双指和键盘 Input Action 均不会穿透 UI。
- 多个相机输入阻断 owner 可独立添加/移除。
- 面板开关不会留下隐藏面板订阅或重复监听。
- 所有必填引用在 Prefab 中存在，运行时没有临时创建补丁。
- 双击、长按和普通点击不会重复触发互斥操作。
- 不同分辨率下 ScrollRect、LayoutGroup、ContentSizeFitter 没有循环驱动。
- 关闭/返回操作由统一 Back 事件处理并按当前面板层级退出。
