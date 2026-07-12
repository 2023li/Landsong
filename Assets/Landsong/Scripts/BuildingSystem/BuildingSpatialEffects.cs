using System;
using System.Collections.Generic;
using Landsong.GridSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.BuildingSystem
{
    public enum BuildingSpatialEffectKind
    {
        ProductionPercent = 10,
        Beauty = 20
    }

    public enum BuildingSpatialTargetFilter
    {
        AnyBuilding = 0,
        Farmland = 10,
        Cell = 20
    }

    public enum BuildingSpatialStackingRule
    {
        NoStack = 0,
        Additive = 10,
        HighestValue = 20
    }

    [CreateAssetMenu(menuName = "Landsong/Building/Spatial Effect", fileName = "BuildingSpatialEffect")]
    public sealed class BuildingSpatialEffectDefinition : ScriptableObject
    {
        [SerializeField, LabelText("稳定 Effect ID")] private string effectId;
        [SerializeField, LabelText("显示名")] private string displayName;
        [SerializeField, LabelText("效果类型")] private BuildingSpatialEffectKind kind;
        [SerializeField, LabelText("目标过滤")] private BuildingSpatialTargetFilter targetFilter;
        [SerializeField, LabelText("曼哈顿半径"), Min(0)] private int range = 1;
        [SerializeField, LabelText("效果数值"), Min(0)] private int value = 1;
        [SerializeField, LabelText("叠加规则")] private BuildingSpatialStackingRule stackingRule;

        public string EffectId => string.IsNullOrWhiteSpace(effectId) ? string.Empty : effectId.Trim();
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? EffectId : displayName.Trim();
        public BuildingSpatialEffectKind Kind => kind;
        public BuildingSpatialTargetFilter TargetFilter => targetFilter;
        public int Range => Mathf.Max(0, range);
        public int Value => Mathf.Max(0, value);
        public BuildingSpatialStackingRule StackingRule => stackingRule;
        public bool IsValid => !string.IsNullOrWhiteSpace(EffectId) && Value > 0;

        public string FormatEffectDescription()
        {
            return kind == BuildingSpatialEffectKind.ProductionPercent
                ? $"{DisplayName} +{Value}%"
                : $"{DisplayName} +{Value}";
        }

        private void OnValidate()
        {
            effectId = EffectId;
            range = Range;
            value = Value;
        }
    }

    [Serializable]
    public sealed class BM_空间效果源 : BuildingModuleBase
    {
        [SerializeField, LabelText("空间效果")]
        private BuildingSpatialEffectDefinition[] effects = Array.Empty<BuildingSpatialEffectDefinition>();

        public IReadOnlyList<BuildingSpatialEffectDefinition> Effects =>
            effects ?? Array.Empty<BuildingSpatialEffectDefinition>();

        public override string ModuleDescription => "从建筑完整占地向外提供忽略障碍的曼哈顿空间效果。";

        public override void Normalize()
        {
            effects ??= Array.Empty<BuildingSpatialEffectDefinition>();
        }
    }

    public sealed class BuildingSpatialEffectPreview
    {
        public BuildingSpatialEffectPreview(
            BuildingSpatialEffectDefinition definition,
            IReadOnlyList<GridPosition> affectedCells)
        {
            Definition = definition;
            AffectedCells = affectedCells ?? Array.Empty<GridPosition>();
        }

        public BuildingSpatialEffectDefinition Definition { get; }
        public IReadOnlyList<GridPosition> AffectedCells { get; }
        public string Description => Definition == null ? string.Empty : Definition.FormatEffectDescription();
    }

    public static class BuildingSpatialEffectService
    {
        public static IReadOnlyList<BuildingSpatialEffectPreview> BuildPlacementPreviews(
            BuildingBase sourcePrefab,
            GridMapBehaviour gridMap,
            GridFootprint sourceFootprint)
        {
            if (sourcePrefab == null || gridMap == null)
            {
                return Array.Empty<BuildingSpatialEffectPreview>();
            }

            var definitions = GetDefinitions(sourcePrefab);
            if (definitions.Count == 0)
            {
                return Array.Empty<BuildingSpatialEffectPreview>();
            }

            var previews = new List<BuildingSpatialEffectPreview>(definitions.Count);
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition != null && definition.IsValid)
                {
                    previews.Add(new BuildingSpatialEffectPreview(
                        definition,
                        CollectAffectedCells(gridMap, sourceFootprint, definition.Range)));
                }
            }

            return previews;
        }

        public static int ApplyProductionPercent(BuildingBase target, int baseAmount)
        {
            baseAmount = Mathf.Max(0, baseAmount);
            if (baseAmount <= 0 || target == null || !target.HasPlacement)
            {
                return baseAmount;
            }

            var additivePercent = 0;
            var noStackByEffectId = new Dictionary<string, int>(StringComparer.Ordinal);
            ForEachAffectingDefinition(
                target,
                BuildingSpatialEffectKind.ProductionPercent,
                definition =>
                {
                    if (definition.StackingRule == BuildingSpatialStackingRule.Additive)
                    {
                        additivePercent += definition.Value;
                    }
                    else
                    {
                        noStackByEffectId.TryGetValue(definition.EffectId, out var current);
                        noStackByEffectId[definition.EffectId] = Mathf.Max(current, definition.Value);
                    }
                });

            var noStackPercent = 0;
            foreach (var pair in noStackByEffectId)
            {
                noStackPercent += pair.Value;
            }

            return Mathf.FloorToInt(baseAmount * (1f + (additivePercent + noStackPercent) / 100f));
        }

        public static int GetBeautyAtCell(
            GridMapBehaviour gridMap,
            GridPosition cell,
            IReadOnlyList<BuildingBase> buildings)
        {
            if (gridMap == null || !gridMap.HasBaseTileAt(cell) || buildings == null)
            {
                return 0;
            }

            var beauty = 0;
            for (var i = 0; i < buildings.Count; i++)
            {
                var source = buildings[i];
                if (!IsUsableSource(source, gridMap))
                {
                    continue;
                }

                var definitions = GetDefinitions(source);
                for (var j = 0; j < definitions.Count; j++)
                {
                    var definition = definitions[j];
                    if (definition != null
                        && definition.IsValid
                        && definition.Kind == BuildingSpatialEffectKind.Beauty
                        && IsCellInRange(source.Footprint, cell, definition.Range))
                    {
                        beauty = Mathf.Max(beauty, definition.Value);
                    }
                }
            }

            return beauty;
        }

        public static int GetBuildingBeauty(BuildingBase target)
        {
            var buildings = target?.GameSystem?.Services?.Buildings?.Buildings;
            if (target == null || !target.HasPlacement || target.GridMap == null || buildings == null)
            {
                return 0;
            }

            var total = 0;
            var count = 0;
            foreach (var cell in target.Footprint.Positions())
            {
                total += GetBeautyAtCell(target.GridMap, cell, buildings);
                count++;
            }

            return count <= 0 ? 0 : Mathf.FloorToInt((float)total / count);
        }

        public static List<GridPosition> CollectAffectedCells(
            GridMapBehaviour gridMap,
            GridFootprint sourceFootprint,
            int range)
        {
            var cells = new List<GridPosition>();
            if (gridMap == null)
            {
                return cells;
            }

            range = Mathf.Max(0, range);
            var minX = sourceFootprint.Origin.X - range;
            var minY = sourceFootprint.Origin.Y - range;
            var maxX = sourceFootprint.Origin.X + sourceFootprint.Size.x - 1 + range;
            var maxY = sourceFootprint.Origin.Y + sourceFootprint.Size.y - 1 + range;
            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var cell = new GridPosition(x, y);
                    if (gridMap.HasBaseTileAt(cell) && IsCellInRange(sourceFootprint, cell, range))
                    {
                        cells.Add(cell);
                    }
                }
            }

            return cells;
        }

        private static void ForEachAffectingDefinition(
            BuildingBase target,
            BuildingSpatialEffectKind kind,
            Action<BuildingSpatialEffectDefinition> action)
        {
            var buildings = target.GameSystem?.Services?.Buildings?.Buildings;
            if (buildings == null || action == null)
            {
                return;
            }

            for (var i = 0; i < buildings.Count; i++)
            {
                var source = buildings[i];
                if (!IsUsableSource(source, target.GridMap))
                {
                    continue;
                }

                var definitions = GetDefinitions(source);
                for (var j = 0; j < definitions.Count; j++)
                {
                    var definition = definitions[j];
                    if (definition != null
                        && definition.IsValid
                        && definition.Kind == kind
                        && CanAffectTarget(definition, target)
                        && AreFootprintsInRange(source.Footprint, target.Footprint, definition.Range))
                    {
                        action(definition);
                    }
                }
            }
        }

        private static bool IsUsableSource(BuildingBase source, GridMapBehaviour gridMap)
        {
            return source != null
                   && source.isActiveAndEnabled
                   && !source.IsDemolishing
                   && source.HasPlacement
                   && source.GridMap == gridMap;
        }

        private static bool CanAffectTarget(BuildingSpatialEffectDefinition definition, BuildingBase target)
        {
            return definition.TargetFilter switch
            {
                BuildingSpatialTargetFilter.Farmland => target is IBuildingCropFieldSource,
                BuildingSpatialTargetFilter.Cell => false,
                _ => true
            };
        }

        private static List<BuildingSpatialEffectDefinition> GetDefinitions(BuildingBase source)
        {
            var definitions = new List<BuildingSpatialEffectDefinition>();
            var modules = source.GetModules<BM_空间效果源>();
            for (var i = 0; i < modules.Count; i++)
            {
                var effects = modules[i].Effects;
                for (var j = 0; j < effects.Count; j++)
                {
                    if (effects[j] != null && effects[j].IsValid)
                    {
                        definitions.Add(effects[j]);
                    }
                }
            }

            return definitions;
        }

        private static bool AreFootprintsInRange(
            GridFootprint source,
            GridFootprint target,
            int range)
        {
            foreach (var targetCell in target.Positions())
            {
                if (IsCellInRange(source, targetCell, range))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsCellInRange(GridFootprint source, GridPosition cell, int range)
        {
            var minX = source.Origin.X;
            var minY = source.Origin.Y;
            var maxX = minX + source.Size.x - 1;
            var maxY = minY + source.Size.y - 1;
            var dx = cell.X < minX ? minX - cell.X : cell.X > maxX ? cell.X - maxX : 0;
            var dy = cell.Y < minY ? minY - cell.Y : cell.Y > maxY ? cell.Y - maxY : 0;
            return dx + dy <= Mathf.Max(0, range);
        }
    }
}
