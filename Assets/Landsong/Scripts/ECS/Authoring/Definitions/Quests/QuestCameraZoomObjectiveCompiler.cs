using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class QuestCameraZoomObjectiveCompiler
    {
        public static void Compile(ref BlobBuilder builder, QuestCameraZoomObjectiveSource source, ref global::Landsong.ECS.Definitions.QuestCameraZoomObjective target)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 QuestCameraZoomObjective 配置。");
            if (source.Order < 0)
                throw new InvalidOperationException("执行顺序不能为负。");
            target.Order = source.Order;
            target.Key = new FixedString64Bytes(source.Key ?? "");
            target.Count = source.Count;
        }
    }
}
