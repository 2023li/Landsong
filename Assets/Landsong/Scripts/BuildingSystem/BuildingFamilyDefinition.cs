using System;
using System.Collections.Generic;
using Landsong.ConditionSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.BuildingSystem
{
    [Serializable]
    public sealed class BuildingConstructionTurnDefinition
    {
        [SerializeField, InspectorName("本回合消耗"), LabelText("本回合消耗")]
        private BuildingCost[] costs = new BuildingCost[0];

        [SerializeField, InspectorName("本回合产出"), LabelText("本回合产出")]
        private BuildingCost[] rewards = new BuildingCost[0];

        public BuildingConstructionTurnDefinition()
        {
        }

        public BuildingConstructionTurnDefinition(
            IReadOnlyList<BuildingCost> sourceCosts,
            IReadOnlyList<BuildingCost> sourceRewards = null)
        {
            costs = CopyCosts(sourceCosts);
            rewards = CopyCosts(sourceRewards);
        }

        public IReadOnlyList<BuildingCost> Costs => costs ?? Array.Empty<BuildingCost>();
        public IReadOnlyList<BuildingCost> Rewards => rewards ?? Array.Empty<BuildingCost>();

        public void Normalize()
        {
            costs ??= Array.Empty<BuildingCost>();
            for (var i = 0; i < costs.Length; i++)
            {
                costs[i] = costs[i].Normalized();
            }

            rewards ??= Array.Empty<BuildingCost>();
            for (var i = 0; i < rewards.Length; i++)
            {
                rewards[i] = rewards[i].Normalized();
            }
        }

        public bool HasAnyCost()
        {
            for (var i = 0; i < Costs.Count; i++)
            {
                if (Costs[i].IsValid)
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasAnyReward()
        {
            for (var i = 0; i < Rewards.Count; i++)
            {
                if (Rewards[i].IsValid)
                {
                    return true;
                }
            }

            return false;
        }

        private static BuildingCost[] CopyCosts(IReadOnlyList<BuildingCost> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<BuildingCost>();
            }

            var result = new BuildingCost[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                result[i] = source[i].Normalized();
            }

            return result;
        }

    }

    [Serializable]
    public sealed class BuildingConstructionDefinition
    {
        [SerializeField, InspectorName("逐回合施工消耗"), LabelText("逐回合施工消耗")]
        private BuildingConstructionTurnDefinition[] turns =
            { new BuildingConstructionTurnDefinition() };

        public IReadOnlyList<BuildingConstructionTurnDefinition> Turns =>
            turns ?? Array.Empty<BuildingConstructionTurnDefinition>();

        public int RequiredTurns => Mathf.Max(1, Turns.Count);

        public IReadOnlyList<BuildingCost> GetCosts(int turnIndex)
        {
            return turnIndex >= 0 && turnIndex < Turns.Count && Turns[turnIndex] != null
                ? Turns[turnIndex].Costs
                : Array.Empty<BuildingCost>();
        }

        public IReadOnlyList<BuildingCost> GetRewards(int turnIndex)
        {
            return turnIndex >= 0 && turnIndex < Turns.Count && Turns[turnIndex] != null
                ? Turns[turnIndex].Rewards
                : Array.Empty<BuildingCost>();
        }

        public void Configure(
            IReadOnlyList<IReadOnlyList<BuildingCost>> turnCosts,
            IReadOnlyList<IReadOnlyList<BuildingCost>> turnRewards = null)
        {
            var turnCount = Mathf.Max(turnCosts?.Count ?? 0, turnRewards?.Count ?? 0);
            if (turnCount == 0)
            {
                turns = new[] { new BuildingConstructionTurnDefinition() };
                return;
            }

            turns = new BuildingConstructionTurnDefinition[turnCount];
            for (var i = 0; i < turnCount; i++)
            {
                turns[i] = new BuildingConstructionTurnDefinition(
                    i < (turnCosts?.Count ?? 0) ? turnCosts[i] : null,
                    i < (turnRewards?.Count ?? 0) ? turnRewards[i] : null);
            }

            Normalize();
        }

        public void Normalize()
        {
            if (turns == null || turns.Length == 0)
            {
                turns = new[] { new BuildingConstructionTurnDefinition() };
            }

            for (var i = 0; i < turns.Length; i++)
            {
                turns[i] ??= new BuildingConstructionTurnDefinition();
                turns[i].Normalize();
            }
        }

        public bool HasAnyCost()
        {
            for (var i = 0; i < Turns.Count; i++)
            {
                if (Turns[i] != null && Turns[i].HasAnyCost())
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasAnyReward()
        {
            for (var i = 0; i < Turns.Count; i++)
            {
                if (Turns[i] != null && Turns[i].HasAnyReward())
                {
                    return true;
                }
            }

            return false;
        }

        public IReadOnlyList<BuildingCost> GetTotalCosts()
        {
            var totals = new List<BuildingCost>();
            for (var turnIndex = 0; turnIndex < Turns.Count; turnIndex++)
            {
                var costs = Turns[turnIndex]?.Costs;
                if (costs == null) continue;
                for (var costIndex = 0; costIndex < costs.Count; costIndex++)
                {
                    var cost = costs[costIndex];
                    if (!cost.IsValid) continue;

                    var existingIndex = -1;
                    for (var i = 0; i < totals.Count; i++)
                    {
                        if (string.Equals(totals[i].ItemId, cost.ItemId, StringComparison.Ordinal))
                        {
                            existingIndex = i;
                            break;
                        }
                    }

                    if (existingIndex < 0)
                    {
                        totals.Add(cost);
                    }
                    else
                    {
                        var existing = totals[existingIndex];
                        totals[existingIndex] = new BuildingCost(
                            existing.ItemDefinition ?? cost.ItemDefinition,
                            existing.Amount + cost.Amount);
                    }
                }
            }

            return totals;
        }
    }

    [Serializable]
    public sealed class BuildingLevelDefinition
    {
        [SerializeField, InspectorName("等级"), Min(1), LabelText("等级")]
        private int level = 1;

        [SerializeField, InspectorName("允许进入该等级"), LabelText("允许进入该等级")]
        private bool configured = true;

        [SerializeField, InspectorName("升级条件"), LabelText("升级条件")]
        [SerializeReference]
        private GameCondition upgradeCondition;

        [SerializeField, InspectorName("升级消耗"), LabelText("升级消耗")]
        private BuildingCost[] upgradeCosts = new BuildingCost[0];

        [SerializeReference, InspectorName("本级配置"), LabelText("本级配置")]
        private List<BuildingLevelConfigurationBase> configurations =
            new List<BuildingLevelConfigurationBase>();

        public BuildingLevelDefinition()
        {
        }

        public BuildingLevelDefinition(
            int level,
            bool configured,
            IReadOnlyList<BuildingCost> upgradeCosts = null,
            GameCondition upgradeCondition = null,
            IEnumerable<BuildingLevelConfigurationBase> configurations = null)
        {
            this.level = Mathf.Max(1, level);
            this.configured = this.level == 1 || configured;
            this.upgradeCondition = upgradeCondition;
            if (upgradeCosts != null)
            {
                this.upgradeCosts = new BuildingCost[upgradeCosts.Count];
                for (var i = 0; i < upgradeCosts.Count; i++)
                {
                    this.upgradeCosts[i] = upgradeCosts[i].Normalized();
                }
            }

            this.configurations = configurations == null
                ? new List<BuildingLevelConfigurationBase>()
                : new List<BuildingLevelConfigurationBase>(configurations);
            Normalize();
        }

        public int Level => Mathf.Max(1, level);
        public bool IsConfigured => Level == 1 || configured;
        public GameCondition UpgradeCondition => upgradeCondition;
        public IReadOnlyList<BuildingCost> UpgradeCosts => upgradeCosts ?? Array.Empty<BuildingCost>();
        public IReadOnlyList<BuildingLevelConfigurationBase> Configurations =>
            configurations ?? (IReadOnlyList<BuildingLevelConfigurationBase>)Array.Empty<BuildingLevelConfigurationBase>();

        public bool IsConditionMet(Landsong.GameSystem gameSystem)
        {
            return upgradeCondition == null || upgradeCondition.IsMet(gameSystem);
        }

        public void Apply(BuildingBase building)
        {
            if (building == null || configurations == null)
            {
                return;
            }

            for (var i = 0; i < configurations.Count; i++)
            {
                configurations[i]?.Apply(building);
            }
        }

        public void Normalize()
        {
            level = Mathf.Max(1, level);
            if (level == 1)
            {
                configured = true;
            }

            upgradeCosts ??= new BuildingCost[0];
            for (var i = 0; i < upgradeCosts.Length; i++)
            {
                upgradeCosts[i] = upgradeCosts[i].Normalized();
            }

            configurations ??= new List<BuildingLevelConfigurationBase>();
            for (var i = configurations.Count - 1; i >= 0; i--)
            {
                if (configurations[i] == null)
                {
                    configurations.RemoveAt(i);
                    continue;
                }

                configurations[i].Normalize();
            }
        }
    }

    [CreateAssetMenu(menuName = "Landsong/Building/Building Family", fileName = "BuildingFamily")]
    public sealed class BuildingFamilyDefinition : ScriptableObject
    {
        [SerializeField, InspectorName("基础定义"), InlineProperty, HideLabel]
        private BuildingDefinition definition = new BuildingDefinition();

        [SerializeField, InspectorName("唯一运行时 Prefab"), LabelText("唯一运行时 Prefab")]
        private BuildingBase runtimePrefab;

        [SerializeField, InspectorName("施工数据"), LabelText("施工数据")]
        private BuildingConstructionDefinition construction = new BuildingConstructionDefinition();

        [SerializeField, InspectorName("运营等级"), LabelText("运营等级")]
        private BuildingLevelDefinition[] levels =
            { new BuildingLevelDefinition(1, true) };

        [SerializeField, InspectorName("模块集合"), LabelText("模块集合")]
        private BuildingModuleSetDefinition moduleSet;

        [SerializeField, InspectorName("表现定义"), LabelText("表现定义")]
        private BuildingPresentationDefinition presentation;

        public BuildingDefinition Definition => definition;
        public string FamilyId => definition == null ? string.Empty : definition.FamilyId;
        public BuildingBase RuntimePrefab => runtimePrefab;
        public BuildingConstructionDefinition Construction => construction;
        public IReadOnlyList<BuildingLevelDefinition> Levels =>
            levels ?? Array.Empty<BuildingLevelDefinition>();
        public BuildingModuleSetDefinition ModuleSet => moduleSet;
        public BuildingPresentationDefinition Presentation => presentation;
        public bool IsValid => definition != null
                               && definition.IsValid
                               && runtimePrefab != null
                               && levels != null
                               && levels.Length > 0
                               && levels[0] != null
                               && levels[0].Level == 1;
        public int MaxConfiguredLevel
        {
            get
            {
                var max = 0;
                for (var i = 0; i < Levels.Count; i++)
                {
                    if (Levels[i] != null && Levels[i].IsConfigured)
                    {
                        max = Mathf.Max(max, Levels[i].Level);
                    }
                }

                return max;
            }
        }

        private void OnEnable()
        {
            Normalize();
        }

        private void OnValidate()
        {
            Normalize();
        }

        public bool TryGetLevel(int level, out BuildingLevelDefinition result)
        {
            result = null;
            if (level <= 0 || levels == null)
            {
                return false;
            }

            for (var i = 0; i < levels.Length; i++)
            {
                if (levels[i] != null && levels[i].Level == level)
                {
                    result = levels[i];
                    return true;
                }
            }

            return false;
        }

        public void ApplyLevel(BuildingBase building, int level)
        {
            if (!TryGetLevel(level, out var levelDefinition))
            {
                throw new InvalidOperationException(
                    $"Building family '{FamilyId}' does not define operational level {level}.");
            }

            levelDefinition.Apply(building);
        }

        public void ConfigureRuntime(
            BuildingBase prefab,
            BuildingConstructionDefinition constructionDefinition,
            IEnumerable<BuildingLevelDefinition> levelDefinitions,
            BuildingModuleSetDefinition moduleSetDefinition,
            BuildingPresentationDefinition presentationDefinition)
        {
            runtimePrefab = prefab;
            construction = constructionDefinition ?? new BuildingConstructionDefinition();
            levels = levelDefinitions == null
                ? new[] { new BuildingLevelDefinition(1, true) }
                : new List<BuildingLevelDefinition>(levelDefinitions).ToArray();
            moduleSet = moduleSetDefinition;
            presentation = presentationDefinition;
            Normalize();
        }

        /// <summary>
        /// 由正式建筑数值表更新施工与等级数据。
        /// 不触碰 Runtime Prefab、ModuleSet、Presentation，也不会顺带规范化这些 Unity 管理的资产。
        /// </summary>
        public void ConfigureImportedNumericData(
            BuildingConstructionDefinition constructionDefinition,
            IEnumerable<BuildingLevelDefinition> levelDefinitions)
        {
            construction = constructionDefinition ?? new BuildingConstructionDefinition();
            levels = levelDefinitions == null
                ? new[] { new BuildingLevelDefinition(1, true) }
                : new List<BuildingLevelDefinition>(levelDefinitions).ToArray();
            Normalize(false);
        }

        private void Normalize()
        {
            Normalize(true);
        }

        private void Normalize(bool normalizeModuleSet)
        {
            definition ??= new BuildingDefinition();
            definition.Normalize();
            construction ??= new BuildingConstructionDefinition();
            construction.Normalize();

            if (levels == null || levels.Length == 0)
            {
                levels = new[] { new BuildingLevelDefinition(1, true) };
            }

            Array.Sort(levels, CompareLevels);
            for (var i = 0; i < levels.Length; i++)
            {
                levels[i] ??= new BuildingLevelDefinition(i + 1, i == 0);
                levels[i].Normalize();
            }

            if (normalizeModuleSet)
            {
                moduleSet?.Normalize();
            }
        }

        private static int CompareLevels(BuildingLevelDefinition left, BuildingLevelDefinition right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            return left.Level.CompareTo(right.Level);
        }
    }
}
