using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingExpeditionSiteLevel
    {
        public int Level;
        public int MinimumCrew;
        public int MaximumCrew;
        public float FullCrewRewardBonus;
    }
}
