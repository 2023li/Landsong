using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingGatheringCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuildingGatheringSource source, ref global::Landsong.ECS.Definitions.BuildingGathering target, ItemCatalogIndex itemIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuildingGathering 配置。");
            if (!source.Enabled)
            {
                target.Enabled = false;
                builder.Allocate(ref target.Levels, 0);
                builder.Allocate(ref target.Rewards, 0);
                return;
            }

            target.Enabled = source.Enabled;
            if (source.Levels == null)
                throw new InvalidOperationException("配置项（Levels）列表不能为空引用。");
            var Levels = builder.Allocate(ref target.Levels, source.Levels.Length);
            for (int i = 0; i < source.Levels.Length; i++)
            {
                BuildingGatheringLevelCompiler.Compile(ref builder, source.Levels[i], ref Levels[i], itemIndex);
            }

            if (source.Rewards == null)
                throw new InvalidOperationException("完成奖励列表不能为空引用。");
            var Rewards = builder.Allocate(ref target.Rewards, source.Rewards.Length);
            for (int i = 0; i < source.Rewards.Length; i++)
            {
                BuildingGatheringRewardCompiler.Compile(ref builder, source.Rewards[i], ref Rewards[i], itemIndex);
            }
        }
    }
}
