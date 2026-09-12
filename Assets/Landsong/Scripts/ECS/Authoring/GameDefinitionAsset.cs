using UnityEngine;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Authoring
{
    [CreateAssetMenu(menuName = "Landsong/ECS/Content Definition")]
    public sealed class GameDefinitionAsset : ScriptableObject
    {
        [InlineProperty, HideLabel, LabelText("内容配置")] public ContentSource Data = new ContentSource();
    }
}
