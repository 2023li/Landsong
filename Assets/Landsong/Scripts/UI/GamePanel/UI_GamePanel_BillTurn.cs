using TMPro;
using UnityEngine;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_BillTurn : MonoBehaviour
    {
        [Sirenix.OdinInspector.LabelText("回合")] public TMP_Text TurnLabel;
        [Sirenix.OdinInspector.LabelText("资源条目容器")] public RectTransform Rows;
        [Sirenix.OdinInspector.LabelText("资源账单模板")] public UI_GamePanel_BillRow RowTemplate;
    }
}
