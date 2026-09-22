using System;
using System.Collections.Generic;
using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS
{
    // Claimed drops stay in typed journals until the dawn transaction commits.
    public static class NightResultOps
    {
        public static void Reset(EntityManager em, Entity root)
        {
            EntityState.Buffer<NightItemReward>(em, root);
            em.GetBuffer<NightItemReward>(root).Clear();
            EntityState.Buffer<NightBlueprintReward>(em, root);
            em.GetBuffer<NightBlueprintReward>(root).Clear();
            EntityState.Buffer<NightBuffReward>(em, root);
            em.GetBuffer<NightBuffReward>(root).Clear();
            EntityState.Buffer<NightFeatureReward>(em, root);
            em.GetBuffer<NightFeatureReward>(root).Clear();
            EntityState.Set(em, root, new NightResultState { Turn = em.GetComponentData<GameClock>(root).Turn });
            PeacefulOps.Reset(em, root);
        }

        static bool CanRecord(EntityManager em, Entity root, ulong source, int entry)
        {
            if (source == 0)
                return false;
            if (!em.HasComponent<NightResultState>(root))
                Reset(em, root);
            if (em.GetComponentData<NightResultState>(root).Committed != 0)
                return false;
            foreach (var r in em.GetBuffer<NightItemReward>(root))
                if (r.Source == source && r.Entry == entry)
                    return false;
            foreach (var r in em.GetBuffer<NightBlueprintReward>(root))
                if (r.Source == source && r.Entry == entry)
                    return false;
            foreach (var r in em.GetBuffer<NightBuffReward>(root))
                if (r.Source == source && r.Entry == entry)
                    return false;
            foreach (var r in em.GetBuffer<NightFeatureReward>(root))
                if (r.Source == source && r.Entry == entry)
                    return false;
            return true;
        }

        static int Sequence(EntityManager em, Entity root) => em.GetBuffer<NightItemReward>(root).Length + em.GetBuffer<NightBlueprintReward>(root).Length + em.GetBuffer<NightBuffReward>(root).Length + em.GetBuffer<NightFeatureReward>(root).Length;
        public static void RecordItem(EntityManager em, Entity root, ulong source, int entry, ItemId item, int quantity, FixedString128Bytes sourceName = default)
        {
            if (source == 0 || quantity <= 0)
                return;
            if (!ItemDefinitions.IsValid(em, root, item))
                throw new InvalidOperationException("Invalid night item reward.");
            if (!CanRecord(em, root, source, entry))
                return;
            if (sourceName.IsEmpty)
            {
                var entity = WorldQueries.Find(em, source);
                if (entity != Entity.Null)
                    sourceName = em.GetComponentData<Identity>(entity).Name;
            }

            em.GetBuffer<NightItemReward>(root).Add(new NightItemReward { Source = source, Entry = entry, Sequence = Sequence(em, root), Item = item, Quantity = quantity, SourceName = sourceName });
            em.GetBuffer<BattleReportEntry>(root).Add(new BattleReportEntry { Kind = EventKind.Reward, Id = source, Item = item, Amount = quantity, SourceName = sourceName });
        }

        public static void RecordBlueprint(EntityManager em, Entity root, ulong source, int entry, BuildingId building, int level, FixedString128Bytes sourceName = default)
        {
            if (source == 0)
                return;
            if (!BuildingDefinitions.IsValid(em, root, building) || level <= 0 || level > BuildingDefinitions.Get(em, root, building).MaximumLevel)
                throw new InvalidOperationException("Invalid night blueprint reward.");
            if (!CanRecord(em, root, source, entry))
                return;
            if (sourceName.IsEmpty)
            {
                var entity = WorldQueries.Find(em, source);
                if (entity != Entity.Null)
                    sourceName = em.GetComponentData<Identity>(entity).Name;
            }

            em.GetBuffer<NightBlueprintReward>(root).Add(new NightBlueprintReward { Source = source, Entry = entry, Sequence = Sequence(em, root), Building = building, Level = level, SourceName = sourceName });
            em.GetBuffer<BattleReportEntry>(root).Add(new BattleReportEntry { Kind = EventKind.Reward, Id = source, Building = building, Amount = level, SourceName = sourceName });
        }

        public static void RecordOpportunityRewards(EntityManager em, Entity root, ulong source, OpportunityId opportunity)
        {
            ref var definition = ref OpportunityDefinitions.Get(em, root, opportunity);
            RecordRewards(em, root, source, ref definition.Rewards, definition.Metadata.Name);
        }

        public static void RecordEnemyRewards(EntityManager em, Entity root, ulong source, EnemyId enemy, FixedString128Bytes sourceName)
        {
            ref var definition = ref EnemyDefinitions.Get(em, root, enemy);
            RecordRewards(em, root, source, ref definition.KillRewards, sourceName);
        }

        static void RecordRewards(EntityManager em, Entity root, ulong source, ref DefinitionRewards rewards, FixedString128Bytes sourceName)
        {
            // Validate every entitlement before recording any part of a reward set.
            for (int i = 0; i < rewards.Items.Length; i++)
                if (!ItemDefinitions.IsValid(em, root, rewards.Items[i].Item) || rewards.Items[i].Quantity <= 0)
                    throw new InvalidOperationException("Invalid item reward.");
            for (int i = 0; i < rewards.Blueprints.Length; i++)
            {
                var reward = rewards.Blueprints[i];
                if (!BuildingDefinitions.IsValid(em, root, reward.Building) || reward.GrantedLevel <= 0 || reward.GrantedLevel > BuildingDefinitions.Get(em, root, reward.Building).MaximumLevel)
                    throw new InvalidOperationException("Invalid blueprint reward.");
            }

            for (int i = 0; i < rewards.Buffs.Length; i++)
                if (!BuffDefinitions.IsValid(em, root, rewards.Buffs[i].Buff) || rewards.Buffs[i].GrantedLevel <= 0)
                    throw new InvalidOperationException("Invalid buff reward.");
            for (int i = 0; i < rewards.Features.Length; i++)
                if (!FeatureDefinitions.IsValid(em, root, rewards.Features[i].Feature) || rewards.Features[i].GrantedLevel != 1)
                    throw new InvalidOperationException("Invalid feature reward.");
            // Entry is a stable identity across the four arrays; authored Order only controls presentation order.
            var entries = new List<(int Order, int Entry, Action Record)>();
            int nextEntry = 0;
            for (int i = 0; i < rewards.Items.Length; i++)
            {
                var reward = rewards.Items[i];
                int entry = nextEntry++;
                entries.Add((reward.Order, entry, () => RecordItem(em, root, source, entry, reward.Item, reward.Quantity, sourceName)));
            }

            for (int i = 0; i < rewards.Blueprints.Length; i++)
            {
                var reward = rewards.Blueprints[i];
                int entry = nextEntry++;
                entries.Add((reward.Order, entry, () => RecordBlueprint(em, root, source, entry, reward.Building, reward.GrantedLevel, sourceName)));
            }

            for (int i = 0; i < rewards.Buffs.Length; i++)
            {
                var reward = rewards.Buffs[i];
                int entry = nextEntry++;
                entries.Add((reward.Order, entry, () =>
                {
                    if (!CanRecord(em, root, source, entry))
                        return;
                    em.GetBuffer<NightBuffReward>(root).Add(new NightBuffReward { Source = source, Entry = entry, Sequence = Sequence(em, root), Buff = reward.Buff, Level = reward.GrantedLevel, SourceName = sourceName });
                    em.GetBuffer<BattleReportEntry>(root).Add(new BattleReportEntry { Kind = EventKind.Reward, Id = source, Amount = reward.GrantedLevel, SourceName = BuffDefinitions.Get(em, root, reward.Buff).Metadata.Name });
                }));
            }

            for (int i = 0; i < rewards.Features.Length; i++)
            {
                var reward = rewards.Features[i];
                int entry = nextEntry++;
                entries.Add((reward.Order, entry, () =>
                {
                    if (!CanRecord(em, root, source, entry))
                        return;
                    em.GetBuffer<NightFeatureReward>(root).Add(new NightFeatureReward { Source = source, Entry = entry, Sequence = Sequence(em, root), Feature = reward.Feature, SourceName = sourceName });
                    em.GetBuffer<BattleReportEntry>(root).Add(new BattleReportEntry { Kind = EventKind.Reward, Id = source, Amount = 1, SourceName = FeatureDefinitions.Get(em, root, reward.Feature).Metadata.Name });
                }));
            }

            entries.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : a.Entry.CompareTo(b.Entry));
            foreach (var entry in entries)
                entry.Record();
        }

        public static void Commit(EntityManager em, Entity root)
        {
            if (!em.HasComponent<NightResultState>(root))
                return;
            var state = em.GetComponentData<NightResultState>(root);
            if (state.Committed != 0 || state.Turn != em.GetComponentData<GameClock>(root).Turn)
                return;
            var items = new List<NightItemReward>();
            foreach (var item in em.GetBuffer<NightItemReward>(root))
                items.Add(item);
            var permissions = new List<(int Sequence, Action Grant)>();
            foreach (var reward in em.GetBuffer<NightBlueprintReward>(root))
            {
                var value = reward;
                permissions.Add((value.Sequence, () => BuildingBlueprints.Grant(em, root, value.Building, value.Level)));
            }

            foreach (var reward in em.GetBuffer<NightBuffReward>(root))
            {
                var value = reward;
                permissions.Add((value.Sequence, () => PermanentBuffs.Grant(em, root, value.Buff, value.Level)));
            }

            foreach (var reward in em.GetBuffer<NightFeatureReward>(root))
            {
                var value = reward;
                permissions.Add((value.Sequence, () => FeatureUnlocks.Unlock(em, root, value.Feature)));
            }

            items.Sort((a, b) => a.Sequence.CompareTo(b.Sequence));
            permissions.Sort((a, b) => a.Sequence.CompareTo(b.Sequence));
            using var transaction = new ProgressionRewardTransaction(em, root);
            foreach (var item in items)
            {
                int stored = InventoryOps.Add(em, root, item.Item, item.Quantity, true);
                if (stored < item.Quantity)
                    em.GetBuffer<BattleReportEntry>(root).Add(new BattleReportEntry { Kind = EventKind.RewardOverflow, Id = item.Source, Item = item.Item, Amount = item.Quantity - stored, SourceName = item.SourceName });
            }

            foreach (var permission in permissions)
                permission.Grant();
            state.Committed = 1;
            em.SetComponentData(root, state);
            transaction.Commit();
        }
    }
}
