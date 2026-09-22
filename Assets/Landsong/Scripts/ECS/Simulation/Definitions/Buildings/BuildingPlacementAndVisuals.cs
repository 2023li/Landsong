using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingPlacementAndVisuals
    {
        public BuildingCategory Category;
        public int SpawnExclusionPadding;
        public int MenuOrder;
        public int ProviderPriority;
        public bool CanMove;
        public bool CanRotate;
        public float MoveMaterialRatio;
        public float MoveExperienceRatio;
        public float RuinMovementCost;
        public FixedString128Bytes DefaultSkin;
    }
}
