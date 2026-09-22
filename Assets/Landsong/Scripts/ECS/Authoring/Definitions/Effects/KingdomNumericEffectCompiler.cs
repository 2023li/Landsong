using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class KingdomNumericEffectCompiler
    {
        public static void Compile(ref BlobBuilder builder, KingdomNumericEffectSource source, ref global::Landsong.ECS.Definitions.KingdomNumericEffect target)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 KingdomNumericEffect 配置。");
            if (source.Order < 0)
                throw new InvalidOperationException("执行顺序不能为负。");
            if (source.Level < 0)
                throw new InvalidOperationException("适用等级不能为负。");
            EffectAuthoringBoundary.Kingdom(source.Effect);
            target.Order = source.Order;
            target.Level = source.Level;
            target.Effect = source.Effect;
            if (!math.isfinite(source.Magnitude))
                throw new InvalidOperationException("效果数值必须是有限数值。");
            target.Magnitude = source.Magnitude;
        }
    }
}
