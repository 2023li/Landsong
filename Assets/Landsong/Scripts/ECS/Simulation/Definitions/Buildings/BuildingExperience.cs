using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingExperience
    {
        public int Level;
        public int ExperiencePerTurn;
        public int UpgradeExperience;
        public int RequiredWorkers;
    }
}
