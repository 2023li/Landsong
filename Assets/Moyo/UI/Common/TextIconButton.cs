using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sirenix.OdinInspector;
namespace Moyo.Unity
{
    public class TextIconButton : MonoBehaviour
    {
        [SerializeField, LabelText("按钮"), Required] private Button btn;
        [SerializeField, LabelText("文字"), Required] private TMP_Text text;
        [SerializeField, LabelText("图标"), Required] private Image icon;
        private UnityEngine.Events.UnityAction boundAction;




        public bool SetText(string value)
        {
            if (text == null)
            {
                return false;
            }
            text.text = value;
            return true;
        }
        public bool SetIcon(Sprite value)
        {
            if (icon == null)
            {
                return false;
            }
           
            icon.sprite = value;
            return true;
        }
        public bool BindOnClick(UnityEngine.Events.UnityAction action)
        {
            if (btn == null)
            {
                Debug.LogWarning("Button is not assigned.");
                return false;
            }
            if (boundAction != null) btn.onClick.RemoveListener(boundAction);
            boundAction = action;
            btn.onClick.AddListener(action);
            return true;
        }
        public bool TryGetIcon(out Image result)
        {
            result = icon;
            return icon != null;
        }

        public bool TryGetText(out TMP_Text result)
        {
            result = text;
            return text != null;
        }
        public bool TryGetButton(out Button result)
        {
            result = btn;
            return btn != null;
        }

        private void OnDestroy()
        {
            if (btn != null && boundAction != null) btn.onClick.RemoveListener(boundAction);
            boundAction = null;
        }
    }
}
