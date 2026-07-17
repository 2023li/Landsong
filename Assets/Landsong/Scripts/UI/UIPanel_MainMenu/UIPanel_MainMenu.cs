using System;
using System.Collections;
using System.Collections.Generic;
using Landsong.Localization;
using System.Threading.Tasks;
using Landsong.AppSystem;
using Moyo.Unity;
using Sirenix.OdinInspector;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;


    public class UIPanel_MainMenu : UIPanelBase
    {

        [SerializeField] private Button btn_继续游戏;
        [SerializeField] private Button btn_新游戏;
        [SerializeField] private Button btn_存档界面;
        [SerializeField] private Button btn_设置界面;
        [SerializeField] private Button btn_显示退出pop;
        [SerializeField] private TMP_Text txt_App版本号;



        private void Awake()
        {
            RefreshRuntimeReferences();
            RefreshContinueGameButton();

            btn_继续游戏.onClick.AddListener(() => { AppManager.Instance.ContinueGame(); });

            btn_新游戏.onClick.AddListener(() => { Show_POP世界参数(); });

            btn_存档界面.onClick.AddListener(async () => { await UIManager.Instance.OpenAsync<UIPanel_LoadGame>(); });

            btn_设置界面.onClick.AddListener(async () =>
            {
                await UIManager.Instance.OpenAsync<UIPanel_Setting>();
            });

            btn_显示退出pop.onClick.AddListener(() =>
            {
                Show_退出pop();
            });

            Awake_退出相关();

            Awake_语言选择弹框();

            worldArgsPanel?.Hide();

            txt_App版本号.text = AppManager.Instance.Version;
        }


        public override Task OnOpenAsync(object args)
        {
            RefreshContinueGameButton();
            return base.OnOpenAsync(args);
        }

        public override Task OnFocusAsync()
        {
            RefreshContinueGameButton();
            return base.OnFocusAsync();
        }

        private void RefreshContinueGameButton()
        {
            if (btn_继续游戏 == null)
            {
                return;
            }

            bool hasSave = DataManager.Instance != null && DataManager.Instance.GetLastGameDataMeta() != null;
            btn_继续游戏.gameObject.SetActive(hasSave);
        }

        protected override void Start()
        {
            base.Start();
            Start_pop语言选择();
        }


        #region 退出相关
        [SerializeField, BoxGroup("退出相关")] private GameObject pop_Quit;
        [SerializeField, BoxGroup("退出相关")] private Button btn_退出弹框_确认退出;
        [SerializeField, BoxGroup("退出相关")] private Button btn_退出弹框_取消退出;

        private void Awake_退出相关()
        {
            btn_退出弹框_确认退出.onClick.AddListener(() =>
            {
                AppManager.Instance.ExitApp();
            });
            btn_退出弹框_取消退出.onClick.AddListener(() =>
            {
                Hide_退出pop();
            });

            pop_Quit.gameObject.SetActive(false);
        }

        private void Show_退出pop()
        {
            pop_Quit.gameObject.SetActive(true);
        }

        private void Hide_退出pop()
        {
            pop_Quit.gameObject.SetActive(false);
        }
        #endregion

        #region POP语言选择

        [SerializeField, BoxGroup("POP语言选择")] private RectTransform pop_确认语言;
        [SerializeField, BoxGroup("POP语言选择")] private Button pop_上一个语言;
        [SerializeField, BoxGroup("POP语言选择")] private Button pop_下一个语言;
        [SerializeField, BoxGroup("POP语言选择")] private Button btn_确认选择语言;
        [SerializeField, BoxGroup("POP语言选择")] private TMP_Text txt_当前语言显示名称;

        // 需要在真正的 Awake() 里手动调用。
        private void Awake_语言选择弹框()
        {
            if (pop_上一个语言 != null)
            {
                pop_上一个语言.onClick.AddListener(SelectPreviousLanguage);
            }

            if (pop_下一个语言 != null)
            {
                pop_下一个语言.onClick.AddListener(SelectNextLanguage);
            }

            if (btn_确认选择语言 != null)
            {
                btn_确认选择语言.onClick.AddListener(ConfirmCurrentLanguage);
            }

            if (pop_确认语言 != null)
            {
                pop_确认语言.gameObject.SetActive(false);
            }
        }

        // 需要在真正的 Start() 里手动调用。
        private void Start_pop语言选择()
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

            if (GameLocalizationManager.Instance.CurrentLanguageData.UseSystemLanguage)
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

            if (pop_确认语言 != null)
            {
                pop_确认语言.gameObject.SetActive(true);
            }

            RefreshCurrentLanguageText();
        }

        // 隐藏语言选择弹框。
        private void Hide_pop语言选择()
        {
            if (pop_确认语言 != null)
            {
                pop_确认语言.gameObject.SetActive(false);
            }
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
            txt_当前语言显示名称.text = L10n.Ui("ui.language.none_available", "无可用语言");
                return;
            }

            LanguagePackInfo languagePack = GameLocalizationManager.Instance.GetCurrentPreviewLanguagePack();

            if (languagePack == null)
            {
            txt_当前语言显示名称.text = L10n.Ui("ui.language.none_available", "无可用语言");
                return;
            }

            if (!string.IsNullOrWhiteSpace(languagePack.DisplayName))
            {
                txt_当前语言显示名称.text = languagePack.DisplayName;
                return;
            }

            txt_当前语言显示名称.text = languagePack.LocaleCode;
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

            Hide_pop语言选择();
        }

        #endregion

        #region POP世界参数
        [SerializeField] private MainMenuItem_MapSelectionPopup worldArgsPanel;


        private void Show_POP世界参数()
        {
            RefreshRuntimeReferences();

            if (worldArgsPanel == null)
            {
                Debug.LogWarning("打开世界参数面板失败：没有找到 MainMenuItem_WorldSettingPanel。", this);
                return;
            }

            worldArgsPanel.Show();

        }

        private void RefreshRuntimeReferences()
        {
            if (worldArgsPanel == null)
            {
                worldArgsPanel = GetComponentInChildren<MainMenuItem_MapSelectionPopup>(true);
            }
        }
        #endregion

    }

