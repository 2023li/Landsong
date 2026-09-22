#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Landsong.ECS.Authoring;
using Landsong.ECS.Authoring.Definitions;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.OdinInspector.Editor.ValueResolvers;
using UnityEditor;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public static class ContentInspectorVerification
    {
        static readonly (string path, string label)[] Labels =
        {
            ("Metadata.Name", "名称"),
            ("MaximumLevel", "最高等级"),
            ("PlacementAndVisuals.CanMove", "允许移动"),
            ("PlacementAndVisuals.RuinMovementCost", "废墟通行消耗"),
            ("DefenseStats.Profile.DetectionRadius", "索敌半径"),
            ("Capabilities", "建筑功能"),
            ("Capabilities.Construction", "建造与施工"),
            ("Capabilities.Construction.PlacementCosts", "放置材料"),
            ("Capabilities.Upgrade.MaintenanceRequirements", "升级需维护"),
            ("Capabilities.Workforce.EfficiencyTiers", "工作效率档位"),
            ("Capabilities.Garrison.InitialUnits", "开局驻军")
        };
        public static string Run()
        {
            var log = new StringBuilder();
            int checks = 0;
            void Check(bool valid, string message)
            {
                if (!valid)
                    throw new InvalidOperationException(message);
                checks++;
                log.AppendLine("PASS " + message);
            }

            var catalog = AssetDatabase.LoadAssetAtPath<BuildingCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/BuildingCatalog.asset");
            var copy = UnityEngine.Object.Instantiate(catalog.Definitions.First(d => d.Metadata.Id == "b王宫"));
            var before = EditorJsonUtility.ToJson(copy);
            var inspector = UnityEditor.Editor.CreateEditor(copy);
            try
            {
                Check(inspector is BuildingDefinitionInspector && inspector is OdinEditor, "Content asset uses the Odin inspector entry point");
                using var tree = PropertyTree.Create(copy);
                tree.UpdateTree();
                foreach (var(path, label)in Labels)
                {
                    var property = tree.GetPropertyAtPath(path);
                    var attribute = property?.GetAttribute<LabelTextAttribute>();
                    Check(attribute != null && ValueResolver.GetForString(property, attribute.Text).GetValue() == label, "Odin resolves Chinese field: " + path);
                }

                var authoredTypes = typeof(BuildingDefinitionAsset).Assembly.GetTypes().Where(type => type.Namespace == "Landsong.ECS.Authoring.Definitions").ToArray();
                var assets = authoredTypes.Where(type => type.Name.EndsWith("DefinitionAsset", StringComparison.Ordinal) && typeof(ScriptableObject).IsAssignableFrom(type)).ToArray();
                Check(assets.Length == 22, "Inspector label verification includes all 22 definition assets");
                var types = assets.Concat(authoredTypes.Where(type => type.Name.EndsWith("Source", StringComparison.Ordinal))).Concat(new[] { typeof(CombatProfile), typeof(SoldierGrowth), typeof(HeroGrowth), typeof(OpportunityProfile), typeof(TheftProfile) });
                var fields = types.SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)).ToArray();
                Check(fields.All(field => field.GetCustomAttribute<LabelTextAttribute>() != null), "All direct definition fields and nested module/profile fields carry Odin Chinese labels");
                Check(fields.All(field => field.GetCustomAttribute<MinAttribute>() == null), "Localized Odin fields avoid the Unity Min drawer that overrides Chinese labels");
                Check(EditorJsonUtility.ToJson(copy) == before, "Reading Odin labels does not rewrite serialized data");
                var soldier = copy.Capabilities.Garrison.InitialUnits[0].Soldier;
                const string pathRef = "Capabilities.Garrison.InitialUnits.Array.data[0].Soldier";
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                try
                {
                    Undo.RecordObject(copy, "修改兵种引用");
                    var serialized = new SerializedObject(copy);
                    serialized.FindProperty(pathRef).objectReferenceValue = null;
                    serialized.ApplyModifiedProperties();
                    Undo.FlushUndoRecordObjects();
                    Check(copy.Capabilities.Garrison.InitialUnits[0].Soldier == null, "Odin reference picker commits through Unity serialization");
                    Undo.RevertAllDownToGroup(group);
                    Check(copy.Capabilities.Garrison.InitialUnits[0].Soldier == soldier, "Undo restores the exact referenced soldier asset");
                    var field = typeof(BuildingInitialGarrisonSource).GetField(nameof(BuildingInitialGarrisonSource.Soldier));
                    Check(field.FieldType == typeof(SoldierDefinitionAsset) && !field.FieldType.IsAssignableFrom(typeof(ItemDefinitionAsset)), "Wrong asset type is excluded by the concrete serialized field type");
                }
                finally
                {
                    Undo.ClearUndo(copy);
                }

                log.AppendLine("Assertions: " + checks);
                return log.ToString();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(inspector);
                UnityEngine.Object.DestroyImmediate(copy);
                File.WriteAllText("Library/LandsongEcs/content-inspector-verification.txt", log.ToString());
            }
        }

        public static void RunGui() => ContentInspectorProbe.Open();
    }

    public sealed class ContentInspectorProbe : EditorWindow
    {
        BuildingDefinitionInspector inspector;
        BuildingDefinitionAsset copy;
        Vector2 scroll;
        int frames, captureStage;
        bool complete;
        double started;
        const string Report = "Library/LandsongEcs/content-inspector-gui.txt";
        public static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请先退出 Play");
            var window = CreateInstance<ContentInspectorProbe>();
            window.titleContent = new GUIContent("内容检查器验证");
            window.position = new Rect(100, 80, 820, 900);
            var catalog = AssetDatabase.LoadAssetAtPath<BuildingCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/BuildingCatalog.asset");
            window.copy = Instantiate(catalog.Definitions.First(d => d.Capabilities.Construction.Enabled && d.MaximumLevel > 1));
            window.inspector = (BuildingDefinitionInspector)UnityEditor.Editor.CreateEditor(window.copy);
            window.started = EditorApplication.timeSinceStartup;
            window.ShowUtility();
            window.Focus();
            File.WriteAllText(Report, "Started " + DateTimeOffset.Now.ToString("O"));
        }

        void Update()
        {
            if (complete)
            {
                Close();
                return;
            }

            if (inspector == null)
            {
                Close();
                return;
            }

            // Own the capture state in Update: delayCall may be postponed indefinitely by an idle editor.
            if (captureStage == 1)
            {
                try
                {
                    Capture("content-inspector-top.png");
                    scroll.y = 900;
                    frames = 0;
                    captureStage = 2;
                    Repaint();
                }
                catch (Exception error)
                {
                    complete = true;
                    File.WriteAllText(Report, "FAIL " + error);
                }

                return;
            }

            if (captureStage == 3)
            {
                complete = true;
                Finish();
                return;
            }

            if (EditorApplication.timeSinceStartup - started > 45)
            {
                complete = true;
                File.WriteAllText(Report, "INCOMPLETE " + DateTimeOffset.Now.ToString("O") + "\nInspector did not receive visible repaint events; restore the interactive desktop and retry.");
                Close();
            }
            else
                Repaint();
        }

        void OnGUI()
        {
            if (inspector == null)
                return;
            try
            {
                EditorGUIUtility.labelWidth = 280;
                scroll = EditorGUILayout.BeginScrollView(scroll);
                inspector.OnInspectorGUI();
                EditorGUILayout.EndScrollView();
                var tree = inspector.Tree;
                foreach (var path in new[]
                {
                    "PlacementAndVisuals",
                    "DefenseStats.Profile",
                    "Capabilities",
                    "Capabilities.Construction",
                    "Capabilities.Construction.PlacementCosts",
                    "Capabilities.Upgrade",
                    "Capabilities.Maintenance",
                    "Capabilities.Workforce"
                }

                )
                {
                    var property = tree.GetPropertyAtPath(path);
                    if (property != null)
                        property.State.Expanded = true;
                }

                if (Event.current.type == EventType.Repaint && !complete && ++frames >= 8)
                {
                    if (captureStage == 0)
                        captureStage = 1;
                    else if (captureStage == 2)
                        captureStage = 3;
                }

                Repaint();
            }
            catch (Exception error)
            {
                complete = true;
                File.WriteAllText(Report, "FAIL " + error);
                throw;
            }
        }

        void Capture(string name)
        {
            var pixels = UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(position.position, (int)position.width, (int)position.height);
            var texture = new Texture2D((int)position.width, (int)position.height, TextureFormat.RGB24, false);
            try
            {
                texture.SetPixels(pixels);
                texture.Apply();
                File.WriteAllBytes("Library/LandsongEcs/" + name, texture.EncodeToPNG());
            }
            finally
            {
                DestroyImmediate(texture);
            }
        }

        void Finish()
        {
            try
            {
                Capture("content-inspector-modules.png");
                File.WriteAllText(Report, "PASS " + DateTimeOffset.Now.ToString("O") + "\nActual Odin ContentInspector drawn with expanded nested modules; temporary asset only.");
            }
            catch (Exception error)
            {
                File.WriteAllText(Report, "FAIL " + error);
            }
            finally
            {
                Close();
            }
        }

        void OnDisable()
        {
            if (inspector != null)
                DestroyImmediate(inspector);
            if (copy != null)
                DestroyImmediate(copy);
        }
    }
}
#endif
