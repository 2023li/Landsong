using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Landsong.UISystem
{
    [DisallowMultipleComponent]
    public sealed class GamePanelItem_Quest_Requirement : MonoBehaviour
    {
        [SerializeField] private TMP_Text txt_任务要求;
        [SerializeField, LabelText("已完成标记")] private GameObject 已完成标记;

        private void Reset()
        {
            txt_任务要求 = GetComponentInChildren<TMP_Text>(true);
        }

        private void Awake()
        {
            ResourceRichTextFormatter.ApplySpriteAsset(txt_任务要求);
        }

        public void Bind(string requirementText, bool isCompleted)
        {
            ResourceRichTextFormatter.ApplySpriteAsset(txt_任务要求);
            SetText(txt_任务要求, requirementText);
            SetActive(已完成标记, isCompleted);
        }

        public void Clear()
        {
            SetText(txt_任务要求, string.Empty);
            SetActive(已完成标记, false);
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value ?? string.Empty;
            }
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }
    }
}
