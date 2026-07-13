using System;
using System.Collections;
using System.Collections.Generic;
using Landsong.BuildingSystem;

namespace Landsong.TurnSystem
{
    public sealed class TurnService
    {
        private static readonly IReadOnlyList<BuildingBase> EmptyBuildings = Array.Empty<BuildingBase>();
        private readonly List<BM_科技点产出> technologyPointModules =
            new List<BM_科技点产出>();
        private readonly List<IBuildingResourceConsumptionSource> resourceConsumptionSources =
            new List<IBuildingResourceConsumptionSource>();
        private readonly List<IBuildingResourceProductionSource> resourceProductionSources =
            new List<IBuildingResourceProductionSource>();
        private readonly List<IBuildingTaxSource> taxSources = new List<IBuildingTaxSource>();

        public TurnService(int startingTurn = 1)
        {
            CurrentTurn = Math.Max(1, startingTurn);
        }

        public event Action<TurnService> BeforeTurnAdvanced;
        public event Action<TurnService, TurnAdvanceSummary> TurnAdvanced;
        public event Action<TurnService, BuildingResourceProvidedEvent> BuildingResourceProvided;
        public event Action<TurnService, BuildingResourceConsumedEvent> BuildingResourceConsumed;
        public event Action<TurnService, BuildingTechnologyPointsProvidedEvent> BuildingTechnologyPointsProvided;

        public int CurrentTurn { get; private set; }
        public bool IsAdvancingTurn { get; private set; }

        public void SetCurrentTurn(int currentTurn)
        {
            ThrowIfAdvancing();
            CurrentTurn = Math.Max(1, currentTurn);
        }

        public TurnAdvanceSummary NextTurn(IReadOnlyList<BuildingBase> runtimeBuildings)
        {
            ThrowIfAdvancing();

            IsAdvancingTurn = true;
            try
            {
                var summary = BeginTurnAdvance();
                var snapshot = CreateBuildingSnapshot(runtimeBuildings);
                BeginResourceProvisionTurn(snapshot);

                for (var i = 0; i < snapshot.Length; i++)
                {
                    ProcessBuildingTurn(snapshot[i], ref summary);
                }

                CompleteResourceProvisionTurn(snapshot);
                CompleteTurnAdvance(summary);
                return summary;
            }
            finally
            {
                IsAdvancingTurn = false;
            }
        }

        public IEnumerator NextTurnRoutine(
            IReadOnlyList<BuildingBase> runtimeBuildings,
            int buildingsPerFrame,
            Action<TurnAdvanceSummary> completed = null)
        {
            ThrowIfAdvancing();

            IsAdvancingTurn = true;
            try
            {
                buildingsPerFrame = Math.Max(1, buildingsPerFrame);

                var summary = BeginTurnAdvance();
                var snapshot = CreateBuildingSnapshot(runtimeBuildings);
                BeginResourceProvisionTurn(snapshot);
                var processedThisFrame = 0;

                for (var i = 0; i < snapshot.Length; i++)
                {
                    ProcessBuildingTurn(snapshot[i], ref summary);
                    processedThisFrame++;

                    if (processedThisFrame < buildingsPerFrame || i >= snapshot.Length - 1)
                    {
                        continue;
                    }

                    processedThisFrame = 0;
                    yield return null;
                }

                CompleteResourceProvisionTurn(snapshot);
                CompleteTurnAdvance(summary);
                completed?.Invoke(summary);
            }
            finally
            {
                IsAdvancingTurn = false;
            }
        }

        private void ThrowIfAdvancing()
        {
            if (IsAdvancingTurn)
            {
                throw new InvalidOperationException("Cannot advance to the next turn while a turn advance is already running.");
            }
        }

        private TurnAdvanceSummary BeginTurnAdvance()
        {
            BeforeTurnAdvanced?.Invoke(this);
            return new TurnAdvanceSummary(CurrentTurn, CurrentTurn + 1);
        }

        private void CompleteTurnAdvance(TurnAdvanceSummary summary)
        {
            CurrentTurn = summary.ToTurn;
            TurnAdvanced?.Invoke(this, summary);
        }

        private static BuildingBase[] CreateBuildingSnapshot(IReadOnlyList<BuildingBase> runtimeBuildings)
        {
            runtimeBuildings ??= EmptyBuildings;
            var snapshot = new BuildingBase[runtimeBuildings.Count];
            for (var i = 0; i < runtimeBuildings.Count; i++)
            {
                snapshot[i] = runtimeBuildings[i];
            }

            return snapshot;
        }

        private void ProcessBuildingTurn(BuildingBase building, ref TurnAdvanceSummary summary)
        {
            if (building == null || building.IsDemolishing || !building.IsInitialized)
            {
                summary.Skipped++;
                return;
            }

            ResetProvidedTechnologyPoints(building);
            var succeeded = building.ProcessTurn();
            NotifyConsumedResources(building);
            if (!succeeded)
            {
                summary.Failed++;
                return;
            }

            summary.OperatingConsumed++;
            NotifyProvidedResources(building);
            NotifyProvidedTechnologyPoints(building);
        }

        private void NotifyConsumedResources(BuildingBase building)
        {
            if (BuildingResourceConsumed == null || building == null)
            {
                return;
            }

            resourceConsumptionSources.Clear();
            building.GetCapabilities(resourceConsumptionSources);
            for (var i = 0; i < resourceConsumptionSources.Count; i++)
            {
                NotifyConsumedResources(building, resourceConsumptionSources[i].LastResourceConsumptions);
            }
            resourceConsumptionSources.Clear();
        }

