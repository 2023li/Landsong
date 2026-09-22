using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingQuests
    {
        public bool Enabled;
        public BlobArray<BuildingQuestRecruitCost> InvitationCosts;
        public BlobArray<BuildingQuestCapacity> Capacity;
        public BlobArray<BuildingQuestInvitation> Invitations;
    }
}
