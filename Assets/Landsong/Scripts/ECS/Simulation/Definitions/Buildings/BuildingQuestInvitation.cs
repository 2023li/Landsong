using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingQuestInvitation
    {
        public int Level;
        public int Slots;
        public BuildingInvitationKind Type;
        public int MinimumRefreshTurns;
        public int MaximumRefreshTurns;
    }
}
