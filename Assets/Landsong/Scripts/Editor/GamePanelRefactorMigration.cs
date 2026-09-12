#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Landsong.ECS.Authoring;
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
    /// <summary>One-time authoring migration. Legacy file IDs are resolved only here, never by the player.</summary>
    public static class GamePanelRefactorMigration
    {
        const string OldPath = "Assets/Landsong/Objects/Prefabs/UI/UIGamePanel/ECS_UI_Game.prefab";
        const string NewPath = "Assets/Landsong/Objects/Prefabs/UI/GamePanel/UI_GamePanel.prefab";
        const string ItemPath = "Assets/Landsong/Objects/Prefabs/UI/GamePanel/Items/";
        const string ProfilePath = "Assets/Landsong/Editor/UI/Profiles/";
        const string Marker = "Library/LandsongEcs/game-ui-migrated.txt";
        const string LegacyRoot = @"BuildingBar: {fileID: 3368182523081684481}
BuildingCard: {fileID: 8180394270465322802}
BuildingCatalog: {fileID: 11400000, guid: 55548412761483842b03be3e06e32ae4, type: 2}
BuildingToolbar: {fileID: 8679991759046308993}
BuildingConfirmRows: {fileID: 2687226635595518550}
BuildingDetailsPanel: {fileID: 8890815687451193559}
BuildingConfirmPanel: {fileID: 9123325910098873026}
BuildingMoveButton: {fileID: 3415913611154074929}
BuildingRangeButton: {fileID: 1404360229307909245}
BuildingUpgradeButton: {fileID: 8783452821490875813}
BuildingRepairButton: {fileID: 8396223760776745159}
BuildingDemolishButton: {fileID: 9078075895065449776}
BuildingDetailsClose: {fileID: 5825668869682373234}
BuildingHint: {fileID: 200698922920499400}
BuildingConfirmTitle: {fileID: 2897088108498509953}
BuildingConfirmCanvas: {fileID: 6296232149680502337}
BuildingConfirmRaycaster: {fileID: 9173486447255643578}
BuildingConfirmGroup: {fileID: 5451101576117161816}
BuildingPlacementPanel: {fileID: 5791640881599789173}
CourtGraph: {fileID: 3705220726726302387}
RoyalCrownIcon: {fileID: 0}
Camera: {fileID: 0}
Status: {fileID: 6029306323018256453}
Selection: {fileID: 6736521971206170590}
Message: {fileID: 2567574524041021093}
Moon: {fileID: 1278522851780958899}
AdvanceLabel: {fileID: 8090297870961121104}
MessageButton: {fileID: 8217986276187907580}
MoonProgress: {fileID: 7401233445611587956}
Advance: {fileID: 418787086968069674}
RowTemplate: {fileID: 7815280718184649960}
NameInput: {fileID: 3556250967836093174}
OverlayMesh: {fileID: 10202, guid: 0000000000000000e000000000000000, type: 0}
OverlayMaterial: {fileID: 2100000, guid: 7d5eef082278b23499439adace9c7286, type: 2}
PauseMenu: {fileID: 1179340291806946308}
InterfaceGroup: {fileID: 2008140643595598257}
InterfaceScaler: {fileID: 1418490451676677608}
BuildingFeatureButton: {fileID: 1846961167645948932}
InventoryFeatureButton: {fileID: 6649944556009796343}
ExpeditionFeatureButton: {fileID: 2603159762467549631}
BattleHud: {fileID: 1017045685320164456}
NavigationPanel: {fileID: 1675607478774565246}
HistoryTools: {fileID: 5272847539969184066}
HistoryFilter: {fileID: 8419825957215339889}
WorldPresentation: {fileID: 3850785804220592472}
IntelligenceButtonLabel: {fileID: 3088503212340264338}
HudRoot: {fileID: 2472392534592523551}
BuildingRoot: {fileID: 6738390089064562964}
FeatureRoot: {fileID: 361473729928692353}
ModalRoot: {fileID: 7390864769500847270}
MarriagePanel: {fileID: 9103352785200402807}
MarriageEventButton: {fileID: 7636777098547334714}
MarriageEventLabel: {fileID: 4422716148536776431}
NightHud: {fileID: 6042121000687668643}
PersonRequestsPanel: {fileID: 2730287796776184402}
PortraitPanel: {fileID: 7297868468265775381}
BeautyEventButton: {fileID: 1906377035831448809}
BeautyEventLabel: {fileID: 7744521195154336291}
QuestPanel: {fileID: 7567169049054468766}
QuestTracking: {fileID: 1096202484840312181}
ResearchHud: {fileID: 7081837540202655172}
RoyalDetails: {fileID: 5096057732768234242}
SoldierDetailsPanel: {fileID: 5355856840064754900}
TechnologyButton: {fileID: 3777332963140372884}
TechnologyTree: {fileID: 999833085491177205}";
        const string LegacyRow = @"Select: {fileID: 3400554105072621481}
Label: {fileID: 4758791463895930967}
Layout: {fileID: 3966500407983250628}
Icon: {fileID: 556530786438550450}
PersonPortrait: {fileID: 3018983990158504671}
PersonPortraitBinding: {fileID: 7365608973842663496}
WorkerHover: {fileID: 7087434080353177030}
GarrisonGroup: {fileID: 6759112185640721190}
SoldierItem: {fileID: 150255660531331230}
InventoryGrid: {fileID: 7906646955978255130}
InventoryGridLayout: {fileID: 8348893634030786268}
InventorySlotTemplate: {fileID: 7419331413923649016}
InventoryQuantity: {fileID: 897885888300956612}
QuestQuantity: {fileID: 5878974577358182893}
MilitaryName: {fileID: 0}
WorkforceScale: {fileID: 8398850338613958617}";

        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请退出运行模式后迁移游戏界面。");
            if (File.Exists(Marker)) throw new InvalidOperationException("游戏界面已迁移，不能再次用旧字段覆盖新配置。");
            Folder(Path.GetDirectoryName(NewPath)); Folder(ItemPath); Folder(ProfilePath);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(NewPath) == null)
            {
                string error = AssetDatabase.MoveAsset(OldPath, NewPath);
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            }
            pendingRecipes.Clear();
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(NewPath);
            var game = PrefabUtility.LoadPrefabContents(NewPath);
            try
            {
                var idMap = LocalIds(asset, game);
                var legacy = ReadReferences(LegacyRoot, idMap);
                var rowFields = ReadReferences(LegacyRow, idMap);
                foreach (var oldId in new long[] { 3319902776612355742, 2322720949406533739, 4015106073268150435, 3780886140288373332 })
                    if (idMap.TryGetValue(oldId, out var oldPage) && oldPage != null) Object.DestroyImmediate(GameObjectOf(oldPage));
                var root = game.GetComponent<UI_GamePanel>() ?? throw new InvalidOperationException("旧游戏面板缺少组合根。");
                var childViews = new List<UIViewBase>();
                T Add<T>(string field, GameObject target) where T : Component
                {
                    if (target.GetComponent<T>() != null) throw new InvalidOperationException(target.name + " 已有目标控制器，禁止重复迁移。");
                    var component = target.AddComponent<T>();
                    CopyReferences(component, legacy); SetReference(root, field, component);
                    if (component is UIViewBase view) { view.ConfigureChildren(Array.Empty<UIViewBase>(), component is UI_GamePanel_Hud || component is UI_GamePanel_History); childViews.Add(view); }
                    return component;
                }
                GameObject Host(string field) => GameObjectOf(legacy[field]);
                var building = Add<UI_GamePanel_Building>("buildingController", Host("BuildingCard"));
                var technology = Add<UI_GamePanel_Technology>("technologyController", Host("TechnologyTree"));
                var quest = Add<UI_GamePanel_Quest>("questController", Host("QuestPanel"));
                var court = Add<UI_GamePanel_Court>("courtController", Host("CourtGraph"));
                Add<UI_GamePanel_Marriage>("marriageController", Host("MarriagePanel"));
                var portrait = Add<UI_GamePanel_Portrait>("portraitController", Host("PortraitPanel"));
                Add<UI_GamePanel_PersonRequests>("requestsController", Host("PersonRequestsPanel"));
                Add<UI_GamePanel_Soldier>("soldierController", Host("SoldierDetailsPanel"));
                var hud = Add<UI_GamePanel_Hud>("hudController", Host("HudRoot"));
                Add<UI_GamePanel_History>("historyController", Host("NavigationPanel"));
                Add<UI_GamePanel_Expedition>("expeditionController", root.GetListPanel(GamePanelId.Expedition).gameObject);
                Add<UI_GamePanel_Phase>("phaseController", Child(game.transform, "阶段与结算").gameObject);
                var rows = Add<UI_GamePanel_RowRenderer>("rowsController", Child(game.transform, "公共条目渲染").gameObject);
                var world = Add<UI_GamePanel_WorldInteraction>("worldController", Child(game.transform, "GameWorldInteraction").gameObject);
                var talentGraph = Object.Instantiate(court.CourtGraph, root.FeatureRoot, false);
                talentGraph.name = "人才展示";
                var policyGraph = Object.Instantiate(court.CourtGraph, root.FeatureRoot, false);
                policyGraph.name = "政策展示";
                // The source was cloned after its controller was attached. Each branch owns exactly one presenter.
                Object.DestroyImmediate(talentGraph.GetComponent<UI_GamePanel_Court>());
                Object.DestroyImmediate(policyGraph.GetComponent<UI_GamePanel_Court>());
                foreach (var extra in talentGraph.GetComponentsInChildren<UI_GamePanel_RoyalPersonDetails>(true)) Object.DestroyImmediate(extra.gameObject);
                foreach (var extra in policyGraph.GetComponentsInChildren<UI_GamePanel_RoyalPersonDetails>(true)) Object.DestroyImmediate(extra.gameObject);
                var talent = Add<UI_GamePanel_Talent>("talentController", talentGraph.gameObject); talent.CourtGraph = talentGraph;
                var policy = Add<UI_GamePanel_Policy>("policyController", policyGraph.gameObject); policy.CourtGraph = policyGraph;
                ConfigureGraph(court.CourtGraph, true); ConfigureGraph(talentGraph, false); ConfigureGraph(policyGraph, false);
                foreach (var panel in root.FeaturePanels)
                {
                    panel.ContentRoot = panel.PrimaryScroll.gameObject;
                    if (panel.PanelId == GamePanelId.DynastyEnd) panel.CloseButton.interactable = false;
                }
                var royal = root.GetListPanel(GamePanelId.Royal);
                court.RoyalOverviewRoot = (RectTransform)royal.PrimaryScroll.transform;
                court.RoyalOverviewRoot.SetParent(court.RoyalDetails.OverviewHost, false);
                Stretch(court.RoyalOverviewRoot);
                var sourceRow = (UI_GamePanel_Row)legacy["RowTemplate"];
                var templateRoot = Child(game.transform, "强类型条目模板"); templateRoot.gameObject.SetActive(false);
                rows.RowTemplate = MakeRow<UI_GamePanel_Row>(sourceRow, rowFields, templateRoot, "通用条目", "建筑维护 · 本回合产出 +12 木材");
                root.InventoryWindow.GridTemplate = MakeRow<UI_GamePanel_InventoryGridRow>(sourceRow, rowFields, templateRoot, "库存网格", "库存", ("Grid", "InventoryGrid"), ("GridLayout", "InventoryGridLayout"), ("SlotTemplate", "InventorySlotTemplate"));
                root.InventoryWindow.QuantityTemplate = MakeRow<UI_GamePanel_QuantityRow>(sourceRow, rowFields, templateRoot, "库存数量", "转移数量", ("Quantity", "InventoryQuantity"));
                root.GarrisonWindow.GroupTemplate = MakeRow<UI_GamePanel_GarrisonRow>(sourceRow, rowFields, templateRoot, "驻军分组", "城堡守军", ("Group", "GarrisonGroup"));
                root.GarrisonWindow.SoldierTemplate = MakeRow<UI_GamePanel_SoldierRow>(sourceRow, rowFields, templateRoot, "士兵条目", "守卫 · 等级 3", ("Soldier", "SoldierItem"));
                building.WorkerInfoTemplate = MakeRow<UI_GamePanel_WorkerInfoRow>(sourceRow, rowFields, templateRoot, "工人信息", "工人岗位 · 4 / 6", ("WorkerHover", "WorkerHover"));
                building.WorkforceTemplate = MakeRow<UI_GamePanel_WorkforceRow>(sourceRow, rowFields, templateRoot, "岗位预算", "招募预算 · 24 金币", ("Workforce", "WorkforceScale"));
                quest.QuantityTemplate = MakeRow<UI_GamePanel_QuantityRow>(sourceRow, rowFields, templateRoot, "任务数量", "提交数量", ("Quantity", "QuestQuantity"));
                portrait.PortraitTemplate = MakeRow<UI_GamePanel_PortraitRow>(sourceRow, rowFields, templateRoot, "人物肖像", "伊莲娜 · 王室成员", ("Portrait", "PersonPortrait"), ("PortraitBinding", "PersonPortraitBinding"));
                Object.DestroyImmediate(sourceRow.gameObject);
                world.PreviewTemplates = ConfigureBuildingPreviews(building.BuildingCatalog);
                if (childViews.Distinct().Count() != childViews.Count) throw new InvalidOperationException("子控制器引用重复。");
                ApplicationUiMigration.StripCanvas(game);
                var dragSpace = (RectTransform)Child(game.transform, "拖拽层"); Stretch(dragSpace);
                root.InventoryWindow.DragRoot.SetParent(dragSpace, false);
                root.InventoryWindow.DragRoot.anchorMin = root.InventoryWindow.DragRoot.anchorMax = root.InventoryWindow.DragRoot.pivot = new Vector2(.5f, .5f);
                root.InventoryWindow.DragRoot.gameObject.SetActive(false);
                root.InventoryWindow.DragSpace = dragSpace;
                hud.NightHud.RewardSpace = (RectTransform)hud.NightHud.transform;
                root.HudRoot.SetSiblingIndex(0); root.BuildingRoot.SetSiblingIndex(1); root.FeatureRoot.SetSiblingIndex(2); root.ModalRoot.SetSiblingIndex(3); dragSpace.SetAsLastSibling();
                ApplicationUiMigration.Configure(root);
                root.ConfigureChildren(childViews.ToArray());
                building.BuildingConfirmGroup.ignoreParentGroups = true;
                // Popup input must remain active when the game HUD blocks its own input.
                foreach (var group in root.ModalRoot.GetComponentsInChildren<CanvasGroup>(true)) group.ignoreParentGroups = true;
                ApplicationUiMigration.BindPresentation(game);
                CreatePreviews(root, hud, technology, talent, building);
                talentGraph.gameObject.SetActive(false); policyGraph.gameObject.SetActive(false); court.CourtGraph.gameObject.SetActive(false);
                foreach (var panel in root.FeaturePanels) panel.gameObject.SetActive(false);
                building.BuildingCard.gameObject.SetActive(false);
                quest.QuestPanel.gameObject.SetActive(false);
                technology.TechnologyTree.gameObject.SetActive(false);
                if (game.GetComponentsInChildren<UIPanelBase>(true).Length != 1 || game.GetComponentsInChildren<UI_SettingPanel>(true).Length != 0 || game.GetComponentsInChildren<MonoBehaviour>(true).Where(component => component != null && component.GetType().Name == "SaveSlotView").ToArray().Length != 0)
                    throw new InvalidOperationException("游戏预制体仍有重复的全局面板或旧存档模板。");
                root.ValidateConfiguration();
                var saved = PrefabUtility.SaveAsPrefabAsset(game, NewPath);
                SaveRecipes(game, saved);
            }
            finally { PrefabUtility.UnloadPrefabContents(game); }
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(Path.GetDirectoryName(Marker)); File.WriteAllText(Marker, DateTimeOffset.Now.ToString("O"));
            return "游戏组合根、独立子控制器、强类型条目、世界预览引用和编辑示例已迁移。";
        }

        static void CopyReferences(Component target, Dictionary<string, Object> references)
        {
            var serialized = new SerializedObject(target);
            foreach (var entry in references)
            {
                var property = serialized.FindProperty(entry.Key);
                if (property != null && property.propertyType == SerializedPropertyType.ObjectReference) property.objectReferenceValue = entry.Value;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        static void SetReference(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ?? throw new InvalidOperationException(target.name + " 未声明字段：" + field);
            property.objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        static T MakeRow<T>(UI_GamePanel_Row source, Dictionary<string, Object> legacy, Transform templateRoot, string name, string example, params (string field, string old)[] extra) where T : UI_GamePanel_Row
        {
            var clone = Object.Instantiate(source.gameObject);
            clone.name = "UI_GamePanel_" + name;
            try
            {
                var pairs = PairObjects(source.gameObject, clone);
                Object.DestroyImmediate(clone.GetComponent<UI_GamePanel_Row>());
                var row = clone.AddComponent<T>();
                var refs = new Dictionary<string, Object>();
                foreach (var key in new[] { "Select", "Label", "Layout", "Icon" }) refs[key] = Remap(legacy[key], pairs);
                foreach (var entry in extra)
                {
                    var mapped = Remap(legacy[entry.old], pairs);
                    refs[entry.field] = entry.field == "Grid" ? GameObjectOf(mapped).transform : mapped;
                }
                CopyReferences(row, refs);
                var required = refs.Values.Where(x => x != null).Select(GameObjectOf).ToArray();
                foreach (Transform child in clone.transform.Cast<Transform>().ToArray())
                    if (!required.Any(x => x.transform == child || x.transform.IsChildOf(child))) Object.DestroyImmediate(child.gameObject);
                foreach (var hover in clone.GetComponents<UI_GamePanel_BuildingWorkerHover>())
                    if (!(row is UI_GamePanel_WorkerInfoRow)) Object.DestroyImmediate(hover);
                row.Label.text = example;
                foreach (var reference in refs.Values.OfType<TMP_InputField>()) reference.SetTextWithoutNotify("1");
                clone.SetActive(false); row.ValidateConfiguration();
                ApplicationUiMigration.BindPresentation(clone);
                // Scene-owner references are authored as nested prefab overrides, never stored across prefab files.
                var external = new List<(Component component, string path, Object value)>();
                foreach (var component in clone.GetComponentsInChildren<Component>(true))
                {
                    var serialized = new SerializedObject(component); var property = serialized.GetIterator();
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                        var value = property.objectReferenceValue;
                        if (value == null || EditorUtility.IsPersistent(value) || !(value is Component || value is GameObject)) continue;
                        var transform = GameObjectOf(value).transform;
                        if (transform == clone.transform || transform.IsChildOf(clone.transform)) continue;
                        external.Add((component, property.propertyPath, value)); property.objectReferenceValue = null;
                    }
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                var prefab = PrefabUtility.SaveAsPrefabAsset(clone, ItemPath + clone.name + ".prefab");
                var nested = (GameObject)PrefabUtility.InstantiatePrefab(prefab, templateRoot);
                var nestedPairs = PairObjects(clone, nested);
                foreach (var entry in external)
                {
                    var serialized = new SerializedObject(nestedPairs[entry.component]);
                    serialized.FindProperty(entry.path).objectReferenceValue = entry.value;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(nestedPairs[entry.component]);
                }
                var configured = nested.GetComponent<T>(); configured.ValidateConfiguration(); return configured;
            }
            finally { Object.DestroyImmediate(clone); }
        }
        static Object Remap(Object value, Dictionary<Object, Object> pairs) => value != null && pairs.TryGetValue(value, out var clone) ? clone : value;
        static Dictionary<Object, Object> PairObjects(GameObject source, GameObject target)
        {
            var result = new Dictionary<Object, Object>();
            void Visit(Transform a, Transform b)
            {
                result[a.gameObject] = b.gameObject;
                var ac = a.GetComponents<Component>(); var bc = b.GetComponents<Component>();
                if (ac.Length != bc.Length || a.childCount != b.childCount) throw new InvalidOperationException("预制体层级与迁移基线不一致。");
                for (int i = 0; i < ac.Length; i++) if (ac[i] != null) result[ac[i]] = bc[i];
                for (int i = 0; i < a.childCount; i++) Visit(a.GetChild(i), b.GetChild(i));
            }
            Visit(source.transform, target.transform); return result;
        }
        static Dictionary<long, Object> LocalIds(GameObject source, GameObject target)
        {
            var result = new Dictionary<long, Object>();
            foreach (var pair in PairObjects(source, target))
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(pair.Key, out string _, out long id)) result[id] = pair.Value;
            return result;
        }
        static Dictionary<string, Object> ReadReferences(string manifest, Dictionary<long, Object> local)
        {
            var result = new Dictionary<string, Object>();
            foreach (Match match in Regex.Matches(manifest, @"(?m)^\s*(\w+): \{fileID: (-?\d+)(?:, guid: ([a-f0-9]+), type: \d+)?\}"))
            {
                string name = match.Groups[1].Value; long id = long.Parse(match.Groups[2].Value); string guid = match.Groups[3].Value;
                Object value = null;
                if (id != 0)
                {
                    if (string.IsNullOrEmpty(guid))
                    { if (!local.TryGetValue(id, out value)) throw new InvalidOperationException("旧引用无法解析：" + name + " / " + id); }
                    else if (guid == "0000000000000000e000000000000000") value = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                    else
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        value = AssetDatabase.LoadAllAssetsAtPath(path).FirstOrDefault(asset => AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string _, out long localId) && localId == id);
                        if (value == null) throw new InvalidOperationException("旧资产引用无法解析：" + name + " / " + guid);
                    }
                }
                result.Add(name, value);
            }
            return result;
        }
        static UI_GamePanel_WorldInteraction.PreviewTemplate[] ConfigureBuildingPreviews(GameCatalogAsset catalog)
        {
            var templates = new List<UI_GamePanel_WorldInteraction.PreviewTemplate>();
            var configured = new Dictionary<string, BuildingPlacementPreviewBinding>();
            foreach (var definition in catalog.Definitions.Where(x => x != null && x.Data.Kind == ContentKind.Building))
            {
                var source = definition.Data;
                if (source.Prefab == null) throw new InvalidOperationException(source.Id + " 缺少建筑表现预制体。");
                var path = AssetDatabase.GetAssetPath(source.Prefab);
                if (!configured.TryGetValue(path, out var binding))
                {
                    var prefab = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        binding = prefab.GetComponent<BuildingPlacementPreviewBinding>() ?? prefab.AddComponent<BuildingPlacementPreviewBinding>();
                        binding.Colliders = prefab.GetComponentsInChildren<Collider>(true);
                        binding.Behaviours = prefab.GetComponentsInChildren<Behaviour>(true).Where(x => x != binding).ToArray();
                        binding.Slots = prefab.GetComponentsInChildren<BuildingVisualSlotAuthoring>(true);
                        binding.Parts = prefab.GetComponentsInChildren<MeshRenderer>(true).Select(renderer => new BuildingPlacementPreviewBinding.RenderPart
                        {
                            Renderer = renderer,
                            Slot = renderer.GetComponentInParent<BuildingVisualSlotAuthoring>(true),
                            Crop = !string.IsNullOrEmpty(renderer.GetComponentInParent<BuildingVisualPartAuthoring>(true)?.CropId)
                        }).ToArray();
                        binding.ValidateConfiguration();
                        binding = PrefabUtility.SaveAsPrefabAsset(prefab, path).GetComponent<BuildingPlacementPreviewBinding>();
                        configured.Add(path, binding);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(prefab); }
                }
                templates.Add(new UI_GamePanel_WorldInteraction.PreviewTemplate { DefinitionId = source.Id, Template = binding });
            }
            return templates.ToArray();
        }
        static void ConfigureGraph(UI_GamePanel_CourtGraph graph, bool family)
        {
            var rect = (RectTransform)graph.transform;
            rect.anchorMin = new Vector2(family ? 0 : .31f, .04f); rect.offsetMin = new Vector2(220, 0);
            ((RectTransform)graph.Scroll.transform).anchorMax = new Vector2(family ? .705f : .99f, .91f);
        }
        static void CreatePreviews(UI_GamePanel root, UI_GamePanel_Hud hud, UI_GamePanel_Technology technology, UI_GamePanel_Talent talent, UI_GamePanel_Building building)
        {
            Preview(hud, "GameHud", UIPreviewKind.GameHud,
                new[] { new UIPreviewTextBinding("turn", hud.Status), new UIPreviewTextBinding("message", hud.Message), new UIPreviewTextBinding("population", hud.Selection) }, Array.Empty<UIPreviewListBinding>());
            Preview(technology, "Technology", UIPreviewKind.Technology,
                new[] { new UIPreviewTextBinding("title", technology.TechnologyTree.Header) },
                new[] { new UIPreviewListBinding("nodes", technology.TechnologyTree.GraphScroll.content, (RectTransform)technology.TechnologyTree.NodeTemplate.transform, new TMP_Text[] { technology.TechnologyTree.NodeTemplate.Label }, new[] { 0 }) });
            Preview(talent, "Talent", UIPreviewKind.Talent,
                new[] { new UIPreviewTextBinding("title", talent.CourtGraph.Header) },
                new[] { new UIPreviewListBinding("people", talent.CourtGraph.NodesRoot, (RectTransform)talent.CourtGraph.NodeTemplate.transform, new TMP_Text[] { talent.CourtGraph.NodeTemplate.Label }, new[] { 0 }) });
            Preview(building, "BuildingDetails", UIPreviewKind.BuildingDetails,
                new[] { new UIPreviewTextBinding("level", building.BuildingCard.Level), new UIPreviewTextBinding("production", building.BuildingCard.Block<UI_GamePanel_BuildingDetails_Block_基础产出>().Label), new UIPreviewTextBinding("workers", building.BuildingCard.Block<UI_GamePanel_BuildingDetails_Block_岗位>().Jobs), new UIPreviewTextBinding("experience", building.BuildingCard.Experience) }, Array.Empty<UIPreviewListBinding>());
        }
        static void Preview(UIViewBase owner, string name, UIPreviewKind kind, UIPreviewTextBinding[] texts, UIPreviewListBinding[] lists)
        {
            var profile = UIPreviewBuilder.EnsureDefaultProfile(ProfilePath + name + ".asset", kind);
            var marker = UIPreviewBuilder.Apply(owner, profile, texts, lists);
            if (kind == UIPreviewKind.Technology || kind == UIPreviewKind.Talent)
            {
                int i = 0;
                foreach (var sample in marker.SampleObjects)
                {
                    var rect = (RectTransform)sample.transform;
                    rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
                    rect.anchoredPosition = new Vector2(24 + i % 3 * 240, -24 - i / 3 * 150);
                    rect.sizeDelta = new Vector2(220, 130); i++;
                }
                foreach (var list in lists) list.container.sizeDelta = new Vector2(780, 480);
            }
            pendingRecipes.Add((owner, name, profile, texts, lists));
        }
        static readonly List<(UIViewBase owner, string name, UIPreviewProfile profile, UIPreviewTextBinding[] texts, UIPreviewListBinding[] lists)> pendingRecipes = new List<(UIViewBase, string, UIPreviewProfile, UIPreviewTextBinding[], UIPreviewListBinding[])>();
        static void SaveRecipes(GameObject edited, GameObject saved)
        {
            var pairs = PairObjects(edited, saved);
            foreach (var entry in pendingRecipes)
            {
                var path = ProfilePath + entry.name + "Recipe.asset";
                var recipe = AssetDatabase.LoadAssetAtPath<UIPreviewRecipe>(path);
                if (recipe == null) { recipe = ScriptableObject.CreateInstance<UIPreviewRecipe>(); AssetDatabase.CreateAsset(recipe, path); }
                var texts = entry.texts.Select(x => new UIPreviewTextBinding(x.key, (TMP_Text)Remap(x.target, pairs), x.runtimeText)).ToArray();
                var lists = entry.lists.Select(x => new UIPreviewListBinding(x.key, (RectTransform)Remap(x.container, pairs), (RectTransform)Remap(x.template, pairs), x.textTargets.Select(t => (TMP_Text)Remap(t, pairs)).ToArray(), x.columns)).ToArray();
                recipe.Configure((UIViewBase)Remap(entry.owner, pairs), entry.profile, texts, lists);
                EditorUtility.SetDirty(recipe);
            }
            pendingRecipes.Clear();
        }
        static GameObject GameObjectOf(Object value) => value is GameObject go ? go : value is Component component ? component.gameObject : throw new InvalidOperationException("引用不是场景对象。");
        static Transform Child(Transform parent, string name) { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return go.transform; }
        static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        static void Folder(string path) { path = path.Replace('\\', '/').TrimEnd('/'); if (AssetDatabase.IsValidFolder(path)) return; Folder(Path.GetDirectoryName(path)); AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path)); }
    }
}
#endif
