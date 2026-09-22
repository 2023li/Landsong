using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BlueprintRewardCompiler
    {
        public static void Compile(ref BlobBuilder builder, BlueprintRewardSource source, ref global::Landsong.ECS.Definitions.BlueprintReward target, BuildingCatalogIndex buildingIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BlueprintReward 配置。");
            if (source.Order < 0)
                throw new InvalidOperationException("执行顺序不能为负。");
            if (source.Building == null || source.GrantedLevel < 1 || source.GrantedLevel > source.Building.MaximumLevel)
                throw new InvalidOperationException("蓝图许可等级超过建筑允许范围。");
            target.Order = source.Order;
            target.Building = buildingIndex.Resolve(source.Building, false);
            target.GrantedLevel = source.GrantedLevel;
        }
    }
}
