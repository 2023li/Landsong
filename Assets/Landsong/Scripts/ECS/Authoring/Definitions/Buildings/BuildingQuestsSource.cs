using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingQuestsSource
    {
        [LabelText("启用模块")]
        public bool Enabled;
        [LabelText("刷新邀约费用")]
        [ShowIf(nameof(Enabled))]
        public BuildingQuestRecruitCostSource[] InvitationCosts = Array.Empty<BuildingQuestRecruitCostSource>();
        [LabelText("任务容量")]
        [ShowIf(nameof(Enabled))]
        public BuildingQuestCapacitySource[] Capacity = Array.Empty<BuildingQuestCapacitySource>();
        [LabelText("邀约来源")]
        [ShowIf(nameof(Enabled))]
        public BuildingQuestInvitationSource[] Invitations = Array.Empty<BuildingQuestInvitationSource>();
    }
}
