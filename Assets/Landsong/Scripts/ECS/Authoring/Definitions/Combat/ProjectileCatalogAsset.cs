using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [CreateAssetMenu(menuName = "Landsong/Catalogs/Projectile")]
    public sealed class ProjectileCatalogAsset : ScriptableObject
    {
        [LabelText("定义列表")]
        public ProjectileDefinitionAsset[] Definitions = Array.Empty<ProjectileDefinitionAsset>();
    }
}
