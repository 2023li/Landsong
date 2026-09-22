using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [CreateAssetMenu(menuName = "Landsong/Catalogs/Item")]
    public sealed class ItemCatalogAsset : ScriptableObject
    {
        [LabelText("定义列表")]
        public ItemDefinitionAsset[] Definitions = Array.Empty<ItemDefinitionAsset>();
    }
}
