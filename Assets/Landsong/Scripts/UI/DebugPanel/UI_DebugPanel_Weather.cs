using System;
using System.Collections.Generic;
using TMPro;
using Sirenix.OdinInspector;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_DebugPanel_Weather : MonoBehaviour
    {
        [SerializeField, LabelText("按钮模板"), Required] private Button buttonTemplate;
        [SerializeField, LabelText("标题文字"), Required] private TMP_Text title;

        readonly List<Button> controls = new List<Button>();
        TMP_Text status;
        float nextRefresh;

        public static bool HasGameWorld => TryWorld(out _, out _);

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            EnsureControls();
            RefreshStatus();
        }

        void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .25f;
            RefreshStatus();
        }

        void EnsureControls()
        {
            if (status != null) return;
            if (buttonTemplate == null || title == null)
                throw new InvalidOperationException("天气调试页缺少按钮模板或标题文字。");

            buttonTemplate.gameObject.SetActive(false);
            status = Instantiate(title, transform);
            status.name = "Weather Debug Status";
            status.raycastTarget = false;
            status.fontSize = 24;
            var statusRect = status.rectTransform;
            statusRect.anchorMin = statusRect.anchorMax = new Vector2(.5f, .83f);
            statusRect.anchoredPosition = Vector2.zero;
            statusRect.sizeDelta = new Vector2(900, 90);

            var container = new GameObject("Weather Debug Controls", typeof(RectTransform));
            container.transform.SetParent(transform, false);
            var rect = (RectTransform)container.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(730, 230);
            rect.anchoredPosition = Vector2.zero;
            var layout = container.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(175, 50);
            layout.spacing = new Vector2(10, 10);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 4;
            layout.childAlignment = TextAnchor.UpperCenter;

            AddButton(rect, "晴天", () => ChangeWeather(WeatherKind.Sunny));
            AddButton(rect, "小雨", () => ChangeWeather(WeatherKind.LightRain));
            AddButton(rect, "中雨", () => ChangeWeather(WeatherKind.Rain));
            AddButton(rect, "大雨", () => ChangeWeather(WeatherKind.HeavyRain));
            AddButton(rect, "雪天", () => ChangeWeather(WeatherKind.Snow));
            AddButton(rect, "雷鸣", TriggerThunder);
            AddButton(rect, "落雷", TriggerLightning);
            AddButton(rect, "降温 5°C", () => ChangeTemperature(-5));
            AddButton(rect, "无风", () => ChangeWind(WindKind.Calm));
            AddButton(rect, "微风", () => ChangeWind(WindKind.Light));
            AddButton(rect, "中风", () => ChangeWind(WindKind.Moderate));
            AddButton(rect, "强风", () => ChangeWind(WindKind.Strong));
            AddButton(rect, "风向 -45°", () => ChangeDirection(-45));
            AddButton(rect, "风向 +45°", () => ChangeDirection(45));
            AddButton(rect, "升温 5°C", () => ChangeTemperature(5));
        }

        void AddButton(RectTransform parent, string label, Action action)
        {
            var objectButton = new GameObject("btn_" + label, typeof(RectTransform));
            objectButton.transform.SetParent(parent, false);
            var image = objectButton.AddComponent<Image>();
            if (buttonTemplate.targetGraphic is Image prototype)
            {
                image.sprite = prototype.sprite;
                image.type = prototype.type;
                image.color = prototype.color;
            }
            var button = objectButton.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = buttonTemplate.colors;
            objectButton.AddComponent<UI_Common_Click>().Target = button;
            var textObject = new GameObject("Label", typeof(RectTransform));
            textObject.transform.SetParent(objectButton.transform, false);
            var textRect = (RectTransform)textObject.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            var text = textObject.AddComponent<TextMeshProUGUI>();
            text.font = title.font;
            text.fontSize = 22;
            text.color = new Color(.15f, .15f, .15f);
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.text = label;
            var textBinding = textObject.AddComponent<UI_Common_TextBinding>();
            textBinding.Target = text;
            textBinding.ButtonLabel = true;
            button.onClick.AddListener(() =>
            {
                action();
                RefreshStatus();
            });
            controls.Add(button);
        }

        public void RefreshStatus()
        {
            if (status == null && Application.isPlaying) EnsureControls();
            if (status == null) return;
            var available = TryWorld(out var em, out var root);
            foreach (var button in controls)
                button.interactable = available;
            if (!available)
            {
                status.text = "当前没有运行中的游戏世界；F8 仍可打开调试面板。";
                return;
            }
            var state = em.GetComponentData<SeasonWeatherState>(root);
            status.text = $"第 {state.DayTurn} 回合  {state.Season}  {WeatherKindOps.DisplayName(state.Weather)}  {state.Temperature}°C\n"
                + $"风力 {state.Wind} / 风向 {state.WindDegrees:0}°  落雷 {state.LightningCount}/{state.LightningLimit}\n"
                + "调试修改持续到下次黎明，之后恢复每日随机抽取。";
        }

        static bool TryWorld(out EntityManager em, out Entity root)
        {
            em = default;
            root = Entity.Null;
            var world = World.DefaultGameObjectInjectionWorld;
            if (!EcsSceneFlow.GameReady || world == null || !world.IsCreated)
                return false;
            em = world.EntityManager;
            root = WorldQueries.Root(em);
            return root != Entity.Null && em.Exists(root) && em.HasComponent<SimulationReady>(root)
                && em.HasComponent<SeasonWeatherState>(root);
        }

        void ChangeWeather(WeatherKind kind)
        {
            if (!TryWorld(out var em, out var root)) return;
            ApplyWeather(em, root, kind);
        }

        void TriggerThunder()
        {
            if (TryWorld(out var em, out var root))
                LightningOps.DebugThunder(em, root);
        }

        void TriggerLightning()
        {
            if (!TryWorld(out var em, out var root)) return;
            LightningOps.DebugStrike(em, root, (uint)UnityEngine.Random.Range(1, int.MaxValue));
        }

        public static void ApplyWeather(EntityManager em, Entity root, WeatherKind kind)
        {
            var state = em.GetComponentData<SeasonWeatherState>(root);
            var wasSnow = state.Weather == WeatherKind.Snow;
            state.Weather = kind;
            if (WeatherKindOps.IsRain(kind))
            {
                state.Temperature = math.max(0, state.Temperature);
                state.LightningLimit = (byte)math.clamp((int)state.LightningLimit, 1, 3);
                state.LightningCount = (byte)math.min((int)state.LightningCount, (int)state.LightningLimit);
                state.NextThunderAt = math.max(state.NextThunderAt, state.DayElapsed + 60);
            }
            else
            {
                if (kind == WeatherKind.Snow) state.Temperature = math.min(-1, state.Temperature);
                state.LightningLimit = state.LightningCount = 0;
                state.NextThunderAt = 0;
            }
            em.SetComponentData(root, state);
            if (wasSnow != (kind == WeatherKind.Snow) && em.HasComponent<GridData>(root))
            {
                var grid = em.GetComponentData<GridData>(root);
                grid.Revision++;
                em.SetComponentData(root, grid);
                SurfaceNavigationGraph.Invalidate(em, root);
            }
        }

        void ChangeTemperature(int amount)
        {
            if (!TryWorld(out var em, out var root)) return;
            var state = em.GetComponentData<SeasonWeatherState>(root);
            var settings = em.GetComponentData<SeasonWeatherSettings>(root);
            var index = (int)state.Season;
            state.Temperature = math.clamp(state.Temperature + amount,
                settings.MinimumTemperature[index], settings.MaximumTemperature[index]);
            em.SetComponentData(root, state);
            if (WeatherKindOps.IsRain(state.Weather) && state.Temperature < 0)
                ApplyWeather(em, root, WeatherKind.Snow);
            else if (state.Weather == WeatherKind.Snow && state.Temperature >= 0)
                ApplyWeather(em, root, WeatherKind.Rain);
        }

        void ChangeWind(WindKind kind)
        {
            if (!TryWorld(out var em, out var root)) return;
            var state = em.GetComponentData<SeasonWeatherState>(root);
            state.Wind = kind;
            em.SetComponentData(root, state);
        }

        void ChangeDirection(int degrees)
        {
            if (!TryWorld(out var em, out var root)) return;
            var state = em.GetComponentData<SeasonWeatherState>(root);
            state.WindDegrees = (state.WindDegrees + degrees + 360) % 360;
            em.SetComponentData(root, state);
        }
    }
}
