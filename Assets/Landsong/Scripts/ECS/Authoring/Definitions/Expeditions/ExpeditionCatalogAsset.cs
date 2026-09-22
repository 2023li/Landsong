using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [CreateAssetMenu(menuName = "Landsong/Catalogs/Expedition")]
    public sealed class ExpeditionCatalogAsset : ScriptableObject
    {
        [LabelText("定义列表")]
        public ExpeditionDefinitionAsset[] Definitions = Array.Empty<ExpeditionDefinitionAsset>();
    }
}
