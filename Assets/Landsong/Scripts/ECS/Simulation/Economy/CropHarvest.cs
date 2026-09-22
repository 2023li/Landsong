using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class CropHarvest
    {
        public static ResultCode Harvest(EntityManager em, Entity root, Entity e, bool automatic)
        {
            using var scope = EconomyJournalOps.For(em, root, e, EconomyReason.Crop);
            BuildingFarmingState stateFarming = em.GetComponentData<BuildingFarmingState>(e);
            if (!stateFarming.Crop.IsValid)
                return ResultCode.Unavailable;
            ref var crop = ref CropDefinitions.Get(em, root, stateFarming.Crop);
            if (stateFarming.Progress < crop.GrowthTurns)
                return ResultCode.Unavailable;
            if (EconomyJournalOps.Forecast(em, root))
            {
                bool random = false;
                for (int i = 0; i < crop.HarvestOutputs.Length; i++)
                {
                    var output = crop.HarvestOutputs[i];
                    if (output.MaximumQuantity > output.MinimumQuantity)
                    {
                        random = true;
                        EconomyJournalOps.Record(em, root, output.Item, 0, note: new FixedString128Bytes($"基础收获区间 {output.MinimumQuantity}～{output.MaximumQuantity}，另受加成影响；本次未计入"));
                    }
                }

                if (random)
                {
                    EconomyJournalOps.Note(em, root, "随机收获及其费用未计入参考净额");
                    return ResultCode.Unavailable;
                }
            }

            using var harvest = new InventoryTransaction(em, root);
            if (automatic && !InventoryPayments.Pay(em, root, ref crop.AutomaticHarvestCosts))
            {
                harvest.Reject("自动收获费用不足");
                return ResultCode.InsufficientResources;
            }

            var rng = new Random(math.max(1u, stateFarming.Seed));
            float bonus = (stateFarming.FullCycle != 0 ? crop.FullStaffYieldBonus : 0) + BuildingEnvironment.Value(em, root, e, BuildingEnvironmentKind.Production);
            for (int i = 0; i < crop.HarvestOutputs.Length; i++)
            {
                var output = crop.HarvestOutputs[i];
                int amount = (int)math.floor(rng.NextInt(output.MinimumQuantity, math.max(output.MinimumQuantity, output.MaximumQuantity) + 1) * math.max(0, 1 + bonus / 100f + ItemEffects.Modifier(em, root, NumericEffectKind.ProductionMultiplier, output.Item) + ItemEffects.Modifier(em, root, NumericEffectKind.CropHarvestMultiplier, output.Item)));
                if (InventoryOps.Add(em, root, output.Item, amount) != amount)
                {
                    harvest.Reject("收获放不下，全部产出与费用撤销");
                    return ResultCode.NoCapacity;
                }
            }

            harvest.Commit();
            stateFarming.Crop = CropId.None;
            stateFarming.Progress = 0;
            {
                em.SetComponentData(e, stateFarming);
            }

            return ResultCode.Success;
        }
    }
}
