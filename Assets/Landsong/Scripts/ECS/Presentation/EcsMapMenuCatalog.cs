using System;
using UnityEngine;

namespace Landsong.ECS.Presentation
{
    [CreateAssetMenu(menuName = "Landsong/ECS/Map Menu Catalog")]
    public sealed class EcsMapMenuCatalog : ScriptableObject
    {
        [Serializable] public struct Entry { public string Id, DisplayName; [TextArea] public string Description; public Sprite Thumbnail; }
        public Entry[] Maps = Array.Empty<Entry>();
    }
}
