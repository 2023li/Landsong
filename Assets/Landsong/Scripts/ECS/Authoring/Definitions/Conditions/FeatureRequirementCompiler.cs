using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class FeatureRequirementCompiler
    {
        public static void Compile(ref BlobBuilder builder, FeatureRequirementSource source, ref global::Landsong.ECS.Definitions.FeatureRequirement target, FeatureCatalogIndex featureIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 FeatureRequirement 配置。");
            if (source.Order < 0)
                throw new InvalidOperationException("执行顺序不能为负。");
            if (source.Required <= 0)
                throw new InvalidOperationException("前置要求必须为正。");
            if (source.Required != 1)
                throw new InvalidOperationException("功能许可与完成标记要求必须为一。");
            target.Order = source.Order;
            target.Feature = featureIndex.Resolve(source.Feature, false);
            target.Required = source.Required;
        }
    }
}
