using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class BuildingProductionSettlement
    {
        internal static void Settle(EntityManager em, Entity root, Entity e)
        {
            using var scope = EconomyJournalOps.For(em, root, e, EconomyReason.Production);
            var state = em.GetComponentData<Building>(e);
            BuildingWorkforceState stateWorkforce = em.GetComponentData<BuildingWorkforceState>(e);
            BuildingProductionState stateProduction = em.GetComponentData<BuildingProductionState>(e);
            BuildingFarmingState stateFarming = em.GetComponentData<BuildingFarmingState>(e);
            var definition = em.GetComponentData<BuildingDefinitionRef>(e).Definition;
            ref var source = ref BuildingDefinitions.Get(em, root, definition);
            ref var production = ref source.Capabilities.Production;
            int cycleIndex = -1;
            for (int i = 0; i < production.Cycles.Length; i++)
                if (production.Cycles[i].Level == 0 || production.Cycles[i].Level == state.Level)
                {
                    cycleIndex = i;
                    break;
                }

            var cycle = cycleIndex < 0 ? default : production.Cycles[cycleIndex];
            for (int i = 0; i < production.ProcessingTiers.Length; i++)
            {
                var tier = production.ProcessingTiers[i];
                if ((tier.Level == 0 || tier.Level == state.Level) && stateWorkforce.Workers >= tier.RequiredWorkers)
                    cycle.Interval = tier.Interval;
            }

            if (cycleIndex >= 0 && stateWorkforce.Workers >= cycle.RequiredWorkers)
            {
                stateProduction.Progress = math.min(math.max(1, cycle.Interval), stateProduction.Progress + 1);
                if (stateProduction.Progress >= math.max(1, cycle.Interval))
                {
                    using var recipe = new InventoryTransaction(em, root);
                    var provider = ResourceNetworkOps.Provider(em, root, e);
                    var costs = BuildingCostOps.ProductionInputs(em, root, definition, state.Level);
                    bool hasInputs = false;
                    for (int i = 0; i < production.Inputs.Length; i++)
                        if (production.Inputs[i].Level == 0 || production.Inputs[i].Level == state.Level)
                            hasInputs = true;
                    bool success = (!hasInputs || provider != Entity.Null) && BuildingCostOps.Pay(em, root, costs);
                    string failure = hasInputs && provider == Entity.Null ? "断开资源连接，生产暂停" : "原料不足，配方未支付";
                    for (int i = 0; i < production.Outputs.Length && success; i++)
                    {
                        var output = production.Outputs[i];
                        if (output.Level != 0 && output.Level != state.Level || stateWorkforce.Workers < output.MinimumWorkers || output.MaximumWorkers > 0 && stateWorkforce.Workers > output.MaximumWorkers)
                            continue;
                        float flat = ItemEffects.FlatProduction(em, root, output.Item, definition);
                        int amount = (int)math.max(0, math.floor((output.Quantity + flat) * math.max(0, 1 + ItemEffects.Modifier(em, root, NumericEffectKind.ProductionMultiplier, output.Item) + BuildingEnvironment.Value(em, root, e, BuildingEnvironmentKind.Production) / 100f)));
                        success = InventoryOps.Add(em, root, output.Item, amount) == amount;
                        if (!success)
                            failure = "产品放不下，整份配方的原料与产出撤销";
                    }

                    if (success)
                    {
                        recipe.Commit();
                        stateProduction.Progress = 0;
                        if (hasInputs)
                            ResourceNetworkOps.PayRecord(em, root, provider, costs);
                    }
                    else
                        recipe.Reject(new FixedString128Bytes(failure));
                }
                else
                    EconomyJournalOps.Note(em, root, "生产周期尚未到期");
            }
            else if (cycleIndex >= 0)
                EconomyJournalOps.Note(em, root, "工人不足，生产暂停");
            if (stateFarming.Crop.IsValid)
            {
                ref var crop = ref CropDefinitions.Get(em, root, stateFarming.Crop);
                if (stateWorkforce.Workers < crop.FullStaffBonusWorkers)
                    stateFarming.FullCycle = 0;
                if (stateWorkforce.Workers >= crop.RequiredWorkers)
                    stateFarming.Progress = math.min(crop.GrowthTurns, stateFarming.Progress + 1);
                if (stateFarming.Progress >= crop.GrowthTurns && stateFarming.AutoHarvest != 0)
                {
                    {
                        em.SetComponentData(e, state);
                        em.SetComponentData(e, stateWorkforce);
                        em.SetComponentData(e, stateProduction);
                        em.SetComponentData(e, stateFarming);
                    }

                    CropHarvest.Harvest(em, root, e, true);
                    {
                        state = em.GetComponentData<Building>(e);
                        stateWorkforce = em.GetComponentData<BuildingWorkforceState>(e);
                        stateProduction = em.GetComponentData<BuildingProductionState>(e);
                        stateFarming = em.GetComponentData<BuildingFarmingState>(e);
                    }
                }
            }

            ref var research = ref source.Capabilities.Research.Levels;
            for (int i = 0; i < research.Length; i++)
                if (research[i].Level == 0 || research[i].Level == state.Level)
                {
                    var points = em.GetComponentData<ResearchState>(root);
                    points.Points += research[i].PointsPerTurn;
                    em.SetComponentData(root, points);
                }

            for (int i = 0; i < production.RareOutputs.Length; i++)
            {
                var output = production.RareOutputs[i];
                if (output.Level != 0 && output.Level != state.Level || stateWorkforce.Workers < output.RequiredWorkers)
                    continue;
                if (EconomyJournalOps.Forecast(em, root))
                    EconomyJournalOps.Record(em, root, output.Item, 0, note: new FixedString128Bytes($"随机产出概率 {output.Probability:P0}，数量 {output.Quantity}（不计入预计净额）"));
                else if (SimulationRandom.NextRandom(em, root) % 10000 < output.Probability * 10000)
                    InventoryOps.Add(em, root, output.Item, output.Quantity);
            }

            {
                em.SetComponentData(e, state);
                em.SetComponentData(e, stateWorkforce);
                em.SetComponentData(e, stateProduction);
                em.SetComponentData(e, stateFarming);
            }
        }
    }
}
