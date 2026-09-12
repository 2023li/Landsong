using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class WorkerEfficiencyOps
    {
        // Read configuration only: never run a settlement or draw random rewards for a tooltip.
        public static string Describe(EntityManager em, Entity root, Entity entity, string module = "全部")
        {
            if (!em.Exists(entity) || !em.HasComponent<Building>(entity)) return "建筑已不存在";
            var b = em.GetComponentData<Building>(entity); var stats = em.GetComponentData<BuildingStats>(entity);
            var id = em.GetComponentData<Identity>(entity); var d = Sim.Definition(em, root, id.Definition);
            var rules = new List<Rule>();
            for (int i = 0; i < d.RuleCount; i++) { var r = Sim.GetRule(em, root, d.RuleStart + i); if (r.Level == 0 || r.Level == b.Level) rules.Add(r); }
            string Name(int definition) => Sim.ValidDefinition(em, root, definition) ? Sim.Definition(em, root, definition).Name.ToString() : "通用";
            bool Includes(string name) => module == "全部" || module == name;
            var crops = b.Crop >= 0 ? new[] { b.Crop } : rules.Where(r => r.Kind == RuleKind.Crop).Select(r => r.Target).Distinct().ToArray();
            int capacity = math.max(0, stats.JobCapacity);
            var tiers = Tiers(em, root, id.Definition, b.Level);
            if (tiers.Count == 0) return "尚未配置工作效率档位，请在建筑的岗位与补贴模块中配置。";
            var rows = new List<string> { $"{id.Name} · {module}工人档位", $"当前 {b.Workers}/{capacity} 人口（在岗工人）", "按当前等级的基础配置说明；实际结算还需满足运营、维护、原料、连接及库存条件，加成另计。" };
            foreach (var tier in tiers)
            {
                int workers = tier.MinimumWorkers, end = tier.MaximumWorkers; var effects = new List<string>();
                if (Includes("种植")) foreach (var c in crops)
                {
                    if (!Sim.ValidDefinition(em, root, c)) continue;
                    var crop = Sim.Definition(em, root, c);
                    string growth = workers >= crop.Population ? $"每次白天结算生长 1/{crop.Duration} 周期" : "生长暂停";
                    string bonus = workers >= crop.Capacity ? $"全生长期保持此人数，收获 +{crop.Value:0.#}%" : "不能保持全周期人数奖励";
                    var rewards = new List<string>();
                    for (int i = 0; i < crop.RuleCount; i++) { var r = Sim.GetRule(em, root, crop.RuleStart + i); if (r.Kind == RuleKind.RewardItem) rewards.Add($"{Name(r.Target)} {r.Amount}～{math.max(r.Amount, r.B)}"); }
                    effects.Add($"{crop.Name}：{growth}；{bonus}。基础收获 {string.Join("、", rewards)}");
                }
                if (Includes("生产"))
                {
                    var production = Sim.Rule(em, root, id.Definition, RuleKind.Production, b.Level);
                    foreach (var r in rules) if (r.Kind == RuleKind.ProcessingTier && workers >= r.B) production.Amount = r.Amount;
                    if (production.Level >= 0)
                    {
                        var products = rules.Where(r => r.Kind == RuleKind.ProductionTier && workers >= r.B && (r.C <= 0 || workers <= r.C)).Select(r => $"{Name(r.Target)} ×{r.Amount}");
                        effects.Add(workers < production.B ? "生产暂停" : $"每 {math.max(1, production.Amount)} 次白天结算：" + (products.Any() ? string.Join("、", products) : "此人数无配置产品"));
                    }
                    foreach (var r in rules) if (r.Kind == RuleKind.RareOutput) effects.Add(workers < r.B ? $"{Name(r.Target)} 随机产出未启用" : $"每次结算 {r.Value:P0} 概率产出 {Name(r.Target)} ×{r.Amount}");
                }
                foreach (var r in rules)
                {
                    if (Includes("仓储") && r.Kind == RuleKind.Warehouse) effects.Add($"{Name(r.Target)} 库存槽 ×{(workers >= r.B ? r.Amount : 0)}");
                    if (Includes("空间效果") && r.Kind == RuleKind.SpatialEffect) effects.Add($"{EnvironmentName(r.B)} +{(workers >= r.C ? r.Amount : 0)}（范围 {r.Value} 格）");
                    if (Includes("经验") && r.Kind == RuleKind.Experience) effects.Add($"每次结算经验 +{(workers >= r.C ? r.Amount : 0)}（需维护正常，住宅还需住满）");
                    if (Includes("远征") && r.Kind == RuleKind.ExpeditionSite) effects.Add($"出征人数上限 {math.min(workers, r.B)}，还受稳定人数和所选远征要求限制");
                }
                var storage = Sim.Rule(em, root, id.Definition, RuleKind.StorageCondition, b.Level);
                if (Includes("仓储") && storage.Level >= 0) effects.Add($"工人数造成的库存损耗倍率 ×{(workers < storage.Amount ? storage.Extra : 1):0.##}（维护及其他修正另计）");
                if (Includes("神殿") && stats.HeroDefinition >= 0) effects.Add(workers >= stats.RequiredWorkers ? "满足英雄招募、唤醒及供奉的工人数要求" : $"英雄招募、唤醒及供奉缺工（需 {stats.RequiredWorkers} 人）");
                if (Includes("任务") && stats.QuestCapacity > 0) effects.Add($"任务槽位 {(workers >= capacity ? stats.QuestCapacity : 0)}（需满岗且维护正常）");
                if (Includes("邀约") && rules.Any(r=>r.Kind==RuleKind.Market) && em.HasBuffer<QuestOfferSlot>(entity) && em.GetBuffer<QuestOfferSlot>(entity).Length>0) effects.Add(workers>=capacity?"满足市场邀约冷却的满岗要求":"市场邀约冷却因缺工暂停");
                if (Includes("供给") && stats.IsProvider != 0) effects.Add(capacity==0||workers>0?"满足资源提供点的工人数要求（还需运营和维护正常）":"资源提供点缺工失效");
                if (effects.Count == 0) effects.Add("此模块无工人数加成");
                rows.Add((end == workers ? $"{workers}人口" : $"{workers}～{end}人口") + (b.Workers >= workers && b.Workers <= end ? "（当前）" : "") + "：\n" + string.Join("\n", effects));
            }
            if (Includes("种植") && b.Crop >= 0 && b.CropFullCycle == 0) rows.Add("本季曾缺少奖励所需工人，全周期奖励已失效；现在补人不会补回本季奖励。");
            if (Includes("种植") && b.Crop < 0 && crops.Length > 0) rows.Add("尚未种植，以上列出本等级可选作物的工人要求。");
            return string.Join("\n\n", rows);
        }
        public static List<WorkerEfficiencyTier> Tiers(EntityManager em, Entity root, int definition, int level)
        {
            var result = new List<WorkerEfficiencyTier>(); var catalog = em.GetComponentData<ContentCatalog>(root).Value;
            for (int i = 0; i < catalog.Value.WorkerTiers.Length; i++)
            {
                var tier = catalog.Value.WorkerTiers[i];
                if (tier.Definition == definition && (tier.Level == 0 || tier.Level == level)) result.Add(tier);
            }
            result.Sort((a,b)=>a.MinimumWorkers.CompareTo(b.MinimumWorkers));return result;
        }
        static string EnvironmentName(int kind) => kind == 20 ? "美观" : kind == 30 ? "医疗" : kind == 40 ? "治安" : kind == 10 ? "生产/作物收益百分比" : "效果 " + kind;
    }
}
