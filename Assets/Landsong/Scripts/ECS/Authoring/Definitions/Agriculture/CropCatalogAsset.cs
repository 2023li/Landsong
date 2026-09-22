using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [CreateAssetMenu(menuName = "Landsong/Catalogs/Crop")]
    public sealed class CropCatalogAsset : ScriptableObject
    {
        [LabelText("定义列表")]
        public CropDefinitionAsset[] Definitions = Array.Empty<CropDefinitionAsset>();
    }
}
