# AI_添加建筑规则

这份文档给后续 AI 修改 Landsong 建筑系统时使用。它不是玩家教程，而是代码入口、架构边界和禁止事项清单。

## 最高优先级规则

1. 先读本文件，再读 `Document/建筑扩展规则README.md` 和 `Document/建筑创建完整教程README.md`。如果文档和旧脚本冲突，以文档和当前架构入口为准。
2. 不要把 `LumberCabin` 当作 `IBuildingWorkforceFundingSource` 的最终实现规范；它只能作为 API 调用形式参考。
3. 不需要旧兼容时，删除旧 API、旧字段、旧存档分支和无消费者接口，不要保留兼容壳。
4. 不要为某一个具体建筑把字段塞进 `BuildingBase` 或 `BuildingDefinition`。通用能力做模块，独有行为写具体建筑脚本。
5. 不要通过重命名已有 `BuildingModuleBase` 子类来做检查器中文显示。`SerializeReference` 会记录托管类型名，直接改类名会破坏 prefab 引用。现有模块用 `ModuleDisplayName` 和 `ModuleDescription` 显示中文名称与说明。
6. 没有用户明确要求时，不改 prefab、scene、ScriptableObject 资产。代码修改后把需要手工配置的 prefab 字段写清楚。
7. 源码编译通过不等于 Unity 检查器、prefab、运行时交互都正确。涉及 Odin 绘制、SerializeReference、prefab 字段时，要提示需要 Unity Editor 验证。

## 核心入口

| 目标 | 入口 |
| --- | --- |
| 建筑运行时基类 | `Assets/Landsong/Scripts/BuildingSystem/BuildingBase.cs` |
| 建筑静态配置 | `Assets/Landsong/Scripts/BuildingSystem/BuildingDefinition.cs` |
| 建筑模块 | `Assets/Landsong/Scripts/BuildingSystem/BuildingModules.cs` |
| 建筑放置、替换、升级替换 | `Assets/Landsong/Scripts/BuildingSystem/BuildingService.cs` |
| 玩家放置输入和预览 | `Assets/Landsong/Scripts/BuildingSystem/BuildingPlacementController.cs` |
| 建筑目录 | `Assets/Landsong/Scripts/BuildingSystem/BuildingCatalog.cs` |
| 回合推进 | `Assets/Landsong/Scripts/GameSystem/TurnService.cs` |
| 库存服务 | `Assets/Landsong/Scripts/GameSystem/Inventory/InventoryService.cs` |
| 建造/升级成本扣除扩展 | `Assets/Landsong/Scripts/GameSystem/Inventory/InventoryBuildingCostExtensions.cs` |
| 资源产出/消耗/税收/科技点接口 | `Assets/Landsong/Scripts/BuildingSystem/BuildingResourceInterfaces.cs` |
| 工人、岗位、补贴接口 | `Assets/Landsong/Scripts/BuildingSystem/BuildingJobSystem.cs` |
| 详情面板功能块数据结构 | `Assets/Landsong/Scripts/BuildingSystem/BuildingFunctionBlockInterfaces.cs` |
| 建筑异常状态 ID | `Assets/Landsong/Scripts/BuildingSystem/BuildingRuntimeStatusCatalog.cs` |
| 建筑存档类型注册 | `Assets/Landsong/Scripts/BuildingSystem/BuildingSaveDataRegistry.cs` |
| 科技服务总入口 | `Assets/Landsong/Scripts/GameSystem/GameSystem.cs` |

## 新建筑创建流程

1. 判断建筑是否需要独有逻辑。
   - 只有静态配置不同：优先复用已有脚本和模块。
   - 有独立回合、工人、产出、升级、存档、详情 UI：创建新的 `BuildingBase` 子类。
2. 在 prefab 根节点上挂具体建筑脚本。
3. 在 prefab 的 `definition` 中配置静态数据：建筑 ID、显示名、分类、图标、占地、地形要求、移动阻力、放置成本、菜单显示条件、数量限制、专属详情面板。
4. 把运行时状态写在具体建筑脚本或模块中，不写进 `BuildingDefinition`。
5. 可复用能力抽成 `BuildingModuleBase` 子类，并挂到 `BuildingBase` 的 `buildingModules` 列表。
6. 有存档状态时，创建继承 `BuildingDataBase` 的数据类，并添加稳定的 `[BuildingDataTypeId("building.xxx")]`。
7. 需要在详情面板显示产出、消耗、功能、状态时，实现对应接口或重写 `GetFunctionBlockEntries()` / `GetRuntimeStatuses()`。
8. 把 prefab 加入 `BuildingCatalog`，建造菜单只从目录和 `BuildingDefinition` 读取。

