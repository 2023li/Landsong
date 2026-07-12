using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Landsong.GridSystem
{
    [CreateAssetMenu(menuName = "Landsong/Grid/Overlay Style", fileName = "GridOverlayStyle")]
    public sealed class GridOverlayStyle : ScriptableObject
    {
        [SerializeField, LabelText("瓦片")] private TileBase tile;
        [SerializeField, LabelText("颜色")] private Color color = Color.white;

        public TileBase Tile => tile;
        public Color Color => color;
        public bool IsValid => tile != null;
    }

    [CreateAssetMenu(menuName = "Landsong/Grid/Overlay Channel", fileName = "GridOverlayChannel")]
    public sealed class GridOverlayChannelDefinition : ScriptableObject
    {
        [SerializeField, LabelText("稳定 Channel ID")]
        private string channelId;

        [SerializeField, LabelText("样式")]
        private GridOverlayStyle style;

        [SerializeField, LabelText("排序")]
        private int sortingOrder;

        public string ChannelId => string.IsNullOrWhiteSpace(channelId) ? string.Empty : channelId.Trim();
        public GridOverlayStyle Style => style;
        public int SortingOrder => sortingOrder;
        public bool IsValid => !string.IsNullOrWhiteSpace(ChannelId) && style != null && style.IsValid;

        private void OnValidate()
        {
            channelId = ChannelId;
        }
    }
}
