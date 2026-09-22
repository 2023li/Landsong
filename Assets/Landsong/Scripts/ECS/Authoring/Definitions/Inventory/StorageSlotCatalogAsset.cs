using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [CreateAssetMenu(menuName = "Landsong/Catalogs/StorageSlot")]
    public sealed class StorageSlotCatalogAsset : ScriptableObject
    {
        [LabelText("定义列表")]
        public StorageSlotDefinitionAsset[] Definitions = Array.Empty<StorageSlotDefinitionAsset>();
    }
}
