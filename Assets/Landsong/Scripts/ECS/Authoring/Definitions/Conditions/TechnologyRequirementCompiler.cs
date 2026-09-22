using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class TechnologyRequirementCompiler
    {
        public static void Compile(ref BlobBuilder builder, TechnologyRequirementSource source, ref global::Landsong.ECS.Definitions.TechnologyRequirement target, TechnologyCatalogIndex technologyIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 TechnologyRequirement 配置。");
            if (source.Order < 0)
                throw new InvalidOperationException("执行顺序不能为负。");
            if (source.Required <= 0)
                throw new InvalidOperationException("前置要求必须为正。");
            target.Order = source.Order;
            target.Technology = technologyIndex.Resolve(source.Technology, false);
            target.Required = source.Required;
        }
    }
}
