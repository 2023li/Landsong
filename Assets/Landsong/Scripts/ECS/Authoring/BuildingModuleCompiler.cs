using System;
using System.Collections.Generic;

namespace Landsong.ECS.Authoring
{
    // One-way compiler: module assets are authoritative; generated rules are immutable execution data.
    public static class BuildingModuleCompiler
    {
        public static Rule[] Compile(BuildingModules modules, ContentReferenceResolver references)
        {
            if(modules==null)throw new InvalidOperationException("建筑缺少模块配置");
            var result=new List<Rule>();
            int Reference(GameDefinitionAsset asset,bool optional,string field)=>references.Resolve(asset,optional,field);
            if(modules.Construction!=null&&modules.Construction.Enabled)
            {
                if(modules.Construction.PlacementCosts==null)throw new InvalidOperationException("建造与施工 / 放置材料列表为空引用");
                foreach(var entry in modules.Construction.PlacementCosts)
                {
                    if(entry==null)throw new InvalidOperationException("放置材料存在空条目");
                    result.Add(new Rule{Secondary=-1,Kind=RuleKind.PlacementCost,Level=1,Target=Reference(entry.Item,false,"放置材料 / 物品"),Amount=entry.Quantity});
                }
                if(modules.Construction.StageCosts==null)throw new InvalidOperationException("建造与施工 / 施工材料列表为空引用");
                foreach(var entry in modules.Construction.StageCosts)
                {
                    if(entry==null)throw new InvalidOperationException("施工材料存在空条目");
                    result.Add(new Rule{Secondary=-1,Kind=RuleKind.ConstructionCost,Level=entry.Stage,Target=Reference(entry.Item,false,"施工材料 / 物品"),Amount=entry.Quantity});
                }
                if(modules.Construction.StageOutputs==null)throw new InvalidOperationException("建造与施工 / 施工产物列表为空引用");
                foreach(var entry in modules.Construction.StageOutputs)
                {
                    if(entry==null)throw new InvalidOperationException("施工产物存在空条目");
                    result.Add(new Rule{Secondary=-1,Kind=RuleKind.ConstructionOutput,Level=entry.Stage,Target=Reference(entry.Item,false,"施工产物 / 物品"),Amount=entry.Quantity});
                }
            }
            if(modules.Upgrade!=null&&modules.Upgrade.Enabled)
            {
                if(modules.Upgrade.Costs==null)throw new InvalidOperationException("升级与经验 / 升级材料列表为空引用");
                foreach(var entry in modules.Upgrade.Costs)
                {
                    if(entry==null)throw new InvalidOperationException("升级材料存在空条目");
                    result.Add(new Rule{Secondary=-1,Kind=RuleKind.UpgradeCost,Level=entry.TargetLevel,Target=Reference(entry.Item,false,"升级材料 / 物品"),Amount=entry.Quantity});
                }
                if(modules.Upgrade.Workers==null)throw new InvalidOperationException("升级与经验 / 升级所需工人列表为空引用");
                foreach(var entry in modules.Upgrade.Workers)
                {
                    if(entry==null)throw new InvalidOperationException("升级所需工人存在空条目");
                    result.Add(new Rule{Target=-1,Secondary=-1,Kind=RuleKind.UpgradeWorkers,Level=entry.TargetLevel,Amount=entry.Required});
                }
                if(modules.Upgrade.Residents==null)throw new InvalidOperationException("升级与经验 / 升级所需居民列表为空引用");
                foreach(var entry in modules.Upgrade.Residents)
                {
                    if(entry==null)throw new InvalidOperationException("升级所需居民存在空条目");
                    result.Add(new Rule{Target=-1,Secondary=-1,Kind=RuleKind.UpgradePopulation,Level=entry.TargetLevel,Amount=entry.Required});
                }
                if(modules.Upgrade.MaintenanceRequirements==null)throw new InvalidOperationException("升级与经验 / 升级需维护列表为空引用");
                foreach(var entry in modules.Upgrade.MaintenanceRequirements)
                {
                    if(entry==null)throw new InvalidOperationException("升级需维护存在空条目");
                    result.Add(new Rule{Target=-1,Secondary=-1,Kind=RuleKind.UpgradeMaintained,Level=entry.TargetLevel});
                }
                if(modules.Upgrade.Experience==null)throw new InvalidOperationException("升级与经验 / 经验成长列表为空引用");
                foreach(var entry in modules.Upgrade.Experience)
                {
                    if(entry==null)throw new InvalidOperationException("经验成长存在空条目");
                    result.Add(new Rule{Target=-1,Secondary=-1,Kind=RuleKind.Experience,Level=entry.Level,Amount=entry.ExperiencePerTurn,B=entry.UpgradeExperience,C=entry.RequiredWorkers});
                }
            }
            if(modules.Maintenance!=null&&modules.Maintenance.Enabled)
            {
                if(modules.Maintenance.Costs==null)throw new InvalidOperationException("维护与修复 / 每回合维护材料列表为空引用");
                foreach(var entry in modules.Maintenance.Costs)
                {
                    if(entry==null)throw new InvalidOperationException("每回合维护材料存在空条目");
                    result.Add(new Rule{Secondary=-1,Kind=RuleKind.Maintenance,Level=entry.Level,Target=Reference(entry.Item,false,"每回合维护材料 / 物品"),Amount=entry.Quantity});
                }
                if(modules.Maintenance.Repairs==null)throw new InvalidOperationException("维护与修复 / 修复材料列表为空引用");
                foreach(var entry in modules.Maintenance.Repairs)
                {
                    if(entry==null)throw new InvalidOperationException("修复材料存在空条目");
                    result.Add(new Rule{Secondary=-1,Kind=RuleKind.RepairCost,Level=entry.Level,Target=Reference(entry.Item,false,"修复材料 / 物品"),Amount=entry.Quantity,C=entry.RepairTurns});
                }
            }
            if(modules.Production!=null&&modules.Production.Enabled)
            {
                if(modules.Production.Inputs==null)throw new InvalidOperationException("物品生产 / 生产投入材料列表为空引用");
                foreach(var entry in modules.Production.Inputs)
                {
                    if(entry==null)throw new InvalidOperationException("生产投入材料存在空条目");
                    result.Add(new Rule{Secondary=-1,Kind=RuleKind.Input,Level=entry.Level,Target=Reference(entry.Item,false,"生产投入材料 / 物品"),Amount=entry.Quantity});
                }
                if(modules.Production.Cycles==null)throw new InvalidOperationException("物品生产 / 基础生产周期列表为空引用");
                foreach(var entry in modules.Production.Cycles)
                {
                    if(entry==null)throw new InvalidOperationException("基础生产周期存在空条目");
                    result.Add(new Rule{Target=-1,Secondary=-1,Kind=RuleKind.Production,Level=entry.Level,Amount=entry.Interval,B=entry.RequiredWorkers});
                }
                if(modules.Production.ProcessingTiers==null)throw new InvalidOperationException("物品生产 / 工人加工周期列表为空引用");
                foreach(var entry in modules.Production.ProcessingTiers)
                {
                    if(entry==null)throw new InvalidOperationException("工人加工周期存在空条目");
                    result.Add(new Rule{Target=-1,Secondary=-1,Kind=RuleKind.ProcessingTier,Level=entry.Level,Amount=entry.Interval,B=entry.RequiredWorkers});
                }
                if(modules.Production.Outputs==null)throw new InvalidOperationException("物品生产 / 生产产出档位列表为空引用");
                foreach(var entry in modules.Production.Outputs)
                {
                    if(entry==null)throw new InvalidOperationException("生产产出档位存在空条目");
                    result.Add(new Rule{Secondary=-1,Kind=RuleKind.ProductionTier,Level=entry.Level,Target=Reference(entry.Item,false,"生产产出档位 / 物品"),Amount=entry.Quantity,B=entry.MinimumWorkers,C=entry.MaximumWorkers});
                }
                if(modules.Production.RareOutputs==null)throw new InvalidOperationException("物品生产 / 随机产出列表为空引用");
                foreach(var entry in modules.Production.RareOutputs)
                {
                    if(entry==null)throw new InvalidOperationException("随机产出存在空条目");
                    result.Add(new Rule{Secondary=-1,Kind=RuleKind.RareOutput,Level=entry.Level,Target=Reference(entry.Item,false,"随机产出 / 物品"),Amount=entry.Quantity,B=entry.RequiredWorkers,Value=entry.Probability});
                }
            }
            if(modules.Quests!=null&&modules.Quests.Enabled)
            {
                if(modules.Quests.InvitationCosts==null)throw new InvalidOperationException("任务与邀约 / 刷新邀约费用列表为空引用");
                foreach(var entry in modules.Quests.InvitationCosts)
                {
                    if(entry==null)throw new InvalidOperationException("刷新邀约费用存在空条目");
                    result.Add(new Rule{Secondary=-1,Kind=RuleKind.QuestRecruitCost,Level=entry.Level,Target=Reference(entry.Item,false,"刷新邀约费用 / 物品"),Amount=entry.Quantity});
                }
                if(modules.Quests.Capacity==null)throw new InvalidOperationException("任务与邀约 / 任务容量列表为空引用");
                foreach(var entry in modules.Quests.Capacity)
                {
                    if(entry==null)throw new InvalidOperationException("任务容量存在空条目");
                    result.Add(new Rule{Target=-1,Secondary=-1,Kind=RuleKind.QuestCapacity,Level=entry.Level,Amount=entry.Slots});
                }
                if(modules.Quests.Invitations==null)throw new InvalidOperationException("任务与邀约 / 邀约来源列表为空引用");
                foreach(var entry in modules.Quests.Invitations)
                {
                    if(entry==null)throw new InvalidOperationException("邀约来源存在空条目");
                    result.Add(new Rule{Target=-1,Secondary=-1,Kind=RuleKind.QuestSource,Level=entry.Level,Amount=entry.Slots,B=(int)entry.Type,C=entry.MinimumRefreshTurns,Value=entry.MaximumRefreshTurns});
                }
            }
            if(modules.Placement!=null&&modules.Placement.Enabled)
            {
                if(modules.Placement.RequiredTerrains==null)throw new InvalidOperationException("放置地形 / 必须包含的地形列表为空引用");
                foreach(var entry in modules.Placement.RequiredTerrains)
                {
                    if(entry==null)throw new InvalidOperationException("必须包含的地形存在空条目");
                    result.Add(new Rule{Target=-1,Secondary=-1,Kind=RuleKind.RequiredTerrain,Level=0,Key=new Unity.Collections.FixedString64Bytes(entry.Terrain??"")});
                }
                if(modules.Placement.AlternativeTerrains==null)throw new InvalidOperationException("放置地形 / 至少包含一种地形列表为空引用");
                foreach(var entry in modules.Placement.AlternativeTerrains)
                {
                    if(entry==null)throw new InvalidOperationException("至少包含一种地形存在空条目");
                    result.Add(new Rule{Target=-1,Secondary=-1,Kind=RuleKind.AnyTerrain,Level=0,Key=new Unity.Collections.FixedString64Bytes(entry.Terrain??"")});
                }
            }
            if(modules.Storage!=null&&modules.Storage.Enabled)
            {
                if(modules.Storage.Providers==null)throw new InvalidOperationException("仓储与资源提供 / 资源提供点列表为空引用");
                foreach(var entry in modules.Storage.Providers)
                {
                    if(entry==null)throw new InvalidOperationException("资源提供点存在空条目");
                    result.Add(new Rule{Target=-1,Secondary=-1,Kind=RuleKind.Provider,Level=entry.Level});
                }
                if(modules.Storage.Warehouses==null)throw new InvalidOperationException("仓储与资源提供 / 库存容量列表为空引用");
                foreach(var entry in modules.Storage.Warehouses)
                {
                    if(entry==null)throw new InvalidOperationException("库存容量存在空条目");
                    result.Add(new Rule{Secondary=-1,Kind=RuleKind.Warehouse,Level=entry.Level,Target=Reference(entry.SlotType,false,"库存容量 / 库存槽类型"),Amount=entry.Slots,B=entry.RequiredWorkers});
                }
                if(modules.Storage.Conditions==null)throw new InvalidOperationException("仓储与资源提供 / 仓储运行条件列表为空引用");
                foreach(var entry in modules.Storage.Conditions)
                {
                    if(entry==null)throw new InvalidOperationException("仓储运行条件存在空条目");
                    result.Add(new Rule{Target=-1,Secondary=-1,Kind=RuleKind.StorageCondition,Level=entry.Level,Amount=entry.RequiredWorkers,B=entry.MaintenanceLossPercent,Value=entry.AttractionPenalty,Extra=entry.UnderstaffedLossMultiplier});
                }
            }
            if(modules.Workforce!=null&&modules.Workforce.Enabled)
            {
                if(modules.Workforce.Levels==null)throw new InvalidOperationException("岗位与补贴 / 岗位配置列表为空引用");
                foreach(var entry in modules.Workforce.Levels)
                {
                    if(entry==null)throw new InvalidOperationException("岗位配置存在空条目");
                    result.Add(new Rule{Secondary=-1,Kind=RuleKind.Workforce,Level=entry.Level,Target=Reference(entry.Currency,true,"岗位配置 / 补贴和招聘货币（空 = 默认金币）"),Amount=entry.Capacity,B=entry.InitialWorkers,C=entry.InitialSubsidy?1:0,Value=entry.BaseAttraction,Extra=entry.RecruitmentCost});
                }
                if(modules.Workforce.Attraction==null)throw new InvalidOperationException("岗位与补贴 / 额外吸引力列表为空引用");
                foreach(var entry in modules.Workforce.Attraction)
                {
                    if(entry==null)throw new InvalidOperationException("额外吸引力存在空条目");
                    result.Add(new Rule{Target=-1,Secondary=-1,Kind=RuleKind.Attraction,Level=entry.Level,Value=entry.Radius,Extra=entry.PerResidentBonus});
                }
            }
            if(modules.Housing!=null&&modules.Housing.Enabled)
            {
                if(modules.Housing.Population==null)throw new InvalidOperationException("人口与住宅 / 基础人口列表为空引用");
                foreach(var entry in modules.Housing.Population)
                {
                    if(entry==null)throw new InvalidOperationException("基础人口存在空条目");
                    result.Add(new Rule{Target=-1,Secondary=-1,Kind=RuleKind.Population,Level=entry.Level,Amount=entry.Population,B=entry.IsCore?1:0});
                }
                if(modules.Housing.Residences==null)throw new InvalidOperationException("人口与住宅 / 住宅配置列表为空引用");
                foreach(var entry in modules.Housing.Residences)
                {
                    if(entry==null)throw new InvalidOperationException("住宅配置存在空条目");
                    result.Add(new Rule{Target=-1,Secondary=-1,Kind=RuleKind.Residence,Level=entry.Level,Amount=entry.Capacity,B=entry.InitialResidents,C=entry.StarvationThreshold,Value=entry.GrowthInterval});
                }
                if(modules.Housing.Food==null)throw new InvalidOperationException("人口与住宅 / 居民食谱列表为空引用");
                foreach(var entry in modules.Housing.Food)
                {
                    if(entry==null)throw new InvalidOperationException("居民食谱存在空条目");
                    result.Add(new Rule{Secondary=-1,Kind=RuleKind.Food,Level=entry.Level,Target=Reference(entry.FoodGroup,false,"居民食谱 / 食物组"),Amount=entry.Varieties,B=entry.AmountPerResident});
                }
                if(modules.Housing.Taxes==null)throw new InvalidOperationException("人口与住宅 / 住宅税收列表为空引用");
                foreach(var entry in modules.Housing.Taxes)
                {
                    if(entry==null)throw new InvalidOperationException("住宅税收存在空条目");
                    result.Add(new Rule{Secondary=-1,Kind=RuleKind.Tax,Level=entry.Level,Target=Reference(entry.Item,false,"住宅税收 / 物品"),Amount=entry.PerResident,B=entry.Interval});
                }
                if(modules.Housing.Environment==null)throw new InvalidOperationException("人口与住宅 / 环境需求列表为空引用");
                foreach(var entry in modules.Housing.Environment)
                {
                    if(entry==null)throw new InvalidOperationException("环境需求存在空条目");
                    result.Add(new Rule{Target=-1,Secondary=-1,Kind=RuleKind.Environment,Level=entry.Level,Amount=entry.RequiredValue,B=(int)entry.Type});
                }
            }
            if(modules.Research!=null&&modules.Research.Enabled)
            {
                if(modules.Research.Levels==null)throw new InvalidOperationException("科研 / 科研产出列表为空引用");
                foreach(var entry in modules.Research.Levels)
                {
                    if(entry==null)throw new InvalidOperationException("科研产出存在空条目");
                    result.Add(new Rule{Target=-1,Secondary=-1,Kind=RuleKind.ResearchOutput,Level=entry.Level,Amount=entry.PointsPerTurn});
                }
            }
            if(modules.Farming!=null&&modules.Farming.Enabled)
            {
                if(modules.Farming.Crops==null)throw new InvalidOperationException("种植 / 可种植作物列表为空引用");
                foreach(var entry in modules.Farming.Crops)
                {
                    if(entry==null)throw new InvalidOperationException("可种植作物存在空条目");
                    result.Add(new Rule{Secondary=-1,Kind=RuleKind.Crop,Level=entry.Level,Target=Reference(entry.Crop,false,"可种植作物 / 作物")});
                }
            }
            if(modules.Gathering!=null&&modules.Gathering.Enabled)
            {
                if(modules.Gathering.Levels==null)throw new InvalidOperationException("采集 / 采集配置列表为空引用");
                foreach(var entry in modules.Gathering.Levels)
                {
                    if(entry==null)throw new InvalidOperationException("采集配置存在空条目");
                    result.Add(new Rule{Secondary=-1,Kind=RuleKind.Harvest,Level=entry.Level,Target=Reference(entry.Item,true,"采集配置 / 每次采集物品（空 = 最后一次发放奖励）"),Amount=entry.Uses,B=entry.AmountPerUse});
                }
                if(modules.Gathering.Rewards==null)throw new InvalidOperationException("采集 / 最终采集奖励列表为空引用");
                foreach(var entry in modules.Gathering.Rewards)
                {
                    if(entry==null)throw new InvalidOperationException("最终采集奖励存在空条目");
                    result.Add(new Rule{Secondary=-1,Kind=RuleKind.RewardItem,Level=entry.Level,Target=Reference(entry.Item,false,"最终采集奖励 / 物品"),Amount=entry.Quantity});
                }
            }
            if(modules.Garrison!=null&&modules.Garrison.Enabled)
            {
                if(modules.Garrison.Levels==null)throw new InvalidOperationException("驻军 / 驻军容量列表为空引用");
                foreach(var entry in modules.Garrison.Levels)
                {
                    if(entry==null)throw new InvalidOperationException("驻军容量存在空条目");
                    result.Add(new Rule{Target=-1,Secondary=-1,Kind=RuleKind.Garrison,Level=entry.Level,Amount=entry.Capacity,B=entry.DeploymentBatchSize});
                }
                if(modules.Garrison.InitialUnits==null)throw new InvalidOperationException("驻军 / 开局驻军列表为空引用");
                foreach(var entry in modules.Garrison.InitialUnits)
                {
                    if(entry==null)throw new InvalidOperationException("开局驻军存在空条目");
                    result.Add(new Rule{Secondary=-1,Kind=RuleKind.InitialGarrison,Level=entry.Level,Target=Reference(entry.Soldier,false,"开局驻军 / 兵种"),Amount=entry.Count});
                }
            }
            if(modules.Sanctum!=null&&modules.Sanctum.Enabled)
            {
                if(modules.Sanctum.Levels==null)throw new InvalidOperationException("神殿供奉 / 英雄供奉列表为空引用");
                foreach(var entry in modules.Sanctum.Levels)
                {
                    if(entry==null)throw new InvalidOperationException("英雄供奉存在空条目");
                    result.Add(new Rule{Secondary=-1,Kind=RuleKind.Sanctum,Level=entry.Level,Target=Reference(entry.Hero,false,"英雄供奉 / 英雄"),Amount=entry.RequiredWorkers});
                }
            }
            if(modules.Market!=null&&modules.Market.Enabled)
            {
                if(modules.Market.Levels==null)throw new InvalidOperationException("市场 / 市场收入列表为空引用");
                foreach(var entry in modules.Market.Levels)
                {
                    if(entry==null)throw new InvalidOperationException("市场收入存在空条目");
                    result.Add(new Rule{Secondary=-1,Kind=RuleKind.Market,Level=entry.Level,Target=Reference(entry.Currency,false,"市场收入 / 收入货币"),Amount=entry.ValuePerMarketPoint,Value=entry.IncomeRatio});
                }
            }
            if(modules.Effects!=null&&modules.Effects.Enabled)
            {
                if(modules.Effects.Spatial==null)throw new InvalidOperationException("范围效果 / 范围效果列表为空引用");
                foreach(var entry in modules.Effects.Spatial)
                {
                    if(entry==null)throw new InvalidOperationException("范围效果存在空条目");
                    result.Add(new Rule{Secondary=-1,Kind=RuleKind.SpatialEffect,Level=entry.Level,Target=Reference(entry.Building,true,"范围效果 / 指定建筑（空 = 所有建筑）"),Amount=entry.Magnitude,B=(int)entry.Type,C=entry.RequiredWorkers,Value=entry.Radius,Extra=(float)entry.Stacking,Key=new Unity.Collections.FixedString64Bytes(entry.Group??"")});
                }
            }
            if(modules.Defence!=null&&modules.Defence.Enabled)
            {
                if(modules.Defence.Intelligence==null)throw new InvalidOperationException("情报与警铃 / 情报产出列表为空引用");
                foreach(var entry in modules.Defence.Intelligence)
                {
                    if(entry==null)throw new InvalidOperationException("情报产出存在空条目");
                    result.Add(new Rule{Secondary=-1,Kind=RuleKind.Intelligence,Level=entry.Level,Target=Reference(entry.Technology,true,"情报产出 / 所需科技（可选）"),Amount=entry.Points,B=entry.RequiredWorkers});
                }
                if(modules.Defence.Bells==null)throw new InvalidOperationException("情报与警铃 / 警铃集结列表为空引用");
                foreach(var entry in modules.Defence.Bells)
                {
                    if(entry==null)throw new InvalidOperationException("警铃集结存在空条目");
                    result.Add(new Rule{Target=-1,Secondary=-1,Kind=RuleKind.Bell,Level=entry.Level,Value=entry.Radius});
                }
            }
            if(modules.Expeditions!=null&&modules.Expeditions.Enabled)
            {
                if(modules.Expeditions.Levels==null)throw new InvalidOperationException("远征 / 远征所列表为空引用");
                foreach(var entry in modules.Expeditions.Levels)
                {
                    if(entry==null)throw new InvalidOperationException("远征所存在空条目");
                    result.Add(new Rule{Target=-1,Secondary=-1,Kind=RuleKind.ExpeditionSite,Level=entry.Level,Amount=entry.MinimumCrew,B=entry.MaximumCrew,Value=entry.FullCrewRewardBonus});
                }
            }
            return result.ToArray();
        }
    }
}
