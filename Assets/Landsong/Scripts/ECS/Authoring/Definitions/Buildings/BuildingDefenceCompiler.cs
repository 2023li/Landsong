using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingDefenceCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuildingDefenceSource source, ref global::Landsong.ECS.Definitions.BuildingDefence target, TechnologyCatalogIndex technologyIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuildingDefence 配置。");
            if (!source.Enabled)
            {
                target.Enabled = false;
                builder.Allocate(ref target.Intelligence, 0);
                builder.Allocate(ref target.Bells, 0);
                return;
            }

            target.Enabled = source.Enabled;
            if (source.Intelligence == null)
                throw new InvalidOperationException("情报列表不能为空引用。");
            var Intelligence = builder.Allocate(ref target.Intelligence, source.Intelligence.Length);
            for (int i = 0; i < source.Intelligence.Length; i++)
            {
                BuildingIntelligenceLevelCompiler.Compile(ref builder, source.Intelligence[i], ref Intelligence[i], technologyIndex);
            }

            if (source.Bells == null)
                throw new InvalidOperationException("配置项（Bells）列表不能为空引用。");
            var Bells = builder.Allocate(ref target.Bells, source.Bells.Length);
            for (int i = 0; i < source.Bells.Length; i++)
            {
                BuildingBellLevelCompiler.Compile(ref builder, source.Bells[i], ref Bells[i]);
            }
        }
    }
}
