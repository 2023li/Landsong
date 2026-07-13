using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Landsong.GridSystem
{

    [CreateAssetMenu(menuName = "Landsong/Grid/Overlay Channel", fileName = "GridOverlayChannel")]
    public sealed class GridOverlayChannelDefinition : ScriptableObject
    {
        [SerializeField, LabelText("稳定 Channel ID")]
        private string channelId;

        [SerializeField, LabelText("瓦片")]
        private TileBase tile;

        [SerializeField, LabelText("排序")]
        private int sortingOrder;

        public string ChannelId => string.IsNullOrWhiteSpace(channelId) ? string.Empty : channelId.Trim();
        public TileBase Tile => tile;
        public int SortingOrder => sortingOrder;
        public bool IsValid => !string.IsNullOrWhiteSpace(ChannelId) && tile != null;

        private void OnValidate()
        {
            channelId = ChannelId;
        }
    }
}
