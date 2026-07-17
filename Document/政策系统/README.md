# Landsong 政策系统

最后更新：2026-07-17

## 当前实现结论

政策系统已经落地，运行时状态由 `PolicyService` 统一管理，并通过 `GameSystem.Services.Policies` 对外提供。当前实现包含：

1. 民意值是非消耗性、最小为 `0` 的动态门槛。
2. 政策按“政策树 ID + 层级”分组；每棵树可以有任意层，每层可以配置任意数量的政策。
3. 玩家在同一棵树的同一层只能选择一个政策；选择另一政策会替换原选择。
4. 选择政策时必须达到该政策的民意门槛，但选择不会消耗民意。
5. 已选择政策仅在当前民意大于等于门槛时生效。民意跌破门槛时自动失效，选择本身保留；民意再次达标时自动恢复生效。
6. 民意、每层选择结果都进入 `GameData` 存档，并随运行时快照保存和恢复。
7. `GameSystem.prefab` 已绑定空的 `PolicyCatalog.asset`。需求没有提供具体政策内容，因此没有虚构默认政策。

当前项目尚未提供政策面板的结构、入口或视觉稿，因此没有正式政策 UI。后续 UI 必须作为 `PolicyService` 的薄消费者，不得自行保存民意、选择或激活状态。

## 核心规则

政策生效条件只有两个，必须同时满足：

```text
政策已被选中 && 当前民意 >= 政策所需民意
```

例如某政策所需民意为 `100`：

- 民意 `99`：不能首次选择；如果此前已选择，则当前失效。
- 民意从 `99` 升至 `100`：已选择的该政策自动生效，不扣除民意。
- 民意从 `100` 降至 `99`：政策自动失效，但仍保留为该层的选择。
- 民意再次达到 `100`：政策自动恢复生效，不需要重新选择。

层级目前只负责组织和“同层单选”，没有自动附加前置层规则。若以后需要“必须先选择上一层政策”，应作为明确的新规则加入 `PolicyService`，不要让 UI 私自判断。

## 数据配置

### PolicyDefinition

每个政策是一个 `PolicyDefinition` ScriptableObject，字段如下：

- `policyId`：全目录唯一、非空的政策 ID。
- `displayName`：显示名称；为空时回退为资产名。
- `description`：政策说明。
- `icon`：可选图标。
- `treeId`：政策树 ID；同一棵树必须使用同一个非空 ID。
- `treeDisplayName`：政策树显示名称；同一 `treeId` 应保持一致。
- `tier`：层级，从 `1` 开始。
- `requiredPublicOpinion`：所需民意，最小为 `0`。

政策资产统一放在：

`Assets/Landsong/Objects/SO/Policy`

创建菜单：

`Landsong/Policy/Policy Definition`

### PolicyCatalog

目录资产是：

`Assets/Landsong/Objects/SO/PolicyCatalog.asset`

`PolicyCatalog` 负责：

- 保存全部政策定义。
- 建立政策 ID 索引并拒绝重复 ID。
- 按政策树或“政策树 + 层级”查询政策。
- 检查同一政策树是否使用了不一致的显示名称。

在 Inspector 中可使用“从文件夹加载政策”按钮，把 `Assets/Landsong/Objects/SO/Policy` 下的全部 `PolicyDefinition` 重新装入目录。新增政策后必须确认它已进入目录，否则运行时无法选择或恢复该政策。

## 运行时 API

唯一入口：

```csharp
PolicyService policies = GameSystem.Instance.Services.Policies;
```

民意修改：

```csharp
policies.SetPublicOpinion(100); // 设置绝对值
policies.AddPublicOpinion(10);  // 增减；最终值不会低于 0
```

政策选择和查询：

```csharp
PolicySelectionResult result = policies.TrySelectPolicy("policy_id");
bool selected = policies.IsPolicySelected("policy_id");
bool active = policies.IsPolicyActive("policy_id");
PolicyDefinition tierChoice = policies.GetSelectedPolicy("economy", 2);
policies.TryClearSelection("economy", 2);
```

`TrySelectPolicy` 的失败原因：

- `PolicyNotFound`：目录中没有该 ID。
- `InsufficientPublicOpinion`：当前民意未达到门槛。

只想显示锁定状态时使用 `CanSelectPolicy(policyId)`；不要在 UI 再复制门槛判断。需要显示当前全部选择或全部生效政策时，读取 `SelectedPolicyIds` / `ActivePolicyIds`。

## 事件约定

`PolicyService` 提供以下事件：

- `PublicOpinionChanged`：民意发生实际变化。
- `SelectionChanged`：某层的选择发生变化；参数包含新选择与被替换的旧选择。
- `PolicyActivationChanged`：政策因选择或民意门槛变化而生效/失效。
- `StateChanged`：任意政策领域状态完成一次变更或存档恢复。

普通面板通常订阅 `StateChanged` 后整体刷新即可。只有需要播放“政策生效/失效”反馈时才订阅 `PolicyActivationChanged`。订阅者不得反向维护第二份激活列表。

## 存档规则

`PolicySaveData` 保存：

- `PublicOpinion`：当前民意。
- `SelectedPolicyIds`：每层当前选择的政策 ID。

激活列表不单独存档，因为它完全可以由“选择 + 当前民意 + 政策门槛”重新计算。这样不会出现存档中的激活状态与配置门槛冲突。

存档链路：

1. `GameRuntimeSnapshotService.Capture` 调用 `PolicyService.CaptureSaveData()`。
2. 数据写入 `GameData.PolicyData`。
3. 恢复时调用 `PolicyService.RestoreSaveData(...)`。
4. 恢复过程会忽略目录中已经不存在的政策，并保证同一政策树同一层最多恢复一个选择。

## 代码职责

- `PolicyDefinition.cs`：单个政策的数据定义。
- `PolicyCatalog.cs`：目录、ID 索引和树/层查询。
- `PolicyService.cs`：民意、选择、激活判定、事件和政策存档数据。
- `GameSystem.cs`：创建并持有政策服务，提供初始配置。
- `GameServices.cs`：公开 `Policies` 与 `PolicyCatalog` 领域入口。
- `GameRuntimeSnapshotService.cs`：把政策状态接入运行时快照。
- `SaveDataModels.cs`：在 `GameData` 中保存 `PolicyData`。

政策的实际玩法加成应由对应玩法系统查询 `IsPolicyActive(...)`，或订阅 `PolicyActivationChanged` 后刷新自己的派生数据。不要把库存、建筑、人口等玩法修改直接写入政策 UI。

## 后续开发检查清单

1. 新政策的 `policyId` 和 `treeId` 必须非空，`policyId` 必须全局唯一。
2. 同一政策树的 `treeDisplayName` 必须一致，层级必须从 `1` 开始。
3. 新资产放入政策文件夹后，重新加载并保存 `PolicyCatalog.asset`。
4. 民意只能通过 `PolicyService.SetPublicOpinion/AddPublicOpinion` 修改。
5. 选择只能通过 `TrySelectPolicy/TryClearSelection` 修改。
6. 消费政策效果时以 `IsPolicyActive` 为准，不要只判断“已选择”。
7. UI 需要同时区分未选择、民意不足、可选择、已选择且生效、已选择但失效。
8. CLI 编译只能证明 C# 引用成立；ScriptableObject 配置、Inspector 目录加载和未来 UI 交互仍需在 Unity Editor 中运行确认。
