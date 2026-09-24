using Landsong.ECS.Definitions;
using System;
using System.Collections.Generic;
using Landsong.Content;
using UnityEngine;

namespace Landsong.ECS.Presentation
{
    public delegate void BuildingChoiceRow(string label, Action action = null, string key = null);
    public interface IGameBuildingUi
    {
        ContentDisplay BuildingSource(BuildingId definition);
        string CostText(IEnumerable<BuildingCost> costs);
        void FocusBuilding(ulong id);
        void ShowBuildingConfirmation(string title, IEnumerable<string> lines, Action confirm);
        void ShowBuildingChoices(string title, Action<BuildingChoiceRow, Action> populate);
        void OpenCropSelection(ulong buildingId);
        void ConfirmBuildingCommand(CommandKind kind);
    }

    public interface IGameWorldUi
    {
        Camera Camera { get; }

        void LocateHistory(ulong source, Vector3 oldPosition);
    }

    public interface IGameSoldierUi
    {
        void OpenSoldierDetails(ulong id);
    }

    public interface IGameUiFeedback
    {
        void ShowMessage(string text);
    }
}
