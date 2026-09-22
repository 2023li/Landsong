using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_BuildingDetailHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Sirenix.OdinInspector.LabelText("视图")]
        public UI_GamePanel_BuildingDetails View;
        public void OnPointerEnter(PointerEventData e) => View.ShowWarnings(true);
        public void OnPointerExit(PointerEventData e) => View.ShowWarnings(false);
    }
}
