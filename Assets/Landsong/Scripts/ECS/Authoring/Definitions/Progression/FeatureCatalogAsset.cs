using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [CreateAssetMenu(menuName = "Landsong/Catalogs/Feature")]
    public sealed class FeatureCatalogAsset : ScriptableObject
    {
        [LabelText("定义列表")]
        public FeatureDefinitionAsset[] Definitions = Array.Empty<FeatureDefinitionAsset>();
    }
}
