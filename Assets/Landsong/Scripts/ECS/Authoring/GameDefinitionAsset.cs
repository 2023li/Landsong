using UnityEngine;

namespace Landsong.ECS.Authoring
{
    [CreateAssetMenu(menuName = "Landsong/ECS/Content Definition")]
    public sealed class GameDefinitionAsset : ScriptableObject
    {
        public ContentSource Data = new ContentSource();
    }
}
