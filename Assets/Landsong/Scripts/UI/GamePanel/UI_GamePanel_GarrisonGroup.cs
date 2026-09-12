using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_GarrisonGroup : MonoBehaviour
    {
        [Sirenix.OdinInspector.LabelText("建筑标识")]
        public ulong BuildingId;
        [Sirenix.OdinInspector.LabelText("条目容器")]
        public RectTransform Rows;
    }
}
