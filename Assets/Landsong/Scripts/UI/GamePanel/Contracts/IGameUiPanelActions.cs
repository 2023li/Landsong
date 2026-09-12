using System;
using System.Collections.Generic;
using Landsong.ECS.Authoring;
using UnityEngine;

namespace Landsong.ECS.Presentation
{
    public interface IGameBuildingUi
    {
        ContentSource BuildingSource(int definition);
        string CostText(IEnumerable<BuildingCost> costs);
        void FocusBuilding(ulong id);
        void ShowBuildingConfirmation(string title, IEnumerable<string> lines, Action confirm);
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

    public interface IGameCourtUi
    {
        void CourtIntelRows(int known);
    }

    public interface IGameUiFeedback
    {
        void ShowMessage(string text);
    }
}
