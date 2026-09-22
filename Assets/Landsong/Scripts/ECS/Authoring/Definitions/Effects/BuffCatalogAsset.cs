using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [CreateAssetMenu(menuName = "Landsong/Catalogs/Buff")]
    public sealed class BuffCatalogAsset : ScriptableObject
    {
        [LabelText("定义列表")]
        public BuffDefinitionAsset[] Definitions = Array.Empty<BuffDefinitionAsset>();
    }
}
