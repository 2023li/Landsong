using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingQuestInvitationCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuildingQuestInvitationSource source, ref global::Landsong.ECS.Definitions.BuildingQuestInvitation target)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuildingQuestInvitation 配置。");
            if (source.Level < 0)
                throw new InvalidOperationException("适用等级不能为负。");
            target.Level = source.Level;
            target.Slots = source.Slots;
            target.Type = source.Type;
            target.MinimumRefreshTurns = source.MinimumRefreshTurns;
            target.MaximumRefreshTurns = source.MaximumRefreshTurns;
        }
    }
}
