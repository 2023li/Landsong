#if UNITY_EDITOR
using System;
using Landsong.ECS.Authoring;
using Landsong.ECS.Authoring.Definitions;
using UnityEditor;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public sealed class CourtContentWindow : EditorWindow
    {
        Vector2 scroll;
        string validation = "";
        [MenuItem("Landsong/ECS/Court content and rules")]
        static void Open() => GetWindow<CourtContentWindow>("王室与人才配置");
        void OnGUI()
        {
            var talents = AssetDatabase.LoadAssetAtPath<TalentCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/TalentCatalog.asset");
            var slots = AssetDatabase.LoadAssetAtPath<TalentSlotCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/TalentSlotCatalog.asset");
            var traits = AssetDatabase.LoadAssetAtPath<RoyalTraitCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/RoyalTraitCatalog.asset");
            var policies = AssetDatabase.LoadAssetAtPath<PolicyCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/PolicyCatalog.asset");
            EditorGUILayout.HelpBox("人才、职位、王室特质和政策各自使用独立目录。配置修改经 Baking 后在新王朝生效。年龄、政治、交际与出访规则由独立 CourtSettingsAuthoring 配置。", MessageType.Info);
            if (GUILayout.Button("校验人才 / 职位 / 王室 / 政策"))
            {
                try
                {
                    var template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Landsong/ECSContent/World/GameWorldTemplate.prefab");
                    var content = template.GetComponent<GameContentSetAuthoring>().Content;
                    using (var blob = TalentCatalogBaking.Compile(content))
                    {
                    }

                    using (var blob = TalentSlotCatalogBaking.Compile(content))
                    {
                    }

                    using (var blob = RoyalTraitCatalogBaking.Compile(content))
                    {
                    }

                    using (var blob = PolicyCatalogBaking.Compile(content))
                    {
                    }

                    validation = "人才、职位、王室特质和政策校验通过";
                }
                catch (Exception error)
                {
                    validation = error.Message;
                }
            }

            if (!string.IsNullOrEmpty(validation))
                EditorGUILayout.HelpBox(validation, MessageType.Info);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (talents != null)
            {
                if (GUILayout.Button("人才目录"))
                    Selection.activeObject = talents;
                foreach (var definition in talents.Definitions)
                    Entry(definition, definition.Metadata);
            }

            if (slots != null)
            {
                if (GUILayout.Button("职位目录"))
                    Selection.activeObject = slots;
                foreach (var definition in slots.Definitions)
                    Entry(definition, definition.Metadata);
            }

            if (traits != null)
            {
                if (GUILayout.Button("王室特质目录"))
                    Selection.activeObject = traits;
                foreach (var definition in traits.Definitions)
                    Entry(definition, definition.Metadata);
            }

            if (policies != null)
            {
                if (GUILayout.Button("政策目录"))
                    Selection.activeObject = policies;
                foreach (var definition in policies.Definitions)
                    Entry(definition, definition.Metadata);
            }

            EditorGUILayout.EndScrollView();
        }

        static void Entry(UnityEngine.Object asset, DefinitionMetadataSource metadata)
        {
            if (GUILayout.Button(metadata.Name + " [" + metadata.Id + "]"))
                Selection.activeObject = asset;
        }
    }
}
#endif
