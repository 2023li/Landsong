using Sirenix.OdinInspector;
using System;
using UnityEngine;

namespace Landsong.ECS.Presentation
{
    [CreateAssetMenu(menuName = "Landsong/ECS/Map Menu Catalog")]
    public sealed class EcsMapMenuCatalog : ScriptableObject
    {
        [Serializable] public struct Entry { [LabelText("地图标识")] public string Id; [LabelText("显示名称")] public string DisplayName; [TextArea] [LabelText("地图说明")] public string Description; [LabelText("地图缩略图")] public Sprite Thumbnail; }
        [LabelText("可选地图")] public Entry[] Maps = Array.Empty<Entry>();
    }
}