        private void NotifyConsumedResources(
            BuildingBase building,
            IReadOnlyList<BuildingResourceChange> resources)
        {
            if (resources == null)
            {
                return;
            }

            for (var i = 0; i < resources.Count; i++)
            {
                var resource = resources[i];
                if (!resource.IsValid)
                {
                    continue;
                }

                BuildingResourceConsumed?.Invoke(this, new BuildingResourceConsumedEvent(building, resource));
            }
        }

        private void NotifyProvidedResources(BuildingBase building)
        {
            if (BuildingResourceProvided == null || building == null)
            {
                return;
            }

            resourceProductionSources.Clear();
            building.GetCapabilities(resourceProductionSources);
            for (var i = 0; i < resourceProductionSources.Count; i++)
            {
                NotifyProvidedResources(building, resourceProductionSources[i].LastResourceProductions);
            }
            resourceProductionSources.Clear();

            taxSources.Clear();
            building.GetCapabilities(taxSources);
            for (var i = 0; i < taxSources.Count; i++)
            {
                NotifyProvidedResources(building, taxSources[i].LastTaxRewards);
            }
            taxSources.Clear();
        }

        private void NotifyProvidedResources(BuildingBase building, IReadOnlyList<BuildingResourceChange> resources)
        {
            if (resources == null)
            {
                return;
            }

            for (var i = 0; i < resources.Count; i++)
            {
                var resource = resources[i];
                if (!resource.IsValid)
                {
                    continue;
                }

                BuildingResourceProvided?.Invoke(this, new BuildingResourceProvidedEvent(building, resource));
            }
        }

        private void BeginResourceProvisionTurn(IReadOnlyList<BuildingBase> buildings)
        {
            BuildingResourceProviderSystem.BeginResourceProvisionTurn(buildings);
        }

        private void CompleteResourceProvisionTurn(IReadOnlyList<BuildingBase> buildings)
        {
            BuildingResourceProviderSystem.CompleteResourceProvisionTurn(buildings);

            if (buildings == null)
            {
                return;
            }

            for (var i = 0; i < buildings.Count; i++)
            {
                if (buildings[i] != null
                    && buildings[i].TryGetCapability<IBuildingResourceProvisionAccounting>(out _))
                {
                    NotifyProvidedResources(buildings[i]);
                }
            }
        }

        private void ResetProvidedTechnologyPoints(BuildingBase building)
        {
            if (building == null)
            {
                return;
            }

            technologyPointModules.Clear();
            building.GetModules(technologyPointModules);
            for (var i = 0; i < technologyPointModules.Count; i++)
            {
                technologyPointModules[i].ClearLastTechnologyPoints();
            }

            technologyPointModules.Clear();
        }

        private void NotifyProvidedTechnologyPoints(BuildingBase building)
        {
            if (building == null)
            {
                return;
            }

            var points = 0;
            technologyPointModules.Clear();
            building.GetModules(technologyPointModules);
            for (var i = 0; i < technologyPointModules.Count; i++)
            {
                points += technologyPointModules[i].ProvideTechnologyPointsForTurn();
            }

            technologyPointModules.Clear();
            if (points <= 0)
            {
                return;
            }

            BuildingTechnologyPointsProvided?.Invoke(
                this,
                new BuildingTechnologyPointsProvidedEvent(building, points));
        }
    }

    [Serializable]
    public readonly struct BuildingResourceProvidedEvent
    {
        public BuildingResourceProvidedEvent(BuildingBase building, BuildingResourceChange resource)
        {
            Building = building;
            Resource = resource;
        }

        public BuildingBase Building { get; }
        public BuildingResourceChange Resource { get; }
        public string ItemId => Resource.ItemId;
        public int Amount => Resource.Amount;
        public bool IsValid => Building != null && Resource.IsValid;
    }

    [Serializable]
    public readonly struct BuildingResourceConsumedEvent
    {
        public BuildingResourceConsumedEvent(BuildingBase building, BuildingResourceChange resource)
        {
            Building = building;
            Resource = resource;
        }

        public BuildingBase Building { get; }
        public BuildingResourceChange Resource { get; }
        public string ItemId => Resource.ItemId;
        public int Amount => Resource.Amount;
        public bool IsValid => Building != null && Resource.IsValid;
    }

    [Serializable]
    public readonly struct BuildingTechnologyPointsProvidedEvent
    {
        public BuildingTechnologyPointsProvidedEvent(BuildingBase building, int points)
        {
            Building = building;
            Points = Math.Max(0, points);
        }

        public BuildingBase Building { get; }
        public int Points { get; }
        public bool IsValid => Building != null && Points > 0;
    }

    [Serializable]
    public struct TurnAdvanceSummary
    {
        public TurnAdvanceSummary(int fromTurn, int toTurn)
        {
            FromTurn = fromTurn;
            ToTurn = toTurn;
            OperatingConsumed = 0;
            Failed = 0;
            Skipped = 0;
        }

        public int FromTurn { get; }
        public int ToTurn { get; }
        public int OperatingConsumed { get; internal set; }
        public int Failed { get; internal set; }
        public int Skipped { get; internal set; }
    }
}
