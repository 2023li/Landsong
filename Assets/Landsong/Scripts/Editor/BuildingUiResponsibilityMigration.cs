#if UNITY_EDITOR
using System;
using System.Linq;
using Landsong.ECS.Authoring;
using Landsong.ECS.Presentation;
using Moyo.Unity;
using UnityEditor;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public static class BuildingUiResponsibilityMigration
    {
        const string GamePath = "Assets/Landsong/Objects/Prefabs/UI/GamePanel/UI_GamePanel.prefab";
        const string DetailsPath = "Assets/Landsong/Objects/Prefabs/UI/GamePanel/Views/UI_GamePanel_建筑详情.prefab";
        const string ActionBarPath = "Assets/Landsong/Objects/Prefabs/UI/GamePanel/Views/UI_GamePanel_建筑操作条.prefab";
        const string BuildingPrefabRoot = "Assets/Landsong/ECSContent/Prefabs";

        [MenuItem("Landsong/UI/拆分建筑操作条与详情")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请先退出运行模式。");

            var game = PrefabUtility.LoadPrefabContents(GamePath);
            try
            {
                var root = game.GetComponent<UI_GamePanel>();
                if (root == null) throw new InvalidOperationException("Game Prefab 缺少 UI_GamePanel。");
                var oldActionBar = game.GetComponentInChildren<UI_GamePanel_BuildingActionBar>(true);
                var oldDetails = game.GetComponentInChildren<UI_GamePanel_BuildingDetails>(true);
                if (oldActionBar == null || oldDetails == null)
                    throw new InvalidOperationException("Game Prefab 缺少建筑操作条或建筑详情组件。");

                if (oldDetails.transform.parent == root.FeatureRoot && oldActionBar.transform.parent == root.BuildingRoot)
                {
                    BindRoot(root, oldActionBar, oldDetails);
                    PrefabUtility.SaveAsPrefabAsset(game, GamePath);
                    return;
                }

                var instance = PrefabUtility.GetOutermostPrefabInstanceRoot(oldActionBar.gameObject);
                if (instance != null)
                    PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

                var detailsLayout = (RectTransform)oldActionBar.transform;
                var anchorMin = detailsLayout.anchorMin;
                var anchorMax = detailsLayout.anchorMax;
                var anchoredPosition = detailsLayout.anchoredPosition;
                var sizeDelta = detailsLayout.sizeDelta;
                var pivot = detailsLayout.pivot;

                var buildingBar = oldActionBar.BuildingBar;
                var catalog = oldActionBar.BuildingCatalog;
                var confirmRows = oldActionBar.BuildingConfirmRows;
                var confirmPanel = oldActionBar.BuildingConfirmPanel;
                var hint = oldActionBar.BuildingHint;
                var confirmTitle = oldActionBar.BuildingConfirmTitle;
                var confirmGroup = oldActionBar.BuildingConfirmGroup;
                var placementPanel = oldActionBar.BuildingPlacementPanel;
                var childViews = root.ChildViews.ToArray();

                var oldPreview = oldActionBar.GetComponent<UIPreviewOnly>();
                if (oldPreview != null)
                {
                    var objects = oldPreview.SampleObjects.Where(value => value != null && value.transform.IsChildOf(oldDetails.transform)).ToArray();
                    var pairs = oldPreview.SampleTextTargets.Select((text, index) => new { text, runtime = oldPreview.RuntimeTexts[index] })
                        .Where(pair => pair.text != null && (pair.text.transform == oldDetails.transform || pair.text.transform.IsChildOf(oldDetails.transform))).ToArray();
                    var detailsPreview = oldDetails.gameObject.GetComponent<UIPreviewOnly>() ?? oldDetails.gameObject.AddComponent<UIPreviewOnly>();
                    detailsPreview.Configure(objects, pairs.Select(pair => pair.text).ToArray(), pairs.Select(pair => pair.runtime).ToArray());
                    UnityEngine.Object.DestroyImmediate(oldPreview);
                }

                oldDetails.transform.SetParent(root.FeatureRoot, false);
                var detailRect = (RectTransform)oldDetails.transform;
                detailRect.anchorMin = anchorMin;
                detailRect.anchorMax = anchorMax;
                detailRect.anchoredPosition = anchoredPosition;
                detailRect.sizeDelta = sizeDelta;
                detailRect.pivot = pivot;
                oldDetails.name = "Building Details Panel";
                oldActionBar.transform.SetParent(root.BuildingRoot, false);
                var actionRect = (RectTransform)oldActionBar.transform;
                actionRect.anchorMin = Vector2.zero;
                actionRect.anchorMax = Vector2.one;
                actionRect.offsetMin = actionRect.offsetMax = Vector2.zero;
                actionRect.pivot = new Vector2(.5f, .5f);
                oldActionBar.name = "Building Action Bar";
                oldActionBar.DetailsPanel = oldDetails;
                oldActionBar.ConfigurePreview(Array.Empty<UIPreviewOnly>());

                PrefabUtility.SaveAsPrefabAsset(oldDetails.gameObject, DetailsPath);
                PrefabUtility.SaveAsPrefabAsset(oldActionBar.gameObject, ActionBarPath);

                UnityEngine.Object.DestroyImmediate(oldDetails.gameObject);
                UnityEngine.Object.DestroyImmediate(oldActionBar.gameObject);
                var detailsObject = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(DetailsPath), root.FeatureRoot);
                var actionObject = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ActionBarPath), root.BuildingRoot);
                var details = detailsObject.GetComponent<UI_GamePanel_BuildingDetails>();
                var actionBar = actionObject.GetComponent<UI_GamePanel_BuildingActionBar>();

                actionBar.BuildingBar = buildingBar;
                actionBar.BuildingCatalog = catalog;
                actionBar.BuildingConfirmRows = confirmRows;
                actionBar.BuildingConfirmPanel = confirmPanel;
                actionBar.BuildingHint = hint;
                actionBar.BuildingConfirmTitle = confirmTitle;
                actionBar.BuildingConfirmGroup = confirmGroup;
                actionBar.BuildingPlacementPanel = placementPanel;
                actionBar.DetailsPanel = details;
                actionBar.gameObject.SetActive(true);
                actionBar.BuildingActionBar.gameObject.SetActive(false);
                details.gameObject.SetActive(false);

                BindRoot(root, actionBar, details);
                root.ConfigureChildren(childViews.Select(view => view == oldActionBar ? actionBar : view).Where(view => view != null).Distinct().ToArray());
                PrefabUtility.SaveAsPrefabAsset(game, GamePath);
                AssetDatabase.SaveAssets();
                Debug.Log("建筑 UI 已拆分：详情直属 FeatureRoot，操作条直属 BuildingRoot。");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(game);
            }
        }

        [MenuItem("Landsong/UI/补齐建筑 SelectionAnchor")]
        public static void AddMissingSelectionAnchors()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += AddMissingSelectionAnchors;
                return;
            }

            var changed = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { BuildingPrefabRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var authoring = prefab.GetComponentInChildren<BuildingVisualAuthoring>(true);
                    if (authoring == null)
                        continue;
                    var existing = authoring.GetComponentsInChildren<Transform>(true).Where(value => value.name == "SelectionAnchor").ToArray();
                    if (existing.Length > 1)
                        throw new InvalidOperationException(path + " 配置了多个 SelectionAnchor。");
                    if (existing.Length == 1)
                        continue;

                    var anchor = new GameObject("SelectionAnchor").transform;
                    anchor.SetParent(authoring.transform, false);
                    var renderers = authoring.GetComponentsInChildren<Renderer>(true)
                        .Where(value => !(value is ParticleSystemRenderer)).ToArray();
                    if (renderers.Length > 0)
                    {
                        var bounds = renderers[0].bounds;
                        foreach (var renderer in renderers.Skip(1))
                            bounds.Encapsulate(renderer.bounds);
                        anchor.localPosition = authoring.transform.InverseTransformPoint(
                            new Vector3(bounds.center.x, bounds.max.y, bounds.center.z));
                    }

                    PrefabUtility.SaveAsPrefabAsset(prefab, path);
                    changed++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefab);
                }
            }

            if (changed > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"已为 {changed} 个建筑表现 Prefab 创建 SelectionAnchor。");
            }
        }

        static void BindRoot(UI_GamePanel root, UI_GamePanel_BuildingActionBar actionBar, UI_GamePanel_BuildingDetails details)
        {
            // 操作条控制器必须随 GamePanel 打开；其内部 BuildingActionBar 节点仍由选中状态单独显隐。
            // 如果这里保持默认 false，UIViewBase.CreateTreeAsync 会禁用整个操作条 Prefab 根对象，
            // 后续即使把内部操作条设为 active，也不会在层级中实际显示。
            actionBar.ConfigureChildren(Array.Empty<UIViewBase>(), true);
            var data = new SerializedObject(root);
            data.FindProperty("buildingController").objectReferenceValue = actionBar;
            data.FindProperty("buildingDetailsController").objectReferenceValue = details;
            data.ApplyModifiedPropertiesWithoutUndo();
            var buildingButton = root.NavigationButtons?.SingleOrDefault(binding => binding.Target == GamePanelId.Building)?.Button;
            if (buildingButton == null)
                throw new InvalidOperationException("建筑导航按钮未配置。");
            var buttonData = new SerializedObject(buildingButton);
            var calls = buttonData.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
            var rebound = false;
            for (var index = 0; index < calls.arraySize; index++)
            {
                var call = calls.GetArrayElementAtIndex(index);
                if (call.FindPropertyRelative("m_MethodName").stringValue != nameof(UI_GamePanel_BuildingActionBar.ToggleBuildingCatalog))
                    continue;
                call.FindPropertyRelative("m_Target").objectReferenceValue = actionBar;
                call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = typeof(UI_GamePanel_BuildingActionBar).FullName + ", " + typeof(UI_GamePanel_BuildingActionBar).Assembly.GetName().Name;
                rebound = true;
            }
            if (!rebound)
                throw new InvalidOperationException("建筑导航按钮缺少 ToggleBuildingCatalog 持久事件。");
            buttonData.ApplyModifiedPropertiesWithoutUndo();
            actionBar.DetailsPanel = details;
            EditorUtility.SetDirty(root);
            EditorUtility.SetDirty(buildingButton);
            EditorUtility.SetDirty(actionBar);
            EditorUtility.SetDirty(details);
        }
    }
}
#endif
