using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class ConstructionSettlement
    {
        internal static void Settle(EntityManager em, Entity root, Entity e)
        {
            var b = em.GetComponentData<Building>(e);
            BuildingConstructionState bConstruction = em.GetComponentData<BuildingConstructionState>(e);
            BuildingWorkforceState bWorkforce = em.GetComponentData<BuildingWorkforceState>(e);
            BuildingHousingState bHousing = em.GetComponentData<BuildingHousingState>(e);
            BuildingMaintenanceState bMaintenance = em.GetComponentData<BuildingMaintenanceState>(e);
            var definition = em.GetComponentData<BuildingDefinitionRef>(e).Definition;
            ref var d = ref BuildingDefinitions.Get(em, root, definition);
            var repair = b.Stage == LifeStage.Repairing;
            using var scope = EconomyJournalOps.For(em, root, e, repair ? EconomyReason.Repair : EconomyReason.Construction);
            if (repair)
            {
                if (!BuildingCostOps.PayRepairStep(em, root, e))
                    return;
            }
            else
            {
                var provider = ResourceNetworkOps.Provider(em, root, e);
                var costs = BuildingCostOps.ConstructionStage(em, root, definition, bConstruction.Progress + 1);
                var carrier = costs.Count > 0 ? TransportWorkerOps.ConstructionWorker(em, root, e) : Entity.Null;
                var prepaid = carrier != Entity.Null;
                if (prepaid) provider = WorldQueries.Find(em, em.GetComponentData<TransportWorker>(carrier).Provider);
                if (prepaid && em.GetComponentData<TransportWorker>(carrier).DeathRecorded != 0
                    && em.GetComponentData<TransportWorker>(carrier).Delivered == 0)
                {
                    EconomyJournalOps.Note(em, root, "施工材料在送达前遗失，本期暂停");
                    return;
                }
                if (costs.Count > 0 && provider == Entity.Null && !prepaid)
                {
                    EconomyJournalOps.Note(em, root, "断开资源连接，施工暂停");
                    return;
                }

                using var payment = new InventoryTransaction(em, root);
                if (!prepaid && !BuildingCostOps.Pay(em, root, costs))
                {
                    payment.Reject("施工材料不足，本期未支付");
                    return;
                }

                ref var outputs = ref d.Capabilities.Construction.StageOutputs;
                for (int i = 0; i < outputs.Length; i++)
                {
                    var output = outputs[i];
                    if (output.Stage != 0 && output.Stage != bConstruction.Progress + 1)
                        continue;
                    if (InventoryOps.Add(em, root, output.Item, output.Quantity) != output.Quantity)
                    {
                        payment.Reject(prepaid ? "施工产物放不下，本期施工暂停；预扣材料在阶段结束确认" : "施工产物放不下，本期材料与产出撤销");
                        return;
                    }
                }

                payment.Commit();
                if (prepaid)
                {
                    TransportWorkerOps.SettleCargo(em, root, carrier);
                }
                ResourceNetworkOps.PayRecord(em, root, provider, costs);
                BuildingCostOps.RecordInvestment(em, e, costs);
            }

            bConstruction.Progress++;
            EconomyJournalOps.Note(em, root, repair ? "修复推进 1 回合（无需工人）" : "施工推进 1 回合");
            var duration = repair ? math.max(1, bConstruction.RepairDuration) : math.max(1, d.ConstructionTurns);
            if (bConstruction.Progress >= duration)
            {
                b.Stage = LifeStage.Operational;
                bConstruction.Progress = 0;
                bWorkforce.ProtectionUntil = em.GetComponentData<GameClock>(root).Turn + 2;
                if (repair)
                {
                    bWorkforce.Workers = 0;
                    bHousing.Population = 0;
                    bHousing.Growth = 0;
                    bHousing.FoodFailures = 0;
                    bHousing.TaxProgress = 0;
                    bConstruction.RepairCompletedTurn = em.GetComponentData<GameClock>(root).Turn;
                    bHousing.DeferredResidents = math.min(2, em.GetComponentData<BuildingHousingStats>(e).MaxPopulation);
                    bMaintenance.Maintained = 1;
                    em.GetBuffer<FoodSelection>(e).Clear();
                    em.GetBuffer<RepairMaterial>(e).Clear();
                }

                var health = em.GetComponentData<Health>(e);
                health.Current = health.Maximum;
                em.SetComponentData(e, health);
            }

            {
                em.SetComponentData(e, b);
                em.SetComponentData(e, bConstruction);
                em.SetComponentData(e, bWorkforce);
                em.SetComponentData(e, bHousing);
                em.SetComponentData(e, bMaintenance);
            }

            if (b.Stage == LifeStage.Operational)
            {
                BuildingLevelConfiguration.Apply(em, root, e, !repair);
                GridOps.Occupy(em, root, e);
            }
        }
    }
}