## BuildingDefinition 规则

`BuildingDefinition` 只放 prefab 级静态配置：

- `buildingId`
- `displayName`
- `category`
- `icon`
- `size`
- `requiredTerrainKeys`
- `movementResistance`
- `placementCosts`
- `visibleCondition`
- `availableCondition`
- `buildMenuSortOrder`
- `maxBuildCount`
- `buildLimitGroupId`
- `uniqueDetailPanel`

不要放这些内容：

- 当前工人数
- 当前经验
- 当前种植作物
- 当前冷却回合
- 上回合产出
- 自动收获开关
- 运行时警告状态
- 具体建筑的 UI 文案拼接

## BuildingBase 规则

具体建筑脚本继承 `BuildingBase`，重点实现这些方法：

- `OnPlaced()`：建筑完成放置并拥有格子位置后触发。
- `OnTurn()`：每回合逻辑。返回 `true` 表示本回合执行成功，返回 `false` 表示本回合失败但不阻断整个回合推进。
- `CaptureBuildingData()`：保存具体建筑运行时状态。
- `RestoreBuildingData(BuildingDataBase data)`：恢复具体建筑运行时状态。
- `GetOverviewInfo()`：建筑列表、选中栏、底栏的一行摘要。
- `GetRuntimeStatuses()`：建筑异常状态。
- `GetFunctionBlockEntries()`：详情面板顶部功能块和右侧行数据。

注意：

- `ProcessTurn()` 已经负责调用 `OnTurn()`，成功后尝试 `BuildingLevelUpgradeModule` 自动升级，并最终 `NotifyStateChanged()`。
- 子类在非回合流程中修改运行时状态后，需要调用 `NotifyStateChanged()`。该方法是 `protected`，模块不能直接调用。
- 子类重写 `Start()` 时必须调用 `base.Start()`，否则建筑不会正确注册到 `GameSystem`。
- 不要绕过 `BuildingService` 自己实例化、占格或替换建筑。

## BuildingModule 规则

模块用于表达“多个建筑可复用的能力”，不是 MonoBehaviour。

模块必须满足：

- 继承 `BuildingModuleBase`。
- 使用 `[Serializable]`。
- 只保存该能力自己的配置和轻量运行时状态。
- 实现 `Normalize()` 修正非法值。
- 通过 `ModuleDisplayName` 返回检查器中文名，例如 `BM_科技点产出`。
- 通过 `ModuleDescription` 返回模块作用说明。
- 需要显示到详情功能块时，重写 `AppendFunctionBlockEntries(BuildingBase building, ref List<BuildingFunctionBlockEntry> entries)`。

模块默认没有完整生命周期。当前基础模块只提供：

- `Normalize()`
- `AppendFunctionBlockEntries(...)`
- `IsEnabled`

如果模块需要参与回合、保存、恢复，必须由拥有它的 `BuildingBase` 子类显式调用模块方法，并在建筑数据类中保存模块状态，除非后续架构已经增加了统一模块生命周期。

现有模块：

- `BuildingNearbyPopulationJobAttractionModule`：检查器显示 `BM_附近人口岗位吸引`。
- `BuildingInventorySlotCapacityModule`：检查器显示 `BM_库存格容量`。
- `BuildingTechnologyPointModule`：检查器显示 `BM_科技点产出`。
- `BuildingLevelUpgradeModule`：检查器显示 `BM_等级升级`。

新增模块示例：

```csharp
[Serializable]
public sealed class BuildingCropPlantingModule : BuildingModuleBase
{
    [SerializeField, LabelText("成熟回合数"), Min(1)]
    private int matureTurns = 3;

    public override string ModuleDisplayName => "BM_种植";
    public override string ModuleDescription => "保存种植配置，提供播种、成熟、收获、铲除相关能力。";

    public int MatureTurns => Mathf.Max(1, matureTurns);

    public override void Normalize()
    {
        matureTurns = Mathf.Max(1, matureTurns);
    }
}
```

