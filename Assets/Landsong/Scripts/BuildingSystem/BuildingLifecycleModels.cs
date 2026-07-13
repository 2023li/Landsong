using System;
using UnityEngine;

namespace Landsong.BuildingSystem
{
    public enum BuildingLifecycleStage
    {
        Construction = 0,
        Operational = 10
    }

    public readonly struct BuildingStageChangedEvent
    {
        public BuildingStageChangedEvent(
            BuildingBase building,
            BuildingLifecycleStage previous,
            BuildingLifecycleStage current)
        {
            Building = building;
            Previous = previous;
            Current = current;
        }

        public BuildingBase Building { get; }
        public BuildingLifecycleStage Previous { get; }
        public BuildingLifecycleStage Current { get; }
    }

    public readonly struct BuildingLevelChangedEvent
    {
        public BuildingLevelChangedEvent(BuildingBase building, int previous, int current)
        {
            Building = building;
            Previous = Mathf.Max(0, previous);
            Current = Mathf.Max(0, current);
        }

        public BuildingBase Building { get; }
        public int Previous { get; }
        public int Current { get; }
    }

    [Serializable]
    public sealed class BuildingRuntimeIdentity
    {
        [SerializeField] private string instanceId = string.Empty;
        [SerializeField] private BuildingLifecycleStage stage = BuildingLifecycleStage.Operational;
        [SerializeField, Min(0)] private int level = 1;
        [SerializeField] private string styleId = string.Empty;
        [SerializeField, Min(0)] private int constructionProgress;

        public string InstanceId => instanceId;
        public BuildingLifecycleStage Stage => stage;
        public int Level => stage == BuildingLifecycleStage.Operational ? Mathf.Max(1, level) : 0;
        public string StyleId => string.IsNullOrWhiteSpace(styleId) ? string.Empty : styleId.Trim();
        public int ConstructionProgress => Mathf.Max(0, constructionProgress);

        public void EnsureInitialized()
        {
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                instanceId = Guid.NewGuid().ToString("N");
            }

            styleId = StyleId;
            constructionProgress = Mathf.Max(0, constructionProgress);
            level = stage == BuildingLifecycleStage.Operational ? Mathf.Max(1, level) : 0;
        }

        public void BeginConstruction(string selectedStyleId)
        {
            instanceId = Guid.NewGuid().ToString("N");
            stage = BuildingLifecycleStage.Construction;
            level = 0;
            styleId = string.IsNullOrWhiteSpace(selectedStyleId) ? string.Empty : selectedStyleId.Trim();
            constructionProgress = 0;
        }

        public void Restore(
            string restoredInstanceId,
            BuildingLifecycleStage restoredStage,
            int restoredLevel,
            string restoredStyleId,
            int restoredConstructionProgress)
        {
            instanceId = string.IsNullOrWhiteSpace(restoredInstanceId)
                ? Guid.NewGuid().ToString("N")
                : restoredInstanceId.Trim();
            stage = restoredStage;
            level = stage == BuildingLifecycleStage.Operational ? Mathf.Max(1, restoredLevel) : 0;
            styleId = string.IsNullOrWhiteSpace(restoredStyleId) ? string.Empty : restoredStyleId.Trim();
            constructionProgress = Mathf.Max(0, restoredConstructionProgress);
        }

        public BuildingLifecycleStage CompleteConstruction()
        {
            var previous = stage;
            stage = BuildingLifecycleStage.Operational;
            level = 1;
            constructionProgress = 0;
            return previous;
        }

        public int SetLevel(int newLevel)
        {
            var previous = Level;
            stage = BuildingLifecycleStage.Operational;
            level = Mathf.Max(1, newLevel);
            constructionProgress = 0;
            return previous;
        }

        public void AdvanceConstruction()
        {
            constructionProgress = Mathf.Max(0, constructionProgress) + 1;
        }
    }
}
