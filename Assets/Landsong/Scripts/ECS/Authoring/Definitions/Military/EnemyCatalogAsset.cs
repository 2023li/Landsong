using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [CreateAssetMenu(menuName = "Landsong/Catalogs/Enemy")]
    public sealed class EnemyCatalogAsset : ScriptableObject
    {
        [LabelText("定义列表")]
        public EnemyDefinitionAsset[] Definitions = Array.Empty<EnemyDefinitionAsset>();
    }
}
