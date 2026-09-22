using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingQuestCapacityCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuildingQuestCapacitySource source, ref global::Landsong.ECS.Definitions.BuildingQuestCapacity target)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuildingQuestCapacity 配置。");
            if (source.Level < 0)
                throw new InvalidOperationException("适用等级不能为负。");
            target.Level = source.Level;
            target.Slots = source.Slots;
        }
    }
}
