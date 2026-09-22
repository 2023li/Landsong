using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [CreateAssetMenu(menuName = "Landsong/Catalogs/Quest")]
    public sealed class QuestCatalogAsset : ScriptableObject
    {
        [LabelText("定义列表")]
        public QuestDefinitionAsset[] Definitions = Array.Empty<QuestDefinitionAsset>();
    }
}
