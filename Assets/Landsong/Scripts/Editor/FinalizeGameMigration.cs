#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Landsong.ECS.Presentation;
using Landsong.Editor.UI;
using Moyo.Unity;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Landsong.ECS.Editor
{
    /// <summary>Idempotent authoring repair for the already migrated Game prefab. Does not read the legacy baseline.</summary>
    public static class FinalizeGameMigration
    {
        const string GamePath = "Assets/Landsong/Objects/Prefabs/UI/GamePanel/UI_GamePanel.prefab";
        const string ViewsPath = "Assets/Landsong/Objects/Prefabs/UI/GamePanel/Views/";
        const string ProfilesPath = "Assets/Landsong/Editor/UI/Profiles/";
        static readonly string[] ViewFields =
        {
            "buildingController", "technologyController", "questController", "courtController", "talentController", "policyController",
            "marriageController", "portraitController", "requestsController", "soldierController", "hudController", "historyController", "expeditionController", "phaseController"
        };

        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出运行模式。");
            Folder(ViewsPath); Folder(ProfilesPath);
            var game = PrefabUtility.LoadPrefabContents(GamePath);
            int extracted = 0;
            try
            {
                var root = game.GetComponent<UI_GamePanel>();
                if (root == null) throw new InvalidOperationException("未找到游戏组合根。");
                RemoveLegacyPages(game, root);
                ApplicationUiMigration.StripCanvas(game);
                RepairTypedRows(game, root);
                EnsureSpecializedEntries(root);
                ConfigurePresenters(root);
                // Each authored view becomes its own asset. Scene owner references remain explicit overrides in Game.
                extracted += Extract(game, root.Buildings.BuildingCard.gameObject, "BuildingDetails");
                extracted += Extract(game, root.Buildings.BuildingBar.gameObject, "BuildingCatalog");
                extracted += Extract(game, root.Technology.TechnologyTree.gameObject, "Technology");
                extracted += Extract(game, root.Quests.QuestPanel.gameObject, "Quest");
                extracted += Extract(game, root.Court.CourtGraph.gameObject, "Royal");
                extracted += Extract(game, root.Talents.CourtGraph.gameObject, "Talent");
                extracted += Extract(game, root.Policies.CourtGraph.gameObject, "Policy");
                extracted += Extract(game, View<UI_GamePanel_Marriage>(root, "marriageController").MarriagePanel.gameObject, "Marriage");
                extracted += Extract(game, View<UI_GamePanel_Portrait>(root, "portraitController").PortraitPanel.gameObject, "Portrait");
                extracted += Extract(game, View<UI_GamePanel_PersonRequests>(root, "requestsController").PersonRequestsPanel.gameObject, "PersonRequests");
                extracted += Extract(game, View<UI_GamePanel_Soldier>(root, "soldierController").SoldierDetailsPanel.gameObject, "SoldierDetails");
                extracted += Extract(game, root.Buildings.BuildingConfirmPanel, "BuildingConfirm");
                foreach (var id in root.FeaturePanels.Select(x => x.PanelId).Where(id => id != GamePanelId.Technology && id != GamePanelId.Quest).ToArray())
                    extracted += Extract(game, root.GetListPanel(id).gameObject, FeatureAssetName(id));
                extracted += Extract(game, root.PauseMenu.gameObject, "PauseMenu");
                extracted += Extract(game, root.HudRoot.gameObject, "HUD");
                ConfigurePresenters(root);
                var children = ViewFields.Select(field => View<UIViewBase>(root, field)).ToArray();
                if (children.Distinct().Count() != children.Length) throw new InvalidOperationException("游戏子视图重复登记。");
                foreach (var child in children)
                {
                    child.ConfigureChildren(Array.Empty<UIViewBase>(), child is UI_GamePanel_Hud || child is UI_GamePanel_History);
                    child.ConfigurePreview(child.GetComponents<UIPreviewOnly>());
                    Record(child);
                }
                var group = root.GetComponent<CanvasGroup>();
                if (group == null) throw new InvalidOperationException("游戏根缺少交互组。");
                root.ConfigurePanel(group, children); // ConfigurePanel itself configures children; never clear it afterwards.
                root.PauseMenu.ModalGroup.ignoreParentGroups = true;
                root.Buildings.BuildingConfirmGroup.ignoreParentGroups = true;
                Record(root.PauseMenu.ModalGroup); Record(root.Buildings.BuildingConfirmGroup);
                Validate(game, root);
                PrefabUtility.SaveAsPrefabAsset(game, GamePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(game); }
            RebindPreviewRecipes();
            AssetDatabase.SaveAssets();
            string report = $"Game 补校完成：14 个明确子视图，HUD/导航随根开启，普通面板配置展示器，0 个子画布；本次抽取 {extracted} 个功能预制体。";
            File.WriteAllText("Library/LandsongEcs/game-ui-finalized.txt", DateTimeOffset.Now.ToString("O") + "\n" + report);
            return report;
        }

        static void RepairTypedRows(GameObject game, UI_GamePanel root)
        {
            var host = game.transform.Find("强类型条目模板");
            if (host == null) { var go = new GameObject("强类型条目模板", typeof(RectTransform)); host = go.transform; host.SetParent(game.transform, false); }
            host.gameObject.SetActive(false);
            var cache = game.GetComponent<PortraitCache>();
            if (cache == null) throw new InvalidOperationException("游戏根缺少明确的肖像缓存组件。");
            var sanitized = new HashSet<string>();
            void Configure(Component owner, string field)
            {
                var ownerData = new SerializedObject(owner);
                var property = ownerData.FindProperty(field);
                var current = property?.objectReferenceValue as UI_GamePanel_Row;
                if (current == null) throw new InvalidOperationException(owner.name + "." + field + " 未配置强类型模板。");
                var path = AssetDatabase.GetAssetPath(current);
                if (string.IsNullOrEmpty(path)) path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(current.gameObject);
                if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/Landsong/Objects/Prefabs/UI/GamePanel/Items/", StringComparison.Ordinal))
                    throw new InvalidOperationException("强类型模板没有独立的 Items 预制体资产：" + field);
                if (sanitized.Add(path))
                {
                    var contents = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        var item = contents.GetComponent<UI_GamePanel_Row>();
                        if (item == null) throw new InvalidOperationException(path + " 缺少条目组件。");
                        if (!(item is UI_GamePanel_WorkerInfoRow))
                            foreach (var hover in contents.GetComponents<UI_GamePanel_BuildingWorkerHover>()) Object.DestroyImmediate(hover);
                        if (item is UI_GamePanel_InventoryGridRow grid && grid.GridLayout != null) grid.Grid = (RectTransform)grid.GridLayout.transform;
                        contents.SetActive(false);
                        PrefabUtility.SaveAsPrefabAsset(contents, path);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(contents); }
                }
                UI_GamePanel_Row configured;
                if (!EditorUtility.IsPersistent(current) && Belongs(current.transform, game.transform)) configured = current;
                else
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, host);
                    configured = instance.GetComponent<UI_GamePanel_Row>();
                    ownerData.Update(); ownerData.FindProperty(field).objectReferenceValue = configured;
                    ownerData.ApplyModifiedPropertiesWithoutUndo(); Record(owner);
                }
                configured.gameObject.SetActive(false);
                if (configured is UI_GamePanel_WorkerInfoRow worker)
                { worker.WorkerHover.View = root.Buildings.BuildingCard; Record(worker.WorkerHover); }
                foreach (var portrait in configured.GetComponentsInChildren<UI_Common_PortraitImageBinding>(true))
                { portrait.Cache = cache; Record(portrait); }
                configured.ValidateConfiguration();
            }
            Configure(View<UI_GamePanel_RowRenderer>(root, "rowsController"), "RowTemplate");
            Configure(root.Buildings, "WorkerInfoTemplate"); Configure(root.Buildings, "WorkforceTemplate");
            Configure(root.InventoryWindow, "GridTemplate"); Configure(root.InventoryWindow, "QuantityTemplate");
            Configure(root.GarrisonWindow, "GroupTemplate"); Configure(root.GarrisonWindow, "SoldierTemplate");
            Configure(root.Quests, "QuantityTemplate"); Configure(View<UI_GamePanel_Portrait>(root, "portraitController"), "PortraitTemplate");
        }

        static void EnsureSpecializedEntries(UI_GamePanel root)
        {
            var panels = root.FeaturePanels.ToList();
            if (!panels.Any(p => p.PanelId == GamePanelId.Technology))
            {
                var tree = root.Technology.TechnologyTree;
                var panel = tree.gameObject.AddComponent<UI_GamePanel_List>();
                panel.PanelId = GamePanelId.Technology; panel.ContentRoot = tree.DetailRows.gameObject; panel.PrimaryRows = tree.DetailRows;
                panel.PrimaryScroll = tree.GraphScroll; panel.Title = tree.Header; panel.CloseButton = tree.CloseButton;
                panel.Presenter = root.Technology; panel.ManageContentVisibility = false; panels.Add(panel);
            }
            if (!panels.Any(p => p.PanelId == GamePanelId.Quest))
            {
                var view = root.Quests.QuestPanel;
                var panel = view.gameObject.AddComponent<UI_GamePanel_List>();
                panel.PanelId = GamePanelId.Quest; panel.ContentRoot = view.AcceptedRows.gameObject; panel.PrimaryRows = view.AcceptedRows;
                panel.PrimaryScroll = view.AcceptedRows.GetComponentInParent<ScrollRect>(true);
                if (panel.PrimaryScroll == null) throw new InvalidOperationException("任务列表没有可配置的滚动容器。");
                panel.Title = view.Capacity; panel.CloseButton = view.Close;
                panel.Presenter = root.Quests; panel.ManageContentVisibility = false; panels.Add(panel);
            }
            root.FeaturePanels = panels.ToArray();
        }

        static void ConfigurePresenters(UI_GamePanel root)
        {
            var presenters = new Dictionary<GamePanelId, MonoBehaviour>
            {
                [GamePanelId.History] = View<UI_GamePanel_History>(root, "historyController"), [GamePanelId.Building] = root.Buildings,
                [GamePanelId.Technology] = root.Technology, [GamePanelId.Quest] = root.Quests, [GamePanelId.Expedition] = View<UI_GamePanel_Expedition>(root, "expeditionController"),
                [GamePanelId.Talent] = root.Talents, [GamePanelId.Royal] = root.Court, [GamePanelId.Policy] = root.Policies,
                [GamePanelId.DynastyEnd] = View<UI_GamePanel_Phase>(root, "phaseController"), [GamePanelId.NightConfirmation] = View<UI_GamePanel_Phase>(root, "phaseController")
            };
            foreach (var panel in root.FeaturePanels)
            {
                if (presenters.TryGetValue(panel.PanelId, out var presenter)) panel.Presenter = presenter;
                panel.CloseAllowed = panel.PanelId != GamePanelId.DynastyEnd;
                panel.CloseButton.interactable = panel.CloseAllowed;
                panel.ValidateConfiguration(); Record(panel); Record(panel.CloseButton);
            }
        }

        static T View<T>(UI_GamePanel root, string field) where T : Component
        {
            var value = new SerializedObject(root).FindProperty(field)?.objectReferenceValue;
            return value as T ?? throw new InvalidOperationException("游戏组合根未配置：" + field);
        }

        static int Extract(GameObject game, GameObject target, string name)
        {
            string path = ViewsPath + "UI_GamePanel_" + name + ".prefab";
            if (PrefabUtility.GetNearestPrefabInstanceRoot(target) == target
                && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(target) == path) return 0;
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing == null && AssetDatabase.LoadMainAssetAtPath(path) != null) throw new InvalidOperationException(path + " 被非预制体资源占用。");
            if (target == game || !target.transform.IsChildOf(game.transform)) throw new InvalidOperationException("只能抽取当前游戏的子树。");
            var parent = target.transform.parent;
            int sibling = target.transform.GetSiblingIndex(); bool active = target.activeSelf;
            var clone = Object.Instantiate(target, parent, false);
            clone.name = "UI_GamePanel_" + name;
            try
            {
                var external = ClearExternalReferences(clone);
                clone.SetActive(true);
                var prefab = existing;
                if (prefab == null) prefab = PrefabUtility.SaveAsPrefabAsset(clone, path);
                var nested = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                nested.name = target.name; nested.SetActive(active); nested.transform.SetSiblingIndex(sibling);
                var cloneToNested = PairObjects(clone, nested);
                foreach (var reference in external)
                {
                    var component = cloneToNested[reference.component];
                    var serialized = new SerializedObject(component);
                    serialized.FindProperty(reference.path).objectReferenceValue = reference.value;
                    serialized.ApplyModifiedPropertiesWithoutUndo(); Record(component);
                }
                var oldToNew = PairObjects(target, nested);
                foreach (var component in game.GetComponentsInChildren<Component>(true))
                {
                    if (component == null || Belongs(component.transform, target.transform) || Belongs(component.transform, clone.transform)) continue;
                    var serialized = new SerializedObject(component); var property = serialized.GetIterator(); bool changed = false;
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference || Structural(property.propertyPath)) continue;
                        var value = property.objectReferenceValue;
                        if (value != null && oldToNew.TryGetValue(value, out var replacement)) { property.objectReferenceValue = replacement; changed = true; }
                    }
                    if (changed) { serialized.ApplyModifiedPropertiesWithoutUndo(); Record(component); }
                }
                Object.DestroyImmediate(target);
                return 1;
            }
            finally { Object.DestroyImmediate(clone); }
        }

        static List<(Component component, string path, Object value)> ClearExternalReferences(GameObject root)
        {
            var references = new List<(Component, string, Object)>();
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null) throw new InvalidOperationException(root.name + " 包含丢失脚本。");
                var serialized = new SerializedObject(component); var property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference || Structural(property.propertyPath)) continue;
                    var value = property.objectReferenceValue;
                    if (value == null || EditorUtility.IsPersistent(value) || !TryTransform(value, out var transform) || Belongs(transform, root.transform)) continue;
                    references.Add((component, property.propertyPath, value)); property.objectReferenceValue = null;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            return references;
        }

        static Dictionary<Object, Object> PairObjects(GameObject from, GameObject to)
        {
            var map = new Dictionary<Object, Object>();
            void Visit(Transform a, Transform b)
            {
                map[a.gameObject] = b.gameObject;
                var first = a.GetComponents<Component>(); var second = b.GetComponents<Component>();
                if (first.Length != second.Length || a.childCount != b.childCount) throw new InvalidOperationException("抽取的预制体层级或组件数发生变化。");
                for (int i = 0; i < first.Length; i++)
                {
                    if (first[i] == null || second[i] == null || first[i].GetType() != second[i].GetType()) throw new InvalidOperationException("抽取的组件类型不一致。");
                    map[first[i]] = second[i];
                }
                for (int i = 0; i < a.childCount; i++) Visit(a.GetChild(i), b.GetChild(i));
            }
            Visit(from.transform, to.transform); return map;
        }

        static void RemoveLegacyPages(GameObject game, UI_GamePanel root)
        {
            foreach (var settings in game.GetComponentsInChildren<UI_SettingPanel>(true)) Object.DestroyImmediate(settings.gameObject);
            foreach (var save in game.GetComponentsInChildren<UI_SavePanel>(true)) Object.DestroyImmediate(save.gameObject);
            foreach (var slot in game.GetComponentsInChildren<MonoBehaviour>(true).Where(component => component != null && component.GetType().Name == "SaveSlotView").ToArray()) Object.DestroyImmediate(slot.gameObject);
            root.PauseMenu.ValidateConfiguration();
        }

        static void Validate(GameObject game, UI_GamePanel root)
        {
            if (root.ChildViews.Count != ViewFields.Length) throw new InvalidOperationException("根子视图配置数量不正确。");
            if (game.GetComponentsInChildren<Canvas>(true).Length != 0 || game.GetComponentsInChildren<CanvasScaler>(true).Length != 0
                || game.GetComponentsInChildren<GraphicRaycaster>(true).Length != 0) throw new InvalidOperationException("Game 仍有独立画布组件。");
            if (game.GetComponentsInChildren<UIPanelBase>(true).Length != 1 || game.GetComponentsInChildren<UI_SettingPanel>(true).Length != 0
                || game.GetComponentsInChildren<UI_SavePanel>(true).Length != 0 || game.GetComponentsInChildren<MonoBehaviour>(true).Where(component => component != null && component.GetType().Name == "SaveSlotView").ToArray().Length != 0)
                throw new InvalidOperationException("Game 仍有重复的全局面板。");
            root.ValidateConfiguration();
            foreach (var panel in root.FeaturePanels) panel.ValidateConfiguration();
            foreach (var row in game.GetComponentsInChildren<UI_GamePanel_Row>(true)) row.ValidateConfiguration();
            foreach (var binding in game.GetComponentsInChildren<UI_Common_PortraitImageBinding>(true)) binding.ValidateConfiguration();
            foreach (var preview in game.GetComponentsInChildren<UIPreviewOnly>(true)) preview.ValidateConfiguration();
            if (root.Buildings.WorkerInfoTemplate.WorkerHover.View != root.Buildings.BuildingCard)
                throw new InvalidOperationException("工人行模板没有绑定当前建筑详情。");
            if (root.InventoryWindow.DragSpace != root.InventoryWindow.DragRoot.parent || root.Hud.NightHud.RewardSpace != root.Hud.NightHud.transform)
                throw new InvalidOperationException("库存拖拽或夜间奖励坐标空间不正确。");
            foreach (var component in game.GetComponentsInChildren<Component>(true))
            {
                if (component == null) throw new InvalidOperationException("Game 含丢失脚本。");
                var serialized = new SerializedObject(component); var property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference || Structural(property.propertyPath)) continue;
                    var value = property.objectReferenceValue;
                    if (value == null && property.objectReferenceInstanceIDValue != 0) throw new InvalidOperationException(component.name + "." + property.propertyPath + " 有失效引用。");
                    if (value != null && !EditorUtility.IsPersistent(value) && TryTransform(value, out var transform) && !Belongs(transform, game.transform))
                        throw new InvalidOperationException(component.name + "." + property.propertyPath + " 指向游戏之外的临时对象。");
                }
            }
        }

        [MenuItem("Landsong/UI/刷新游戏预览配方与示例")]
        public static void RebindPreviewRecipes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请退出运行模式后刷新预览。");
            T PreviewOwner<T>() where T : Component
            {
                var matches = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Landsong/Objects/Prefabs/UI/GamePanel" })
                    .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                    .Select(prefab => prefab.GetComponent<T>()).Where(component => component != null).ToArray();
                if (matches.Length != 1) throw new InvalidOperationException(typeof(T).Name + " 的独立预览预制体必须且只能有一个。");
                return matches[0];
            }
            var hud = PreviewOwner<UI_GamePanel_Hud>();
            var building = PreviewOwner<UI_GamePanel_Building>();
            Recipe(hud, "GameHud", UIPreviewKind.GameHud, new[] { new UIPreviewTextBinding("turn", hud.Status), new UIPreviewTextBinding("message", hud.Message), new UIPreviewTextBinding("population", hud.Selection) }, Array.Empty<UIPreviewListBinding>());
            GameFeaturePreviewRecipes.ConfigureRecipes();
            Recipe(building, "BuildingDetails", UIPreviewKind.BuildingDetails, new[] { new UIPreviewTextBinding("title", (TMP_Text)building.BuildingCard.Name.placeholder, "建筑名称"), new UIPreviewTextBinding("level", building.BuildingCard.Level), new UIPreviewTextBinding("production", building.BuildingCard.Block<UI_GamePanel_BuildingDetails_Block_基础产出>().Label), new UIPreviewTextBinding("workers", building.BuildingCard.Block<UI_GamePanel_BuildingDetails_Block_岗位>().Jobs), new UIPreviewTextBinding("experience", building.BuildingCard.Experience), new UIPreviewTextBinding("position", building.BuildingCard.Footer) }, Array.Empty<UIPreviewListBinding>());
            foreach (var name in new[] { "GameHud", "Technology", "Talent", "BuildingDetails" })
            {
                var recipe = AssetDatabase.LoadAssetAtPath<UIPreviewRecipe>(ProfilesPath + name + "Recipe.asset");
                recipe.ApplyToPrefab();
                AssetDatabase.SaveAssetIfDirty(recipe);
            }
        }

        static void Recipe(UIViewBase view, string name, UIPreviewKind kind, UIPreviewTextBinding[] texts, UIPreviewListBinding[] lists)
        {
            string path = ProfilesPath + name + "Recipe.asset";
            var recipe = AssetDatabase.LoadAssetAtPath<UIPreviewRecipe>(path);
            if (recipe == null) { recipe = ScriptableObject.CreateInstance<UIPreviewRecipe>(); AssetDatabase.CreateAsset(recipe, path); }
            recipe.Configure(view, UIPreviewBuilder.EnsureDefaultProfile(ProfilesPath + name + ".asset", kind), texts, lists); EditorUtility.SetDirty(recipe);
        }

        static string FeatureAssetName(GamePanelId id) => id switch
        {
            GamePanelId.Economy => "Economy", GamePanelId.History => "HistoryList", GamePanelId.Building => "BuildingList", GamePanelId.Inventory => "Inventory", GamePanelId.Garrison => "Garrison",
            GamePanelId.Technology => "TechnologyList", GamePanelId.Quest => "QuestList", GamePanelId.Expedition => "Expedition", GamePanelId.Talent => "TalentList", GamePanelId.Royal => "RoyalList", GamePanelId.Policy => "PolicyList",
            GamePanelId.Intelligence => "Intelligence", GamePanelId.BattleReport => "BattleReport", GamePanelId.DynastyEnd => "DynastyEnd", GamePanelId.NightConfirmation => "NightConfirmation",
            _ => throw new InvalidOperationException("尚未命名的功能资产：" + id)
        };
        static bool Structural(string path) => path == "m_GameObject" || path == "m_Father" || path.StartsWith("m_Children", StringComparison.Ordinal)
            || path == "m_CorrespondingSourceObject" || path == "m_PrefabInstance" || path == "m_PrefabAsset";
        static bool TryTransform(Object value, out Transform transform) { transform = value is GameObject go ? go.transform : value is Component component ? component.transform : null; return transform != null; }
        static bool Belongs(Transform value, Transform root) => value == root || value.IsChildOf(root);
        static void Record(Object value) { EditorUtility.SetDirty(value); if (PrefabUtility.IsPartOfPrefabInstance(value)) PrefabUtility.RecordPrefabInstancePropertyModifications(value); }
        static void Folder(string path) { path = path.Replace('\\', '/').TrimEnd('/'); if (AssetDatabase.IsValidFolder(path)) return; Folder(Path.GetDirectoryName(path)); AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path)); }
    }
}
#endif
