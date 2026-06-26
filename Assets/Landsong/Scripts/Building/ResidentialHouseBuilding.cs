using System.Collections.Generic;
using UnityEngine;

namespace Landsong.BuildingSystem
{
    public class ResidentialHouseBuilding : BuildingBehaviour
    {
        [SerializeField, Min(0)] private int experience;
        [SerializeField, Min(1)] private int experiencePerSuccessfulConsume = 1;
        [SerializeField, Min(1)] private int experienceToUpgrade = 5;
        [SerializeField] private bool resetExperienceOnUpgrade = true;

        public int Experience => experience;
        public int ExperiencePerSuccessfulConsume => experiencePerSuccessfulConsume;
        public int ExperienceToUpgrade => experienceToUpgrade;
        public bool CanUpgrade => Definition != null && Definition.HasUpgradeTarget && experience >= experienceToUpgrade;

        protected override void OnValidate()
        {
            base.OnValidate();
            experience = Mathf.Max(0, experience);
            experiencePerSuccessfulConsume = Mathf.Max(1, experiencePerSuccessfulConsume);
            experienceToUpgrade = Mathf.Max(1, experienceToUpgrade);
        }

        protected override void OnOperatingCostsConsumed(IReadOnlyList<BuildingCost> costs)
        {
            if (Definition == null || !Definition.HasUpgradeTarget)
            {
                return;
            }

            AddExperience(experiencePerSuccessfulConsume);
        }

        protected override void OnOperatingCostsConsumeFailed(IReadOnlyList<BuildingCost> costs)
        {
        }

        public void AddExperience(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            experience += amount;
            OnExperienceChanged();
            TryUpgradeIfReady();
        }

        public bool TryUpgradeIfReady()
        {
            if (!CanUpgrade)
            {
                return false;
            }

            var previousDefinition = Definition;
            var nextDefinition = previousDefinition.UpgradeTargetDefinition;
            Initialize(nextDefinition, Inventory);
            SetConstructionProgress(nextDefinition.ConstructionTurns);

            if (resetExperienceOnUpgrade)
            {
                experience = 0;
            }

            OnUpgraded(previousDefinition, nextDefinition);
            return true;
        }

        protected virtual void OnExperienceChanged()
        {
        }

        protected virtual void OnUpgraded(BuildingDefinition previousDefinition, BuildingDefinition nextDefinition)
        {
        }
    }
}
