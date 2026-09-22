using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [CreateAssetMenu(menuName = "Landsong/Catalogs/ItemGroup")]
    public sealed class ItemGroupCatalogAsset : ScriptableObject
    {
        [LabelText("定义列表")]
        public ItemGroupDefinitionAsset[] Definitions = Array.Empty<ItemGroupDefinitionAsset>();
    }
}
