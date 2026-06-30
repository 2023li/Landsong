using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingPanelItem_Accessibility : MonoBehaviour
{

    private void Awake()
    {
        Awake_语言选择();

    }
    private void OnEnable()
    {
        
    }
    private void Start()
    {
        Start_语言选择();
    }
    #region POP语言选择

    [SerializeField, BoxGroup("POP语言选择")] private Button pop_上一个语言;
    [SerializeField, BoxGroup("POP语言选择")] private Button pop_下一个语言;
    [SerializeField, BoxGroup("POP语言选择")] private TMP_Text txt_当前语言显示名称;

    // 需要在真正的 Awake() 里手动调用。
    private void Awake_语言选择()
    {
        if (pop_上一个语言 != null)
        {
            pop_上一个语言.onClick.AddListener(SelectPreviousLanguage);
        }

        if (pop_下一个语言 != null)
        {
            pop_下一个语言.onClick.AddListener(SelectNextLanguage);
        }

       

        
    }

    // 需要在真正的 Start() 里手动调用。
    private void Start_语言选择()
    {
        if (GameLocalizationManager.Instance == null)
        {
            Debug.LogWarning("语言选择失败：GameLocalizationManager 不存在。");
            return;
        }

        GameLocalizationManager.Instance.RefreshAllLanguagePacksCache();

        if (GameLocalizationManager.Instance.AllLanguagePackCount <= 0)
        {
            Debug.LogWarning("语言选择失败：没有找到任何语言包。");
            return;
        }

        GameLocalizationManager.Instance.SyncCurrentLanguagePackIndexFromSaveData();

        RefreshCurrentLanguageText();

        if (string.IsNullOrEmpty(GameLocalizationManager.Instance.CurrentLanguageData.CurrentLanguageCode))
        {
            Show_pop语言选择();
        }
    }

    // 显示语言选择弹框。
    [Button, BoxGroup("POP语言选择")]
    private void Show_pop语言选择()
    {
        if (GameLocalizationManager.Instance != null)
        {
            GameLocalizationManager.Instance.SyncCurrentLanguagePackIndexFromSaveData();
        }

        RefreshCurrentLanguageText();
    }

  
    // 选择上一个语言。
    private void SelectPreviousLanguage()
    {
        if (GameLocalizationManager.Instance == null)
        {
            return;
        }

        GameLocalizationManager.Instance.SelectPreviousLanguagePack();
        RefreshCurrentLanguageText();
        ConfirmCurrentLanguage();
    }

    // 选择下一个语言。
    private void SelectNextLanguage()
    {
        if (GameLocalizationManager.Instance == null)
        {
            return;
        }

        GameLocalizationManager.Instance.SelectNextLanguagePack();
        RefreshCurrentLanguageText();
        ConfirmCurrentLanguage();
    }

    // 刷新当前显示的语言名称。
    private void RefreshCurrentLanguageText()
    {
        if (txt_当前语言显示名称 == null)
        {
            return;
        }

        if (GameLocalizationManager.Instance == null
            || GameLocalizationManager.Instance.AllLanguagePackCount <= 0)
        {
            txt_当前语言显示名称.text = "无可用语言";
            return;
        }

        LanguagePackInfo languagePack = GameLocalizationManager.Instance.GetCurrentPreviewLanguagePack();

        if (languagePack == null)
        {
            txt_当前语言显示名称.text = "无可用语言";
            return;
        }

        if (!string.IsNullOrWhiteSpace(languagePack.DisplayName))
        {
            txt_当前语言显示名称.text = languagePack.DisplayName;
            return;
        }

        txt_当前语言显示名称.text = languagePack.LanguageCode;
    }

    // 确认当前语言。
    private void ConfirmCurrentLanguage()
    {
        if (GameLocalizationManager.Instance == null || GameLocalizationManager.Instance.AllLanguagePackCount <= 0)
        {
            return;
        }

        StartCoroutine(ConfirmCurrentLanguageCoroutine());
    }

    // 确认语言并应用到 Unity Localization。
    private IEnumerator ConfirmCurrentLanguageCoroutine()
    {
        if (GameLocalizationManager.Instance == null)
        {
            yield break;
        }

        yield return GameLocalizationManager.Instance.ApplyCurrentPreviewLanguagePack();
    }

    #endregion
}
