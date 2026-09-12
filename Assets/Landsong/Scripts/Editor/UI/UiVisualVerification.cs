#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Landsong.ECS.Presentation;
using Landsong.Editor.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Landsong.ECS.Editor
{
    public static class UiVisualVerification
    {
        const string Output = "Library/LandsongEcs/UiPreviews";
        public static string RenderAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("预制体视觉验收需要退出Play。");
            Directory.CreateDirectory(Output);
            var files = new List<string>();
            foreach (var path in new[] { ApplicationUiMigration.BootPath, ApplicationUiMigration.StartPath, ApplicationUiMigration.LoadingPath,
                ApplicationUiMigration.SettingPath, ApplicationUiMigration.SavePath, ApplicationUiMigration.ConfirmPath, ApplicationUiMigration.GamePath })
                files.Add(Render(path, 1920, 1080));
            files.Add(Render(ApplicationUiMigration.StartPath, 1280, 720, "GameStartPop"));
            files.Add(Render(ApplicationUiMigration.GamePath, 1280, 720));
            files.Add(Render(ApplicationUiMigration.GamePath, 1920, 1080, "Technology"));
            files.Add(Render(ApplicationUiMigration.GamePath, 1920, 1080, "BuildingDetails"));
            files.Add(Render(ApplicationUiMigration.GamePath, 1920, 1080, "Talent"));
            files.Add(Render(ApplicationUiMigration.GamePath, 1920, 1080, "RoyalDetails"));
            files.Add(Render(ApplicationUiMigration.GamePath, 1280, 720, "RoyalDetails", 1.5f));
            foreach (var state in new[] { "Marriage", "Portrait", "SoldierDetails" })
            {
                files.Add(Render(ApplicationUiMigration.GamePath, 1920, 1080, state));
                files.Add(Render(ApplicationUiMigration.GamePath, 1280, 720, state, 1.5f));
            }
            foreach (var path in new[] { ApplicationUiMigration.SettingPath, ApplicationUiMigration.SavePath, ApplicationUiMigration.GamePath })
                files.Add(Render(path, 1280, 720, uiScale: 1.5f));
            foreach (var path in new[] { ApplicationUiMigration.StartPath, ApplicationUiMigration.SettingPath, ApplicationUiMigration.SavePath, ApplicationUiMigration.GamePath })
                files.Add(Render(path, 2560, 1080));
            files.Add(Render(ApplicationUiMigration.StartPath, 1280, 720, "GameStartPop", 1.5f));
            files.Add(Render(ApplicationUiMigration.ConfirmPath, 1280, 720, uiScale: 1.5f));
            File.WriteAllLines(Output + "/files.txt", files);
            return string.Join("\n", files);
        }
        static string Render(string path, int width, int height, string state = null, float uiScale = 1)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            RenderTexture render = null; Texture2D pixels = null;
            var previous = RenderTexture.active;
            try
            {
                var cameraObject = new GameObject("PreviewCamera", typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.GetComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .035f, .055f); camera.orthographic = true; camera.orthographicSize = height / 2f; camera.nearClipPlane = .1f; camera.farClipPlane = 100; camera.transform.position = new Vector3(0, 0, -10);
                camera.scene = scene; camera.useOcclusionCulling = false;
                render = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32); render.Create(); camera.targetTexture = render;
                var canvasObject = new GameObject("PreviewCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)); SceneManager.MoveGameObjectToScene(canvasObject, scene);
                var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080) / uiScale; scaler.matchWidthOrHeight = .5f;
                scaler.enabled = false; scaler.enabled = true;
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? throw new InvalidOperationException("缺少面板：" + path);
                UiPanelLayoutAuthoring.RequireStretchRoot(asset);
                var panel = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene); panel.transform.SetParent(canvasObject.transform, false); panel.SetActive(true);
                var rect = (RectTransform)panel.transform;
                UiPanelLayoutAuthoring.RequireStretchRoot(panel);
                if (state == "GameStartPop") panel.GetComponent<UI_StartPanel>().NewDynasty.gameObject.SetActive(true);
                if (state != null && panel.TryGetComponent<UI_GamePanel>(out var game))
                {
                    if (state == "Technology") game.Technology.TechnologyTree.gameObject.SetActive(true);
                    if (state == "BuildingDetails") game.Buildings.BuildingCard.gameObject.SetActive(true);
                    if (state == "Talent") foreach (var view in panel.GetComponentsInChildren<UI_GamePanel_Talent>(true)) view.gameObject.SetActive(true);
                    if (state == "RoyalDetails")
                    {
                        game.Court.gameObject.SetActive(true); game.Court.CourtGraph.gameObject.SetActive(true);
                        game.Court.RoyalDetails.gameObject.SetActive(true);
                        game.Court.RoyalDetails.PersonContent.SetActive(true); game.Court.RoyalDetails.OverviewHost.gameObject.SetActive(false);
                    }
                    if (state == "Marriage")
                    {
                        var marriage = game.GetComponentInChildren<UI_GamePanel_Marriage>(true);
                        marriage.gameObject.SetActive(true);
                        marriage.MarriageWindow.SetActive(true);
                        marriage.MarriagePanel.CandidateList.SetActive(false);
                    }
                    if (state == "Portrait")
                    {
                        var portrait = game.GetComponentInChildren<UI_GamePanel_Portrait>(true);
                        portrait.gameObject.SetActive(true);
                        portrait.PortraitWindow.SetActive(true);
                    }
                    if (state == "SoldierDetails")
                    {
                        var soldier = game.GetComponentInChildren<UI_GamePanel_Soldier>(true);
                        soldier.gameObject.SetActive(true);
                        soldier.SoldierDetailsWindow.SetActive(true);
                    }
                }
                Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                foreach (var text in panel.GetComponentsInChildren<TMP_Text>(true)) if (text.gameObject.activeInHierarchy) text.ForceMeshUpdate(true, true);
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = render;
                if (state == "Marriage" || state == "Portrait")
                {
                    foreach (var button in panel.GetComponentsInChildren<Button>())
                    {
                        var buttonRect = (RectTransform)button.transform;
                        if (buttonRect.rect.width <= 0 || buttonRect.rect.height <= 0)
                            throw new InvalidOperationException("可见交互控件的实际布局尺寸无效：" + button.name);
                    }
                }
                pixels = new Texture2D(width, height, TextureFormat.RGBA32, false); pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                var colors = pixels.GetPixels32(); bool hasContent = false;
                for (int i = 1; i < colors.Length; i += 17)
                    if (!colors[i].Equals(colors[0])) { hasContent = true; break; }
                if (!hasContent) throw new InvalidOperationException("预览未渲染出界面内容：" + path);
                var output = Path.GetFullPath(Output + "/" + Path.GetFileNameWithoutExtension(path) + (state == null ? "" : "_" + state) + "_" + width + "x" + height + (uiScale == 1 ? "" : "_scale150") + ".png");
                File.WriteAllBytes(output, pixels.EncodeToPNG()); return output;
            }
            finally
            {
                RenderTexture.active = previous;
                if (pixels != null) Object.DestroyImmediate(pixels);
                if (render != null) { render.Release(); Object.DestroyImmediate(render); }
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
#endif
