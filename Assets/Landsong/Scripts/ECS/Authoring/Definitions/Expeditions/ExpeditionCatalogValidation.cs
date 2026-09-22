using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class ExpeditionCatalogValidation
    {
        public static void Validate(ExpeditionCatalogAsset catalog)
        {
            foreach (var asset in catalog.Definitions)
            {
                var source = asset;
                void Fail(string reason) => throw new InvalidOperationException(source.Metadata.Id + "：" + reason);
                if (source.MinimumSiteLevel < 1 || source.MinimumCrew < 1 || source.MaximumCrew < 0 || source.MaximumCrew != 0 && source.MaximumCrew < source.MinimumCrew || source.TravelTurns < 1 || source.BaseCompensation < 0 || source.CompensationPerCrew < 0 || (long)source.BaseCompensation + (long)source.CompensationPerCrew * math.max(source.MinimumCrew, source.MaximumCrew) > int.MaxValue)
                    Fail("远征等级、人数、行程或抚恤金无效。");
                foreach (var probability in new[]
                {
                    source.BaseSuccessChance,
                    source.SuccessChancePerCrew,
                    source.MaximumSuccessChance,
                    source.FailureCasualtyRatio
                }

                )
                    if (!math.isfinite(probability) || probability < 0 || probability > 1)
                        Fail("远征概率或比例必须在零到一之间。");
                var items = new HashSet<ItemDefinitionAsset>();
                foreach (var supply in source.Supplies)
                    if (supply == null || supply.Item == null || !items.Add(supply.Item) || items.Count > 8 || supply.MinimumQuantity < 0 || supply.MinimumQuantity > 1000000 || supply.ExtraLimit < 0 || supply.ExtraLimit > supply.MinimumQuantity / 2 || !math.isfinite(supply.SuccessPerExtra) || supply.SuccessPerExtra < 0 || supply.SuccessPerExtra > 1 || !math.isfinite(supply.RewardPerExtra) || supply.RewardPerExtra < 0 || supply.RewardPerExtra > 1)
                        Fail("远征补给必须包含至多八种唯一物品，额外份数及收益比例须在允许范围内。");
                foreach (var penalty in source.FailurePenalties)
                    if (penalty == null || penalty.Quantity <= 0)
                        Fail("失败惩罚数量必须为正。");
            }
        }
    }
}
