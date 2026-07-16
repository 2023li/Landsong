using System;
using System.Collections.Generic;
using Landsong.GridSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.BuildingSystem
{
    public enum BuildingSpatialEffectKind
    {
        [LabelText("生产百分比")]
        ProductionPercent = 10,
        [LabelText("美化")]
        Beauty = 20,
        [LabelText("医疗")]
        Medical = 30,
        [LabelText("治安")]
        Security = 40
    }

    public enum BuildingSpatialTargetFilter
    {
        [LabelText("任意建筑")]
        AnyBuilding = 0,
        [LabelText("农田")]
        Farmland = 10,
        [LabelText("地图格")]
        Cell = 20
    }

    public enum BuildingSpatialStackingRule
    {
        [LabelText("同 Effect ID 不叠加")]
        NoStack = 0,
        [LabelText("累加")]
        Additive = 10,
        [LabelText("取最高值")]
        HighestValue = 20
    }

    [CreateAssetMenu(menuName = "Landsong/Building/Spatial Effect", fileName = "BuildingSpatialEffect")]
    public sealed class BuildingSpatialEffectDefinition : ScriptableObject
    {
        [SerializeField, LabelText("稳定 Effect ID")]
        [PropertyTooltip("空间效果的稳定匹配键。被正式数值表、校验器和后续存档引用后不得随显示文案修改。")]
        private string effectId;

        [SerializeField, LabelText("显示名")]
        [PropertyTooltip("放置预览和建筑详情中显示给玩家或策划的名称。")]
        private string displayName;

        [SerializeField, LabelText("效果类型")]
        [PropertyTooltip("决定数值的结算入口，例如生产百分比或格子美化。")]
        private BuildingSpatialEffectKind kind;

        [SerializeField, LabelText("目标过滤")]
        [PropertyTooltip("限制空间效果可作用的目标。格子型美化、医疗和治安应选择 Cell。")]
        private BuildingSpatialTargetFilter targetFilter;

        [SerializeField, LabelText("生效运营等级"), Min(0)]
        [PropertyTooltip("0 表示所有运营等级；大于 0 时只在建筑处于该等级时生效。")]
        private int operationalLevel;

        [SerializeField, LabelText("最低工人"), Min(0)]
        [PropertyTooltip("0 表示不要求岗位；大于 0 时建筑必须拥有岗位模块且当前工人数达到该值。")]
        private int minimumWorkers;

        [SerializeField, LabelText("曼哈顿半径"), Min(0)]
        [PropertyTooltip("从建筑完整占地向外计算的正交格距离。半径 1 表示紧邻占地边缘的上、下、左、右格。")]
        private int range = 1;

        [SerializeField, LabelText("效果数值"), Min(0)]
        [PropertyTooltip("每个受影响目标获得的基础数值。")]
        private int value = 1;

        [SerializeField, LabelText("叠加规则")]
        [PropertyTooltip("Additive 累加全部来源；NoStack 对同 Effect ID 只取最高；HighestValue 对该类来源只取最高。")]
        private BuildingSpatialStackingRule stackingRule;

        [SerializeField, LabelText("影响自身占地")]
        [PropertyTooltip("关闭时只影响占地外围，不会把建筑自身占用的格子算进范围。")]
        private bool includeSourceFootprint;

        public string EffectId => string.IsNullOrWhiteSpace(effectId) ? string.Empty : effectId.Trim();
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? EffectId : displayName.Trim();
        public BuildingSpatialEffectKind Kind => kind;
        public BuildingSpatialTargetFilter TargetFilter => targetFilter;
        public int OperationalLevel => Mathf.Max(0, operationalLevel);
        public int MinimumWorkers => Mathf.Max(0, minimumWorkers);
        public int Range => Mathf.Max(0, range);
        public int Value => Mathf.Max(0, value);
        public BuildingSpatialStackingRule StackingRule => stackingRule;
        public bool IncludeSourceFootprint => includeSourceFootprint;
        public bool IsValid => !string.IsNullOrWhiteSpace(EffectId) && Value > 0;

        public void ConfigureNumericData(
            string configuredEffectId,
            string configuredDisplayName,
            BuildingSpatialEffectKind configuredKind,
            BuildingSpatialTargetFilter configuredTargetFilter,
            int configuredOperationalLevel,
            int configuredMinimumWorkers,
            int configuredRange,
            int configuredValue,
            BuildingSpatialStackingRule configuredStackingRule,
            bool configuredIncludeSourceFootprint)
        {
            effectId = string.IsNullOrWhiteSpace(configuredEffectId)
                ? string.Empty
                : configuredEffectId.Trim();
            displayName = string.IsNullOrWhiteSpace(configuredDisplayName)
                ? effectId
                : configuredDisplayName.Trim();
            kind = configuredKind;
            targetFilter = configuredTargetFilter;
            operationalLevel = Mathf.Max(0, configuredOperationalLevel);
            minimumWorkers = Mathf.Max(0, configuredMinimumWorkers);
            range = Mathf.Max(0, configuredRange);
            value = Mathf.Max(0, configuredValue);
            stackingRule = configuredStackingRule;
            includeSourceFootprint = configuredIncludeSourceFootprint;
        }

        public string FormatEffectDescription()
        {
            return kind == BuildingSpatialEffectKind.ProductionPercent
                ? $"{DisplayName} +{Value}%"
                : $"{DisplayName} +{Value}";
        }

        public bool IsActiveFor(BuildingBase source)
        {
            if (source == null || !source.IsOperational)
            {
                return false;
            }

            if (OperationalLevel > 0 && source.CurrentLevel != OperationalLevel)
            {
                return false;
            }

            if (MinimumWorkers <= 0)
            {
                return true;
            }

            return BuildingWorkforceUtility.TryGetSource(source, out var workforce)
                   && workforce.CurrentWorkers >= MinimumWorkers;
        }

        private void OnValidate()
        {
            effectId = EffectId;
            operationalLevel = OperationalLevel;
            minimumWorkers = MinimumWorkers;
            range = Range;
            value = Value;
        }
    }

    [Serializable]
    [BuildingModuleId("spatial_effect")]
    public sealed class BM_空间效果源 : BuildingModuleBase
    {
        [SerializeField, LabelText("空间效果")]
        private BuildingSpatialEffectDefinition[] effects = Array.Empty<BuildingSpatialEffectDefinition>();

        public IReadOnlyList<BuildingSpatialEffectDefinition> Effects =>
            effects ?? Array.Empty<BuildingSpatialEffectDefinition>();

        public bool RequiresWorkforce
        {
            get
            {
                for (var i = 0; i < Effects.Count; i++)
                {
                    if (Effects[i] != null && Effects[i].MinimumWorkers > 0)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public override string ModuleDescription => "从建筑完整占地向外提供忽略障碍的曼哈顿空间效果。";

        public override void Normalize()
        {
            effects ??= Array.Empty<BuildingSpatialEffectDefinition>();
        }

        public override string GetOverviewFragment(BuildingBase building)
        {
            if (effects == null)
            {
                return string.Empty;
            }

            for (var i = 0; i < effects.Length; i++)
            {
                if (effects[i] != null && effects[i].IsValid && effects[i].IsActiveFor(building))
                {
                    return effects[i].FormatEffectDescription();
                }
            }

            return string.Empty;
        }

        public override void AppendFunctionBlockEntries(
            BuildingBase building,
            ref List<BuildingFunctionBlockEntry> entries)
        {
            if (!IsEnabled || effects == null)
            {
                return;
            }

            for (var i = 0; i < effects.Length; i++)
            {
                var effect = effects[i];
                if (effect == null || !effect.IsValid || !effect.IsActiveFor(building))
                {
                    continue;
                }

                AddFunctionBlockEntry(
                    ref entries,
                    new BuildingFunctionBlockEntry(
                        BuildingFunctionBlockGroup.功能性,
                        effect.DisplayName,
                        effect.Value,
                        new[]
                        {
                            new BuildingFunctionBlockSidebarRow("曼哈顿半径", effect.Range.ToString()),
                            new BuildingFunctionBlockSidebarRow(
                                "工人条件",
                                effect.MinimumWorkers <= 0 ? "无" : $"至少 {effect.MinimumWorkers}"),
                            new BuildingFunctionBlockSidebarRow(
                                "影响自身占地",
                                effect.IncludeSourceFootprint ? "是" : "否"),
                            new BuildingFunctionBlockSidebarRow("叠加规则", FormatStackingRule(effect.StackingRule))
                        }));
            }
        }

        private static string FormatStackingRule(BuildingSpatialStackingRule rule)
        {
            return rule switch
            {
                BuildingSpatialStackingRule.Additive => "累加",
                BuildingSpatialStackingRule.HighestValue => "取最高值",
                _ => "同 Effect ID 不叠加"
            };
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

            var definitions = GetDefinitions(sourcePrefab, true);
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
                        CollectAffectedCells(gridMap, sourceFootprint, definition)));
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
            return GetCellEffectAtCell(
                gridMap,
                cell,
                buildings,
                BuildingSpatialEffectKind.Beauty);
        }

        public static int GetMedicalAtCell(
            GridMapBehaviour gridMap,
            GridPosition cell,
            IReadOnlyList<BuildingBase> buildings)
        {
            return GetCellEffectAtCell(
                gridMap,
                cell,
                buildings,
                BuildingSpatialEffectKind.Medical);
        }

        public static int GetSecurityAtCell(
            GridMapBehaviour gridMap,
            GridPosition cell,
            IReadOnlyList<BuildingBase> buildings)
        {
            return GetCellEffectAtCell(
                gridMap,
                cell,
                buildings,
                BuildingSpatialEffectKind.Security);
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

        public static int GetBuildingMedical(BuildingBase target)
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
                total += GetMedicalAtCell(target.GridMap, cell, buildings);
                count++;
            }

            return count <= 0 ? 0 : Mathf.FloorToInt((float)total / count);
        }

        public static int GetBuildingSecurity(BuildingBase target)
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
                total += GetSecurityAtCell(target.GridMap, cell, buildings);
                count++;
            }

            return count <= 0 ? 0 : Mathf.FloorToInt((float)total / count);
        }

        private static int GetCellEffectAtCell(
            GridMapBehaviour gridMap,
            GridPosition cell,
            IReadOnlyList<BuildingBase> buildings,
            BuildingSpatialEffectKind kind)
        {
            if (gridMap == null || !gridMap.HasBaseTileAt(cell) || buildings == null)
            {
                return 0;
            }

            var additiveValue = 0;
            var highestValue = 0;
            var noStackByEffectId = new Dictionary<string, int>(StringComparer.Ordinal);
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
                    if (definition == null
                        || !definition.IsValid
                        || definition.Kind != kind
                        || !IsCellAffected(source.Footprint, cell, definition))
                    {
                        continue;
                    }

                    switch (definition.StackingRule)
                    {
                        case BuildingSpatialStackingRule.Additive:
                            additiveValue += definition.Value;
                            break;
                        case BuildingSpatialStackingRule.HighestValue:
                            highestValue = Mathf.Max(highestValue, definition.Value);
                            break;
                        default:
                            noStackByEffectId.TryGetValue(definition.EffectId, out var current);
                            noStackByEffectId[definition.EffectId] = Mathf.Max(current, definition.Value);
                            break;
                    }
                }
            }

            var noStackValue = 0;
            foreach (var pair in noStackByEffectId)
            {
                noStackValue += pair.Value;
            }

            return additiveValue + highestValue + noStackValue;
        }

        public static List<GridPosition> CollectAffectedCells(
            GridMapBehaviour gridMap,
            GridFootprint sourceFootprint,
            int range)
        {
            return CollectAffectedCells(
                gridMap,
                sourceFootprint,
                Mathf.Max(0, range),
                true);
        }

        private static List<GridPosition> CollectAffectedCells(
            GridMapBehaviour gridMap,
            GridFootprint sourceFootprint,
            BuildingSpatialEffectDefinition definition)
        {
            if (definition == null)
            {
                return new List<GridPosition>();
            }

            return CollectAffectedCells(
                gridMap,
                sourceFootprint,
                definition.Range,
                definition.IncludeSourceFootprint);
        }

        private static List<GridPosition> CollectAffectedCells(
            GridMapBehaviour gridMap,
            GridFootprint sourceFootprint,
            int range,
            bool includeSourceFootprint)
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
                    if (gridMap.HasBaseTileAt(cell)
                        && IsCellInRange(sourceFootprint, cell, range)
                        && (includeSourceFootprint || !IsCellInside(sourceFootprint, cell)))
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
                        && AreFootprintsAffected(source.Footprint, target.Footprint, definition))
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
                   && source.IsOperational
                   && source.HasPlacement
                   && source.GridMap == gridMap;
        }

        private static bool CanAffectTarget(BuildingSpatialEffectDefinition definition, BuildingBase target)
        {
            return definition.TargetFilter switch
            {
                BuildingSpatialTargetFilter.Farmland =>
                    target.TryGetCapability<IBuildingCropFieldSource>(out _),
                BuildingSpatialTargetFilter.Cell => false,
                _ => true
            };
        }

        private static List<BuildingSpatialEffectDefinition> GetDefinitions(
            BuildingBase source,
            bool includeInactiveWorkerConditions = false)
        {
            var definitions = new List<BuildingSpatialEffectDefinition>();
            var modules = source.GetModules<BM_空间效果源>();
            if (modules.Count == 0 && source.FamilyDefinition?.ModuleSet != null)
            {
                var moduleTemplates = source.FamilyDefinition.ModuleSet.BuildingModules;
                for (var i = 0; i < moduleTemplates.Count; i++)
                {
                    if (moduleTemplates[i] is BM_空间效果源 template && template.IsEnabled)
                    {
                        modules.Add(template);
                    }
                }
            }

            for (var i = 0; i < modules.Count; i++)
            {
                var effects = modules[i].Effects;
                for (var j = 0; j < effects.Count; j++)
                {
                    var effect = effects[j];
                    if (effect == null || !effect.IsValid)
                    {
                        continue;
                    }

                    if (includeInactiveWorkerConditions)
                    {
                        if (effect.OperationalLevel > 0
                            && source != null
                            && source.CurrentLevel > 0
                            && effect.OperationalLevel != source.CurrentLevel)
                        {
                            continue;
                        }
                    }
                    else if (!effect.IsActiveFor(source))
                    {
                        continue;
                    }

                    definitions.Add(effect);
                }
            }

            return definitions;
        }

        private static bool AreFootprintsAffected(
            GridFootprint source,
            GridFootprint target,
            BuildingSpatialEffectDefinition definition)
        {
            foreach (var targetCell in target.Positions())
            {
                if (IsCellAffected(source, targetCell, definition))
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

        private static bool IsCellAffected(
            GridFootprint source,
            GridPosition cell,
            BuildingSpatialEffectDefinition definition)
        {
            return definition != null
                   && IsCellInRange(source, cell, definition.Range)
                   && (definition.IncludeSourceFootprint || !IsCellInside(source, cell));
        }

        private static bool IsCellInside(GridFootprint source, GridPosition cell)
        {
            return cell.X >= source.Origin.X
                   && cell.Y >= source.Origin.Y
                   && cell.X < source.Origin.X + source.Size.x
                   && cell.Y < source.Origin.Y + source.Size.y;
        }
    }
}
