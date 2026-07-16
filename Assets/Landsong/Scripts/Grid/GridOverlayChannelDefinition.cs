using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Landsong.GridSystem
{
    /// <summary>
    /// 地图 Tilemap 统一使用负排序值，保证默认排序为 0 的建筑表现始终绘制在瓦片之上。
    /// </summary>
    public static class GridRenderOrder
    {
        public const int BaseTilemap = -1000;
        public const int WaterTerrainTilemap = -900;
        public const int StoneDepositTerrainTilemap = -850;
        public const int ObstacleTerrainTilemap = -800;
        public const int OtherTerrainTilemapBase = -790;
        public const int OtherTerrainTilemapMax = -600;
        public const int OccupancyTilemap = -500;
        public const int OverlayTilemapBase = -250;
        public const int OverlayTilemapMax = -1;
        public const int BuildingDefault = 0;

        public static int GetTerrainTilemapOrder(string terrainKey, int fallbackIndex)
        {
            switch (GridTerrainKeys.Normalize(terrainKey))
            {
                case GridTerrainKeys.Water:
                    return WaterTerrainTilemap;
                case GridTerrainKeys.StoneDeposit:
                    return StoneDepositTerrainTilemap;
                case GridTerrainKeys.Obstacle:
                    return ObstacleTerrainTilemap;
                default:
                    return Mathf.Min(
                        OtherTerrainTilemapMax,
                        OtherTerrainTilemapBase + Mathf.Max(0, fallbackIndex));
            }
        }

        public static int GetDecorativeTilemapOrder(int index)
        {
            return Mathf.Min(
                OtherTerrainTilemapMax,
                OtherTerrainTilemapBase + Mathf.Max(0, index));
        }

        public static int GetOverlayTilemapOrder(int relativeOrder)
        {
            int safeRelativeOrder = Mathf.Clamp(
                relativeOrder,
                -32000,
                OverlayTilemapMax - OverlayTilemapBase);
            return OverlayTilemapBase + safeRelativeOrder;
        }
    }

    [CreateAssetMenu(menuName = "Landsong/Grid/Overlay Channel", fileName = "GridOverlayChannel")]
    public sealed class GridOverlayChannelDefinition : ScriptableObject
    {
        
        [SerializeField, LabelText("稳定 Channel ID")]
        private string channelId;

        [SerializeField, LabelText("瓦片")]
        private TileBase tile;

        [SerializeField, LabelText("相对排序")]
        [Tooltip("相对于 Overlay 基础层级的排序；运行时会限制在建筑表现之下。")]
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
