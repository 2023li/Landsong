using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [CreateAssetMenu(menuName = "Landsong/Catalogs/BuildingLimitGroup")]
    public sealed class BuildingLimitGroupCatalogAsset : ScriptableObject
    {
        [LabelText("定义列表")]
        public BuildingLimitGroupDefinitionAsset[] Definitions = Array.Empty<BuildingLimitGroupDefinitionAsset>();
    }
}
