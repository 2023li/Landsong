using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingPlacementSource
    {
        [LabelText("允许地形")]
        public TerrainType AllowedTerrains = TerrainType.All;
        [LabelText("排除地形")]
        public TerrainType ExcludedTerrains = TerrainType.None;
    }
}
