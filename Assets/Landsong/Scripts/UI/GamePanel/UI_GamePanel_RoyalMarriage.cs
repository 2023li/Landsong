using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_RoyalMarriage : MonoBehaviour
    {
        [Sirenix.OdinInspector.LabelText("标题")]
        public TMP_Text Title;
        [Sirenix.OdinInspector.LabelText("提示")]
        public TMP_Text Hint;
        [Sirenix.OdinInspector.LabelText("人物")]
        public UI_GamePanel_RoyalPersonSummary Person;
        [Sirenix.OdinInspector.LabelText("配偶")]
        public UI_GamePanel_RoyalPersonSummary Mate;
        [Sirenix.OdinInspector.LabelText("候选列表")]
        public GameObject CandidateList;
        [Sirenix.OdinInspector.LabelText("候选条目容器")]
        public RectTransform CandidateRows;
        [Sirenix.OdinInspector.LabelText("候选模板")]
        public UI_GamePanel_MarriageCandidate CandidateTemplate;
        [Sirenix.OdinInspector.LabelText("同意")]
        public Button Approve;
        [Sirenix.OdinInspector.LabelText("拒绝")]
        public Button Refuse;
        [Sirenix.OdinInspector.LabelText("关闭")]
        public Button Close;
        [Sirenix.OdinInspector.LabelText("同意文字")]
        public TMP_Text ApproveLabel;
        [Sirenix.OdinInspector.LabelText("拒绝文字")]
        public TMP_Text RefuseLabel;
        [Sirenix.OdinInspector.LabelText("关闭文字")]
        public TMP_Text CloseLabel;
        public void ValidateConfiguration()
        {
            if (Title == null || Hint == null || Person == null || Mate == null || Person.PortraitBinding == null || Mate.PortraitBinding == null || CandidateList == null || CandidateRows == null || CandidateTemplate == null || Approve == null || Refuse == null || Close == null)
                throw new InvalidOperationException("赐婚面板检查器引用不完整。");
            Person.PortraitBinding.ValidateConfiguration();
            Mate.PortraitBinding.ValidateConfiguration();
        }
    }
}
