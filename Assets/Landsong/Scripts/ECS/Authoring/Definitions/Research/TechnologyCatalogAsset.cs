using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [CreateAssetMenu(menuName = "Landsong/Catalogs/Technology")]
    public sealed class TechnologyCatalogAsset : ScriptableObject
    {
        [LabelText("定义列表")]
        public TechnologyDefinitionAsset[] Definitions = Array.Empty<TechnologyDefinitionAsset>();
    }
}
