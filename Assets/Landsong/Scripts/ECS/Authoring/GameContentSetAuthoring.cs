using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.ECS.Authoring
{
    [DisallowMultipleComponent]
    public sealed class GameContentSetAuthoring : MonoBehaviour
    {
        [LabelText("游戏内容集合"), Required]
        public GameContentSetAsset Content;

        public static T Resolve<T>(Component owner) where T : ScriptableObject
        {
            var source = owner == null ? null : owner.GetComponent<GameContentSetAuthoring>();
            if (source == null || source.Content == null)
                throw new InvalidOperationException((owner == null ? "世界组合" : owner.name) + " 缺少 GameContentSetAuthoring 或游戏内容集合。");
            return source.Content.Get<T>();
        }
    }
}
