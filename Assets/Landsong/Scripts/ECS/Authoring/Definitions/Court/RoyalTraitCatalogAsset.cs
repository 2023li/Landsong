using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [CreateAssetMenu(menuName = "Landsong/Catalogs/RoyalTrait")]
    public sealed class RoyalTraitCatalogAsset : ScriptableObject
    {
        [LabelText("定义列表")]
        public RoyalTraitDefinitionAsset[] Definitions = Array.Empty<RoyalTraitDefinitionAsset>();
    }
}
