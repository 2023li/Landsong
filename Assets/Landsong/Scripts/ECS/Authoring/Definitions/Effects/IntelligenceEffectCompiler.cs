using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class IntelligenceEffectCompiler
    {
        public static void Compile(ref BlobBuilder builder, IntelligenceEffectSource source, ref global::Landsong.ECS.Definitions.IntelligenceEffect target, TechnologyCatalogIndex technologyIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 IntelligenceEffect 配置。");
            if (source.Order < 0)
                throw new InvalidOperationException("执行顺序不能为负。");
            if (source.Level < 0)
                throw new InvalidOperationException("适用等级不能为负。");
            target.Order = source.Order;
            target.Level = source.Level;
            target.RequiredTechnology = technologyIndex.Resolve(source.RequiredTechnology, true);
            target.Points = source.Points;
        }
    }
}
