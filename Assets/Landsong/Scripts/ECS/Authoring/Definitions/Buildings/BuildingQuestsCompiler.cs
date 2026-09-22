using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingQuestsCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuildingQuestsSource source, ref global::Landsong.ECS.Definitions.BuildingQuests target, ItemCatalogIndex itemIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuildingQuests 配置。");
            if (!source.Enabled)
            {
                target.Enabled = false;
                builder.Allocate(ref target.InvitationCosts, 0);
                builder.Allocate(ref target.Capacity, 0);
                builder.Allocate(ref target.Invitations, 0);
                return;
            }

            target.Enabled = source.Enabled;
            if (source.InvitationCosts == null)
                throw new InvalidOperationException("配置项（InvitationCosts）列表不能为空引用。");
            var InvitationCosts = builder.Allocate(ref target.InvitationCosts, source.InvitationCosts.Length);
            for (int i = 0; i < source.InvitationCosts.Length; i++)
            {
                BuildingQuestRecruitCostCompiler.Compile(ref builder, source.InvitationCosts[i], ref InvitationCosts[i], itemIndex);
            }

            if (source.Capacity == null)
                throw new InvalidOperationException("配置项（Capacity）列表不能为空引用。");
            var Capacity = builder.Allocate(ref target.Capacity, source.Capacity.Length);
            for (int i = 0; i < source.Capacity.Length; i++)
            {
                BuildingQuestCapacityCompiler.Compile(ref builder, source.Capacity[i], ref Capacity[i]);
            }

            if (source.Invitations == null)
                throw new InvalidOperationException("配置项（Invitations）列表不能为空引用。");
            var Invitations = builder.Allocate(ref target.Invitations, source.Invitations.Length);
            for (int i = 0; i < source.Invitations.Length; i++)
            {
                BuildingQuestInvitationCompiler.Compile(ref builder, source.Invitations[i], ref Invitations[i]);
            }
        }
    }
}
