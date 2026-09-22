using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuffRequirementCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuffRequirementSource source, ref global::Landsong.ECS.Definitions.BuffRequirement target, BuffCatalogIndex buffIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuffRequirement 配置。");
            if (source.Order < 0)
                throw new InvalidOperationException("执行顺序不能为负。");
            if (source.Required <= 0)
                throw new InvalidOperationException("前置要求必须为正。");
            target.Order = source.Order;
            target.Buff = buffIndex.Resolve(source.Buff, false);
            target.Required = source.Required;
        }
    }
}
