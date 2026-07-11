# Landsong API 统一说明

## 唯一领域入口

运行时领域服务统一通过：

```csharp
GameSystem.Instance.Services
```

访问：

- `Inventory`
- `Turn`
- `Dynasty`
- `Buildings`
- `Events`
- `Technology`
- `Quest`
- `Expeditions`
- `Talents`
- `Inheritance`
- `BuildingBlueprints`
- `BuildingSelection`
- `BuildingCatalog`

例如：

```csharp
GameSystem.Instance.Services.Quest.TrySubmitResources(questId);
GameSystem.Instance.Services.Technology.TryStartResearch(technology);
GameSystem.Instance.Services.Talents.TryRefreshOffers(out var result);
```

`GameSystem` 只保留真正的游戏级编排，例如 `NextTurn()`、游戏结束状态和跨系统回合结算。

## 不再保留的接口

已移除以下重复公开门面：

- 任务提交、放弃、领奖、任务列表和任务事件代理
- 科技解锁、研发、存档代理
- 远征创建、领奖、状态属性和事件代理
- 人才刷新、招募、任命、卸任、升级和状态代理
- 王族生育、退位、后天特性和状态代理
- 建筑蓝图解锁代理
- `DataManager` 的快速保存、覆盖保存、新建保存等同义别名
- `GameSystem` 上直接暴露的领域服务属性和领域状态代理

本次迁移不保留兼容别名，也不使用 `[Obsolete]` 维持第二套接口。所有项目内调用点已迁移到唯一入口。

## 存档入口

当前存档统一使用：

```csharp
DataManager.Instance.SaveCurrentGame();
DataManager.Instance.SaveCurrentGame(GameDataSaveMode.NewSave);
```

存档列表统一读取：

```csharp
DataManager.Instance.GameDataMetaList
```
