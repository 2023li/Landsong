using System;
using System.Collections.Generic;
using System.Linq;
using Landsong.ECS.Definitions;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class TalentIncomeDelivery
    {
        public static void Apply(EntityManager em, Entity root, ref TalentPeriodicIncome income, int level)
        {
            var actions = new List<(int Order, Action Apply)>();
            int Scaled(TalentEffectScaling scaling, float initial, float increment) => checked((int)math.floor(TalentEffectScalingOps.Value(em, root, scaling, initial, increment, level)));
            for (int i = 0; i < income.Items.Length; i++)
            {
                var entry = income.Items[i];
                actions.Add((entry.Order, () => InventoryOps.Add(em, root, entry.Item, checked(entry.BaseQuantity + (int)(entry.PerLevel * (level - 1))))));
            }

            for (int i = 0; i < income.ScaledItems.Length; i++)
            {
                var entry = income.ScaledItems[i];
                actions.Add((entry.Order, () => InventoryOps.Add(em, root, entry.Item, Scaled(entry.Scaling, entry.BaseQuantity, entry.PerLevel))));
            }

            for (int i = 0; i < income.ResearchPoints.Length; i++)
            {
                var entry = income.ResearchPoints[i];
                actions.Add((entry.Order, () =>
                {
                    var research = em.GetComponentData<ResearchState>(root);
                    research.Points = checked(research.Points + Scaled(entry.Scaling, entry.BasePoints, entry.PerLevel));
                    em.SetComponentData(root, research);
                }));
            }

            for (int i = 0; i < income.Blueprints.Length; i++)
            {
                var entry = income.Blueprints[i];
                int granted = Scaled(entry.Scaling, entry.BaseLevel, entry.PerLevel);
                ref var definition = ref BuildingDefinitions.Get(em, root, entry.Building);
                if (granted <= 0 || granted > definition.MaximumLevel)
                    throw new InvalidOperationException("Invalid periodic blueprint level.");
                actions.Add((entry.Order, () => BuildingBlueprints.Grant(em, root, entry.Building, Scaled(entry.Scaling, entry.BaseLevel, entry.PerLevel))));
            }

            for (int i = 0; i < income.Buffs.Length; i++)
            {
                var entry = income.Buffs[i];
                if (!BuffDefinitions.IsValid(em, root, entry.Buff) || Scaled(entry.Scaling, entry.BaseLevel, entry.PerLevel) <= 0)
                    throw new InvalidOperationException("Invalid periodic buff level.");
                actions.Add((entry.Order, () => PermanentBuffs.Grant(em, root, entry.Buff, Scaled(entry.Scaling, entry.BaseLevel, entry.PerLevel))));
            }

            for (int i = 0; i < income.Features.Length; i++)
            {
                var entry = income.Features[i];
                if (!FeatureDefinitions.IsValid(em, root, entry.Feature) || Scaled(entry.Scaling, entry.BaseLevel, entry.PerLevel) != 1)
                    throw new InvalidOperationException("Invalid periodic feature permission.");
                actions.Add((entry.Order, () =>
                {
                    if (Scaled(entry.Scaling, entry.BaseLevel, entry.PerLevel) != 1)
                        throw new InvalidOperationException("Feature permission must be binary.");
                    FeatureUnlocks.Unlock(em, root, entry.Feature);
                }));
            }

            using var transaction = new ProgressionRewardTransaction(em, root);
            // Per-period income historically applies available storage and drops overflow.
            // Scaling is evaluated in authored order because earlier income can change its basis.
            foreach (var action in actions.OrderBy(action => action.Order))
                action.Apply();
            transaction.Commit();
        }
    }
}
