using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [CreateAssetMenu(menuName = "Landsong/Catalogs/Hero")]
    public sealed class HeroCatalogAsset : ScriptableObject
    {
        [LabelText("定义列表")]
        public HeroDefinitionAsset[] Definitions = Array.Empty<HeroDefinitionAsset>();
    }
}
