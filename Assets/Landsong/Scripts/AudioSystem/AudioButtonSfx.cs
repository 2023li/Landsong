using Landsong.AudioSystem;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Landsong.UISystem
{
    [DisallowMultipleComponent]
    public sealed class AudioButtonSfx : MonoBehaviour, IPointerClickHandler, ISubmitHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private string channelKey = AudioPlayer.DefaultUiSfxChannelKey;
        [SerializeField] private string clickCueKey;
        [SerializeField] private string disabledClickCueKey;
        [SerializeField] private AudioClip clickSound;
        [SerializeField] private AudioClip disabledClickSound;
        [SerializeField] private bool playWhenDisabled;
        [SerializeField, Range(0f, 1f)] private float volumeScale = 1f;

        private void Reset()
        {
            button = GetComponent<Button>();
        }

        private void Awake()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            PlayClickFeedback();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            PlayClickFeedback();
        }

        private void PlayClickFeedback()
        {
            bool interactable = button == null || button.IsInteractable();
            if (!interactable && !playWhenDisabled)
            {
                return;
            }

            AudioClip clip = interactable ? clickSound : disabledClickSound;
            if (clip != null)
            {
                AudioPlayer.Instance.PlayOneShotClip(ResolveChannelKey(), clip, volumeScale);
                return;
            }

            string cueKey = interactable ? clickCueKey : disabledClickCueKey;
            if (!string.IsNullOrWhiteSpace(cueKey))
            {
                AudioPlayer.Instance.PlayOneShot(ResolveChannelKey(), cueKey, volumeScale);
                return;
            }

            if (interactable)
            {
                AudioPlayer.Instance.PlayUiClick(volumeScale);
            }
            else
            {
                AudioPlayer.Instance.PlayUiBack(volumeScale);
            }
        }

        private string ResolveChannelKey()
        {
            return string.IsNullOrWhiteSpace(channelKey)
                ? AudioPlayer.DefaultUiSfxChannelKey
                : channelKey;
        }
    }
}
