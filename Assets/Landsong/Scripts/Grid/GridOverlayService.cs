using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Landsong.GridSystem
{
    public sealed class GridOverlayOwnerHandle : IDisposable
    {
        private GridOverlayService service;

        internal GridOverlayOwnerHandle(
            GridOverlayService service,
            GridOverlayChannelDefinition channel,
            string ownerKey)
        {
            this.service = service;
            Channel = channel;
            OwnerKey = ownerKey;
        }

        public GridOverlayChannelDefinition Channel { get; }
        public string OwnerKey { get; }

        public void SetCells(IEnumerable<GridPosition> cells, int priority = 0)
        {
            service?.Submit(Channel, OwnerKey, cells, priority);
        }

        public void SetFootprint(GridFootprint footprint, int priority = 0)
        {
            SetCells(footprint.Positions(), priority);
        }

        public void Clear()
        {
            service?.Clear(Channel, OwnerKey);
        }

        public void Dispose()
        {
            if (service == null)
            {
                return;
            }

            service.Clear(Channel, OwnerKey);
            service = null;
        }
    }

    [DisallowMultipleComponent]
    public sealed class GridOverlayService : MonoBehaviour
    {
        private sealed class OwnerSubmission
        {
            public int Priority;
            public readonly HashSet<GridPosition> Cells = new HashSet<GridPosition>();
        }

        private readonly struct WinningSubmission
        {
            public WinningSubmission(int priority, string ownerKey)
            {
                Priority = priority;
                OwnerKey = ownerKey;
            }

            public int Priority { get; }
            public string OwnerKey { get; }
        }

        private sealed class ChannelState
        {
            public GridOverlayChannelDefinition Definition;
            public Tilemap Tilemap;
            public readonly Dictionary<string, OwnerSubmission> Owners =
                new Dictionary<string, OwnerSubmission>(StringComparer.Ordinal);
            public readonly Dictionary<GridPosition, WinningSubmission> Rendered =
                new Dictionary<GridPosition, WinningSubmission>();
        }

        [SerializeField] private UnityEngine.Grid overlayGrid;
        [SerializeField] private GridOverlayChannelCatalog channelCatalog;

        private readonly Dictionary<string, ChannelState> channels =
            new Dictionary<string, ChannelState>(StringComparer.Ordinal);
        private readonly List<GridPosition> changedCells = new List<GridPosition>();

        public UnityEngine.Grid OverlayGrid => overlayGrid;
        public GridOverlayChannelCatalog ChannelCatalog => channelCatalog;

        public bool TryValidateConfiguration(out string error)
        {
            if (channelCatalog == null)
            {
                error = "GridOverlayService 没有绑定 GridOverlayChannelCatalog。";
                return false;
            }

            return channelCatalog.TryValidate(out error);
        }

        public void BindGrid(UnityEngine.Grid targetGrid)
        {
            if (targetGrid == null)
            {
                throw new ArgumentNullException(nameof(targetGrid));
            }

            if (overlayGrid == targetGrid)
            {
                return;
            }

            ClearAll();
            overlayGrid = targetGrid;
        }

        public GridOverlayOwnerHandle AcquireOwner(
            string channelId,
            string ownerKey)
        {
            if (channelCatalog == null
                || !channelCatalog.TryGet(channelId, out var channel))
            {
                return null;
            }

            ownerKey = NormalizeOwnerKey(ownerKey);
            if (string.IsNullOrEmpty(ownerKey))
            {
                return null;
            }

            EnsureChannel(channel);
            return new GridOverlayOwnerHandle(this, channel, ownerKey);
        }

        public void Submit(
            GridOverlayChannelDefinition channel,
            string ownerKey,
            IEnumerable<GridPosition> cells,
            int priority = 0)
        {
            if (channel == null || !channel.IsValid)
            {
                return;
            }

            ownerKey = NormalizeOwnerKey(ownerKey);
            if (string.IsNullOrEmpty(ownerKey))
            {
                return;
            }

            var state = EnsureChannel(channel);
            if (!state.Owners.TryGetValue(ownerKey, out var submission))
            {
                submission = new OwnerSubmission();
                state.Owners.Add(ownerKey, submission);
            }

            submission.Priority = priority;
            submission.Cells.Clear();
            if (cells != null)
            {
                foreach (var cell in cells)
                {
                    submission.Cells.Add(cell);
                }
            }

            RebuildChannel(state);
        }

        public void Clear(GridOverlayChannelDefinition channel, string ownerKey)
        {
            if (channel == null || string.IsNullOrWhiteSpace(channel.ChannelId))
            {
                return;
            }

            ownerKey = NormalizeOwnerKey(ownerKey);
            if (!channels.TryGetValue(channel.ChannelId, out var state)
                || !state.Owners.Remove(ownerKey))
            {
                return;
            }

            RebuildChannel(state);
        }

        public void ClearAll()
        {
            foreach (var pair in channels)
            {
                var state = pair.Value;
                if (state?.Tilemap != null)
                {
                    state.Tilemap.ClearAllTiles();
                    Destroy(state.Tilemap.gameObject);
                }
            }

            channels.Clear();
        }

        private void OnDestroy()
        {
            ClearAll();
        }

        private ChannelState EnsureChannel(GridOverlayChannelDefinition definition)
        {
            if (overlayGrid == null)
            {
                overlayGrid = GetComponentInParent<UnityEngine.Grid>();
            }

            if (overlayGrid == null)
            {
                throw new InvalidOperationException("GridOverlayService requires a bound Unity Grid.");
            }

            if (channels.TryGetValue(definition.ChannelId, out var existing))
            {
                if (existing.Definition != definition)
                {
                    throw new InvalidOperationException(
                        $"Overlay ChannelId '{definition.ChannelId}' 被多个不同资产重复注册。");
                }

                return existing;
            }

            var layer = new GameObject($"Overlay_{definition.ChannelId}");
            layer.transform.SetParent(overlayGrid.transform, false);
            var tilemap = layer.AddComponent<Tilemap>();
            var renderer = layer.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = GridRenderOrder.GetOverlayTilemapOrder(definition.SortingOrder);

            var state = new ChannelState
            {
                Definition = definition,
                Tilemap = tilemap
            };
            channels.Add(definition.ChannelId, state);
            return state;
        }

        private void RebuildChannel(ChannelState state)
        {
            if (state == null || state.Tilemap == null || state.Definition?.Tile == null)
            {
                return;
            }

            var next = new Dictionary<GridPosition, WinningSubmission>();
            foreach (var ownerPair in state.Owners)
            {
                var ownerKey = ownerPair.Key;
                var submission = ownerPair.Value;
                foreach (var cell in submission.Cells)
                {
                    if (!next.TryGetValue(cell, out var current)
                        || IsBetter(submission.Priority, ownerKey, current))
                    {
                        next[cell] = new WinningSubmission(submission.Priority, ownerKey);
                    }
                }
            }

            changedCells.Clear();
            foreach (var pair in state.Rendered)
            {
                if (!next.ContainsKey(pair.Key))
                {
                    changedCells.Add(pair.Key);
                }
            }

            for (var i = 0; i < changedCells.Count; i++)
            {
                state.Tilemap.SetTile(ToCell(changedCells[i]), null);
            }

            var tile = state.Definition.Tile;
            foreach (var pair in next)
            {
                if (state.Rendered.TryGetValue(pair.Key, out var previous)
                    && previous.Priority == pair.Value.Priority
                    && string.Equals(previous.OwnerKey, pair.Value.OwnerKey, StringComparison.Ordinal))
                {
                    continue;
                }

                var cell = ToCell(pair.Key);
                state.Tilemap.SetTile(cell, tile);
            }

            state.Rendered.Clear();
            foreach (var pair in next)
            {
                state.Rendered.Add(pair.Key, pair.Value);
            }
        }

        private static bool IsBetter(int priority, string ownerKey, WinningSubmission current)
        {
            return priority > current.Priority
                   || (priority == current.Priority
                       && string.Compare(ownerKey, current.OwnerKey, StringComparison.Ordinal) < 0);
        }

        private static string NormalizeOwnerKey(string ownerKey)
        {
            return string.IsNullOrWhiteSpace(ownerKey) ? string.Empty : ownerKey.Trim();
        }

        private static Vector3Int ToCell(GridPosition position)
        {
            return new Vector3Int(position.X, position.Y, 0);
        }
    }
}
