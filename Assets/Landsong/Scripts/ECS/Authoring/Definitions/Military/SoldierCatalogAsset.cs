using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [CreateAssetMenu(menuName = "Landsong/Catalogs/Soldier")]
    public sealed class SoldierCatalogAsset : ScriptableObject
    {
        [LabelText("定义列表")]
        public SoldierDefinitionAsset[] Definitions = Array.Empty<SoldierDefinitionAsset>();
    }
}
