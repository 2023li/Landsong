using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [CreateAssetMenu(menuName = "Landsong/Catalogs/Building")]
    public sealed class BuildingCatalogAsset : ScriptableObject
    {
        [LabelText("定义列表")]
        public BuildingDefinitionAsset[] Definitions = Array.Empty<BuildingDefinitionAsset>();
    }
}
