using System;
using System.Threading.Tasks;
using Moyo.Unity;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    // All fixed controls are authored in the prefab. Runtime code only binds values and actions.
    public sealed class UI_SettingPanel : UIPanelBase
    {
        [LabelText("状态说明"), Required]
        public TMP_Text Status;
        [LabelText("主音量"), Required]
        public Slider Master;
        [LabelText("音乐音量"), Required]
        public Slider Music;
        [LabelText("音效音量"), Required]
        public Slider Effects;
        [LabelText("环境音量"), Required]
        public Slider Ambient;
        [LabelText("界面缩放"), Required]
        public Slider UiScale;
        [LabelText("镜头速度"), Required]
        public Slider CameraSpeed;
        [LabelText("缩放速度"), Required]
        public Slider ZoomSpeed;
        [LabelText("镜头缓动"), Required]
        public Slider Smoothing;
        [LabelText("静音按钮"), Required]
        public Button Muted;
        [LabelText("语言按钮"), Required]
        public Button Language;
        [LabelText("重新扫描语言按钮"), Required]
        public Button RescanLanguages;
        [LabelText("全屏按钮"), Required]
        public Button Fullscreen;
        [LabelText("减少动态按钮"), Required]
        public Button ReducedMotion;
        [LabelText("高对比度按钮"), Required]
        public Button HighContrast;
        [LabelText("一般消息按钮"), Required]
        public Button GeneralMessages;
        [LabelText("经济消息按钮"), Required]
        public Button EconomyMessages;
        [LabelText("分辨率按钮"), Required]
        public Button Resolution;
        [LabelText("画质按钮"), Required]
        public Button Quality;
        [LabelText("静音文字"), Required]
        public TMP_Text MutedLabel;
        [LabelText("语言文字"), Required]
        public TMP_Text LanguageLabel;
        [LabelText("全屏文字"), Required]
        public TMP_Text FullscreenLabel;
        [LabelText("减少动态文字"), Required]
        public TMP_Text ReducedMotionLabel;
        [LabelText("高对比度文字"), Required]
        public TMP_Text HighContrastLabel;
        [LabelText("一般消息文字"), Required]
        public TMP_Text GeneralMessagesLabel;
        [LabelText("经济消息文字"), Required]
        public TMP_Text EconomyMessagesLabel;
        [LabelText("分辨率文字"), Required]
        public TMP_Text ResolutionLabel;
        [LabelText("画质文字"), Required]
        public TMP_Text QualityLabel;
        [LabelText("按键按钮"), Required]
        public Button[] KeyButtons;
        [LabelText("按键文字"), Required]
        public TMP_Text[] KeyLabels;
        [LabelText("应用按钮"), Required]
        public Button ApplyButton;
        [LabelText("恢复默认按钮"), Required]
        public Button DefaultsButton;
        [LabelText("返回按钮"), Required]
        public Button BackButton;
        Action close;
        InterfacePreferences draft, rollback;
        float deadline;
        int rebinding = -1;
        readonly List<(int width, int height)> resolutions = new List<(int width, int height)>();
        static readonly string[] keyNames =
        {
            "镜头前移",
            "镜头后移",
            "镜头左移",
            "镜头右移",
            "镜头左转",
            "镜头右转",
            "暂停"
        };
        public bool AwaitingDisplayConfirmation => rollback != null;

        public override Task OnCreateAsync()
        {
            Bind(ClosePanel);
            return base.OnCreateAsync();
        }

        public override Task OnOpenAsync(object args)
        {
            Begin();
            return Task.CompletedTask;
        }

        public override Task OnCloseAsync()
        {
            RevertDisplay();
            rebinding = -1;
            return Task.CompletedTask;
        }

        public override Task<bool> TryHandleBackAsync() => Task.FromResult(CancelRebind());
        async void ClosePanel()
        {
            try
            {
                await Manager.CloseAsync<UI_SettingPanel>();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
        }

        public void Bind(Action back)
        {
            ValidateConfiguration();
            close = back;
            foreach (var slider in new[]
            {
                Master,
                Music,
                Effects,
                Ambient,
                UiScale,
                CameraSpeed,
                ZoomSpeed,
                Smoothing
            }

            )
                slider.onValueChanged.RemoveAllListeners();
            foreach (var button in new[]
            {
                Muted,
                Language,
                RescanLanguages,
                Fullscreen,
                ReducedMotion,
                HighContrast,
                GeneralMessages,
                EconomyMessages,
                Resolution,
                Quality,
                ApplyButton,
                DefaultsButton,
                BackButton
            }.Concat(KeyButtons))
                button.onClick.RemoveAllListeners();
            BindSlider(Master, v => draft.Master = v);
            BindSlider(Music, v => draft.Music = v);
            BindSlider(Effects, v => draft.Effects = v);
            BindSlider(Ambient, v => draft.Ambient = v);
            BindSlider(UiScale, v => draft.UiScale = v);
            BindSlider(CameraSpeed, v => draft.CameraSpeed = v);
            BindSlider(ZoomSpeed, v => draft.ZoomSpeed = v);
            BindSlider(Smoothing, v => draft.Smoothing = v);
            Muted.onClick.AddListener(() =>
            {
                draft.Muted = !draft.Muted;
                RefreshLabels();
            });
            Fullscreen.onClick.AddListener(() =>
            {
                draft.Fullscreen = (draft.Fullscreen < 0 ? Screen.fullScreen : draft.Fullscreen != 0) ? 0 : 1;
                RefreshLabels();
            });
            ReducedMotion.onClick.AddListener(() =>
            {
                draft.ReducedMotion = !draft.ReducedMotion;
                RefreshLabels();
            });
            HighContrast.onClick.AddListener(() =>
            {
                draft.HighContrast = !draft.HighContrast;
                RefreshLabels();
            });
            GeneralMessages.onClick.AddListener(() =>
            {
                draft.GeneralMessages = !draft.GeneralMessages;
                RefreshLabels();
            });
            EconomyMessages.onClick.AddListener(() =>
            {
                draft.EconomyMessages = !draft.EconomyMessages;
                RefreshLabels();
            });
            Language.onClick.AddListener(CycleLanguage);
            RescanLanguages.onClick.AddListener(() =>
            {
                PresentationText.Discover(PresentationRuntime.LanguageDirectory);
                RefreshLabels();
            });
            Resolution.onClick.AddListener(CycleResolution);
            Quality.onClick.AddListener(() =>
            {
                draft.Quality = ((draft.Quality < 0 ? QualitySettings.GetQualityLevel() : draft.Quality) + 1) % QualitySettings.names.Length;
                RefreshLabels();
            });
            for (int i = 0; i < KeyButtons.Length; i++)
            {
                int at = i;
                KeyButtons[i].onClick.AddListener(() => BeginRebind(at));
            }

            ApplyButton.onClick.AddListener(Apply);
            DefaultsButton.onClick.AddListener(() =>
            {
                RevertDisplay();
                draft = new InterfacePreferences();
                RefreshControls();
            });
            BackButton.onClick.AddListener(() =>
            {
                RevertDisplay();
                close();
            });
        }

        protected override void ValidateLocalConfiguration()
        {
            base.ValidateLocalConfiguration();
            static void Need(UnityEngine.Object value, string name)
            {
                if (value == null)
                    throw new InvalidOperationException("设置面板检查器引用缺失：" + name);
            }

            Need(Status, nameof(Status));
            foreach (var pair in new[]
            {
                (Master, nameof(Master)),
                (Music, nameof(Music)),
                (Effects, nameof(Effects)),
                (Ambient, nameof(Ambient)),
                (UiScale, nameof(UiScale)),
                (CameraSpeed, nameof(CameraSpeed)),
                (ZoomSpeed, nameof(ZoomSpeed)),
                (Smoothing, nameof(Smoothing))
            }

            )
                Need(pair.Item1, pair.Item2);
            foreach (var pair in new[]
            {
                (Muted, nameof(Muted)),
                (Language, nameof(Language)),
                (RescanLanguages, nameof(RescanLanguages)),
                (Fullscreen, nameof(Fullscreen)),
                (ReducedMotion, nameof(ReducedMotion)),
                (HighContrast, nameof(HighContrast)),
                (GeneralMessages, nameof(GeneralMessages)),
                (EconomyMessages, nameof(EconomyMessages)),
                (Resolution, nameof(Resolution)),
                (Quality, nameof(Quality)),
                (ApplyButton, nameof(ApplyButton)),
                (DefaultsButton, nameof(DefaultsButton)),
                (BackButton, nameof(BackButton))
            }

            )
                Need(pair.Item1, pair.Item2);
            foreach (var pair in new[]
            {
                (MutedLabel, nameof(MutedLabel)),
                (LanguageLabel, nameof(LanguageLabel)),
                (FullscreenLabel, nameof(FullscreenLabel)),
                (ReducedMotionLabel, nameof(ReducedMotionLabel)),
                (HighContrastLabel, nameof(HighContrastLabel)),
                (GeneralMessagesLabel, nameof(GeneralMessagesLabel)),
                (EconomyMessagesLabel, nameof(EconomyMessagesLabel)),
                (ResolutionLabel, nameof(ResolutionLabel)),
                (QualityLabel, nameof(QualityLabel))
            }

            )
                Need(pair.Item1, pair.Item2);
            if (KeyButtons == null || KeyLabels == null || KeyButtons.Length != 7 || KeyLabels.Length != 7 || KeyButtons.Any(x => x == null) || KeyLabels.Any(x => x == null))
                throw new InvalidOperationException("设置面板 KeyButtons/KeyLabels 必须各绑定 7 项。");
        }

        void BindSlider(Slider slider, Action<float> set) => slider.onValueChanged.AddListener(value => set(value));
        public void Begin()
        {
            RevertDisplay();
            draft = InterfaceSettings.Current.Copy();
            BuildResolutions();
            RefreshControls();
        }

        void BuildResolutions()
        {
            resolutions.Clear();
            resolutions.AddRange(Screen.resolutions.Select(r => (r.width, r.height)).Where(r => r.width >= 800 && r.height >= 600).Distinct());
            if (!resolutions.Contains((Screen.width, Screen.height)))
                resolutions.Add((Screen.width, Screen.height));
        }

        void RefreshControls()
        {
            Master.SetValueWithoutNotify(draft.Master);
            Music.SetValueWithoutNotify(draft.Music);
            Effects.SetValueWithoutNotify(draft.Effects);
            Ambient.SetValueWithoutNotify(draft.Ambient);
            UiScale.SetValueWithoutNotify(draft.UiScale);
            CameraSpeed.SetValueWithoutNotify(draft.CameraSpeed);
            ZoomSpeed.SetValueWithoutNotify(draft.ZoomSpeed);
            Smoothing.SetValueWithoutNotify(draft.Smoothing);
            RefreshLabels();
        }

        void RefreshLabels()
        {
            MutedLabel.text = "静音：" + (draft.Muted ? "开" : "关");
            LanguageLabel.text = "语言 / Language：" + draft.Language;
            FullscreenLabel.text = "全屏：" + ((draft.Fullscreen < 0 ? Screen.fullScreen : draft.Fullscreen != 0) ? "开" : "关");
            ReducedMotionLabel.text = "减少动态效果：" + (draft.ReducedMotion ? "开" : "关");
            HighContrastLabel.text = "高对比度标记：" + (draft.HighContrast ? "开" : "关");
            GeneralMessagesLabel.text = "一般消息提示：" + (draft.GeneralMessages ? "开" : "关");
            EconomyMessagesLabel.text = "经济消息提示：" + (draft.EconomyMessages ? "开" : "关");
            ResolutionLabel.text = "分辨率：" + (draft.Width > 0 ? draft.Width : Screen.width) + " × " + (draft.Height > 0 ? draft.Height : Screen.height) + "（点击切换）";
            QualityLabel.text = "画质：" + QualitySettings.names[draft.Quality < 0 ? QualitySettings.GetQualityLevel() : draft.Quality];
            var keys = Keys();
            for (int i = 0; i < 7; i++)
                KeyLabels[i].text = keyNames[i] + "：" + keys[i];
            Status.text = string.IsNullOrEmpty(PresentationText.Diagnostics) ? "修改后点击应用。显示变更需在 15 秒内确认，否则恢复。" : PresentationText.Diagnostics;
        }

        void CycleLanguage()
        {
            var languages = new[]
            {
                "zh-Hans",
                "en"
            }.Concat(PresentationText.Packs.Keys.OrderBy(k => k)).Distinct().ToArray();
            int index = Array.IndexOf(languages, draft.Language);
            draft.Language = languages[(index + 1 + languages.Length) % languages.Length];
            RefreshLabels();
        }

        void CycleResolution()
        {
            if (resolutions.Count == 0)
                BuildResolutions();
            var current = (draft.Width > 0 ? draft.Width : Screen.width, draft.Height > 0 ? draft.Height : Screen.height);
            int index = resolutions.IndexOf(current);
            var r = resolutions[(index + 1 + resolutions.Count) % resolutions.Count];
            draft.Width = r.width;
            draft.Height = r.height;
            RefreshLabels();
        }

        void BeginRebind(int index)
        {
            rebinding = index;
            Status.text = "按下新按键。Esc 取消；Esc / R / H / ` / 数字键保留，不允许重复。";
        }

        Key[] Keys() => new[]
        {
            draft.Forward,
            draft.Back,
            draft.Left,
            draft.Right,
            draft.RotateLeft,
            draft.RotateRight,
            draft.Pause
        };
        void SetKeys(Key[] keys)
        {
            draft.Forward = keys[0];
            draft.Back = keys[1];
            draft.Left = keys[2];
            draft.Right = keys[3];
            draft.RotateLeft = keys[4];
            draft.RotateRight = keys[5];
            draft.Pause = keys[6];
        }

        public bool CancelRebind()
        {
            if (rebinding < 0)
                return false;
            rebinding = -1;
            Status.text = "已取消按键修改。";
            return true;
        }

        void OnDisable()
        {
            RevertDisplay();
            rebinding = -1;
        }

        void Update()
        {
            if (rollback != null)
            {
                Status.text = "保留画面设置？" + Mathf.CeilToInt(deadline - Time.unscaledTime) + " 秒后恢复。点击“应用 / 保留画面”。";
                if (Time.unscaledTime >= deadline)
                {
                    RevertDisplay();
                    draft = InterfaceSettings.Current.Copy();
                    RefreshControls();
                }
            }

            if (rebinding < 0 || Keyboard.current == null)
                return;
            foreach (var control in Keyboard.current.allKeys)
                if (control.wasPressedThisFrame)
                {
                    if (control.keyCode == Key.Escape)
                    {
                        CancelRebind();
                        return;
                    }

                    var keys = Keys();
                    if (!InterfacePreferences.AllowedKey(control.keyCode) || keys.Where((_, i) => i != rebinding).Contains(control.keyCode))
                    {
                        Status.text = "该按键被保留或已使用，请选择其他按键。";
                        return;
                    }

                    keys[rebinding] = control.keyCode;
                    SetKeys(keys);
                    KeyLabels[rebinding].text = "已绑定：" + control.keyCode;
                    rebinding = -1;
                    Status.text = "按键已修改，应用后保存。";
                    return;
                }
        }

        void Apply()
        {
            if (rollback != null)
            {
                rollback = null;
                InterfaceSettings.Apply(draft);
                Status.text = "设置已保存。";
                return;
            }

            draft.Validate();
            bool changed = (draft.Width > 0 && (draft.Width != Screen.width || draft.Height != Screen.height)) || (draft.Fullscreen >= 0 && (draft.Fullscreen != 0) != Screen.fullScreen);
            if (changed)
            {
                rollback = InterfaceSettings.Current.Copy();
                rollback.Width = Screen.width;
                rollback.Height = Screen.height;
                rollback.Fullscreen = Screen.fullScreen ? 1 : 0;
                deadline = Time.unscaledTime + 15;
                InterfaceSettings.Apply(draft, false);
            }
            else
            {
                InterfaceSettings.Apply(draft);
                Status.text = "设置已保存。";
            }
        }

        void RevertDisplay()
        {
            if (rollback == null)
                return;
            var prior = rollback;
            rollback = null;
            InterfaceSettings.Apply(prior, false);
        }
    }
}
