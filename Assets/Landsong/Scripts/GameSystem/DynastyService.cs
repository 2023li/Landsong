using System;
using System.Collections.Generic;
using Landsong.BuildingSystem;
using UnityEngine;

namespace Landsong.DynastySystem
{
    public sealed class DynastyService
    {
        private readonly Dictionary<BuildingBase, int> populationContributors = new Dictionary<BuildingBase, int>();
        private readonly HashSet<BuildingBase> palaces = new HashSet<BuildingBase>();

        private int basePopulation;
        private int buildingPopulation;

        public DynastyService(int startingPopulation = 0)
        {
            basePopulation = Mathf.Max(0, startingPopulation);
        }

        public event Action<DynastyService> PopulationChanged;
        public event Action<DynastyService> PalaceStateChanged;

        public int Population => basePopulation + buildingPopulation;
        public int BasePopulation => basePopulation;
        public int BuildingPopulation => buildingPopulation;
        public int PalaceCount => palaces.Count;
        public bool HasPalace => palaces.Count > 0;

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
    }
}
