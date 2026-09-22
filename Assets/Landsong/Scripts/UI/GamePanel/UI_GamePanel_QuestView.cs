using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_QuestView : MonoBehaviour
    {
        [Sirenix.OdinInspector.LabelText("关闭")]
        public Button Close;
        [Sirenix.OdinInspector.LabelText("自动跟踪")]
        public Button AutoTrack;
        [Sirenix.OdinInspector.LabelText("来源筛选")]
        public Button SourceFilter;
        [Sirenix.OdinInspector.LabelText("容量")]
        public TextMeshProUGUI Capacity;
        [Sirenix.OdinInspector.LabelText("等待")]
        public TextMeshProUGUI Waiting;
        [Sirenix.OdinInspector.LabelText("来源筛选文字")]
        public TextMeshProUGUI SourceFilterLabel;
        [Sirenix.OdinInspector.LabelText("已承接条目容器")]
        public RectTransform AcceptedRows;
        [Sirenix.OdinInspector.LabelText("邀请条目容器")]
        public RectTransform InvitationRows;
        [Sirenix.OdinInspector.LabelText("类型开关集合")]
        public Toggle[] TypeToggles;
        [Sirenix.OdinInspector.LabelText("卡片模板")]
        public UI_GamePanel_QuestSlot CardTemplate;
        public void ValidateConfiguration()
        {
            if (Close == null || AutoTrack == null || SourceFilter == null || Capacity == null || Waiting == null || SourceFilterLabel == null || AcceptedRows == null || InvitationRows == null || CardTemplate == null || TypeToggles == null || TypeToggles.Length != 4)
                throw new InvalidOperationException("任务面板检查器引用不完整。");
        }
    }
}
