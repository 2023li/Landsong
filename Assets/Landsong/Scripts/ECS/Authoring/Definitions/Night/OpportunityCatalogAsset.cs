using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [CreateAssetMenu(menuName = "Landsong/Catalogs/Opportunity")]
    public sealed class OpportunityCatalogAsset : ScriptableObject
    {
        [LabelText("定义列表")]
        public OpportunityDefinitionAsset[] Definitions = Array.Empty<OpportunityDefinitionAsset>();
    }
}