## 回合与产出规则

回合入口是 `TurnService`：

- `TurnService.NextTurn(...)` 和 `NextTurnRoutine(...)` 会快照当前建筑列表。
- 每个建筑由 `ProcessBuildingTurn(...)` 调用 `building.ProcessTurn()`。
- 建筑未初始化、正在拆除或为空会计入 `Skipped`。
- `OnTurn()` 返回 `false` 会计入 `Failed`。
- `OnTurn()` 返回 `true` 会计入 `OperatingConsumed`，并触发资源和科技点事件。

建筑产出资源时：

- 实现 `IBuildingResourceProductionSource`。
- `CurrentResourceProductions` 表示当前预期产出。
- `LastResourceProductions` 表示上一次成功产出。
- 使用 `GameSystem.Inventory.TryAddItem(itemId, amount)` 增加库存。
- 库存写入失败时返回 `false` 或记录异常状态，具体取决于建筑规则。

建筑消耗资源时：

- 实现 `IBuildingResourceConsumptionSource`。
- 使用 `GameSystem.Inventory.HasItem(...)`、`TryRemoveItem(...)`、`TryRemoveItems(...)`。
- `BuildingCost[]` 类型成本用 `CanAffordBuildingCosts(...)` 和 `TrySpendBuildingCosts(...)`。

科技点不要由建筑直接写入科技服务。需要每回合科技点时，使用或确保存在 `BuildingTechnologyPointModule`，回合系统会在建筑成功处理后收集。

## 工人规则

需要工人 UI、岗位吸引、补贴或稳定工人数时，建筑实现 `IBuildingWorkforceFundingSource`。

工人相关逻辑入口：

- `IBuildingJobSource`
- `IBuildingWorkforceFundingSource`
- `BuildingJobSystem.Calculate(...)`
- `BuildingDetailsBlock_Workforce`
- `BuildingNearbyPopulationJobAttractionModule`

工人数不足需要警告时，优先使用 `BuildingRuntimeStatusCatalog.BS_工人不足` 或 `BS_缺工`，不要新增重复含义的状态 ID。

## 升级规则

可升级建筑优先使用 `BuildingLevelUpgradeModule`：

- 当前经验：`CurrentExperience`
- 所需经验：`RequiredExperience`
- 自动升级开关：`AutoUpgradeEnabled`
- 升级目标 prefab：`UpgradeTargetPrefab`
- 升级条件：`UpgradeCondition`
- 升级成本：`UpgradeCosts`

成功产出后加经验：

```csharp
if (TryGetModule<BuildingLevelUpgradeModule>(out var levelModule))
{
    levelModule.AddExperience(1);
}
```

升级本质是建筑替换：

- 不要在同一个实例上硬切等级状态。
- LV1 和 LV2 行为明显不同、最大工人数不同、特殊产出不同，通常创建两个 prefab 和两个脚本，或一个基类加两个子类。
- 使用 `BuildingService.TryReplace(...)` 或 `BuildingLevelUpgradeModule.TryUpgrade(...)` 完成替换。

需要科技解锁条件时，优先使用条件系统中的科技解锁条件。直接查询时使用 `GameSystem.IsTechnologyUnlocked(string technologyId)`。

## 详情 UI 规则

不要在 UI 层用建筑类型分支拼接业务数据。建筑或模块负责提供结构化数据。

详情入口：

- `BuildingBase.GetOverviewInfo()`
- `BuildingBase.GetRuntimeStatuses()`
- `BuildingBase.GetFunctionBlockEntries()`
- `BuildingFunctionBlockEntry`
- `BuildingFunctionBlockSidebarRow`
- `BuildingDetailsBlock_Function`
- `BuildingDetailsBlock_Workforce`

功能块分组：

- `BuildingFunctionBlockGroup.资源组`
- `BuildingFunctionBlockGroup.功能性`

如果建筑实现了 `IBuildingResourceProductionSource` 或 `IBuildingResourceConsumptionSource`，默认 `GetFunctionBlockEntries()` 会通过 `AppendDefaultResourceFunctionBlockEntries(...)` 输出资源变化。自定义行数据时可以创建带 `SidebarRows` 的 `BuildingFunctionBlockEntry`。

## 运行状态规则

异常状态统一走 `BuildingRuntimeStatus` 和 `BuildingRuntimeStatusCatalog`。

