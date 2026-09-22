using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingIntelligenceLevelCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuildingIntelligenceLevelSource source, ref global::Landsong.ECS.Definitions.BuildingIntelligenceLevel target, TechnologyCatalogIndex technologyIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuildingIntelligenceLevel 配置。");
            if (source.Level < 0)
                throw new InvalidOperationException("适用等级不能为负。");
            target.Level = source.Level;
            target.Technology = technologyIndex.Resolve(source.Technology, true);
            target.Points = source.Points;
            target.RequiredWorkers = source.RequiredWorkers;
        }
    }
}
