using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [CreateAssetMenu(menuName = "Landsong/Catalogs/TalentSlot")]
    public sealed class TalentSlotCatalogAsset : ScriptableObject
    {
        [LabelText("定义列表")]
        public TalentSlotDefinitionAsset[] Definitions = Array.Empty<TalentSlotDefinitionAsset>();
    }
}
