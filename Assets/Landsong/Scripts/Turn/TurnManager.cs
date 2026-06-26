using System;
using System.Collections.Generic;
using Landsong.BuildingSystem;
using Sirenix.OdinInspector;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Landsong.TurnSystem
{
    [DisallowMultipleComponent]
    public sealed class TurnManager : MonoBehaviour
    {
        [SerializeField, Min(1)] private int currentTurn = 1;
        [SerializeField] private bool discoverBuildingsOnAwake = true;
        [SerializeField] private bool allowKeyboardNextTurn = true;
#if ENABLE_INPUT_SYSTEM
        [SerializeField] private Key nextTurnKey = Key.N;
#else
        [SerializeField] private KeyCode nextTurnKey = KeyCode.N;
#endif
        [SerializeField] private bool logTurnResult = true;
        [SerializeField] private List<BuildingBehaviour> buildings = new List<BuildingBehaviour>();

        public event Action<TurnManager> BeforeTurnAdvanced;
        public event Action<TurnManager, TurnAdvanceSummary> TurnAdvanced;

        public int CurrentTurn => currentTurn;
        public IReadOnlyList<BuildingBehaviour> Buildings => buildings;

        private void Awake()
        {
            if (discoverBuildingsOnAwake)
            {
                DiscoverBuildings();
            }
        }

        private void OnValidate()
        {
            currentTurn = Mathf.Max(1, currentTurn);
            RemoveMissingBuildings();
        }

        private void Update()
        {
            if (allowKeyboardNextTurn && IsNextTurnKeyPressed())
            {
                NextTurn();
            }
        }

        [Button]
        public void NextTurn()
        {
            BeforeTurnAdvanced?.Invoke(this);

            var summary = ProcessBuildings();
            currentTurn++;

            TurnAdvanced?.Invoke(this, summary);

            if (logTurnResult)
            {
                Debug.Log(
                    $"Advanced to turn {currentTurn}. Construction: {summary.ConstructionAdvanced}, operating: {summary.OperatingConsumed}, failed: {summary.Failed}.",
                    this);
            }
        }

        [ContextMenu("Discover Buildings")]
        public void DiscoverBuildings()
        {
            buildings.Clear();

            var discovered = FindObjectsByType<BuildingBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
            foreach (var building in discovered)
            {
                RegisterBuilding(building);
            }
        }

        public void RegisterBuilding(BuildingBehaviour building)
        {
            if (building == null || buildings.Contains(building))
            {
                return;
            }

            buildings.Add(building);
        }

        public bool UnregisterBuilding(BuildingBehaviour building)
        {
            return building != null && buildings.Remove(building);
        }

        private TurnAdvanceSummary ProcessBuildings()
        {
            RemoveMissingBuildings();

            var summary = new TurnAdvanceSummary(currentTurn, currentTurn + 1);
            foreach (var building in buildings)
            {
                if (building == null || !building.HasDefinition)
                {
                    summary.Skipped++;
                    continue;
                }

                var wasConstructionComplete = building.IsConstructionComplete;
                var succeeded = wasConstructionComplete
                    ? building.TryConsumeOperatingTurnCosts()
                    : building.TryAdvanceConstructionTurn();

                if (!succeeded)
                {
                    summary.Failed++;
                    continue;
                }

                if (wasConstructionComplete)
                {
                    summary.OperatingConsumed++;
                }
                else
                {
                    summary.ConstructionAdvanced++;
                }
            }

            return summary;
        }

        private bool IsNextTurnKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current[nextTurnKey].wasPressedThisFrame;
#else
            return Input.GetKeyDown(nextTurnKey);
#endif
        }

        private void RemoveMissingBuildings()
        {
            buildings.RemoveAll(building => building == null);
        }
    }

    [Serializable]
    public struct TurnAdvanceSummary
    {
        public TurnAdvanceSummary(int fromTurn, int toTurn)
        {
            FromTurn = fromTurn;
            ToTurn = toTurn;
            ConstructionAdvanced = 0;
            OperatingConsumed = 0;
            Failed = 0;
            Skipped = 0;
        }

        public int FromTurn { get; }
        public int ToTurn { get; }
        public int ConstructionAdvanced { get; internal set; }
        public int OperatingConsumed { get; internal set; }
        public int Failed { get; internal set; }
        public int Skipped { get; internal set; }
    }
}
