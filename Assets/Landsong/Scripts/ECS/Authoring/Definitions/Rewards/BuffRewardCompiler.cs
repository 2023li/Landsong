using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuffRewardCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuffRewardSource source, ref global::Landsong.ECS.Definitions.BuffReward target, BuffCatalogIndex buffIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuffReward 配置。");
            if (source.Order < 0)
                throw new InvalidOperationException("执行顺序不能为负。");
            if (source.GrantedLevel < 1)
                throw new InvalidOperationException("永久增益许可等级必须为正。");
            target.Order = source.Order;
            target.Buff = buffIndex.Resolve(source.Buff, false);
            target.GrantedLevel = source.GrantedLevel;
        }
    }
}
