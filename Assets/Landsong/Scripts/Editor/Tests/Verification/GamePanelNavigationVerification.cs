#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using Landsong.ECS.Definitions;
using Landsong.ECS.Authoring.Definitions;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Authoring;
using Landsong.ECS.Presentation;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Landsong.ECS.Editor
{
    public static class GamePanelNavigationVerification
    {
        static StringBuilder report;
        static int assertions;
        static void Check(bool value, string label)
        {
            if (!value)
                throw new InvalidOperationException("FAIL " + label);
            assertions++;
            report.AppendLine("PASS " + label);
        }

        [MenuItem("Landsong/ECS/Verification/Game panel navigation")]
        public static string Run()
        {
            assertions = 0;
            report = new StringBuilder();
            try
            {
                Check(typeof(UI_GamePanel).GetMethod("OpenPanel", new[] { typeof(GamePanelId) }) != null && typeof(UI_GamePanel).GetMethod("OpenPanel", new[] { typeof(string) }) == null, "Local navigation accepts a typed identity and has no display-text fallback");
                Check(typeof(UI_GamePanel).GetProperty("Panel").PropertyType == typeof(GamePanelId) && typeof(UI_GamePanel_List).GetField("PanelId").FieldType == typeof(GamePanelId), "Navigation exposes a typed current identity and child views serialize typed destinations");
                Prefab();
                Permissions();
                SessionLifetime();
                ViewSessionReload();
                report.AppendLine("Assertions: " + assertions);
                return report.ToString();
            }
            catch (Exception error)
            {
                report.AppendLine(error.ToString());
                throw;
            }
            finally
            {
                Directory.CreateDirectory("Library/LandsongEcs");
                File.WriteAllText("Library/LandsongEcs/game-panel-navigation-verification.txt", report.ToString());
            }
        }

        static void SessionLifetime()
        {
            using var world = new World("Isolated game UI session ownership");
            var manager = world.EntityManager;
            var root = manager.CreateEntity(typeof(SimulationReady));
            var session = new GameUiSessionHandle();
            var refresh = new GameUiRefreshScheduler();
            var selection = new WorldSelectionState();
            var intelligence = new IntelligenceViewState();
            var commands = new GameUiCommandWriter();
            var navigation = new GamePanelNavigator(session, null, refresh, intelligence, commands, Array.Empty<UI_GamePanel_View>(), Array.Empty<UI_GamePanel.NavigationButtonBinding>(), null, null, null, null, null, null, null, null, null);
            int released = 0;
            bool failRelease = true;
            var lifetime = new GameUiSessionLifetime(session, refresh, selection, intelligence, commands, navigation, new Action[] { () =>
            {
                released++;
                if (failRelease)
                    throw new InvalidOperationException("injected view cleanup failure");
            }, () => released++ });
            lifetime.Bind(() => session.Bind(manager, root));
            selection.SelectedEntityId = 42;
            intelligence.IsOpen = true;
            refresh.NextPanel = float.PositiveInfinity;
            bool failed = false;
            try
            {
                lifetime.Unbind();
            }
            catch (AggregateException)
            {
                failed = true;
            }

            Check(failed && released == 2, "A failed view release does not prevent the remaining views from releasing");
            Check(!lifetime.IsBound && !session.IsBound && selection.SelectedEntityId == 0 && !intelligence.IsOpen && refresh.NextPanel == 0, "Failed view release still detaches session and clears independent interaction state");
            lifetime.Unbind();
            Check(released == 2, "Repeated detach is idempotent after cleanup failure");
            failRelease = false;
            try
            {
                lifetime.Bind(() =>
                {
                    session.Bind(manager, root);
                    throw new InvalidOperationException("injected initialization failure");
                });
            }
            catch (InvalidOperationException)
            {
            }

            Check(released == 4 && !lifetime.IsBound && !session.IsBound, "Failed initialization releases all views and leaves no bound session");
            lifetime.Bind(() => session.Bind(manager, root));
            Check(lifetime.IsBound && session.IsBound, "A subsequent session can bind after failure");
            lifetime.Unbind();
            Check(released == 6 && !session.IsBound, "Subsequent session releases exactly once");
        }

        static void ViewSessionReload()
        {
            var game = PrefabUtility.LoadPrefabContents(ApplicationUiAuthoring.GamePath);
            var cameraObject = new GameObject("Session reload camera", typeof(Camera));
            var canvasObject = new GameObject("Session reload canvas", typeof(Canvas), typeof(CanvasScaler));
            var eventsObject = new GameObject("Session reload events", typeof(EventSystem));
            var first = new World("First UI map session");
            var second = new World("Second UI map session");
            var view = game.GetComponent<UI_GamePanel>();
            try
            {
                var firstRoot = first.EntityManager.CreateEntity();
                var secondRoot = second.EntityManager.CreateEntity(typeof(SimulationReady));
                var camera = cameraObject.GetComponent<Camera>();
                var scaler = canvasObject.GetComponent<CanvasScaler>();
                var events = eventsObject.GetComponent<EventSystem>();
                view.BindSession(first.EntityManager, firstRoot, camera, scaler, events);
                Check(!view.Session.IsBound && !view.InputPolicy.Capture().CanNavigate,
                    "Actual UI binding keeps input unavailable before SimulationReady");
                first.EntityManager.AddComponent<SimulationReady>(firstRoot);
                Check(view.Session.IsBound, "The same UI handle becomes ready when its bound root is initialized");
                var expedition = game.GetComponentInChildren<UI_GamePanel_Expedition>(true);
                var world = view.WorldInteraction;
                var selection = Field<WorldSelectionState>(view, "worldSelection");
                var intelligence = Field<IntelligenceViewState>(view, "intelligence");
                var heroes = Field<Dictionary<ulong, UI_GamePanel_英雄选择Item>>(view.Hud, "heroSelectionItems");
                var catalogs = (view.Buildings.BuildingCatalog, view.Technology.Technologies, view.Hud.Heroes);
                GameObject ghost = null;
                UI_GamePanel_英雄选择Item hero = null;

                void Populate(EntityManager manager, Entity root, ulong id)
                {
                    selection.SelectedEntityId = id;
                    intelligence.IsOpen = true;
                    Set(expedition, "expeditionDestination", ExpeditionId.FromIndex(0));
                    Set(expedition, "expeditionCaptain", id);
                    Set(expedition, "expeditionCrew", 27);
                    Set(expedition, "expeditionAmounts", new[] { 7, 9 });
                    view.IntelligenceWindow.View = new IntelView { SelectedWave = 4, CompletedGroups = 5 };
                    Set(view.IntelligenceWindow, "intelligenceWave", 4);
                    Set(world, "buildDefinition", BuildingId.FromIndex(0));
                    Set(world, "roadStart", (int2?)new int2(2, 3));
                    ghost = new GameObject("Owned placement ghost " + id);
                    ghost.transform.SetParent(game.transform);
                    Set(world, "buildingGhost", ghost);
                    hero = UnityEngine.Object.Instantiate(view.Hud.HeroSelectionTemplate, view.Hud.HeroSelection);
                    hero.Bind(manager, root, id, true, true, _ => { });
                    heroes.Add(id, hero);
                    Check(hero.PortraitBinding.BoundPersonId == id && ghost != null,
                        "Session fixture owns a real hero portrait binding and placement object " + id);
                }

                void CheckReleased(string label)
                {
                    Check(selection.SelectedEntityId == 0 && !intelligence.IsOpen
                        && Field<ExpeditionId>(expedition, "expeditionDestination") == ExpeditionId.None
                        && Field<ulong>(expedition, "expeditionCaptain") == 0
                        && Field<int>(expedition, "expeditionCrew") == 10
                        && Field<int[]>(expedition, "expeditionAmounts") == null,
                        label + " clears world and expedition identities, crew and supply choices");
                    Check(view.IntelligenceWindow.View == null && Field<int>(view.IntelligenceWindow, "intelligenceWave") == 0,
                        label + " clears cached intelligence and selected wave");
                    Check(view.WorldInteraction.WorldPresentation.BuildingRangeSourceId == 0
                        && !world.HasBuildingPlacement && Field<int2?>(world, "roadStart") == null && ghost == null,
                        label + " destroys the placement object and releases presentation-owned range geometry");
                    Check(heroes.Count == 0 && hero == null && !view.Hud.HeroSelection.gameObject.activeSelf,
                        label + " releases and destroys hero selection objects rather than retaining old world bindings");
                }

                Populate(first.EntityManager, firstRoot, 41);
                view.BindSession(second.EntityManager, secondRoot, camera, scaler, events);
                CheckReleased("Rebinding a second world");
                first.Dispose();
                Check(view.Session.IsBound && catalogs == (view.Buildings.BuildingCatalog, view.Technology.Technologies, view.Hud.Heroes),
                    "After the first world is disposed, the reused UI keeps its new session and explicit display assets");
                Populate(second.EntityManager, secondRoot, 41);
                second.Dispose();
                view.UnbindSession();
                CheckReleased("Unbinding after world disposal");
                Check(!view.Session.IsBound && view.InputEvents == null && view.InterfaceScaler == null && world.Camera == null,
                    "Disposing the world before UI cleanup still detaches all session services");
                view.UnbindSession();
            }
            finally
            {
                view.UnbindSession();
                PrefabUtility.UnloadPrefabContents(game);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(canvasObject);
                UnityEngine.Object.DestroyImmediate(eventsObject);
                if (first.IsCreated) first.Dispose();
                if (second.IsCreated) second.Dispose();
            }

            static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(owner);
            static void Set(object owner, string name, object value) => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(owner, value);
        }

        static void Prefab()
        {
            var game = PrefabUtility.LoadPrefabContents(ApplicationUiAuthoring.GamePath);
            try
            {
                var view = game.GetComponent<UI_GamePanel>();
                Check(view != null && view.Panel == GamePanelId.Building, "Game root preserves its initial building destination");
                var ids = view.FeaturePanels.Select(panel => panel.PanelId).ToArray();
                Check(ids.All(id => id != GamePanelId.None && Enum.IsDefined(typeof(GamePanelId), id)) && ids.Distinct().Count() == ids.Length, "All child identities survive migration without missing or duplicate enum values");
                Check(!ids.Contains(GamePanelId.Building) && view.Buildings.BuildingBar != null, "Building navigation owns the catalog bar directly and has no generic list panel");
                Check(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Landsong/UI/Prefabs/GamePanel/Views/UI_GamePanel_List_Building.prefab") == null, "Obsolete building permission panel asset is deleted");
                foreach (var panel in view.FeaturePanels)
                    panel.ValidateConfiguration();
                foreach (var requirement in new[]
                {
                    (GamePanelId.Inventory, "feature.Inventory"),
                    (GamePanelId.Expedition, "feature.Expedition"),
                    (GamePanelId.Technology, ResearchOps.FeatureId)
                }

                )
                    Check(view.GetPanel(requirement.Item1).RequiredFeatureId == requirement.Item2, "Permission remains configured on destination: " + requirement.Item1);
                Check(view.GetListPanel(GamePanelId.Technology).AllowMissingFeature, "Optional custom-catalog research semantics are preserved");
                int typedEvents = 0;
                foreach (var button in game.GetComponentsInChildren<Button>(true))
                {
                    var calls = new SerializedObject(button).FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
                    for (int i = 0; i < calls.arraySize; i++)
                    {
                        var call = calls.GetArrayElementAtIndex(i);
                        string method = call.FindPropertyRelative("m_MethodName").stringValue;
                        Check(UnityEventBindingValidation.IsValidCall(call), "Every authored button call resolves its configured target type and exact argument signature: " + button.name + "." + method);
                        Check(method != "OpenPanel", "Persistent button does not retain the old string signature: " + button.name);
                        if (method != "OpenPanelFromEvent")
                            continue;
                        typedEvents++;
                        int value = call.FindPropertyRelative("m_Arguments.m_IntArgument").intValue;
                        Check(call.FindPropertyRelative("m_Target").objectReferenceValue == view && call.FindPropertyRelative("m_Mode").intValue == 3 && Enum.IsDefined(typeof(GamePanelId), value) && value != 0 && ((GamePanelId)value == GamePanelId.Pause || (GamePanelId)value == GamePanelId.Building || ids.Contains((GamePanelId)value)), "Typed static event retains target and valid destination: " + button.name);
                    }
                }

                Check(typedEvents == 9, "All nine authored navigation button calls survive the parameter migration");
                foreach (var binding in view.NavigationButtons)
                {
                    Check(binding.Button != null && (binding.Target == GamePanelId.Building || ids.Contains(binding.Target)), "Permission-controlled navigation button has a configured destination: " + binding.Target);
                    var calls = new SerializedObject(binding.Button).FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
                    bool matches = false;
                    for (int i = 0; i < calls.arraySize; i++)
                    {
                        var call = calls.GetArrayElementAtIndex(i);
                        matches |= call.FindPropertyRelative("m_MethodName").stringValue == "OpenPanelFromEvent" && call.FindPropertyRelative("m_Arguments.m_IntArgument").intValue == (int)binding.Target;
                        matches |= binding.Target == GamePanelId.Building && call.FindPropertyRelative("m_MethodName").stringValue == "ToggleBuildingCatalog" && call.FindPropertyRelative("m_Target").objectReferenceValue == view.Buildings;
                    }

                    Check(matches, "Button permission destination agrees with its retained click event: " + binding.Target);
                }

                foreach (int invalid in new[]
                {
                    0,
                    int.MaxValue
                }

                )
                {
                    bool rejected = false;
                    try
                    {
                        view.OpenPanelFromEvent(invalid);
                    }
                    catch (InvalidOperationException)
                    {
                        rejected = true;
                    }

                    Check(rejected, "Invalid serialized event destination fails as configuration: " + invalid);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(game);
            }
        }

        static void Permissions()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<FeatureCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/FeatureCatalog.asset");
            using var blob = FeatureCatalogCompiler.Build(catalog);
            using var world = new World("Owned navigation permission fixture");
            var em = world.EntityManager;
            var root = em.CreateEntity();
            em.AddComponentData(root, new FeatureCatalog { Value = blob });
            em.AddBuffer<UnlockedFeature>(root);
            var owned = new GameObject("Owned permission view");
            try
            {
                var panel = owned.AddComponent<UI_GamePanel_List>();
                panel.PanelId = GamePanelId.History;
                panel.RequiredFeatureId = "feature.Inventory";
                Check(!panel.CanOpen(em, root), "A history destination can declare an inventory permission without a root mapping");
                FeatureUnlocks.Unlock(em, root, FeatureDefinitions.Find(em, root, "feature.Inventory"));
                Check(panel.CanOpen(em, root), "Granting configured stable permission enables the same destination");
                panel.RequiredFeatureId = "feature.owned_missing";
                Check(!panel.CanOpen(em, root), "Missing required permission remains denied");
                panel.AllowMissingFeature = true;
                Check(panel.CanOpen(em, root), "Explicit optional catalog permission remains supported");
                panel.AllowMissingFeature = false;
                panel.AllowLockedOpen = true;
                Check(panel.CanOpen(em, root) && !panel.IsFeatureUnlocked(em, root), "Locked explanation access does not mark its navigation button unlocked");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owned);
            }
        }
    }
}
#endif
