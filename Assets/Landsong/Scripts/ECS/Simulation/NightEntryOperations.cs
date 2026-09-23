using Landsong.ECS.Definitions;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Landsong.ECS.Persistence;
using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS
{
    public static class NightEntryOps
    {
        public static void Clear(EntityManager em, Entity root)
        {
            if (em.HasComponent<NightEntryReview>(root))
                em.SetComponentData(root, new NightEntryReview());
            if (em.HasComponent<QuestCapacityReview>(root))
                em.SetComponentData(root, new QuestCapacityReview());
            if (em.HasBuffer<NightEntryLoss>(root))
                em.GetBuffer<NightEntryLoss>(root).Clear();
        }

        public static ResultCode Begin(EntityManager em, Entity root, bool confirmed, ulong token, Action<string> probe = null)
        {
            if (em.GetComponentData<Session>(root).Phase != Phase.Day || em.GetComponentData<GameClock>(root).DawnRemaining > 0)
                return ResultCode.WrongPhase;
            var bytes = SnapshotCodec.Capture(em, root);
            string fingerprint;
            using (var hash = SHA256.Create())
                // Walking cargo representatives cannot invalidate an otherwise unchanged economy review.
                fingerprint = Convert.ToBase64String(hash.ComputeHash(SnapshotCodec.Capture(em, root, includeTransportWorkers: false)));
            var review = em.HasComponent<NightEntryReview>(root) ? em.GetComponentData<NightEntryReview>(root) : default;
            var approved = confirmed && token != 0 && token == review.Token && review.Fingerprint.ToString() == fingerprint;
            var expected = new List<NightEntryLoss>();
            if (em.HasBuffer<NightEntryLoss>(root))
                foreach (var loss in em.GetBuffer<NightEntryLoss>(root))
                    expected.Add(loss);
            List<NightEntryLoss> losses = null;
            QuestCapacityReview overflow = default;
            using (var transaction = new RestoreTransaction(em, root))
            {
                var candidate = transaction.Root;
                SnapshotCodec.RebuildNightEntryCandidate(em, candidate, bytes, probe);
                // Invalid quest slots can charge penalties and destroy quests. Preview those writes
                // on the candidate too, so cancellation/failure preserves the reviewed live day.
                QuestLifecycle.ReconcileQuestContainers(em, candidate);
                // No invalid relationship may survive staging into settlement or publication.
                SnapshotCodec.ValidateReconciledNightEntry(em, candidate);
                probe?.Invoke("quest-containers-reconciled");
                em.GetBuffer<BattleReportEntry>(candidate).Clear();
                var state = em.GetComponentData<Session>(candidate);
                GameClock stateClock = em.GetComponentData<GameClock>(candidate);
                NightRuntimeState stateNight = em.GetComponentData<NightRuntimeState>(candidate);
                HeroSelection stateHeroSelection = em.GetComponentData<HeroSelection>(candidate);
                BellState stateBell = em.GetComponentData<BellState>(candidate);
                PersistenceGate statePersistence = em.GetComponentData<PersistenceGate>(candidate);
                state.Phase = Phase.Settlement;
                {
                    em.SetComponentData(candidate, state);
                    em.SetComponentData(candidate, stateClock);
                    em.SetComponentData(candidate, stateNight);
                    em.SetComponentData(candidate, stateHeroSelection);
                    em.SetComponentData(candidate, stateBell);
                    em.SetComponentData(candidate, statePersistence);
                }

                BuildingFireOps.EnterNight(em, candidate);
                DailyEconomySettlement.Settle(em, candidate);
                probe?.Invoke("day-settled");
                if (CourtOps.State(em, candidate).Extinction != 0)
                {
                    transaction.Commit(probe);
                    return ResultCode.Success;
                }

                var capacity = QuestLifecycle.QuestCapacity(em);
                var count = QuestLifecycle.QuestCount(em);
                if (count > capacity)
                    overflow = new QuestCapacityReview
                    {
                        Turn = stateClock.Turn,
                        Capacity = capacity,
                        Count = count
                    };
                losses = Losses(em, candidate);
                if (overflow.Turn == 0 && (losses.Count == 0 || approved && SameLosses(expected, losses)))
                {
                    NightResultOps.Reset(em, candidate);
                    EconomyJournalOps.DiscardPending(em, candidate);
                    NightPlanOps.Prepare(em, candidate);
                    BattleLifecycle.PrepareNight(em, candidate);
                    probe?.Invoke("night-prepared");
                    {
                        state = em.GetComponentData<Session>(candidate);
                        stateClock = em.GetComponentData<GameClock>(candidate);
                        stateNight = em.GetComponentData<NightRuntimeState>(candidate);
                        stateHeroSelection = em.GetComponentData<HeroSelection>(candidate);
                        stateBell = em.GetComponentData<BellState>(candidate);
                        statePersistence = em.GetComponentData<PersistenceGate>(candidate);
                    }

                    IntelOps.Refresh(em, candidate);
                    stateNight.Intelligence = IntelOps.Known(em, candidate);
                    state.Phase = Phase.Deployment;
                    stateClock.PhaseTime = 0;
                    stateClock.DawnRemaining = 0;
                    stateBell.ActiveBell = 0;
                    stateHeroSelection.SelectedHero = Entity.Null;
                    stateNight.DeploymentTime = stateClock.Time;
                    statePersistence.CheckpointPending = 1;
                    {
                        em.SetComponentData(candidate, state);
                        em.SetComponentData(candidate, stateClock);
                        em.SetComponentData(candidate, stateNight);
                        em.SetComponentData(candidate, stateHeroSelection);
                        em.SetComponentData(candidate, stateBell);
                        em.SetComponentData(candidate, statePersistence);
                    }

                    SimulationEvents.Emit(em, candidate, EventKind.DuskCheckpoint, "黄昏节点");
                    transaction.Commit(probe);
                    return ResultCode.Success;
                }
            }

            if (overflow.Turn != 0)
            {
                EntityState.Set(em, root, overflow);
                return ResultCode.QuestOverflow;
            }

            // Only the review is published. All settlement costs, RNG, entities and messages rolled back.
            EntityState.Set(em, root, new NightEntryReview { Fingerprint = fingerprint, Token = review.Token == ulong.MaxValue ? 1 : review.Token + 1 });
            EntityState.Buffer<NightEntryLoss>(em, root);
            var rows = em.GetBuffer<NightEntryLoss>(root);
            rows.Clear();
            foreach (var loss in losses)
                rows.Add(loss);
            return ResultCode.ConfirmationRequired;
        }

        static List<NightEntryLoss> Losses(EntityManager em, Entity root)
        {
            var result = new List<NightEntryLoss>();
            var items = new SortedDictionary<ItemId, int>();
            foreach (var item in em.GetBuffer<PendingItem>(root))
                if (item.Amount > 0)
                {
                    items.TryGetValue(item.Item, out var count);
                    items[item.Item] = checked(count + item.Amount);
                }

            foreach (var item in items)
                result.Add(new NightEntryLoss { Item = item.Key, Amount = item.Value });
            using var troops = WorldQueries.OrderedEntities<Soldier>(em);
            foreach (var e in troops)
                if (em.GetComponentData<Soldier>(e).Garrison == 0 && EntityState.Alive(em, e))
                {
                    var id = em.GetComponentData<Identity>(e);
                    result.Add(new NightEntryLoss { Soldier = id.Id, Name = id.Name, Item = ItemId.None, Amount = 1 });
                }

            return result;
        }

        static bool SameLosses(List<NightEntryLoss> a, List<NightEntryLoss> b)
        {
            if (a.Count != b.Count)
                return false;
            for (var i = 0; i < a.Count; i++)
                if (a[i].Soldier != b[i].Soldier || a[i].Item != b[i].Item || a[i].Amount != b[i].Amount || a[i].Name != b[i].Name)
                    return false;
            return true;
        }
    }
}