常用状态：

- `BS_库存缺失`
- `BS_工人不足`
- `BS_缺工`
- `BS_招工金币不足`
- `BS_补贴金币不足`
- `BS_道路不通`
- `BS_消耗失败`

添加状态时：

- 先查 `BuildingRuntimeStatusCatalog` 是否已有同义状态。
- 只有 UI 需要统一识别的新异常才添加 catalog 常量。
- 建筑本地临时状态可以在 `GetRuntimeStatuses()` 中按当前状态生成。

## 存档规则

有运行时状态的建筑必须提供数据类：

```csharp
[Serializable]
[BuildingDataTypeId("building.example")]
private sealed class ExampleBuildingData : BuildingDataBase
{
    public int CurrentProgress;
    public bool AutoEnabled;
}
```

规则：

- `BuildingDataTypeId` 必须稳定，不要用 C# 类名或命名空间当作存档 ID。
- 字段只保存恢复玩法所需的数据，不保存可由 prefab 配置重新计算的数据。
- `BuildingBase` 已经统一保存 `BuildingLevelUpgradeModule` 的自动升级和经验。
- 不需要旧兼容时，不写旧字段映射和旧 ID fallback。

## 放置与目录规则

放置统一走 `BuildingService.TryPlace(...)`：

- 扣除放置成本。
- 检查地形和占地。
- 实例化 prefab。
- 写入 grid 占用。
- 注册建筑。

替换统一走 `BuildingService.TryReplace(...)`：

- 用于升级、形态变化、阶段替换。
- 不要在建筑脚本里手动 `Instantiate` 新建筑再 `Destroy` 旧建筑。

`BuildingPlacementController` 只处理玩家输入、预览、高亮和确认请求，不承载业务规则。

新建筑进入建造菜单时，确认：

- prefab `definition.BuildingId` 唯一且非空。
- prefab `definition.Size` 正确。
- prefab `definition.RequiredTerrainKeys` 正确。
- prefab 已加入 `BuildingCatalog`。
- 放置成本在 `definition.PlacementCosts`。
- 运营、生产、升级成本不要放在 `PlacementCosts`。

## 检查器与 Odin 规则

`SerializeReference` 模块列表：

- 不要为了中文显示重命名已有 C# 类型。
- 用 `ModuleDisplayName`、`ModuleDescription` 和 `ToString()` 改善检查器显示。
- 新模块字段用 `[LabelText("中文名")]` 和 `[PropertyTooltip("说明")]`。

Odin 表格：

- `TableList` 中的行类型不要随意加 `[InlineProperty]`，容易造成表头、单元格重叠。
- 表格列字段用 `[LabelText("中文列名")]`。
- 字段重命名但需要保留当前 prefab 数据时，用 `[FormerlySerializedAs("oldFieldName")]`。
- 如果用户明确说不需要旧数据兼容，可以不加旧字段兼容。

## 验证规则

代码修改后至少运行：

```powershell
dotnet build Assembly-CSharp.csproj --no-restore
```

如果缺 restore 产物，再运行：

```powershell
dotnet build Assembly-CSharp.csproj
```

文档或纯注释修改不需要编译，但最终说明要写清楚“未运行编译，因为只改文档”。

涉及这些内容时，需要 Unity Editor 侧继续确认：

- prefab 字段绑定
- `SerializeReference` 模块列表
- Odin 检查器绘制
- Addressables / Catalog 引用
- UI prefab 显示
- 场景初始建筑配置

## 常见错误

- 把当前工人数、成熟回合、经验写进 `BuildingDefinition`。
- 在 `BuildingPlacementController` 里添加建筑业务规则。
- 直接改已有模块 C# 类名导致 prefab 丢失 `SerializeReference`。
- 建筑产出后没有更新 `LastResourceProductions`，导致回合事件和 UI 看不到上回合产出。
- `OnTurn()` 失败仍然给经验、科技点或产物。
- 存档数据类缺少 `[BuildingDataTypeId(...)]`。
- 重写 `Start()` 不调用 `base.Start()`。
- UI 层按建筑类型写 `if (building is XxxBuilding)` 拼接详情。
- 为了一个建筑的独有逻辑污染 `BuildingBase`。
- 只跑 `dotnet build` 就断言 prefab 和检查器已经正确。
