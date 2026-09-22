using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [CreateAssetMenu(menuName = "Landsong/Catalogs/Talent")]
    public sealed class TalentCatalogAsset : ScriptableObject
    {
        [LabelText("定义列表")]
        public TalentDefinitionAsset[] Definitions = Array.Empty<TalentDefinitionAsset>();
    }
}
