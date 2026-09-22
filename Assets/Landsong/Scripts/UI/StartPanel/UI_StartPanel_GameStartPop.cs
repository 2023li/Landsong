using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Moyo.Unity;
using System.Threading.Tasks;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_StartPanel_GameStartPop : UIViewBase
    {
        [LabelText("地图目录"), Required]
        public EcsMapMenuCatalog Catalog;
        [LabelText("地图下拉框"), Required]
        public TMP_Dropdown MapSelection;
        [LabelText("难度下拉框"), Required]
        public TMP_Dropdown DifficultySelection;
        [LabelText("王朝名称"), Required]
        public TMP_InputField DynastyName;
        [LabelText("地图说明"), Required]
        public TMP_Text MapInfo;
        [LabelText("地图缩略图"), Required]
        public Image MapPreview;
        [LabelText("建立王朝按钮"), Required]
        public Button CreateDynastyButton;
        [LabelText("返回按钮"), Required]
        public Button BackButton;
        Action<Action> begin;
        Action close;
        bool initialized;
        bool HasMaps => Catalog != null && Catalog.Maps != null && Catalog.Maps.Length > 0;

        protected override void ValidateLocalConfiguration()
        {
            base.ValidateLocalConfiguration();
            foreach (var value in new UnityEngine.Object[]
            {
                Catalog,
                MapSelection,
                DifficultySelection,
                DynastyName,
                MapInfo,
                MapPreview,
                CreateDynastyButton,
                BackButton
            }

            )
                if (value == null)
                    throw new InvalidOperationException("开始游戏弹窗检查器引用不完整。");
        }

        public void Bind(Action<Action> start, Action back)
        {
            ValidateConfiguration();
            begin = start;
            close = back;
            if (initialized)
                return;
            initialized = true;
            MapSelection.ClearOptions();
            if (HasMaps)
                MapSelection.AddOptions(Catalog.Maps.Select(map => map.DisplayName).ToList());
            DifficultySelection.ClearOptions();
            DifficultySelection.AddOptions(new List<string> { "简单", "普通", "困难" });
            DifficultySelection.SetValueWithoutNotify(1);
            CreateDynastyButton.onClick.AddListener(() =>
            {
                if (!HasMaps)
                {
                    MapInfo.text = "未配置地图，请先添加可用地图。";
                    return;
                }

                begin(() => EcsSceneFlow.NewGame(Catalog.Maps[Mathf.Clamp(MapSelection.value, 0, Catalog.Maps.Length - 1)].Id, DynastyName.text));
            });
            BackButton.onClick.AddListener(() => close());
            MapSelection.onValueChanged.AddListener(_ => Refresh());
        }

        public async void Open()
        {
            try
            {
                await OpenViewAsync();
                DynastyName.Select();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
        }

        public async void Close()
        {
            try
            {
                await CloseViewAsync();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
        }

        public override Task OnOpenAsync(object args)
        {
            Refresh();
            return Task.CompletedTask;
        }

        public override Task OnCloseAsync()
        {
            if (MapSelection.IsExpanded)
                MapSelection.Hide();
            if (DifficultySelection.IsExpanded)
                DifficultySelection.Hide();
            return Task.CompletedTask;
        }

        public override Task OnReleaseAsync()
        {
            begin = null;
            close = null;
            return Task.CompletedTask;
        }

        public void Back()
        {
            if (MapSelection.IsExpanded)
            {
                MapSelection.Hide();
                return;
            }

            if (DifficultySelection.IsExpanded)
            {
                DifficultySelection.Hide();
                return;
            }

            close();
        }

        public void Refresh()
        {
            CreateDynastyButton.interactable = MapSelection.interactable = HasMaps;
            if (!HasMaps)
            {
                MapInfo.text = "未配置地图，请先添加可用地图。";
                MapPreview.gameObject.SetActive(false);
                return;
            }

            var map = Catalog.Maps[Mathf.Clamp(MapSelection.value, 0, Catalog.Maps.Length - 1)];
            MapInfo.text = map.DisplayName + "\n" + (string.IsNullOrWhiteSpace(map.Description) ? "在此地图建立新王朝。白天建造，夜晚巡逻/作战。" : map.Description);
            MapPreview.sprite = map.Thumbnail;
            MapPreview.gameObject.SetActive(map.Thumbnail != null);
        }
    }
}
