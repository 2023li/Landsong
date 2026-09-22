using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class FeatureRewardCompiler
    {
        public static void Compile(ref BlobBuilder builder, FeatureRewardSource source, ref global::Landsong.ECS.Definitions.FeatureReward target, FeatureCatalogIndex featureIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 FeatureReward 配置。");
            if (source.Order < 0)
                throw new InvalidOperationException("执行顺序不能为负。");
            if (source.GrantedLevel != 1)
                throw new InvalidOperationException("功能许可等级必须为一。");
            target.Order = source.Order;
            target.Feature = featureIndex.Resolve(source.Feature, false);
            target.GrantedLevel = source.GrantedLevel;
        }
    }
}
