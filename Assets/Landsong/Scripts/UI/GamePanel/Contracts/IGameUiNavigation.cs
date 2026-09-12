using System.Collections.Generic;
using Unity.Entities;
using System;
using System.Linq;
using Landsong.ECS.Authoring;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.InputSystem;
using Text = TMPro.TextMeshProUGUI;
using System.Text;
using Unity.Transforms;
using UnityEngine.EventSystems;
using InputField = TMPro.TMP_InputField;
using Landsong.ECS.Persistence;

namespace Landsong.ECS.Presentation
{
    public interface IGameUiNavigation
    {
        GameUiInputPolicy InputPolicy { get; }
        UI_GamePanel_List ActiveListPanel { get; }

        void BackPanel();
        void ClosePanel();
        void EndInventoryDrag();
        UI_GamePanel_List GetListPanel(GamePanelId id);
        UI_GamePanel_Intelligence IntelligenceWindow { get; }

        CanvasGroup InterfaceGroup { get; set; }

        CanvasScaler InterfaceScaler { get; set; }

        bool IsPanelOpen { get; set; }

        void LocateGarrison(ulong id);
        void OpenEconomy(ulong source = 0);
        void OpenPanel(GamePanelId panel);
        GamePanelId Panel { get; set; }

        UI_GamePanel_PausePop PauseMenu { get; set; }

        RectTransform PrimaryRows { get; }

        RectTransform SecondaryRows { get; }

        bool TextFocused { get; }

        EventSystem InputEvents { get; }
    }
}
