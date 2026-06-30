using System;
using System.Collections;
using System.Collections.Generic;
using Landsong.BuildingSystem;

namespace Landsong.TurnSystem
{
    public sealed class TurnService
    {
        private readonly List<BuildingBase> buildings = new List<BuildingBase>();
        private readonly HashSet<BuildingBase> registeredBuildings = new HashSet<BuildingBase>();

        public TurnService(int startingTurn = 1)
        {
            CurrentTurn = Math.Max(1, startingTurn);
        }

        public event Action<TurnService> BeforeTurnAdvanced;
        public event Action<TurnService, TurnAdvanceSummary> TurnAdvanced;

        public int CurrentTurn { get; private set; }
        public bool IsAdvancingTurn { get; private set; }
        public IReadOnlyList<BuildingBase> Buildings => buildings;

        public void RegisterBuilding(BuildingBase building)
        {
            if (building == null || !registeredBuildings.Add(building))
            {
                return;
            }

            buildings.Add(building);
        }

        public bool UnregisterBuilding(BuildingBase building)
        {
            if (building == null || !registeredBuildings.Remove(building))
            {
                return false;
            }

            buildings.Remove(building);
            return true;
        }

        public void ClearBuildings()
        {
            buildings.Clear();
            registeredBuildings.Clear();
        }

        private void RemoveMissingBuildings()
        {
            var removed = buildings.RemoveAll(building => building == null);
            if (removed <= 0)
            {
                return;
            }

            registeredBuildings.Clear();
            foreach (var building in buildings)
            {
                registeredBuildings.Add(building);
            }
        }

        public TurnAdvanceSummary NextTurn()
        {
            ThrowIfAdvancing();

            IsAdvancingTurn = true;
            try
            {
                var summary = BeginTurnAdvance();
                var snapshot = buildings.ToArray();

                foreach (var building in snapshot)
                {
                    ProcessBuildingTurn(building, ref summary);
                }

                CompleteTurnAdvance(summary);
                return summary;
            }
            finally
            {
                IsAdvancingTurn = false;
            }
        }

        public IEnumerator NextTurnRoutine(int buildingsPerFrame, Action<TurnAdvanceSummary> completed = null)
        {
            ThrowIfAdvancing();

            IsAdvancingTurn = true;
            try
            {
                buildingsPerFrame = Math.Max(1, buildingsPerFrame);

                var summary = BeginTurnAdvance();
                var snapshot = buildings.ToArray();
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
            RemoveMissingBuildings();
            return new TurnAdvanceSummary(CurrentTurn, CurrentTurn + 1);
        }

        private void CompleteTurnAdvance(TurnAdvanceSummary summary)
        {
            CurrentTurn = summary.ToTurn;
            TurnAdvanced?.Invoke(this, summary);
        }

        private void ProcessBuildingTurn(BuildingBase building, ref TurnAdvanceSummary summary)
        {
            if (building == null || !registeredBuildings.Contains(building) || !building.IsInitialized)
            {
                summary.Skipped++;
                return;
            }

            var succeeded = building.ProcessTurn();
            if (!succeeded)
            {
                summary.Failed++;
                return;
            }

            summary.OperatingConsumed++;
        }
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
