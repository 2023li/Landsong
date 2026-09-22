using Landsong.ECS.Definitions;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public sealed class ExpeditionQuote
    {
        public ResultCode Code;
        public string Reason = "", Stamp = "";
        public bool Visible = true;
        public int Minimum, Maximum, Workers, StableWorkers, Crew, Arrival, Casualties, Subsidy, MissingSubsidy;
        public float SuccessChance, RewardBonus;
        public readonly List<Definitions.ExpeditionSupply> Options = new List<Definitions.ExpeditionSupply>();
        public readonly List<ExpeditionSupply> Supplies = new List<ExpeditionSupply>();
        public List<BuildingCost> Costs = new List<BuildingCost>(), Rewards = new List<BuildingCost>();
        public ExpeditionQuote Fail(ResultCode code, string reason)
        {
            Code = code;
            Reason = reason;
            return this;
        }
    }

    public static class ExpeditionOps
    {
        public static List<BuildingCost> Rewards(EntityManager em, Entity root, ExpeditionId definition, float bonus)
        {
            var result = new List<BuildingCost>();
            ref var source = ref ExpeditionDefinitions.Get(em, root, definition);
            for (int i = 0; i < source.Rewards.Items.Length; i++)
            {
                var item = source.Rewards.Items[i];
                result.Add(new BuildingCost(item.Item, (int)math.floor(item.Quantity * (1 + bonus))));
            }

            return result;
        }

        public static int SupplyMaximum(Definitions.ExpeditionSupply r) => r.MinimumQuantity + math.min(r.MinimumQuantity / 2, r.ExtraLimit > 0 ? r.ExtraLimit : r.MinimumQuantity / 2);
        public static bool CompletedAt(EntityManager em, Entity site, ExpeditionId definition)
        {
            if (!em.HasBuffer<ExpeditionDestinationHistory>(site))
                return false;
            foreach (var entry in em.GetBuffer<ExpeditionDestinationHistory>(site))
                if (entry.Definition == definition)
                    return true;
            return false;
        }

        public static ExpeditionQuote Quote(EntityManager em, Entity root, Entity site, ExpeditionId definition, int crew, int[] amounts = null)
        {
            var q = new ExpeditionQuote
            {
                Crew = crew
            };
            if (!ExpeditionDefinitions.IsValid(em, root, definition))
                return q.Fail(ResultCode.InvalidContent, "目的地不存在");
            ref var d = ref ExpeditionDefinitions.Get(em, root, definition);
            q.Visible = PrerequisiteEvaluation.Satisfied(em, root, ref d.Visibility);
            for (int i = 0; i < d.Supplies.Length; i++)
                q.Options.Add(d.Supplies[i]);
            if (site == Entity.Null || !em.HasComponent<Building>(site))
                return q.Fail(ResultCode.InvalidTarget, "请选择远征所");
            var b = em.GetComponentData<Building>(site);
            BuildingWorkforceState bWorkforce = em.GetComponentData<BuildingWorkforceState>(site);
            BuildingMaintenanceState bMaintenance = em.GetComponentData<BuildingMaintenanceState>(site);
            var id = em.GetComponentData<Identity>(site);
            var rule = SiteLevel(em, root, site);
            q.Workers = bWorkforce.Workers;
            q.StableWorkers = bWorkforce.StableWorkers;
            q.Minimum = math.max(rule.MinimumCrew, d.MinimumCrew);
            q.Maximum = math.min(math.min(bWorkforce.Workers, bWorkforce.StableWorkers), rule.MaximumCrew);
            if (d.MaximumCrew > 0)
                q.Maximum = math.min(q.Maximum, d.MaximumCrew);
            var s = em.GetComponentData<Session>(root);
            GameClock sClock = em.GetComponentData<GameClock>(root);
            PersistenceGate sPersistence = em.GetComponentData<PersistenceGate>(root);
            q.Arrival = sClock.Turn + d.TravelTurns;
            q.SuccessChance = d.BaseSuccessChance + d.SuccessChancePerCrew * crew;
            q.RewardBonus = crew >= rule.MaximumCrew && rule.MaximumCrew > 0 ? rule.FullCrewRewardBonus : 0;
            q.Casualties = math.clamp((int)math.round(crew * d.FailureCasualtyRatio), d.FailureCasualtyRatio > 0 && crew > 0 ? 1 : 0, math.max(0, crew));
            q.Subsidy = d.BaseCompensation + math.max(0, crew) * d.CompensationPerCrew;
            q.MissingSubsidy = math.max(0, q.Subsidy - InventoryOps.Count(em, root, em.GetComponentData<CurrencySettings>(root).Gold));
            if (amounts != null && amounts.Length != q.Options.Count)
                return q.Fail(ResultCode.InvalidContent, "补给项目已变化，请重新确认");
            for (var i = 0; i < q.Options.Count; i++)
            {
                var option = q.Options[i];
                var amount = amounts == null ? option.MinimumQuantity : amounts[i];
                if (amount < option.MinimumQuantity || amount > SupplyMaximum(option))
                    return q.Fail(ResultCode.InvalidContent, "补给数量超出允许范围");
                q.Supplies.Add(new ExpeditionSupply { Item = option.Item, Amount = amount });
                q.Costs.Add(new BuildingCost { Item = option.Item, Amount = amount });
                q.SuccessChance += (amount - option.MinimumQuantity) * option.SuccessPerExtra;
                q.RewardBonus += (amount - option.MinimumQuantity) * option.RewardPerExtra;
            }

            q.SuccessChance = math.clamp(q.SuccessChance, 0, d.MaximumSuccessChance);
            q.Rewards = Rewards(em, root, definition, q.RewardBonus);
            // Read-only consent binds inventory, site workforce/level, chosen supplies and turn. No RNG in previews.
            var stamp = InventoryLayout.Fingerprint(em, root) + ":" + id.Id + ":" + b.Level + ":" + bWorkforce.Workers + ":" + bWorkforce.StableWorkers + ":" + b.Stage + ":" + bMaintenance.Maintained + ":" + sClock.Turn + ":" + d.Metadata.Id + ":" + crew;
            foreach (var supply in q.Supplies)
                stamp += ":" + supply.Amount;
            ulong hash = 14695981039346656037UL;
            foreach (var ch in stamp)
            {
                hash ^= ch;
                hash *= 1099511628211UL;
            }

            q.Stamp = hash.ToString("X16");
            if (!FeatureOps.Unlocked(em, root, "Expedition"))
                return q.Fail(ResultCode.Unavailable, "远征许可尚未解锁");
            if (s.Phase != Phase.Day || sPersistence.CheckpointPending != 0)
                return q.Fail(ResultCode.WrongPhase, "只能在可操作的白天派遣");
            if (!q.Visible || !PrerequisiteEvaluation.Satisfied(em, root, ref d.Prerequisites) || !d.Repeatable && CompletedAt(em, site, definition))
                return q.Fail(ResultCode.Unavailable, "目的地条件未满足，或此驻地已成功完成不可重复的目的地");
            if (!BuildingStatus.Operational(em, site) || bMaintenance.Maintained == 0 || rule.Level < 0 || b.Level < d.MinimumSiteLevel)
                return q.Fail(ResultCode.Unavailable, "需要正常运营、维护满足且等级足够的远征所");
            if (WorkforceSettlement.Locked(em, id.Id))
                return q.Fail(ResultCode.Busy, "该远征所已有队伍在途");
            if (crew < q.Minimum || crew > q.Maximum)
                return q.Fail(ResultCode.InsufficientPopulation, "需要 " + q.Minimum + "～" + q.Maximum + " 人，受当前工人和稳定岗位限制");
            if (!BuildingCostOps.CanPay(em, root, Aggregate(q.Costs)))
                return q.Fail(ResultCode.InsufficientResources, "出发物资不足");
            q.Code = ResultCode.Success;
            return q;
        }

        static List<BuildingCost> Aggregate(List<BuildingCost> costs)
        {
            var totals = new Dictionary<ItemId, int>();
            foreach (var c in costs)
            {
                totals.TryGetValue(c.Item, out var n);
                totals[c.Item] = checked(n + c.Amount);
            }

            var result = new List<BuildingCost>();
            foreach (var c in totals)
                if (c.Value > 0)
                    result.Add(new BuildingCost { Item = c.Key, Amount = c.Value });
            return result;
        }

        public static BuildingExpeditionSiteLevel SiteLevel(EntityManager em, Entity root, Entity site)
        {
            ref var definition = ref BuildingDefinitions.Get(em, root, em.GetComponentData<BuildingDefinitionRef>(site).Definition);
            var result = new BuildingExpeditionSiteLevel
            {
                Level = -1
            };
            int level = em.GetComponentData<Building>(site).Level;
            if (definition.Capabilities.Expeditions.Enabled)
                for (int i = 0; i < definition.Capabilities.Expeditions.Levels.Length; i++)
                {
                    var entry = definition.Capabilities.Expeditions.Levels[i];
                    if (entry.Level <= level && entry.Level >= result.Level)
                        result = entry;
                }

            return result;
        }

        static ResultCode Editable(EntityManager em, Entity root)
        {
            if (em.GetComponentData<Session>(root).Phase != Phase.Day || em.GetComponentData<PersistenceGate>(root).CheckpointPending != 0)
                return ResultCode.WrongPhase;
            return FeatureOps.Unlocked(em, root, "Expedition") ? ResultCode.Success : ResultCode.Unavailable;
        }

        public static ResultCode Start(EntityManager em, Entity root, StartExpeditionRequest request)
        {
            var permission = Editable(em, root);
            if (permission != ResultCode.Success)
                return permission;
            if (request.ExpectedQuote.IsEmpty)
                return ResultCode.ConfirmationRequired;
            var site = WorldQueries.Find(em, request.Site);
            var quantities = new int[request.SupplyQuantities.Length];
            for (int i = 0; i < quantities.Length; i++)
                quantities[i] = request.SupplyQuantities[i];
            var quote = Quote(em, root, site, request.Destination, request.Crew, quantities);
            if (quote.Code != ResultCode.Success)
                return quote.Code;
            if (quote.Stamp != request.ExpectedQuote.ToString())
                return ResultCode.Unavailable;
            if (request.Captain != 0 && !CourtOps.AvailableCaptain(em, root, WorldQueries.Find(em, request.Captain)))
                return ResultCode.Unavailable;
            if (!BuildingCostOps.Pay(em, root, Aggregate(quote.Costs)))
                return ResultCode.InsufficientResources;
            var journey = ExpeditionEntities.Spawn(em, root, request.Destination, default, true);
            EntityState.Set(em, journey, new Expedition { Site = request.Site, Captain = request.Captain, SourceName = em.GetComponentData<Identity>(site).Name, Crew = quote.Crew, Departure = em.GetComponentData<GameClock>(root).Turn, Arrival = quote.Arrival, SourceLevel = em.GetComponentData<Building>(site).Level, SuccessChance = quote.SuccessChance, RewardBonus = quote.RewardBonus, SubsidyRequired = quote.Subsidy });
            EntityState.Buffer<ExpeditionSupply>(em, journey);
            foreach (var supply in quote.Supplies)
                em.GetBuffer<ExpeditionSupply>(journey).Add(supply);
            PersonRequestOps.Departed(em, root, journey);
            return ResultCode.Success;
        }

        public static ResultCode Finish(EntityManager em, Entity root, ulong expedition, bool claim)
        {
            var permission = Editable(em, root);
            if (permission != ResultCode.Success)
                return permission;
            var entity = WorldQueries.Find(em, expedition);
            if (entity == Entity.Null || !em.HasComponent<Expedition>(entity))
                return ResultCode.InvalidTarget;
            var state = em.GetComponentData<Expedition>(entity);
            if (claim)
            {
                if (state.Status == ExpeditionStatus.Travelling)
                    return ResultCode.Busy;
                var id = em.GetComponentData<ExpeditionDefinitionRef>(entity).Definition;
                ref var definition = ref ExpeditionDefinitions.Get(em, root, id);
                if (state.Status == ExpeditionStatus.Success && !RewardDelivery.Apply(em, root, ref definition.Rewards, definition.Metadata.Name, 1 + state.RewardBonus, false, () => ExpeditionCompletions.RecordSuccess(em, root, id)))
                    return ResultCode.NoCapacity;
            }

            if (state.Status == ExpeditionStatus.Travelling)
                PersonRequestOps.Returned(em, root, entity, false);
            em.DestroyEntity(entity);
            return ResultCode.Success;
        }

        public static float Penalty(EntityManager em, Entity root)
        {
            if (!FeatureOps.Unlocked(em, root, "Expedition"))
                return 0;
            GameClock sClock = em.GetComponentData<GameClock>(root);
            ExpeditionPenaltyState sExpeditionPenalty = em.GetComponentData<ExpeditionPenaltyState>(root);
            return sClock.Turn <= sExpeditionPenalty.UntilTurn ? sExpeditionPenalty.Stacks * em.GetComponentData<ExpeditionSettings>(root).AttractionPerStack : 0;
        }

        public static void Settle(EntityManager em, Entity root)
        {
            if (!FeatureOps.Unlocked(em, root, "Expedition"))
            {
                using var frozen = WorldQueries.OrderedEntities<Expedition>(em);
                foreach (var e in frozen)
                {
                    var j = em.GetComponentData<Expedition>(e);
                    if (j.Status == ExpeditionStatus.Travelling)
                    {
                        j.Arrival++;
                        em.SetComponentData(e, j);
                    }
                }

                GameClock stateClock = em.GetComponentData<GameClock>(root);
                ExpeditionPenaltyState stateExpeditionPenalty = em.GetComponentData<ExpeditionPenaltyState>(root);
                if (stateExpeditionPenalty.Stacks > 0 && stateExpeditionPenalty.UntilTurn >= stateClock.Turn)
                {
                    stateExpeditionPenalty.UntilTurn++;
                    {
                        em.SetComponentData(root, stateClock);
                        em.SetComponentData(root, stateExpeditionPenalty);
                    }
                }

                return;
            }

            GameClock sClock = em.GetComponentData<GameClock>(root);
            ExpeditionPenaltyState sExpeditionPenalty = em.GetComponentData<ExpeditionPenaltyState>(root);
            if (sClock.Turn > sExpeditionPenalty.UntilTurn)
            {
                sExpeditionPenalty.Stacks = 0;
                sExpeditionPenalty.UntilTurn = 0;
                {
                    em.SetComponentData(root, sClock);
                    em.SetComponentData(root, sExpeditionPenalty);
                }
            }

            using var journeys = WorldQueries.OrderedEntities<Expedition>(em);
            foreach (var e in journeys)
            {
                var expedition = em.GetComponentData<Expedition>(e);
                if (expedition.Status != ExpeditionStatus.Travelling)
                    continue;
                var site = WorldQueries.Find(em, expedition.Site);
                if (!BuildingStatus.Operational(em, site))
                {
                    PersonRequestOps.Returned(em, root, e, false);
                    em.DestroyEntity(e);
                    SimulationEvents.Emit(em, root, EventKind.Message, "远征所失效，队伍撤回；出发物资不返还", expedition.Site);
                    continue;
                }

                if (sClock.Turn + 1 < expedition.Arrival)
                    continue;
                using var scope = EconomyJournalOps.For(em, root, site, EconomyReason.Expedition);
                if (EconomyJournalOps.Forecast(em, root))
                {
                    EconomyJournalOps.Note(em, root, "远征将抵达，成败、伤亡和抚恤不计入参考预测");
                    continue;
                }

                ref var d = ref ExpeditionDefinitions.Get(em, root, em.GetComponentData<ExpeditionDefinitionRef>(e).Definition);
                var success = SimulationRandom.NextRandom(em, root) % 10000 < expedition.SuccessChance * 10000;
                expedition.Status = success ? ExpeditionStatus.Success : ExpeditionStatus.Failure;
                if (expedition.Captain != 0)
                    CourtOps.Influence(em, root, WorldQueries.Find(em, expedition.Captain), success ? 10 : -5, success ? "远征成功" : "远征失败");
                if (success)
                {
                    BuildingExperienceState bExperience = em.GetComponentData<BuildingExperienceState>(site);
                    bExperience.Experience++;
                    {
                        em.SetComponentData(site, bExperience);
                    }

                    var definition = em.GetComponentData<ExpeditionDefinitionRef>(e).Definition;
                    if (!CompletedAt(em, site, definition))
                    {
                        EntityState.Buffer<ExpeditionDestinationHistory>(em, site);
                        em.GetBuffer<ExpeditionDestinationHistory>(site).Add(new ExpeditionDestinationHistory { Definition = definition });
                    }
                }
                else
                {
                    expedition.Casualties = math.clamp((int)math.round(expedition.Crew * d.FailureCasualtyRatio), d.FailureCasualtyRatio > 0 ? 1 : 0, expedition.Crew);
                    BuildingWorkforceState bWorkforce = em.GetComponentData<BuildingWorkforceState>(site);
                    bWorkforce.Workers = math.max(0, bWorkforce.Workers - expedition.Casualties);
                    {
                        em.SetComponentData(site, bWorkforce);
                    }

                    PopulationOps.RemovePopulation(em, root, expedition.Casualties);
                    var gold = em.GetComponentData<CurrencySettings>(root).Gold;
                    expedition.SubsidyPaid = math.min(expedition.SubsidyRequired, InventoryOps.Count(em, root, gold));
                    InventoryOps.Remove(em, root, gold, expedition.SubsidyPaid);
                    var missing = expedition.SubsidyRequired - expedition.SubsidyPaid;
                    if (missing > 0)
                    {
                        expedition.PenaltyStacks = (missing + 9) / 10;
                        ExpeditionPenaltyState sessionExpeditionPenalty = em.GetComponentData<ExpeditionPenaltyState>(root);
                        sessionExpeditionPenalty.Stacks += expedition.PenaltyStacks;
                        sessionExpeditionPenalty.UntilTurn = math.max(sessionExpeditionPenalty.UntilTurn, sClock.Turn + em.GetComponentData<ExpeditionSettings>(root).PenaltyTurns - 1);
                        {
                            em.SetComponentData(root, sessionExpeditionPenalty);
                        }

                        SimulationEvents.Emit(em, root, EventKind.Message, "远征抚恤不足，全局岗位吸引力下降", expedition.Site, category: HistoryCategory.Economy);
                    }
                }

                em.SetComponentData(e, expedition);
                SimulationEvents.Emit(em, root, EventKind.Message, success ? "远征归来，可领取奖励" : "远征归来，请查看伤亡及抚恤", em.GetComponentData<Identity>(e).Id);
                PersonRequestOps.Returned(em, root, e, true);
            }
        }

        public static void WithdrawFromSite(EntityManager em, Entity root, ulong site)
        {
            using var journeys = WorldQueries.OrderedEntities<Expedition>(em);
            foreach (var e in journeys)
            {
                var j = em.GetComponentData<Expedition>(e);
                if (j.Site != site || j.Status != ExpeditionStatus.Travelling)
                    continue;
                PersonRequestOps.Returned(em, root, e, false);
                em.DestroyEntity(e);
                SimulationEvents.Emit(em, root, EventKind.Message, "远征所失效，队伍撤回；出发物资不返还", site);
            }
        }
    }
}
