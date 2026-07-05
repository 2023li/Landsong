using Landsong.AudioSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SettingPanelItem_Audio : MonoBehaviour
{
    [Header("Volume")]
    [SerializeField] private Slider bgmVolumeSlider;
    [SerializeField] private Slider ambienceVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private TMP_Text bgmVolumeText;
    [SerializeField] private TMP_Text ambienceVolumeText;
    [SerializeField] private TMP_Text sfxVolumeText;

    [Header("Mute")]
    [SerializeField] private Toggle mutedToggle;

    [Header("Preview")]
    [SerializeField] private Button previewSfxButton;
    [SerializeField] private AudioClip previewSfxClip;

    private AudioPlayer audioPlayer;

    private void OnEnable()
    {
        audioPlayer = AudioPlayer.Instance;
        audioPlayer.ChannelSettingsChanged += HandleAudioSettingsChanged;

        RefreshControls(audioPlayer.BgmVolume, audioPlayer.AmbienceVolume, audioPlayer.SfxVolume, audioPlayer.IsMuted);
        BindControls();
    }

    private void OnDisable()
    {
        UnbindControls();

        if (audioPlayer != null)
        {
            audioPlayer.ChannelSettingsChanged -= HandleAudioSettingsChanged;
            audioPlayer = null;
        }
    }

    private void BindControls()
    {
        if (bgmVolumeSlider != null)
        {
            bgmVolumeSlider.onValueChanged.RemoveListener(HandleBgmVolumeChanged);
            bgmVolumeSlider.onValueChanged.AddListener(HandleBgmVolumeChanged);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.onValueChanged.RemoveListener(HandleSfxVolumeChanged);
            sfxVolumeSlider.onValueChanged.AddListener(HandleSfxVolumeChanged);
        }

        if (ambienceVolumeSlider != null)
        {
            ambienceVolumeSlider.onValueChanged.RemoveListener(HandleAmbienceVolumeChanged);
            ambienceVolumeSlider.onValueChanged.AddListener(HandleAmbienceVolumeChanged);
        }

        if (mutedToggle != null)
        {
            mutedToggle.onValueChanged.RemoveListener(HandleMutedChanged);
            mutedToggle.onValueChanged.AddListener(HandleMutedChanged);
        }

        if (previewSfxButton != null)
        {
            previewSfxButton.onClick.RemoveListener(HandlePreviewSfxClicked);
            previewSfxButton.onClick.AddListener(HandlePreviewSfxClicked);
        }
    }

    private void UnbindControls()
    {
        if (bgmVolumeSlider != null)
        {
            bgmVolumeSlider.onValueChanged.RemoveListener(HandleBgmVolumeChanged);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.onValueChanged.RemoveListener(HandleSfxVolumeChanged);
        }

        if (ambienceVolumeSlider != null)
        {
            ambienceVolumeSlider.onValueChanged.RemoveListener(HandleAmbienceVolumeChanged);
        }

        if (mutedToggle != null)
        {
            mutedToggle.onValueChanged.RemoveListener(HandleMutedChanged);
        }

        if (previewSfxButton != null)
        {
            previewSfxButton.onClick.RemoveListener(HandlePreviewSfxClicked);
        }
    }

    private void HandleBgmVolumeChanged(float value)
    {
        AudioPlayer.Instance.SetBgmVolume(value);
        RefreshBgmVolumeText(value);
    }

    private void HandleSfxVolumeChanged(float value)
    {
        AudioPlayer.Instance.SetSfxVolume(value);
        RefreshSfxVolumeText(value);
    }

    private void HandleAmbienceVolumeChanged(float value)
    {
        AudioPlayer.Instance.SetAmbienceVolume(value);
        RefreshAmbienceVolumeText(value);
    }

    private void HandleMutedChanged(bool muted)
    {
        AudioPlayer.Instance.SetMuted(muted);
    }

    private void HandlePreviewSfxClicked()
    {
        AudioPlayer player = AudioPlayer.Instance;
        if (!player.PlaySfx(previewSfxClip))
        {
            player.PlayUiClick();
        }
    }

    private void HandleAudioSettingsChanged(float bgmVolume, float ambienceVolume, float sfxVolume, bool muted)
    {
        RefreshControls(bgmVolume, ambienceVolume, sfxVolume, muted);
    }

    private void RefreshControls(float bgmVolume, float ambienceVolume, float sfxVolume, bool muted)
    {
        if (bgmVolumeSlider != null)
        {
            bgmVolumeSlider.SetValueWithoutNotify(Mathf.Clamp01(bgmVolume));
        }

        if (ambienceVolumeSlider != null)
        {
            ambienceVolumeSlider.SetValueWithoutNotify(Mathf.Clamp01(ambienceVolume));
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.SetValueWithoutNotify(Mathf.Clamp01(sfxVolume));
        }

        if (mutedToggle != null)
        {
            mutedToggle.SetIsOnWithoutNotify(muted);
        }

        RefreshBgmVolumeText(bgmVolume);
        RefreshAmbienceVolumeText(ambienceVolume);
        RefreshSfxVolumeText(sfxVolume);
    }

    private void RefreshBgmVolumeText(float value)
    {
        if (bgmVolumeText != null)
        {
            bgmVolumeText.text = FormatVolume(value);
        }
    }

    private void RefreshSfxVolumeText(float value)
    {
        if (sfxVolumeText != null)
        {
            sfxVolumeText.text = FormatVolume(value);
        }
    }

    private void RefreshAmbienceVolumeText(float value)
    {
        if (ambienceVolumeText != null)
        {
            ambienceVolumeText.text = FormatVolume(value);
        }
    }

    private static string FormatVolume(float value)
    {
        return $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}%";
    }
}
