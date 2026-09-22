using System;
using System.Collections.Generic;
using System.Linq;
using GiantGrey.TileWorldCreator;
using Landsong.GridSystem;
using UnityEngine;

namespace Landsong.EditorTools
{
    internal static class TileWorldCreatorCellCompiler
    {
        private const float PositionTolerance = 0.0001f;
        private sealed class MutableCell
        {
            public GridPosition Position;
            public bool EdgeZone;
            public bool Buildable;
            public bool Traversable;
            public int ElevationLevel;
            public int SurfaceLayer;
            public string PrimaryTerrainKey;
            public readonly HashSet<string> OverlayTerrainKeys = new HashSet<string>(StringComparer.Ordinal);
            public bool HasElevationOverride;
            public bool HasSurfaceLayerOverride;
        }

        internal static bool TryBakeCells(TileWorldCreatorMapBakeProfile profile, Configuration configuration, out List<GridMapCellRecord> records, out string error)
        {
            records = new List<GridMapCellRecord>();
            error = string.Empty;
            if (profile.UseLayerBlueprintRules)
            {
                try
                {
                    records = LayerTerrainCompiler.Compile(configuration, profile.BlueprintRules, profile.OverlapRules, profile.EdgeWidth).Primary;
                    return true;
                }
                catch (InvalidOperationException exception)
                {
                    error = exception.Message;
                    return false;
                }
            }

            MapBoundaryCompiler boundary;
            try
            {
                boundary = new MapBoundaryCompiler(configuration, profile.EdgeWidth);
            }
            catch (InvalidOperationException exception)
            {
                error = exception.Message;
                return false;
            }

            var baseLayer = TileWorldCreatorLayerResolver.ResolveLayer(configuration, profile.BaseLayerGuid, profile.BaseLayerName);
            if (baseLayer == null)
            {
                error = $"Required base layer '{profile.BaseLayerName}' was not found.";
                return false;
            }

            if (!TryConvertWorldHeightToElevationLevel(baseLayer.defaultLayerHeight, profile.ElevationWorldStep, baseLayer.layerName, out var baseElevationLevel, out error))
            {
                return false;
            }

            var cells = new Dictionary<GridPosition, MutableCell>();
            foreach (var twcPosition in baseLayer.allPositions)
            {
                if (!TryConvertPosition(twcPosition, configuration, out var position, out error))
                {
                    return false;
                }

                if (cells.ContainsKey(position))
                {
                    error = $"Base layer contains duplicate logical cell {position}.";
                    return false;
                }

                cells.Add(position, new MutableCell { Position = position, Buildable = profile.BaseBuildable, Traversable = profile.BaseTraversable, ElevationLevel = baseElevationLevel, PrimaryTerrainKey = profile.BaseTerrainKey });
            }

            if (cells.Count == 0)
            {
                error = "The TWC base layer is empty.";
                return false;
            }

            var usedLayerGuids = new HashSet<string>(StringComparer.Ordinal);
            for (var bindingIndex = 0; bindingIndex < profile.LayerBindings.Count; bindingIndex++)
            {
                var binding = profile.LayerBindings[bindingIndex];
                if (binding == null)
                {
                    error = $"Layer binding {bindingIndex} is null.";
                    return false;
                }

                var layer = TileWorldCreatorLayerResolver.ResolveLayer(configuration, binding.LayerGuid, binding.LayerName);
                if (layer == null)
                {
                    error = $"Bound TWC layer '{binding.LayerName}' was not found.";
                    return false;
                }

                if (!usedLayerGuids.Add(layer.guid))
                {
                    error = $"TWC layer '{layer.layerName}' is bound more than once.";
                    return false;
                }

                foreach (var twcPosition in layer.allPositions)
                {
                    if (!TryConvertPosition(twcPosition, configuration, out var position, out error))
                    {
                        return false;
                    }

                    if (!cells.TryGetValue(position, out var cell))
                    {
                        error = $"Layer '{layer.layerName}' contains {position}, but that cell is outside the base layer.";
                        return false;
                    }

                    if (!ApplyBinding(cell, binding, layer, profile.ElevationWorldStep, out error))
                    {
                        return false;
                    }
                }
            }

            foreach (var position in boundary.Edge)
            {
                if (cells.TryGetValue(new GridPosition(position.x, position.y), out var cell))
                {
                    cell.EdgeZone = true;
                    cell.Buildable = false;
                }
            }

            records = cells.Values.OrderBy(cell => cell.Position.Z).ThenBy(cell => cell.Position.X).Select(cell => new GridMapCellRecord(cell.Position, cell.Buildable, cell.Traversable, cell.ElevationLevel, cell.SurfaceLayer, cell.PrimaryTerrainKey, cell.OverlayTerrainKeys, cell.EdgeZone)).ToList();
            return true;
        }

