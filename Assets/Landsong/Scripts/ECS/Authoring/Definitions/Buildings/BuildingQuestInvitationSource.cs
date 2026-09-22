using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingQuestInvitationSource
    {
        [LabelText("适用等级（0 = 全部）")]
        [MinValue(0)]
        public int Level = 1;
        [LabelText("邀约槽位")]
        [MinValue(0)]
        public int Slots;
        [LabelText("邀约种类")]
        public BuildingInvitationKind Type;
        [LabelText("最短刷新回合")]
        [MinValue(0)]
        public int MinimumRefreshTurns;
        [LabelText("最长刷新回合")]
        [MinValue(1)]
        public int MaximumRefreshTurns = 1;
    }
}
