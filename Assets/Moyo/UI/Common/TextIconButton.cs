using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Moyo.Unity
{
    public class TextIconButton : MonoBehaviour
    {
        [SerializeField] private Button btn;
        [SerializeField] private TMP_Text text;
        [SerializeField] private Image icon;




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
            btn.onClick.RemoveAllListeners();
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
    }
}
