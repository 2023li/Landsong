using Landsong.ECS.Definitions;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public sealed class QuestOfferQuote
    {
        public ResultCode Code;
        public string Reason;
        public int Strength, Type, Wait;
        public ulong Offer;
        public readonly List<QuestId> Candidates = new List<QuestId>();
        public readonly List<double> Weights = new List<double>();
        public List<BuildingCost> Costs = new List<BuildingCost>();
    }

    public static class QuestOfferOps
    {
        public static string TypeName(int type) => type == 0 ? "贸易" : type == 1 ? "建设" : type == 2 ? "民生" : "探索";
        public static BuildingQuestInvitation SourceRule(EntityManager em, Entity root, Entity source, int type)
        {
            var building = em.GetComponentData<Building>(source);
            ref var definition = ref BuildingDefinitions.Get(em, root, em.GetComponentData<BuildingDefinitionRef>(source).Definition);
            if (definition.Capabilities.Quests.Enabled)
                for (int i = 0; i < definition.Capabilities.Quests.Invitations.Length; i++)
                {
                    var invitation = definition.Capabilities.Quests.Invitations[i];
                    if ((int)invitation.Type == type && (invitation.Level == 0 || invitation.Level == building.Level))
                        return invitation;
                }

            return new BuildingQuestInvitation
            {
                Level = -1
            };
        }

        public static bool HasExpeditionSite(EntityManager em, Entity root, Entity source)
        {
            ref var definition = ref BuildingDefinitions.Get(em, root, em.GetComponentData<BuildingDefinitionRef>(source).Definition);
            int level = em.GetComponentData<Building>(source).Level;
            if (definition.Capabilities.Expeditions.Enabled)
                for (int i = 0; i < definition.Capabilities.Expeditions.Levels.Length; i++)
                    if (definition.Capabilities.Expeditions.Levels[i].Level <= level)
                        return true;
            return false;
        }

        static bool HasMarket(EntityManager em, Entity root, Entity source)
        {
            ref var definition = ref BuildingDefinitions.Get(em, root, em.GetComponentData<BuildingDefinitionRef>(source).Definition);
            int level = em.GetComponentData<Building>(source).Level;
            if (definition.Capabilities.Market.Enabled)
                for (int i = 0; i < definition.Capabilities.Market.Levels.Length; i++)
                    if (definition.Capabilities.Market.Levels[i].Level <= level)
                        return true;
            return false;
        }

        static List<BuildingCost> InvitationCosts(EntityManager em, Entity root, Entity source)
        {
            ref var definition = ref BuildingDefinitions.Get(em, root, em.GetComponentData<BuildingDefinitionRef>(source).Definition);
            int level = em.GetComponentData<Building>(source).Level;
            var costs = new List<BuildingCost>();
            if (definition.Capabilities.Quests.Enabled)
                for (int i = 0; i < definition.Capabilities.Quests.InvitationCosts.Length; i++)
                {
                    var cost = definition.Capabilities.Quests.InvitationCosts[i];
                    if (cost.Level == 0 || cost.Level == level)
                        BuildingCostOps.Add(costs, cost.Item, cost.Quantity);
                }

            costs.Sort((left, right) => left.Item.Index.CompareTo(right.Item.Index));
            return costs;
        }

        public static bool Available(EntityManager em, Entity root, Entity source, out string reason)
        {
            reason = "";
            if (!BuildingStatus.Operational(em, source))
            {
                reason = "建筑未正常运营，冷却暂停";
                return false;
            }

            BuildingWorkforceState bWorkforce = em.GetComponentData<BuildingWorkforceState>(source);
            BuildingMaintenanceState bMaintenance = em.GetComponentData<BuildingMaintenanceState>(source);
            if (bMaintenance.Maintained == 0)
            {
                reason = "维护未满足，冷却暂停";
                return false;
            }

            if (HasExpeditionSite(em, root, source))
            {
                if (!FeatureOps.Unlocked(em, root, "Expedition"))
                {
                    reason = "远征许可未解锁";
                    return false;
                }

                if (WorkforceSettlement.Locked(em, em.GetComponentData<Identity>(source).Id))
                {
                    reason = "队伍在途，冷却暂停";
                    return false;
                }
            }

            if (HasMarket(em, root, source) && bWorkforce.Workers < em.GetComponentData<BuildingWorkforceStats>(source).Capacity)
            {
                reason = "市场需要满额工人，冷却暂停";
                return false;
            }

            return true;
        }

        public static int Strength(EntityManager em, Entity root, Entity source)
        {
            var b = em.GetComponentData<Building>(source);
            BuildingMarketState bMarket = em.GetComponentData<BuildingMarketState>(source);
            BuildingExperienceState bExperience = em.GetComponentData<BuildingExperienceState>(source);
            if (HasExpeditionSite(em, root, source))
                return (int)math.min(int.MaxValue, (long)b.Level * 10 + bExperience.Experience);
            var settings = em.GetComponentData<QuestGenerationSettings>(root);
            return (int)math.min(int.MaxValue, bMarket.LifetimeValue / math.max(1, settings.MarketValuePerStrength));
        }

        public static double Weight(QuestGenerationSettings settings, ref QuestDefinition d, int strength)
        {
            var band = math.min(3, strength / math.max(1, settings.StrengthStep));
            var weights = band == 0 ? settings.Low : band == 1 ? settings.Medium : band == 2 ? settings.High : settings.Maximum;
            return (double)d.OfferWeight * weights[math.clamp(d.Intensity, 0, 3)];
        }

        public static Entity Offered(EntityManager em, ulong source, int slot)
        {
            using var quests = WorldQueries.OrderedEntities<Quest>(em);
            foreach (var e in quests)
            {
                var q = em.GetComponentData<Quest>(e);
                if (q.Status == QuestStatus.Offered && q.Source == source && q.Slot == slot)
                    return e;
            }

            return Entity.Null;
        }

        public static QuestOfferQuote Quote(EntityManager em, Entity root, Entity source, int index)
        {
            var q = new QuestOfferQuote
            {
                Code = ResultCode.InvalidTarget,
                Reason = "邀约来源不存在"
            };
            if (source == Entity.Null || !em.HasComponent<Building>(source) || !em.HasBuffer<QuestOfferSlot>(source))
                return q;
            var slots = em.GetBuffer<QuestOfferSlot>(source);
            if (index < 0 || index >= slots.Length)
                return q;
            var slot = slots[index];
            var rule = SourceRule(em, root, source, slot.Type);
            q.Type = slot.Type;
            if (rule.Level < 0 || slot.Index >= rule.Slots)
                return q;
            var id = em.GetComponentData<Identity>(source);
            q.Strength = Strength(em, root, source);
            q.Wait = math.max(0, slot.NextTurn - em.GetComponentData<GameClock>(root).Turn);
            q.Costs = InvitationCosts(em, root, source);
            var existing = Offered(em, id.Id, index);
            if (existing != Entity.Null)
            {
                q.Offer = em.GetComponentData<Identity>(existing).Id;
                q.Code = ResultCode.Busy;
                q.Reason = "已有邀约";
                return q;
            }

            if (!Available(em, root, source, out q.Reason))
            {
                q.Code = ResultCode.Unavailable;
                return q;
            }

            var settings = em.GetComponentData<QuestGenerationSettings>(root);
            for (int questIndex = 0; questIndex < QuestDefinitions.Count(em, root); questIndex++)
            {
                var quest = QuestId.FromIndex(questIndex);
                ref var definition = ref QuestDefinitions.Get(em, root, quest);
                if ((definition.Behavior & (QuestBehaviorFlags.Mainline | QuestBehaviorFlags.Draft)) != 0 || (int)definition.OfferType != slot.Type || QuestOps.HasQuestPredecessor(em, root, quest) || !QuestOps.Prerequisites(em, root, quest))
                    continue;
                double weight = Weight(settings, ref definition, q.Strength);
                if (weight <= 0)
                    continue;
                q.Candidates.Add(quest);
                q.Weights.Add(weight);
            }

            q.Code = q.Candidates.Count == 0 ? ResultCode.Unavailable : ResultCode.Success;
            q.Reason = q.Candidates.Count == 0 ? "暂无符合条件的邀约，不会扣费" : "";
            return q;
        }

        // Stable buffer indices are never reused for another source type. Excess slots are dormant tombstones.
        public static void Synchronize(EntityManager em, Entity root, Entity source)
        {
            var slots = em.GetBuffer<QuestOfferSlot>(source);
            var id = em.GetComponentData<Identity>(source).Id;
            for (var i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                var r = SourceRule(em, root, source, s.Type);
                if (r.Level >= 0 && s.Index < r.Slots)
                    continue;
                var e = Offered(em, id, i);
                if (e != Entity.Null)
                    em.DestroyEntity(e);
                s.NextTurn = 0;
                slots = em.GetBuffer<QuestOfferSlot>(source);
                slots[i] = s;
            }
        }

        public static void Restart(EntityManager em, Entity root, Entity source, int type, int turn)
        {
            var rule = SourceRule(em, root, source, type);
            if (rule.Level < 0)
                return;
            var next = turn + math.max(1, rule.MinimumRefreshTurns) + (int)(SimulationRandom.NextRandom(em, root) % (uint)math.max(1, (int)rule.MaximumRefreshTurns - rule.MinimumRefreshTurns + 1));
            var slots = em.GetBuffer<QuestOfferSlot>(source);
            for (var i = 0; i < slots.Length; i++)
                if (slots[i].Type == type)
                {
                    var slot = slots[i];
                    slot.NextTurn = next;
                    slots[i] = slot;
                }
        }

        public static ResultCode Generate(EntityManager em, Entity root, Entity source, int index, bool paid)
        {
            var session = em.GetComponentData<Session>(root);
            PersistenceGate sessionPersistence = em.GetComponentData<PersistenceGate>(root);
            if (paid && (session.Phase != Phase.Day || sessionPersistence.CheckpointPending != 0))
                return ResultCode.WrongPhase;
            var q = Quote(em, root, source, index);
            if (q.Code != ResultCode.Success)
                return q.Code;
            if (paid && !BuildingCostOps.CanPay(em, root, q.Costs))
                return ResultCode.InsufficientResources;
            // Candidate validation precedes both cost and RNG. Failed payment cannot advance selection/cooldown.
            if (paid && !BuildingCostOps.Pay(em, root, q.Costs))
                return ResultCode.InsufficientResources;
            var total = 0d;
            foreach (var w in q.Weights)
                total += w;
            var roll = SimulationRandom.NextRandom(em, root) / ((double)uint.MaxValue + 1) * total;
            var choice = q.Candidates[q.Candidates.Count - 1];
            for (var i = 0; i < q.Candidates.Count; i++)
            {
                roll -= q.Weights[i];
                if (roll < 0)
                {
                    choice = q.Candidates[i];
                    break;
                }
            }

            var id = em.GetComponentData<Identity>(source);
            QuestLifecycle.CreateQuest(em, root, choice, id.Id, index);
            Restart(em, root, source, q.Type, em.GetComponentData<GameClock>(root).Turn + (paid ? 0 : 1));
            SimulationEvents.Emit(em, root, EventKind.Message, "新邀约：" + id.Name.ToString().Substring(0, math.min(30, id.Name.ToString().Length)), id.Id);
            return ResultCode.Success;
        }

        public static void Settle(EntityManager em, Entity root)
        {
            if (EconomyJournalOps.Forecast(em, root))
                return;
            var turn = em.GetComponentData<GameClock>(root).Turn;
            using var buildings = WorldQueries.OrderedEntities<Building>(em);
            foreach (var b in buildings)
            {
                Synchronize(em, root, b);
                var count = em.GetBuffer<QuestOfferSlot>(b).Length;
                var types = new HashSet<int>();
                for (var i = 0; i < count; i++)
                {
                    var slot = em.GetBuffer<QuestOfferSlot>(b)[i];
                    var rule = SourceRule(em, root, b, slot.Type);
                    if (rule.Level < 0 || slot.Index >= rule.Slots)
                        continue;
                    if (!Available(em, root, b, out _))
                    {
                        if (slot.NextTurn > 0)
                        {
                            slot.NextTurn++;
                            var pausedSlots = em.GetBuffer<QuestOfferSlot>(b);
                            pausedSlots[i] = slot;
                        }

                        continue;
                    }

                    if (Offered(em, em.GetComponentData<Identity>(b).Id, i) != Entity.Null || !types.Add(slot.Type))
                        continue;
                    if (slot.NextTurn == 0)
                        Restart(em, root, b, slot.Type, turn);
                    else if (slot.NextTurn <= turn + 1)
                        Generate(em, root, b, i, false);
                }
            }
        }
    }
}
