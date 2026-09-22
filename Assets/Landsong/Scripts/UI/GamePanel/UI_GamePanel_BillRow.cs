using TMPro;
using UnityEngine;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_BillRow : MonoBehaviour
    {
        [Sirenix.OdinInspector.LabelText("资源名")] public TMP_Text Resource;
        [Sirenix.OdinInspector.LabelText("产出")] public TMP_Text Income;
        [Sirenix.OdinInspector.LabelText("消耗")] public TMP_Text Expense;
        [Sirenix.OdinInspector.LabelText("净量")] public TMP_Text Net;
        [Sirenix.OdinInspector.LabelText("结算后库存")] public TMP_Text Stored;
        public void Show(string name, EconomyBillEntry entry)
        {
            Resource.text = name; Income.text = entry.Income.ToString(); Expense.text = entry.Expense.ToString();
            Net.text = UI_GamePanel_Economy.Signed(entry.Income - entry.Expense); Stored.text = entry.Stored.ToString();
        }
    }
}
