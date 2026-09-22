using System;
using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS
{
    internal sealed class ProgressionRewardTransaction : IDisposable
    {
        readonly EntityManager em;
        readonly Entity root;
        readonly InventoryTransaction inventory;
        readonly BlueprintUnlock[] blueprints;
        readonly OwnedBuff[] buffs;
        readonly UnlockedFeature[] features;
        readonly ClaimedQuest[] quests;
        readonly CompletedExpedition[] expeditions;
        readonly TechnologyProgress[] technologies;
        readonly GameEvent[] events;
        readonly ResearchCompletedEvent[] researchEvents;
        readonly BattleReportEntry[] report;
        readonly HistoryEntry[] history;
        readonly bool hadNightResult;
        readonly NightResultState nightResult;
        readonly ResearchState research;
        bool committed;
        internal ProgressionRewardTransaction(EntityManager em, Entity root)
        {
            this.em = em;
            this.root = root;
            blueprints = Capture<BlueprintUnlock>();
            buffs = Capture<OwnedBuff>();
            features = Capture<UnlockedFeature>();
            quests = Capture<ClaimedQuest>();
            expeditions = Capture<CompletedExpedition>();
            technologies = Capture<TechnologyProgress>();
            events = Capture<GameEvent>();
            report = Capture<BattleReportEntry>();
            history = Capture<HistoryEntry>();
            researchEvents = Capture<ResearchCompletedEvent>();
            hadNightResult = em.HasComponent<NightResultState>(root);
            nightResult = hadNightResult ? em.GetComponentData<NightResultState>(root) : default;
            research = em.GetComponentData<ResearchState>(root);
            inventory = new InventoryTransaction(em, root);
        }

        T[] Capture<T>()
            where T : unmanaged, IBufferElementData
        {
            if (!em.HasBuffer<T>(root))
                return null;
            using var data = em.GetBuffer<T>(root).ToNativeArray(Allocator.Temp);
            return data.ToArray();
        }

        void Restore<T>(T[] rows)
            where T : unmanaged, IBufferElementData
        {
            if (rows == null)
            {
                if (em.HasBuffer<T>(root))
                    em.RemoveComponent<T>(root);
                return;
            }

            if (!em.HasBuffer<T>(root))
                em.AddBuffer<T>(root);
            em.GetBuffer<T>(root).CopyFrom(rows);
        }

        internal void Commit()
        {
            inventory.Commit();
            committed = true;
        }

        public void Dispose()
        {
            try
            {
                if (!committed)
                {
                    Restore(blueprints);
                    Restore(buffs);
                    Restore(features);
                    Restore(quests);
                    Restore(expeditions);
                    Restore(technologies);
                    Restore(events);
                    Restore(researchEvents);
                    Restore(report);
                    em.SetComponentData(root, research);
                    if (hadNightResult)
                        em.SetComponentData(root, nightResult);
                    else if (em.HasComponent<NightResultState>(root))
                        em.RemoveComponent<NightResultState>(root);
                }
            }
            finally
            {
                try
                {
                    inventory.Dispose();
                }
                finally
                {
                    if (!committed)
                        Restore(history);
                }
            }
        }
    }
}
