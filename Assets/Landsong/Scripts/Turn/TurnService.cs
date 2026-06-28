using System;
using System.Collections.Generic;
using Landsong.BuildingSystem;

namespace Landsong.TurnSystem
{
    public sealed class TurnService
    {
        private readonly List<BuildingBase> buildings = new List<BuildingBase>();

        public TurnService(int startingTurn = 1)
        {
            CurrentTurn = Math.Max(1, startingTurn);
        }

        public event Action<TurnService> BeforeTurnAdvanced;
        public event Action<TurnService, TurnAdvanceSummary> TurnAdvanced;

        public int CurrentTurn { get; private set; }
        public IReadOnlyList<BuildingBase> Buildings => buildings;

        public void RegisterBuilding(BuildingBase building)
        {
            if (building == null || buildings.Contains(building))
            {
                return;
            }

            buildings.Add(building);
        }

        public bool UnregisterBuilding(BuildingBase building)
        {
            return building != null && buildings.Remove(building);
        }

        public void ClearBuildings()
        {
            buildings.Clear();
        }

        private void RemoveMissingBuildings()
        {
            buildings.RemoveAll(building => building == null);
        }

        public TurnAdvanceSummary NextTurn()
        {
            BeforeTurnAdvanced?.Invoke(this);

            RemoveMissingBuildings();

            var summary = new TurnAdvanceSummary(CurrentTurn, CurrentTurn + 1);
            var snapshot = buildings.ToArray();

            foreach (var building in snapshot)
            {
                if (building == null || !buildings.Contains(building) || !building.IsInitialized)
                {
                    summary.Skipped++;
                    continue;
                }

                var succeeded = building.ProcessTurn();
                if (!succeeded)
                {
                    summary.Failed++;
                    continue;
                }

                summary.OperatingConsumed++;
            }

            CurrentTurn = summary.ToTurn;
            TurnAdvanced?.Invoke(this, summary);
            return summary;
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
