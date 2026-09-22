using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [CreateAssetMenu(menuName = "Landsong/Catalogs/Policy")]
    public sealed class PolicyCatalogAsset : ScriptableObject
    {
        [LabelText("定义列表")]
        public PolicyDefinitionAsset[] Definitions = Array.Empty<PolicyDefinitionAsset>();
    }
}
