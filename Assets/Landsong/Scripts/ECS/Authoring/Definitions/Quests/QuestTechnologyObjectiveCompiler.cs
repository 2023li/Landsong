using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class QuestTechnologyObjectiveCompiler
    {
        public static void Compile(ref BlobBuilder builder, QuestTechnologyObjectiveSource source, ref global::Landsong.ECS.Definitions.QuestTechnologyObjective target, TechnologyCatalogIndex technologyIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 QuestTechnologyObjective 配置。");
            if (source.Order < 0)
                throw new InvalidOperationException("执行顺序不能为负。");
            target.Order = source.Order;
            target.Key = new FixedString64Bytes(source.Key ?? "");
            target.Technology = technologyIndex.Resolve(source.Technology, true);
            target.Count = source.Count;
        }
    }
}
