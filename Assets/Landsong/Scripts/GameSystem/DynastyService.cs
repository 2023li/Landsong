using System;
using System.Collections.Generic;
using Landsong.BuildingSystem;
using UnityEngine;

namespace Landsong.DynastySystem
{

    public enum DynastyStage
    {

        营地,
        聚落,
        村庄,
        城镇,
        城邦,
        王国,
        帝国
    }


    public sealed class DynastyService
    {
        public const string DefaultDynastyName = "无名王朝";

        private readonly Dictionary<BuildingBase, int> populationContributors = new Dictionary<BuildingBase, int>();
        private readonly Dictionary<string, int> externalEmploymentContributors = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<BuildingBase> palaces = new HashSet<BuildingBase>();

        private string dynastyName = DefaultDynastyName;
        private int basePopulation;
        private int buildingPopulation;
        private int employedPopulation;
        private int externalEmployedPopulation;
        private DynastyStage stage = DynastyStage.营地;

        public DynastyService(
            int startingPopulation = 0,
            DynastyStage startingStage = DynastyStage.营地,
            string startingDynastyName = DefaultDynastyName)
        {
            dynastyName = NormalizeDynastyName(startingDynastyName);
            basePopulation = Mathf.Max(0, startingPopulation);
            stage = startingStage;
        }

        public event Action<DynastyService> DynastyNameChanged;
        public event Action<DynastyService> PopulationChanged;
        public event Action<DynastyService> StageChanged;
        public event Action<DynastyService> PalaceStateChanged;

        public string DynastyName => dynastyName;
        public DynastyStage Stage => stage;
        public int Population => basePopulation + buildingPopulation + externalEmployedPopulation;
        public int EmployedPopulation => Mathf.Clamp(employedPopulation + externalEmployedPopulation, 0, Population);
        public int AvailablePopulation => Mathf.Max(0, Population - EmployedPopulation);
        public int BasePopulation => basePopulation;
        public int BuildingPopulation => buildingPopulation;
        public int ExternalEmployedPopulation => externalEmployedPopulation;
        public int PalaceCount => palaces.Count;
        public bool HasPalace => palaces.Count > 0;

        public void SetDynastyName(string newName)
        {
            newName = NormalizeDynastyName(newName);
            if (dynastyName == newName)
            {
                return;
            }

            dynastyName = newName;
            DynastyNameChanged?.Invoke(this);
        }

        public void SetStage(DynastyStage newStage)
        {
            if (stage == newStage)
            {
                return;
            }

            stage = newStage;
            StageChanged?.Invoke(this);
        }

        public void SetBasePopulation(int population)
        {
            population = Mathf.Max(0, population);
            if (basePopulation == population)
            {
                return;
            }

            basePopulation = population;
            PopulationChanged?.Invoke(this);
        }

        public void AddPopulation(int amount)
        {
            if (amount == 0)
            {
                return;
            }

            SetBasePopulation(Mathf.Max(0, basePopulation + amount));
        }

        public bool TryConsumePopulation(int amount)
        {
            amount = Mathf.Max(0, amount);
            if (amount == 0)
            {
                return true;
            }

            if (basePopulation < amount)
            {
                return false;
            }

            SetBasePopulation(basePopulation - amount);
            return true;
        }

        public void SetEmployedPopulation(int population)
        {
            population = Mathf.Max(0, population);
            if (employedPopulation == population)
            {
                return;
            }

            employedPopulation = population;
            PopulationChanged?.Invoke(this);
        }

        public void SetExternalEmployedPopulation(string sourceId, int population)
        {
            sourceId = string.IsNullOrWhiteSpace(sourceId) ? string.Empty : sourceId.Trim();
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                return;
            }

            population = Mathf.Max(0, population);
            externalEmploymentContributors.TryGetValue(sourceId, out var previousPopulation);
            if (population <= 0)
            {
                if (!externalEmploymentContributors.Remove(sourceId))
                {
                    return;
                }

                externalEmployedPopulation = Mathf.Max(0, externalEmployedPopulation - previousPopulation);
                PopulationChanged?.Invoke(this);
                return;
            }

            if (previousPopulation == population)
            {
                return;
            }

            externalEmploymentContributors[sourceId] = population;
            externalEmployedPopulation += population - previousPopulation;
            externalEmployedPopulation = Mathf.Max(0, externalEmployedPopulation);
            PopulationChanged?.Invoke(this);
        }

        public void SetPopulationContribution(BuildingBase building, int population)
        {
            if (building == null)
            {
                return;
            }

            population = Mathf.Max(0, population);
            populationContributors.TryGetValue(building, out var previousPopulation);

            if (population <= 0)
            {
                RemovePopulationContribution(building);
                return;
            }

            if (previousPopulation == population)
            {
                return;
            }

            populationContributors[building] = population;
            buildingPopulation += population - previousPopulation;
            PopulationChanged?.Invoke(this);
        }

        public bool RemovePopulationContribution(BuildingBase building)
        {
            if (ReferenceEquals(building, null) || !populationContributors.TryGetValue(building, out var population))
            {
                return false;
            }

            populationContributors.Remove(building);
            buildingPopulation = Mathf.Max(0, buildingPopulation - population);
            PopulationChanged?.Invoke(this);
            return true;
        }

        public void RegisterPalace(BuildingBase building)
        {
            if (building == null || !palaces.Add(building))
            {
                return;
            }

            PalaceStateChanged?.Invoke(this);
        }

        public bool UnregisterPalace(BuildingBase building)
        {
            if (ReferenceEquals(building, null) || !palaces.Remove(building))
            {
                return false;
            }

            PalaceStateChanged?.Invoke(this);
            return true;
        }

        public void Refresh()
        {
            RemoveMissingPalaces();
            RemoveMissingPopulationContributors();
        }

        private void RemoveMissingPalaces()
        {
            if (palaces.Count == 0)
            {
                return;
            }

            var removed = palaces.RemoveWhere(building => building == null || !building.IsRegistered);
            if (removed > 0)
            {
                PalaceStateChanged?.Invoke(this);
            }
        }

        private void RemoveMissingPopulationContributors()
        {
            if (populationContributors.Count == 0)
            {
                return;
            }

            List<BuildingBase> missingBuildings = null;
            foreach (var entry in populationContributors)
            {
                if (entry.Key != null && entry.Key.IsRegistered)
                {
                    continue;
                }

                missingBuildings ??= new List<BuildingBase>();
                missingBuildings.Add(entry.Key);
            }

            if (missingBuildings == null)
            {
                return;
            }

            foreach (var building in missingBuildings)
            {
                RemovePopulationContribution(building);
            }
        }

        public static string NormalizeDynastyName(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? DefaultDynastyName : value.Trim();
        }
    }
}
