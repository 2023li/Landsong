using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingCatalogValidation
    {
        public static void Validate(BuildingCatalogAsset catalog)
        {
            foreach (var asset in catalog.Definitions)
                Validate(asset);
        }

        public static void Validate(BuildingDefinitionAsset source)
        {
            void Fail(string message) => throw new InvalidOperationException(source.Metadata.Id + "：" + message);
            if (source.MaximumLevel < 1 || source.ConstructionTurns < 1 || source.MaximumCount < 0)
                Fail("建筑最高等级、施工回合或数量限制无效。");
            if (source.Capabilities == null || source.PlacementAndVisuals == null)
                Fail("缺少建筑功能或占位配置。");
            if (source.PlacementAndVisuals.SpawnExclusionPadding < 0 || source.PlacementAndVisuals.SpawnExclusionPadding > 256)
                Fail("隐性占位扩展必须在零到256格之间。");
            if (source.Capabilities.Connection.Enabled && (source.Footprint.x < 1 || source.Footprint.x > 32 || source.Footprint.y < 3 || source.Footprint.y > 128))
                Fail("通行建筑宽度须为1到32格，跨度须为3到128格。");
            // This traversal validates only this building's owned value records. Domain references are resolved by the explicit typed compilers.
            void Visit(object value, string path)
            {
                if (value == null)
                    Fail(path + "存在空配置。");
                var type = value.GetType();
                var enabled = type.GetField("Enabled");
                if (enabled != null && !(bool)enabled.GetValue(value))
                    return;
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    var item = field.GetValue(value);
                    var at = path + "/" + (field.GetCustomAttribute<LabelTextAttribute>()?.Text ?? field.Name);
                    if (typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))
                        continue;
                    if (item is TerrainType terrain)
                    {
                        if (!TerrainTypes.Valid(terrain))
                            Fail(at + "包含未知地形位。");
                    }
                    else if (field.FieldType.IsEnum)
                    {
                        if (!Enum.IsDefined(field.FieldType, item))
                            Fail(at + "枚举无效。");
                    }
                    else if (item is int integer)
                    {
                        if (integer < 0 || integer > 1000000)
                            Fail(at + "必须在零到一百万之间。");
                        var minimum = field.GetCustomAttribute<MinValueAttribute>();
                        if (minimum != null && integer < minimum.MinValue)
                            Fail(at + "低于允许下限。");
                        if ((field.Name == "Level" || field.Name == "TargetLevel") && integer > source.MaximumLevel)
                            Fail(at + "超过建筑最高等级。");
                        if (field.Name == "Stage" && integer > source.ConstructionTurns)
                            Fail(at + "超过施工回合。");
                    }
                    else if (item is float number)
                    {
                        if (float.IsNaN(number) || float.IsInfinity(number) || Math.Abs(number) > 1000000)
                            Fail(at + "必须是允许范围内的有限数值。");
                        var minimum = field.GetCustomAttribute<MinValueAttribute>();
                        if (minimum != null && number < minimum.MinValue)
                            Fail(at + "低于允许下限。");
                        var range = field.GetCustomAttribute<RangeAttribute>();
                        if (range != null && (number < range.min || number > range.max))
                            Fail(at + "超过允许范围。");
                    }
                    else if (field.FieldType == typeof(string))
                    {
                        if (System.Text.Encoding.UTF8.GetByteCount((string)item ?? "") > 60)
                            Fail(at + "过长。");
                    }
                    else if (item is Array array)
                    {
                        foreach (var row in array)
                            Visit(row, at);
                    }
                    else if (field.FieldType.IsClass)
                        Visit(item, at);
                }
            }

            Visit(source.Capabilities, "建筑功能");
            var modules = source.Capabilities;
            for (int level = 1; level <= source.MaximumLevel; level++)
            {
                bool Active(int at) => at == 0 || at == level;
                void Unique(int count, string name)
                {
                    if (count > 1)
                        Fail("等级" + level + "的" + name + "重复，通用项与指定等级项不能重叠。");
                }

                var workforce = modules.Workforce.Enabled ? modules.Workforce.Levels.Where(x => Active(x.Level)).ToArray() : Array.Empty<BuildingWorkforceLevelSource>();
                var residents = modules.Housing.Enabled ? modules.Housing.Residences.Where(x => Active(x.Level)).ToArray() : Array.Empty<BuildingResidenceLevelSource>();
                var garrisons = modules.Garrison.Enabled ? modules.Garrison.Levels.Where(x => Active(x.Level)).ToArray() : Array.Empty<BuildingGarrisonLevelSource>();
                Unique(workforce.Length, "岗位");
                Unique(residents.Length, "住宅");
                Unique(garrisons.Length, "驻军");
                if (modules.Upgrade.Enabled)
                    Unique(modules.Upgrade.Experience.Count(x => Active(x.Level)), "经验");
                if (modules.Production.Enabled)
                    Unique(modules.Production.Cycles.Count(x => Active(x.Level)), "生产周期");
                if (modules.Market.Enabled)
                    Unique(modules.Market.Levels.Count(x => Active(x.Level)), "市场");
                if (modules.Sanctum.Enabled)
                    Unique(modules.Sanctum.Levels.Count(x => Active(x.Level)), "神殿");
                if (modules.Expeditions.Enabled)
                    Unique(modules.Expeditions.Levels.Count(x => Active(x.Level)), "远征所");
                if (modules.Storage.Enabled)
                    Unique(modules.Storage.Conditions.Count(x => Active(x.Level)), "仓储状态");
                if (residents.Length != 0 && residents[0].InitialResidents > residents[0].Capacity)
                    Fail("初始居民超过住宅容量。");
                if (modules.Garrison.Enabled)
                {
                    long initial = modules.Garrison.InitialUnits.Where(x => Active(x.Level)).Sum(x => (long)x.Count);
                    if (initial > (garrisons.Length == 0 ? 0 : garrisons[0].Capacity))
                        Fail("初始驻军总数超过容量。");
                    if (garrisons.Length != 0 && (garrisons[0].DeploymentBatchSize < 1 || garrisons[0].Capacity > 1000))
                        Fail("驻军每批出勤须至少一人，容量不得超过一千。");
                }

                if (modules.Production.Enabled)
                {
                    foreach (var cycle in modules.Production.Cycles.Where(x => Active(x.Level)))
                        if (cycle.Interval < 1)
                            Fail("生产间隔须至少一回合。");
                    foreach (var cycle in modules.Production.ProcessingTiers.Where(x => Active(x.Level)))
                        if (cycle.Interval < 1)
                            Fail("加工间隔须至少一回合。");
                    foreach (var output in modules.Production.Outputs.Where(x => Active(x.Level)))
                        if (output.MaximumWorkers > 0 && output.MaximumWorkers < output.MinimumWorkers)
                            Fail("生产档位最多工人不能少于最少工人。");
                }

                if (modules.Quests.Enabled)
                    foreach (var invitation in modules.Quests.Invitations.Where(x => Active(x.Level)))
                        if (invitation.MinimumRefreshTurns < 1 || invitation.MaximumRefreshTurns < invitation.MinimumRefreshTurns || invitation.Slots > 100)
                            Fail("邀约数量或刷新区间无效。");
                if (modules.Expeditions.Enabled)
                    foreach (var expedition in modules.Expeditions.Levels.Where(x => Active(x.Level)))
                        if (expedition.MinimumCrew < 1 || expedition.MaximumCrew < expedition.MinimumCrew || expedition.FullCrewRewardBonus < 0)
                            Fail("驻地远征人数或奖励无效。");
                if (modules.Farming.Enabled && workforce.Length == 0)
                    Fail("种植模块需要岗位配置。");
                if (workforce.Length == 0)
                    continue;
                int workers = workforce[0].Capacity;
                if (workforce[0].InitialWorkers > workers || workforce[0].BaseAttraction < 0 || workforce[0].RecruitmentCost < 0)
                    Fail("岗位初始工人数、吸引力或招聘费用无效。");
                var tiers = modules.Workforce.EfficiencyTiers.Where(x => Active(x.Level)).OrderBy(x => x.MinimumWorkers).ToArray();
                int next = 0;
                foreach (var tier in tiers)
                {
                    if (tier.MinimumWorkers != next || tier.MaximumWorkers < tier.MinimumWorkers || tier.MaximumWorkers > workers)
                        Fail("效率档位必须连续、不重叠且不超过岗位容量。");
                    next = tier.MaximumWorkers + 1;
                }

                if (tiers.Length == 0 || next != workers + 1)
                    Fail("效率档位必须显式覆盖零到岗位容量的所有人数。");
                if (modules.Farming.Enabled && (modules.Farming.RequiredWorkers < 1 || modules.Farming.FullCycleBonusWorkers < modules.Farming.RequiredWorkers || modules.Farming.FullCycleBonusWorkers > workers || modules.Farming.FullCycleYieldBonusPercent < 0))
                    Fail("种植工人门槛或收获加成超出岗位配置。");
                void Boundary(int threshold)
                {
                    if (threshold > 0 && threshold <= workers && tiers.Any(x => x.MinimumWorkers < threshold && x.MaximumWorkers >= threshold))
                        Fail("效果阈值" + threshold + "人必须是效率档位的边界。");
                }

                if (modules.Production.Enabled)
                {
                    foreach (var row in modules.Production.Cycles.Where(x => Active(x.Level)))
                        Boundary(row.RequiredWorkers);
                    foreach (var row in modules.Production.ProcessingTiers.Where(x => Active(x.Level)))
                        Boundary(row.RequiredWorkers);
                    foreach (var row in modules.Production.Outputs.Where(x => Active(x.Level)))
                    {
                        Boundary(row.MinimumWorkers);
                        if (row.MaximumWorkers > 0)
                            Boundary(row.MaximumWorkers + 1);
                    }

                    foreach (var row in modules.Production.RareOutputs.Where(x => Active(x.Level)))
                        Boundary(row.RequiredWorkers);
                }

                if (modules.Storage.Enabled)
                {
                    foreach (var row in modules.Storage.Warehouses.Where(x => Active(x.Level)))
                        Boundary(row.RequiredWorkers);
                    foreach (var row in modules.Storage.Conditions.Where(x => Active(x.Level)))
                        Boundary(row.RequiredWorkers);
                    if (modules.Storage.Providers.Any(x => Active(x.Level)))
                        Boundary(1);
                }

                if (modules.Effects.Enabled)
                    foreach (var row in modules.Effects.Spatial.Where(x => Active(x.Level)))
                        Boundary(row.RequiredWorkers);
                if (modules.Upgrade.Enabled)
                    foreach (var row in modules.Upgrade.Experience.Where(x => Active(x.Level)))
                        Boundary(row.RequiredWorkers);
                if (modules.Sanctum.Enabled)
                    foreach (var row in modules.Sanctum.Levels.Where(x => Active(x.Level)))
                        Boundary(row.RequiredWorkers);
                if (modules.Quests.Enabled && modules.Quests.Capacity.Any(x => Active(x.Level)) || modules.Market.Enabled && modules.Market.Levels.Any(x => Active(x.Level)))
                    Boundary(workers);
                if (modules.Expeditions.Enabled)
                    foreach (var row in modules.Expeditions.Levels.Where(x => Active(x.Level)))
                        for (int n = 1; n <= Math.Min(workers, row.MaximumCrew); n++)
                            Boundary(n);
                if (modules.Farming.Enabled)
                {
                    Boundary(modules.Farming.RequiredWorkers);
                    Boundary(modules.Farming.FullCycleBonusWorkers);
                }
            }
        }
    }
}
