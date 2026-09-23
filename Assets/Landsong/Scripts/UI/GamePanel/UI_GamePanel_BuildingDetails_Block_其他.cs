using System;
using System.Collections.Generic;
using Landsong.ECS.Definitions;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_BuildingDetails_Block_其他 : UI_GamePanel_BuildingDetails_Block
    {
        [LabelText("详情条目模板"), Required]
        public UI_GamePanel_Row RowTemplate;
        UI_GamePanel_RowCollection rowsController;
        bool showSupplySources;
        bool showSpatialSources;
        public RectTransform Rows => (RectTransform)transform;

        public void Initialize()
        {
            if (RowTemplate == null)
                throw new InvalidOperationException("建筑详情缺少条目模板。");
            RowTemplate.ValidateConfiguration();
            rowsController = new UI_GamePanel_RowCollection(RowTemplate);
        }

        public void Clear() => rowsController.Clear(Rows);

        public void ResetSession()
        {
            showSupplySources = showSpatialSources = false;
            rowsController?.ClearAll();
        }

        public void Refresh(Entity entity)
        {
            var b = View.sessionController.em.GetComponentData<Building>(entity);
            BuildingConstructionState bConstruction = View.sessionController.em.GetComponentData<BuildingConstructionState>(entity);
            BuildingWorkforceState bWorkforce = View.sessionController.em.GetComponentData<BuildingWorkforceState>(entity);
            BuildingHousingState bHousing = View.sessionController.em.GetComponentData<BuildingHousingState>(entity);
            BuildingProductionState bProduction = View.sessionController.em.GetComponentData<BuildingProductionState>(entity);
            BuildingSanctumState bSanctum = View.sessionController.em.GetComponentData<BuildingSanctumState>(entity);
            BuildingGatheringState bGathering = View.sessionController.em.GetComponentData<BuildingGatheringState>(entity);
            BuildingMarketState bMarket = View.sessionController.em.GetComponentData<BuildingMarketState>(entity);
            BuildingMaintenanceState bMaintenance = View.sessionController.em.GetComponentData<BuildingMaintenanceState>(entity);
            var stats = View.sessionController.em.GetComponentData<BuildingHousingStats>(entity);
            BuildingIntelligenceStats statsIntelligence = View.sessionController.em.GetComponentData<BuildingIntelligenceStats>(entity);
            BuildingSanctumStats statsSanctum = View.sessionController.em.GetComponentData<BuildingSanctumStats>(entity);
            BuildingBellStats statsBell = View.sessionController.em.GetComponentData<BuildingBellStats>(entity);
            var id = View.sessionController.em.GetComponentData<Identity>(entity);
            ref var d = ref BuildingDefinitions.Get(View.sessionController.em, View.sessionController.root, DefinitionOf(entity));
            var day = View.sessionController.em.GetComponentData<Session>(View.sessionController.root).Phase == Phase.Day;
            var normal = b.Stage == LifeStage.Operational;
            rowsController.Clear(Rows);
            void Detail(string text, Action action = null, string key = null)
            {
                rowsController.Row(text, action, parent: Rows, key: key);
            }

            void ProductionDetail(string text, string key = null, [System.Runtime.CompilerServices.CallerLineNumber] int sourceLine = 0) => Detail(text, key: "building:" + id.Id + ":production:" + (key ?? sourceLine.ToString()));
            Detail("查看本建筑账单", () => View.economy.OpenEconomy(id.Id));
            var source = View.buildingUi.BuildingSource(DefinitionOf(entity));
            if (!string.IsNullOrEmpty(source?.Description))
                Detail(source.Description);
            var provider = ResourceNetworkOps.Provider(View.sessionController.em, View.sessionController.root, entity);
            if (b.Stage == LifeStage.Construction)
            {
                Detail($"施工 {bConstruction.Progress}/{d.ConstructionTurns} 回合");
                Detail("下期材料：" + View.buildingUi.CostText(BuildingCostOps.ConstructionStage(View.sessionController.em, View.sessionController.root, DefinitionOf(entity), bConstruction.Progress + 1)));
                if (provider == Entity.Null)
                    Detail("需要从正常库存支付的施工：断连时暂停。");
            }

            if (b.Stage == LifeStage.Ruined || b.Stage == LifeStage.Repairing)
            {
                BuildingCostOps.RepairTotal(View.sessionController.em, View.sessionController.root, entity, out var duration);
                Detail(b.Stage == LifeStage.Ruined ? "荒废：无建筑功能；修复无需工人。" : $"修复 {bConstruction.Progress}/{bConstruction.RepairDuration} 回合，材料不足或断连暂停。");
                if (b.RuinPending != 0)
                    Detail($"本夜已失效，黎明提交居民损失 {bHousing.Population} / 工人失业 {bWorkforce.Workers}。库存已标记损失。");
                RepairDetails(entity, (text, action) => Detail(text, action));
                if (day && b.Stage == LifeStage.Ruined)
                    Detail("开始修复（" + duration + " 回合）", () => View.buildingUi.ConfirmBuildingCommand(CommandKind.Repair));
                return;
            }

            var maintenance = BuildingCostOps.Maintenance(View.sessionController.em, View.sessionController.root, DefinitionOf(entity), b.Level);
            if (maintenance.Count > 0)
                Detail("每回合维护：" + View.buildingUi.CostText(maintenance) + (bMaintenance.Maintained != 0 ? " · 已满足" : " · 未满足"));
            if (stats.MaxPopulation > 0)
            {
                Detail($"居住 {bHousing.Population}/{stats.MaxPopulation} · 增长进度 {bHousing.Growth} · 税收进度 {bHousing.TaxProgress} · 连续缺粮 {bHousing.FoodFailures}");
                if (bHousing.DeferredResidents > 0)
                    Detail("修复迁入人口将在黎明加入：" + bHousing.DeferredResidents);
                foreach (var food in View.sessionController.em.GetBuffer<FoodSelection>(entity))
                    Detail("上次实际食物：" + ItemName(food.Item) + " × " + food.Amount);
            }

            for (int i = 0; i < d.Capabilities.Production.Cycles.Length; i++)
            {
                var cycle = d.Capabilities.Production.Cycles[i];
                if (cycle.Level != 0 && cycle.Level != b.Level)
                    continue;
                ProductionDetail($"生产周期进度 {bProduction.Progress}/{cycle.Interval} · 所需工人 {cycle.RequiredWorkers}");
                ProductionDetail("原料：" + View.buildingUi.CostText(BuildingCostOps.ProductionInputs(View.sessionController.em, View.sessionController.root, DefinitionOf(entity), b.Level)));
                break;
            }

            for (int i = 0; i < d.Capabilities.Production.Outputs.Length; i++)
            {
                var output = d.Capabilities.Production.Outputs[i];
                if (output.Level != 0 && output.Level != b.Level)
                    continue;
                ProductionDetail("产品：" + ItemName(output.Item) + " × " + output.Quantity + " · 工人 " + output.MinimumWorkers + (output.MaximumWorkers > 0 ? "～" + output.MaximumWorkers : " 以上") + (bWorkforce.Workers >= output.MinimumWorkers && (output.MaximumWorkers <= 0 || bWorkforce.Workers <= output.MaximumWorkers) ? "（当前档）" : ""), "output:" + i);
            }

            for (int i = 0; i < d.Capabilities.Production.RareOutputs.Length; i++)
            {
                var output = d.Capabilities.Production.RareOutputs[i];
                if (output.Level != 0 && output.Level != b.Level)
                    continue;
                ProductionDetail($"随机产出：{ItemName(output.Item)} ×{output.Quantity} · 概率 {output.Probability:P0} · 工人 ≥{output.RequiredWorkers}", "random:" + i);
            }

            for (int i = 0; i < d.Capabilities.Housing.Food.Length; i++)
            {
                var food = d.Capabilities.Housing.Food[i];
                if (food.Level == 0 || food.Level == b.Level)
                    Detail($"食谱：{ItemGroupDefinitions.Get(View.sessionController.em, View.sessionController.root, food.FoodGroup).Metadata.Name} · {food.Varieties} 种 · 每居民每种 {math.max(1, food.AmountPerResident)}");
            }

            for (int i = 0; i < d.Capabilities.Housing.Environment.Length; i++)
            {
                var requirement = d.Capabilities.Housing.Environment[i];
                if (requirement.Level == 0 || requirement.Level == b.Level)
                    Detail($"环境条件 {UI_GamePanel_BuildingDetails.EnvironmentName((int)requirement.Type)} ≥ {requirement.RequiredValue} · 当前 {BuildingEnvironment.Value(View.sessionController.em, View.sessionController.root, entity, requirement.Type):0.#}");
            }

            for (int i = 0; i < d.Capabilities.Effects.Spatial.Length; i++)
            {
                var effect = d.Capabilities.Effects.Spatial[i];
                if (effect.Level == 0 || effect.Level == b.Level)
                    Detail($"作用 {UI_GamePanel_BuildingDetails.EnvironmentName((int)effect.Type)} +{effect.Magnitude} · 范围 {effect.Radius} 格 · 工人 ≥{effect.RequiredWorkers}", key: "spatial:" + i);
            }

            for (int i = 0; i < d.Capabilities.Market.Levels.Length; i++)
            {
                var market = d.Capabilities.Market.Levels[i];
                if (market.Level == 0 || market.Level == b.Level)
                    Detail("市场结算比例：" + market.IncomeRatio + " · 本回合归因价值 " + bMarket.TurnValue, key: "market:" + i);
            }

            for (int i = 0; i < d.Capabilities.Gathering.Levels.Length; i++)
            {
                var gathering = d.Capabilities.Gathering.Levels[i];
                if (gathering.Level == 0 || gathering.Level == b.Level)
                    Detail("剩余采集次数：" + bGathering.RemainingUses, day && normal ? () => View.commandsController.TryQueue(new HarvestBuildingRequest { Building = id.Id }) : null);
            }

            View.Block<UI_GamePanel_BuildingDetails_Block_种植>().AppendDetails(entity, Detail);
            View.Block<UI_GamePanel_BuildingDetails_Block_驻军>().AppendDetails(entity, Detail);

            if (statsBell.Radius > 0)
                Detail("警铃集结范围 " + statsBell.Radius, !day && normal ? () => View.commandsController.TryQueue(new RingBellRequest { Building = id.Id }) : null);
            if (statsIntelligence.Points > 0)
                Detail("情报贡献 " + statsIntelligence.Points, () => View.navigation.OpenPanel(GamePanelId.Intelligence));
            if (HasExpeditionSite(ref d.Capabilities.Expeditions, b.Level))
                Detail(WorkforceSettlement.Locked(View.sessionController.em, id.Id) ? "远征在途，岗位/移动/升级锁定" : "打开远征", () => View.navigation.OpenPanel(GamePanelId.Expedition));
            if (statsSanctum.Hero.IsValid)
            {
                ref var hero = ref HeroDefinitions.Get(View.sessionController.em, View.sessionController.root, statsSanctum.Hero);
                Detail($"{hero.Metadata.Name} · 招募 {hero.FallbackWakeGold} 金币 · 人口 {hero.PopulationCost} · 神殿所需工人 {statsSanctum.RequiredWorkers}");
                Detail("供奉：" + View.buildingUi.CostText(HeroCosts(ref hero.OfferingCosts)) + " · 唤醒：" + View.buildingUi.CostText(HeroCosts(ref hero.AwakeningCosts)));
                Detail($"当前工人 {bWorkforce.Workers}/{statsSanctum.RequiredWorkers} · 持续供奉{(bSanctum.Offering == 0 ? "关闭" : "开启")} · 本回合供奉{(bSanctum.PaidOfferingTurn == View.sessionController.em.GetComponentData<GameClock>(View.sessionController.root).Turn ? "已支付" : "未支付")}");
                Detail("供奉在平安夜也消耗资源；唤醒另外付费，未实际参战不获得战斗经验。英雄阵亡后经验清零，冷却结束重招支付完整费用。缺工不会杀死英雄，神殿荒废/拆除会。");
                bool wake = !day;
                string reason = HeroOps.HeroAvailability(View.sessionController.em, View.sessionController.root, entity, wake);
                Detail((wake ? "唤醒英雄" : "招募英雄") + (reason.Length > 0 ? " · " + reason : ""), reason.Length == 0 ? () =>
                {
                    if (wake)
                        View.commandsController.TryQueue(new WakeHeroRequest { Sanctum = id.Id });
                    else
                        View.commandsController.TryQueue(new RecruitHeroRequest { Sanctum = id.Id });
                } : null);
                if (normal && day)
                    Detail(bSanctum.Offering == 0 ? "开启持续供奉" : "关闭持续供奉", () => View.commandsController.TryQueue(new SetOfferingRequest { Building = id.Id, Enabled = (byte)((bSanctum.Offering == 0 ? 1 : 0) != 0 ? 1 : 0) }));
            }
        }

        Landsong.ECS.Definitions.BuildingId DefinitionOf(Entity entity) => View.sessionController.em.GetComponentData<BuildingDefinitionRef>(entity).Definition;
        string ItemName(ItemId item) => item.IsValid ? ItemDefinitions.Get(View.sessionController.em, View.sessionController.root, item).Metadata.Name.ToString() : "—";
        static List<BuildingCost> HeroCosts(ref BlobArray<LeveledItemAmount> costs)
        {
            var result = new List<BuildingCost>();
            for (int i = 0; i < costs.Length; i++)
                if (costs[i].Level == 0 || costs[i].Level == 1)
                    result.Add(new BuildingCost(costs[i].Item, costs[i].Quantity));
            return result;
        }

        static bool HasExpeditionSite(ref BuildingExpeditions expeditions, int level)
        {
            if (!expeditions.Enabled)
                return false;
            for (int i = 0; i < expeditions.Levels.Length; i++)
                if (expeditions.Levels[i].Level == 0 || expeditions.Levels[i].Level == level)
                    return true;
            return false;
        }

        internal void RepairDetails(Entity entity, Action<string, Action> detail)
        {
            var q = BuildingCostOps.QuoteRepair(View.sessionController.em, View.sessionController.root, entity);
            detail((View.sessionController.em.GetComponentData<Building>(entity).Stage == LifeStage.Repairing ? "已冻结修复总额：" : "拟定修复总额：") + View.buildingUi.CostText(q.Total) + " · 尚需 " + View.buildingUi.CostText(q.Remaining), null);
            detail($"下一期 {q.Step + 1}/{q.Duration}：{q.Reason}", null);
            foreach (var p in q.Payments)
                detail($"{ItemName(p.Item)}：本期需 {p.Required} = 待存放 {p.Pending} + 正常库存 {p.Normal}；正常可用 {p.Available}；缺口 {p.Missing}", null);
            detail("以上为当前资源预览，不预留材料；同回合较早结算的建筑仍可能先用这些物资。修复开始只冻结计划，不立即付款。", null);
        }

        internal void SupplyDetails(Entity entity, Action<string, Action> detail)
        {
            detail(showSupplySources ? "收起供给来源" : "展开供给来源", () =>
            {
                showSupplySources = !showSupplySources;
                View.refresh.NextPanel = 0;
            });
            if (!showSupplySources)
                return;
            var q = ResourceNetworkOps.Quote(View.sessionController.em, View.sessionController.root, entity);
            detail("先比较提供点优先级，再比较道路加权距离，同值按稳定建筑 ID。提供点用于连接/市场归因，材料仍从全城正常库存扣除。", null);
            foreach (var c in q.Candidates)
            {
                var candidate = c;
                detail($"{View.sessionController.EntityName(c.Id)} · 优先级 {c.Priority} · 路径成本 {(float.IsInfinity(c.Cost) ? "不可达" : c.Cost.ToString("0.##"))} · {(q.Selected == c.Entity ? "已选中" : c.Reason)}", () => View.buildingUi.FocusBuilding(candidate.Id));
            }

            if (q.Candidates.Count == 0)
                detail("没有其他资源提供点。", null);
        }

        internal void SpatialDetails(Entity entity, Action<string, Action> detail)
        {
            detail(showSpatialSources ? "收起空间效果来源" : "展开空间效果来源", () =>
            {
                showSpatialSources = !showSpatialSources;
                View.refresh.NextPanel = 0;
            });
            if (!showSpatialSources)
                return;
            foreach (var kind in new[]
            {
                10,
                20,
                30,
                40
            }

            )
            {
                var q = SpatialOps.Quote(View.sessionController.em, View.sessionController.root, entity, (BuildingEnvironmentKind)kind);
                detail(UI_GamePanel_BuildingDetails.EnvironmentName(kind) + " · 实际合计 " + q.Value, null);
                foreach (var source in q.Sources)
                {
                    var s = source;
                    detail($"{View.sessionController.EntityName(s.Source)} · {s.Group} · 配置 {s.Amount} / 计入 {s.Applied} · {s.Reason}", () => View.buildingUi.FocusBuilding(s.Source));
                }
            }
        }

    }
}
