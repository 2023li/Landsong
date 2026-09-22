using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingQuestRecruitCostCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuildingQuestRecruitCostSource source, ref global::Landsong.ECS.Definitions.BuildingQuestRecruitCost target, ItemCatalogIndex itemIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuildingQuestRecruitCost 配置。");
            if (source.Level < 0)
                throw new InvalidOperationException("适用等级不能为负。");
            target.Level = source.Level;
            target.Item = itemIndex.Resolve(source.Item, true);
            target.Quantity = source.Quantity;
        }
    }
}
