using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoadingPanelItem_ProgressBar : MonoBehaviour
{
   

    [SerializeField, BoxGroup("引用"), Required]
    private Slider progressSlider;

    [SerializeField, BoxGroup("引用"), Required]
    private TMP_Text loadingProgressText;

    [SerializeField, BoxGroup("文本"), MinValue(0.02f)]
    private float progressTextUpdateInterval = 0.2f;

    [SerializeField, BoxGroup("文本")]
    private string progressTextFormat = "{0}%";

    [SerializeField, BoxGroup("文本")]
    private string waitConfirmText = "按任意键继续";

    private float currentProgress;
    private float lastProgressTextUpdateTime;
    private int lastDisplayedProgressPercent = -1;
    private bool isWaitingPlayerConfirm;

    private void OnEnable()
    {
        Landsong.Localization.L10n.LanguageChanged += RefreshLocalizedText;
    }

    private void OnDisable()
    {
        Landsong.Localization.L10n.LanguageChanged -= RefreshLocalizedText;
    }

    public void Show()
    {
        isWaitingPlayerConfirm = false;
        gameObject.SetActive(true);
        SetProgress(0f, true);
    }

    public void Hide()
    {
        isWaitingPlayerConfirm = false;
        gameObject.SetActive(false);
    }

    public void SetProgress(float progress)
    {
        SetProgress(progress, false);
    }

    public void BeginWaitPlayerConfirm()
    {
        isWaitingPlayerConfirm = true;
        currentProgress = 1f;

        if (progressSlider != null)
        {
            progressSlider.value = currentProgress;
        }

        if (loadingProgressText != null)
        {
            loadingProgressText.text = Landsong.Localization.L10n.Ui("ui.loading.press_any_key", waitConfirmText);
        }
    }

    private void RefreshLocalizedText()
    {
        if (isWaitingPlayerConfirm && loadingProgressText != null)
        {
            loadingProgressText.text = Landsong.Localization.L10n.Ui("ui.loading.press_any_key", waitConfirmText);
        }
    }

    private void SetProgress(float progress, bool forceUpdateText)
    {
        currentProgress = Mathf.Clamp01(progress);

        if (progressSlider != null)
        {
            progressSlider.value = currentProgress;
        }

        if (isWaitingPlayerConfirm)
        {
            return;
        }

        TryUpdateProgressText(forceUpdateText);
    }

    private void TryUpdateProgressText(bool forceUpdate)
    {
        int progressPercent = Mathf.RoundToInt(currentProgress * 100f);
        bool reachedEndForFirstTime = progressPercent >= 100 && lastDisplayedProgressPercent != 100;
        bool intervalReached = Time.unscaledTime - lastProgressTextUpdateTime >= progressTextUpdateInterval;

        if (!forceUpdate && !reachedEndForFirstTime && !intervalReached)
        {
            return;
        }

        lastProgressTextUpdateTime = Time.unscaledTime;
        lastDisplayedProgressPercent = progressPercent;

        if (loadingProgressText != null)
        {
            loadingProgressText.text = string.Format(progressTextFormat, progressPercent);
        }
    }

   
}
