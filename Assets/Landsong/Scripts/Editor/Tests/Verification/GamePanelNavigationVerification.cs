#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Authoring;
using Landsong.ECS.Presentation;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Editor
{
    public static class GamePanelNavigationVerification
    {
        static StringBuilder report;
        static int assertions;
        static void Check(bool value, string label)
        {
            if (!value) throw new InvalidOperationException("FAIL " + label);
            assertions++; report.AppendLine("PASS " + label);
        }
        [MenuItem("Landsong/ECS/Verification/Game panel navigation")]
        public static string Run()
        {
            assertions = 0; report = new StringBuilder();
            try
            {
                Check(typeof(UI_GamePanel).GetMethod("OpenPanel", new[] { typeof(GamePanelId) }) != null
                    && typeof(UI_GamePanel).GetMethod("OpenPanel", new[] { typeof(string) }) == null,
                    "Local navigation accepts a typed identity and has no display-text fallback");
                Check(typeof(UI_GamePanel).GetField("Panel").FieldType == typeof(GamePanelId)
                    && typeof(UI_GamePanel_List).GetField("PanelId").FieldType == typeof(GamePanelId), "Serialized local identities are typed on root and child views");
                Prefab(); Permissions();
                report.AppendLine("Assertions: " + assertions); return report.ToString();
            }
            catch (Exception error) { report.AppendLine(error.ToString()); throw; }
            finally { Directory.CreateDirectory("Library/LandsongEcs"); File.WriteAllText("Library/LandsongEcs/game-panel-navigation-verification.txt", report.ToString()); }
        }
        static void Prefab()
        {
            var game = PrefabUtility.LoadPrefabContents(ApplicationUiMigration.GamePath);
            try
            {
                var view = game.GetComponent<UI_GamePanel>();
                Check(view != null && view.Panel == GamePanelId.Building, "Game root preserves its initial building destination");
                var ids = view.FeaturePanels.Select(panel => panel.PanelId).ToArray();
                Check(ids.All(id => id != GamePanelId.None && Enum.IsDefined(typeof(GamePanelId), id)) && ids.Distinct().Count() == ids.Length,
                    "All child identities survive migration without missing or duplicate enum values");
                foreach (var panel in view.FeaturePanels) panel.ValidateConfiguration();
                foreach (var requirement in new[] { (GamePanelId.Building, "feature.Building"), (GamePanelId.Inventory, "feature.Inventory"), (GamePanelId.Expedition, "feature.Expedition"), (GamePanelId.Technology, ResearchOps.FeatureId) })
                    Check(view.GetListPanel(requirement.Item1).RequiredFeatureId == requirement.Item2, "Permission remains configured on destination: " + requirement.Item1);
                Check(view.GetListPanel(GamePanelId.Building).AllowLockedOpen && view.GetListPanel(GamePanelId.Technology).AllowMissingFeature,
                    "Building explanation and optional custom-catalog research semantics are preserved");
                int typedEvents = 0;
                foreach (var button in game.GetComponentsInChildren<Button>(true))
                {
                    var calls = new SerializedObject(button).FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
                    for (int i = 0; i < calls.arraySize; i++)
                    {
                        var call = calls.GetArrayElementAtIndex(i);
                        string method = call.FindPropertyRelative("m_MethodName").stringValue;
                        Check(GamePanelEventMigration.IsValidCall(call), "Every authored button call resolves its configured target type and exact argument signature: " + button.name + "." + method);
                        Check(method != "OpenPanel", "Persistent button does not retain the old string signature: " + button.name);
                        if (method != "OpenPanelFromEvent") continue;
                        typedEvents++;
                        int value = call.FindPropertyRelative("m_Arguments.m_IntArgument").intValue;
                        Check(call.FindPropertyRelative("m_Target").objectReferenceValue == view && call.FindPropertyRelative("m_Mode").intValue == 3
                            && Enum.IsDefined(typeof(GamePanelId), value) && value != 0
                            && ((GamePanelId)value == GamePanelId.Pause || ids.Contains((GamePanelId)value)), "Typed static event retains target and valid destination: " + button.name);
                    }
                }
                Check(typedEvents == 9, "All nine authored navigation button calls survive the parameter migration");
                foreach (var binding in view.NavigationButtons)
                {
                    Check(binding.Button != null && ids.Contains(binding.Target), "Permission-controlled navigation button has a configured destination: " + binding.Target);
                    var calls = new SerializedObject(binding.Button).FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
                    bool matches = false;
                    for (int i = 0; i < calls.arraySize; i++)
                    {
                        var call = calls.GetArrayElementAtIndex(i);
                        matches |= call.FindPropertyRelative("m_MethodName").stringValue == "OpenPanelFromEvent"
                            && call.FindPropertyRelative("m_Arguments.m_IntArgument").intValue == (int)binding.Target;
                        matches |= binding.Target == GamePanelId.Building && call.FindPropertyRelative("m_MethodName").stringValue == "ToggleBuildingCatalog"
                            && call.FindPropertyRelative("m_Target").objectReferenceValue == view.Buildings;
                    }
                    Check(matches, "Button permission destination agrees with its retained click event: " + binding.Target);
                }
                foreach (int invalid in new[] { 0, int.MaxValue })
                {
                    bool rejected = false; try { view.OpenPanelFromEvent(invalid); } catch (InvalidOperationException) { rejected = true; }
                    Check(rejected, "Invalid serialized event destination fails as configuration: " + invalid);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(game); }
        }
        static void Permissions()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameCatalogAsset>("Assets/Landsong/ECSContent/GameCatalog.asset");
            using var blob = GameWorldAuthoring.BuildCatalog(catalog);
            using var world = new World("Owned navigation permission fixture");
            var em = world.EntityManager; var root = em.CreateEntity();
            em.AddComponentData(root, new ContentCatalog { Value = blob }); em.AddBuffer<Entitlement>(root);
            var owned = new GameObject("Owned permission view");
            try
            {
                var panel = owned.AddComponent<UI_GamePanel_List>(); panel.PanelId = GamePanelId.History;
                panel.RequiredFeatureId = "feature.Inventory";
                Check(!panel.CanOpen(em, root), "A history destination can declare an inventory permission without a root mapping");
                FeatureOps.Unlock(em, root, Sim.FindDefinition(em, root, "feature.Inventory"));
                Check(panel.CanOpen(em, root), "Granting configured stable permission enables the same destination");
                panel.RequiredFeatureId = "feature.owned_missing";
                Check(!panel.CanOpen(em, root), "Missing required permission remains denied");
                panel.AllowMissingFeature = true;
                Check(panel.CanOpen(em, root), "Explicit optional catalog permission remains supported");
                panel.AllowMissingFeature = false; panel.AllowLockedOpen = true;
                Check(panel.CanOpen(em, root) && !panel.IsFeatureUnlocked(em, root), "Locked explanation access does not mark its navigation button unlocked");
            }
            finally { UnityEngine.Object.DestroyImmediate(owned); }
        }
    }
}
#endif
