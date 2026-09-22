using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [CreateAssetMenu(menuName = "Landsong/Catalogs/PolicyGroup")]
    public sealed class PolicyGroupCatalogAsset : ScriptableObject
    {
        [LabelText("定义列表")]
        public PolicyGroupDefinitionAsset[] Definitions = Array.Empty<PolicyGroupDefinitionAsset>();
    }
}
