using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [CreateAssetMenu(menuName = "Landsong/Catalogs/Loot")]
    public sealed class LootCatalogAsset : ScriptableObject
    {
        [LabelText("定义列表")]
        public LootDefinitionAsset[] Definitions = Array.Empty<LootDefinitionAsset>();
    }
}
