using TMPro;
using UnityEngine;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_HistoryTurn : MonoBehaviour
    {
        [Sirenix.OdinInspector.LabelText("回合")] public TMP_Text TurnLabel;
        [Sirenix.OdinInspector.LabelText("回合历史正文")] public TMP_Text Body;
    }
}
