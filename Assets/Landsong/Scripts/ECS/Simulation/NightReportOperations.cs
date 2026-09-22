using Landsong.ECS.Definitions;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public enum NightEndPose : byte
    {
        None,
        Celebrate,
        Guard,
        Aid
    }

    [InternalBufferCapacity(0)]
    public struct BattleHistoryEntry : IBufferElementData
    {
        public int Turn;
        public BattleReportEntry Entry;
    }

    public static class NightReportOps
    {
        public static void Put(EntityManager em, Entity root, BattleReportEntry entry)
        {
            var report = em.GetBuffer<BattleReportEntry>(root);
            for (int i = 0; i < report.Length; i++)
                if (report[i].Kind == entry.Kind && report[i].Id == entry.Id && report[i].Item == entry.Item && report[i].Building == entry.Building)
                {
                    report[i] = entry;
                    return;
                }

            report.Add(entry);
        }

        public static NightEndPose Prepare(EntityManager em, Entity root)
        {
            var s = em.GetComponentData<Session>(root);
            var plan = NightPlanOps.State(em, root);
            int lost = 0, survivors = 0;
            bool damage = false;
            foreach (var row in em.GetBuffer<BattleReportEntry>(root))
            {
                if (row.Kind == EventKind.Ruin || row.Kind == EventKind.ResidentsLost)
                    damage = true;
                if ((row.Kind == EventKind.SoldierDeath || row.Kind == EventKind.HeroDeath))
                    lost += row.Amount;
            }

            using (var units = WorldQueries.OrderedEntities<Combatant>(em))
                foreach (var e in units)
                    if (em.GetComponentData<Combatant>(e).Faction == 0 && EntityState.Alive(em, e))
                        survivors++;
            var pose = damage || lost > 0 && lost * 4 >= lost + survivors ? NightEndPose.Aid : s.Phase == Phase.Retreat || plan.BossEscaped != 0 ? NightEndPose.Guard : NightEndPose.Celebrate;
            Put(em, root, new BattleReportEntry { Kind = EventKind.NightClosure, Amount = (int)pose });
            if (plan.BossKilled != 0 || plan.BossEscaped != 0)
                Put(em, root, new BattleReportEntry { Kind = plan.BossKilled != 0 ? EventKind.BossKilled : EventKind.BossRetreated, Amount = 1, SourceName = EnemyDefinitions.Get(em, root, plan.BossDefinition).Metadata.Name });
            HeroTimes(em, root);
            return pose;
        }

        public static void HeroTimes(EntityManager em, Entity root)
        {
            using var heroes = WorldQueries.OrderedEntities<HeroCombat>(em);
            foreach (var e in heroes)
            {
                if (em.GetComponentData<Combatant>(e).Deployed == 0)
                    continue;
                var id = em.GetComponentData<Identity>(e);
                var h = em.GetComponentData<HeroCombat>(e);
                Put(em, root, new BattleReportEntry { Kind = EventKind.HeroEffectiveTime, Id = id.Id, Value = h.EffectiveSeconds, SourceName = id.Name });
            }
        }

        public static void Archive(EntityManager em, Entity root)
        {
            EntityState.Buffer<BattleHistoryEntry>(em, root);
            var history = em.GetBuffer<BattleHistoryEntry>(root);
            int turn = em.GetComponentData<GameClock>(root).Turn;
            foreach (var h in history)
                if (h.Turn == turn)
                    return;
            foreach (var row in em.GetBuffer<BattleReportEntry>(root))
                history.Add(new BattleHistoryEntry { Turn = turn, Entry = row });
            // Keep complete turns, never a misleading partial report. Full long-term history UI is wave 14.
            while (history.Length > 2048 && history[0].Turn != turn)
            {
                int oldest = history[0].Turn;
                while (history.Length > 0 && history[0].Turn == oldest)
                    history.RemoveAt(0);
            }
        }

        public static List<string> Lines(EntityManager em, Entity root, IEnumerable<BattleReportEntry> entries)
        {
            var rows = new List<BattleReportEntry>(entries);
            var lines = new List<string>();
            int soldiers = 0, residents = 0, jobs = 0, ruins = 0, heroes = 0, enemies = 0;
            foreach (var e in rows)
            {
                if (e.Kind == EventKind.ResidentsLost)
                    residents += e.Amount;
                if (e.Kind == EventKind.JobsLost)
                    jobs += e.Amount;
                if (e.Kind == EventKind.Ruin)
                    ruins += e.Amount;
                if (e.Kind == EventKind.SoldierDeath)
                    soldiers += e.Amount;
                if (e.Kind == EventKind.HeroDeath)
                    heroes += e.Amount;
                if (e.Kind == EventKind.EnemyDeath)
                    enemies += e.Amount;
            }

            lines.Add($"战损：士兵阵亡 {soldiers} · 英雄陨落 {heroes} · 居民死亡 {residents} · 工人失业 {jobs} · 建筑荒废 {ruins} · 击杀敌军 {enemies}");
            string ItemName(ItemId item) => ItemDefinitions.IsValid(em, root, item) ? ItemDefinitions.Get(em, root, item).Metadata.Name.ToString() : "";
            string Name(BattleReportEntry entry) => entry.Item.IsValid ? ItemName(entry.Item) : entry.Building.IsValid ? BuildingDefinitions.Get(em, root, entry.Building).Metadata.Name.ToString() : entry.SourceName.ToString();
            foreach (var e in rows)
            {
                string name = Name(e), source = e.SourceName.ToString();
                if (source == name)
                    source = "";
                switch (e.Kind)
                {
                    case EventKind.NightClosure:
                        lines.Add(e.Amount == (int)NightEndPose.Aid ? "收尾：救助与整备" : e.Amount == (int)NightEndPose.Guard ? "收尾：警戒，敌军已撤离" : "收尾：欢呼庆祝");
                        break;
                    case EventKind.BossKilled:
                        lines.Add(name + "已击杀，额外战利品纳入本夜收益。");
                        break;
                    case EventKind.BossRetreated:
                        lines.Add(name + "已撤离并形成后续事件；再次来袭时恢复生命。");
                        break;
                    case EventKind.HeroEffectiveTime:
                        lines.Add($"{e.SourceName} · 有效参战 {e.Value:0.0} 秒");
                        break;
                    case EventKind.TheftPrevented:
                        lines.Add(e.SourceName + "附近：阻止盗窃，损失 0");
                        break;
                    case EventKind.FairyCaught:
                        lines.Add(e.SourceName + "附近：接到小精灵礼物");
                        break;
                    case EventKind.VisitorEscaped:
                        lines.Add(e.SourceName + "附近：" + name + "离开" + (e.Amount == 0 ? "，损失 0" : "，被盗明细见下方"));
                        break;
                    case EventKind.VisitorCancelled:
                        lines.Add(e.SourceName + "附近：互动取消，损失 0");
                        break;
                    default:
                        lines.Add($"{Label(e.Kind)}　{name} × {e.Amount}　{source}");
                        break;
                }
            }

            foreach (var kind in new[]
            {
                EventKind.Reward,
                EventKind.Theft,
                EventKind.InventoryLost,
                EventKind.RewardOverflow,
                EventKind.HeroWakeCost,
                EventKind.HeroOfferingCost
            }

            )
            {
                var totals = new SortedDictionary<ItemId, long>();
                foreach (var row in rows)
                    if (row.Kind == kind && row.Item.IsValid)
                    {
                        totals.TryGetValue(row.Item, out long amount);
                        totals[row.Item] = amount + row.Amount;
                    }

                foreach (var total in totals)
                    lines.Add($"合计 · {Label(kind)}：{ItemName(total.Key)} × {total.Value}");
            }

            return lines;
        }

        static string Label(EventKind kind) => kind == EventKind.Death || kind == EventKind.SoldierDeath || kind == EventKind.HeroDeath || kind == EventKind.EnemyDeath ? "死亡 / 击杀" : kind == EventKind.Ruin ? "建筑荒废" : kind == EventKind.ResidentsLost ? "居民死亡" : kind == EventKind.JobsLost ? "工人失业" : kind == EventKind.InventoryLost ? "库存损失" : kind == EventKind.Theft ? "被盗物资" : kind == EventKind.Reward ? "获得收益" : kind == EventKind.RewardOverflow ? "转入待存放" : kind == EventKind.HeroWakeCost ? "英雄出场消耗" : kind == EventKind.HeroOfferingCost ? "英雄供奉消耗（已付）" : kind == EventKind.HeroOfferingExperience ? "供奉经验（已结算）" : kind == EventKind.HeroExperience || kind == EventKind.SoldierExperience ? "参战经验" : kind.ToString();
    }
}