        private static bool ApplyBinding(MutableCell cell, TileWorldCreatorLayerBinding binding, BlueprintLayer layer, float elevationWorldStep, out string error)
        {
            error = string.Empty;
            var layerName = layer.layerName;
            var terrainKey = binding.TerrainKey;
            if (!string.IsNullOrEmpty(terrainKey))
            {
                if (binding.ReplacePrimaryTerrain)
                {
                    cell.PrimaryTerrainKey = terrainKey;
                    cell.OverlayTerrainKeys.Remove(terrainKey);
                }
                else if (!string.Equals(cell.PrimaryTerrainKey, terrainKey, StringComparison.Ordinal))
                {
                    cell.OverlayTerrainKeys.Add(terrainKey);
                }
            }

            ApplyPermission(ref cell.Buildable, binding.Buildable);
            ApplyPermission(ref cell.Traversable, binding.Traversable);
            if (binding.UseLayerHeight)
            {
                if (!TryConvertWorldHeightToElevationLevel(layer.defaultLayerHeight, elevationWorldStep, layerName, out var elevationLevel, out error))
                {
                    return false;
                }

                if (cell.HasElevationOverride && cell.ElevationLevel != elevationLevel)
                {
                    error = $"Cell {cell.Position} has conflicting elevation layers; latest is '{layerName}'.";
                    return false;
                }

                cell.ElevationLevel = elevationLevel;
                cell.HasElevationOverride = true;
            }

            if (binding.OverrideSurfaceLayer)
            {
                if (cell.HasSurfaceLayerOverride && cell.SurfaceLayer != binding.SurfaceLayer)
                {
                    error = $"Cell {cell.Position} has conflicting surface layers; latest is '{layerName}'.";
                    return false;
                }

                cell.SurfaceLayer = binding.SurfaceLayer;
                cell.HasSurfaceLayerOverride = true;
            }

            return true;
        }

        internal static bool TryConvertWorldHeightToElevationLevel(float worldHeight, float elevationWorldStep, string layerName, out int elevationLevel, out string error)
        {
            elevationLevel = 0;
            error = string.Empty;
            if (float.IsNaN(worldHeight) || float.IsInfinity(worldHeight) || float.IsNaN(elevationWorldStep) || float.IsInfinity(elevationWorldStep) || elevationWorldStep <= 0f)
            {
                error = $"TWC layer '{layerName}' has an invalid height or the bake profile height step is invalid.";
                return false;
            }

            var ratio = worldHeight / elevationWorldStep;
            elevationLevel = Mathf.RoundToInt(ratio);
            var convertedHeight = elevationLevel * elevationWorldStep;
            var tolerance = Mathf.Max(0.00001f, elevationWorldStep * 0.0001f);
            if (Mathf.Abs(convertedHeight - worldHeight) <= tolerance)
            {
                return true;
            }

            error = $"TWC layer '{layerName}' height {worldHeight:0.####} is not a multiple of the configured height step {elevationWorldStep:0.####}.";
            return false;
        }

        internal static void ApplyPermission(ref bool value, GridCellPermissionOverride permission)
        {
            switch (permission)
            {
                case GridCellPermissionOverride.Allow:
                    value = true;
                    break;
                case GridCellPermissionOverride.Deny:
                    value = false;
                    break;
            }
        }

        internal static bool TryConvertPosition(Vector2 twcPosition, Configuration configuration, out GridPosition position, out string error)
        {
            var x = Mathf.RoundToInt(twcPosition.x);
            var z = Mathf.RoundToInt(twcPosition.y);
            position = new GridPosition(x, z);
            error = string.Empty;
            if (Mathf.Abs(twcPosition.x - x) > PositionTolerance || Mathf.Abs(twcPosition.y - z) > PositionTolerance)
            {
                error = $"TWC position {twcPosition} is not an integer logical X/Z cell.";
                return false;
            }

            if (x < 0 || z < 0 || x >= configuration.width || z >= configuration.height)
            {
                error = $"TWC cell {position} is outside configuration bounds {configuration.width}x{configuration.height}.";
                return false;
            }

            return true;
        }
    }
}
