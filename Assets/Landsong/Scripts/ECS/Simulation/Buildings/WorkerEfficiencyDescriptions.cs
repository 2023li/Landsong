using System.Collections.Generic;
using System.Linq;
using Landsong.ECS.Definitions;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class WorkerEfficiencyOps
    {
        // Reads authored thresholds without settlement or random draws.
        public static string Describe(EntityManager em, Entity root, Entity entity, string module = "全部")
        {
            if (!em.Exists(entity) || !em.HasComponent<Building>(entity))
                return "建筑已不存在";
            var building = em.GetComponentData<Building>(entity);
            var workersState = em.GetComponentData<BuildingWorkforceState>(entity);
            var farming = em.GetComponentData<BuildingFarmingState>(entity);
            var workforce = em.GetComponentData<BuildingWorkforceStats>(entity);
            var storageStats = em.GetComponentData<BuildingStorageStats>(entity);
            var quests = em.GetComponentData<BuildingQuestStats>(entity);
            var sanctum = em.GetComponentData<BuildingSanctumStats>(entity);
            var identity = em.GetComponentData<Identity>(entity);
            var buildingId = em.GetComponentData<BuildingDefinitionRef>(entity).Definition;
            ref var definition = ref BuildingDefinitions.Get(em, root, buildingId);
            ref var capabilities = ref definition.Capabilities;
            bool Level(int level) => level == 0 || level == building.Level;
            bool Includes(string name) => module == "全部" || module == name;
            string ItemName(ItemId item) => ItemDefinitions.IsValid(em, root, item) ? ItemDefinitions.Get(em, root, item).Metadata.Name.ToString() : "通用";
            var crops = farming.Crop.IsValid ? new[]
            {
                farming.Crop
            }

            : Copy(ref capabilities.Farming.Crops).Where(row => Level(row.Level)).Select(row => row.Crop).Distinct().ToArray();
            var cycles = Copy(ref capabilities.Production.Cycles).Where(row => row.Level <= building.Level).OrderBy(row => row.Level).ToArray();
            var processing = Copy(ref capabilities.Production.ProcessingTiers).Where(row => Level(row.Level)).ToArray();
            var products = Copy(ref capabilities.Production.Outputs).Where(row => Level(row.Level)).ToArray();
            var rare = Copy(ref capabilities.Production.RareOutputs).Where(row => Level(row.Level)).ToArray();
            var warehouses = Copy(ref capabilities.Storage.Warehouses).Where(row => Level(row.Level)).ToArray();
            var spatial = Copy(ref capabilities.Effects.Spatial).Where(row => Level(row.Level)).ToArray();
            var experience = Copy(ref capabilities.Upgrade.Experience).Where(row => Level(row.Level)).ToArray();
            var expeditions = Copy(ref capabilities.Expeditions.Levels).Where(row => Level(row.Level)).ToArray();
            var storage = Copy(ref capabilities.Storage.Conditions).Where(row => row.Level <= building.Level).OrderBy(row => row.Level).ToArray();
            bool hasMarket = capabilities.Market.Enabled;
            int capacity = math.max(0, workforce.Capacity);
            var tiers = Tiers(em, root, buildingId, building.Level);
            if (tiers.Count == 0)
                return "尚未配置工作效率档位，请在建筑的岗位与补贴模块中配置。";
            var rows = new List<string>
            {
                $"{identity.Name} · {module}工人档位",
                $"当前 {workersState.Workers}/{capacity} 工人",
                "按当前等级的基础配置说明；实际结算还需满足运营、维护、原料、连接及库存条件，加成另计。"
            };
            foreach (var tier in tiers)
            {
                int workers = tier.MinimumWorkers, end = tier.MaximumWorkers;
                var effects = new List<string>();
                if (Includes("种植") && capabilities.Farming.Enabled && crops.Length > 0)
                {
                    ref var rules = ref capabilities.Farming;
                    if (workers < rules.RequiredWorkers)
                        effects.Add($"作物生长暂停（至少需要 {rules.RequiredWorkers} 工人）");
                    else if (rules.FullCycleYieldBonusPercent > 0 && workers >= rules.FullCycleBonusWorkers)
                        effects.Add($"作物产量 +{rules.FullCycleYieldBonusPercent}%（全生长期保持至少 {rules.FullCycleBonusWorkers} 工人）");
                    else
                        effects.Add("作物正常生长");
                }

                if (Includes("生产"))
                {
                    if (cycles.Length > 0)
                    {
                        var cycle = cycles[cycles.Length - 1];
                        foreach (var rate in processing)
                            if (workers >= rate.RequiredWorkers)
                                cycle.Interval = rate.Interval;
                        var outputs = products.Where(row => workers >= row.MinimumWorkers && (row.MaximumWorkers <= 0 || workers <= row.MaximumWorkers)).Select(row => $"{ItemName(row.Item)} ×{row.Quantity}").ToArray();
                        effects.Add(workers < cycle.RequiredWorkers ? "生产暂停" : $"每 {math.max(1, cycle.Interval)} 次白天结算：" + (outputs.Length > 0 ? string.Join("、", outputs) : "此人数无配置产品"));
                    }

                    foreach (var output in rare)
                        effects.Add(workers < output.RequiredWorkers ? $"{ItemName(output.Item)} 随机产出未启用" : $"每次结算 {output.Probability:P0} 概率产出 {ItemName(output.Item)} ×{output.Quantity}");
                }

                if (Includes("仓储"))
                {
                    foreach (var warehouse in warehouses)
                    {
                        string name = StorageSlotDefinitions.IsValid(em, root, warehouse.SlotType) ? StorageSlotDefinitions.Get(em, root, warehouse.SlotType).Metadata.Name.ToString() : "通用";
                        effects.Add($"{name} 库存槽 ×{(workers >= warehouse.RequiredWorkers ? warehouse.Slots : 0)}");
                    }

                    if (storage.Length > 0)
                    {
                        var condition = storage[storage.Length - 1];
                        effects.Add($"工人数造成的库存损耗倍率 ×{(workers < condition.RequiredWorkers ? condition.UnderstaffedLossMultiplier : 1):0.##}（维护及其他修正另计）");
                    }
                }

                if (Includes("空间效果"))
                    foreach (var effect in spatial)
                        effects.Add($"{EnvironmentName(effect.Type)} +{(workers >= effect.RequiredWorkers ? effect.Magnitude : 0)}（范围 {effect.Radius} 格）");
                if (Includes("经验"))
                    foreach (var gain in experience)
                        effects.Add($"每次结算经验 +{(workers >= gain.RequiredWorkers ? gain.ExperiencePerTurn : 0)}（需维护正常，住宅还需住满）");
                if (Includes("远征"))
                    foreach (var site in expeditions)
                        effects.Add($"出征人数上限 {math.min(workers, site.MaximumCrew)}，还受稳定人数和所选远征要求限制");
                if (Includes("神殿") && sanctum.Hero.IsValid)
                    effects.Add(workers >= sanctum.RequiredWorkers ? "满足英雄招募、唤醒及供奉的工人数要求" : $"英雄招募、唤醒及供奉缺工（需 {sanctum.RequiredWorkers} 人）");
                if (Includes("任务") && quests.Capacity > 0)
                    effects.Add($"任务槽位 {(workers >= capacity ? quests.Capacity : 0)}（需满岗且维护正常）");
                if (Includes("邀约") && hasMarket && em.HasBuffer<QuestOfferSlot>(entity) && em.GetBuffer<QuestOfferSlot>(entity).Length > 0)
                    effects.Add(workers >= capacity ? "满足市场邀约冷却的满岗要求" : "市场邀约冷却因缺工暂停");
                if (Includes("供给") && storageStats.IsProvider != 0)
                    effects.Add(capacity == 0 || workers > 0 ? "满足资源提供点的工人数要求（还需运营和维护正常）" : "资源提供点缺工失效");
                if (effects.Count == 0)
                    effects.Add("此模块无工人数加成");
                string unit = module == "种植" ? "工人" : "人口";
                rows.Add((end == workers ? $"{workers}{unit}" : $"{workers}～{end}{unit}") + (workersState.Workers >= workers && workersState.Workers <= end ? "（当前）" : "") + "：\n" + string.Join("\n", effects));
            }

            if (Includes("种植"))
                foreach (var cropId in crops)
                {
                    if (!CropDefinitions.IsValid(em, root, cropId))
                        continue;
                    ref var crop = ref CropDefinitions.Get(em, root, cropId);
                    var rewards = new List<string>();
                    for (int i = 0; i < crop.HarvestOutputs.Length; i++)
                    {
                        var output = crop.HarvestOutputs[i];
                        rewards.Add($"{ItemName(output.Item)} {output.MinimumQuantity}～{math.max(output.MinimumQuantity, output.MaximumQuantity)}");
                    }

                    rows.Add($"{crop.Metadata.Name}：成熟 {crop.GrowthTurns} 回合；基础收获 {string.Join("、", rewards)}");
                }
            if (Includes("种植") && farming.Crop.IsValid && farming.FullCycle == 0 && capabilities.Farming.FullCycleYieldBonusPercent > 0)
                rows.Add("本季曾缺少奖励所需工人，全周期奖励已失效；现在补人不会补回本季奖励。");
            if (Includes("种植") && !farming.Crop.IsValid && crops.Length > 0)
                rows.Add("尚未种植，以上为农田工人规则和本等级可选作物。");
            return string.Join("\n\n", rows);
        }

        public static List<BuildingWorkerEfficiencyTier> Tiers(EntityManager em, Entity root, BuildingId definition, int level)
        {
            ref var building = ref BuildingDefinitions.Get(em, root, definition);
            return Copy(ref building.Capabilities.Workforce.EfficiencyTiers).Where(tier => tier.Level == 0 || tier.Level == level).OrderBy(tier => tier.MinimumWorkers).ToList();
        }

        static T[] Copy<T>(ref BlobArray<T> source)
            where T : unmanaged
        {
            var values = new T[source.Length];
            for (int i = 0; i < source.Length; i++)
                values[i] = source[i];
            return values;
        }

        static string EnvironmentName(BuildingEnvironmentKind kind) => kind == BuildingEnvironmentKind.Beauty ? "美观" : kind == BuildingEnvironmentKind.Medical ? "医疗" : kind == BuildingEnvironmentKind.Security ? "治安" : kind == BuildingEnvironmentKind.Production ? "生产/作物收益百分比" : "效果 " + kind;
    }
}
