#if UNITY_EDITOR
using System;
using Landsong.ECS.Authoring;
using UnityEditor;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    // All production editors follow the same explicit composition as world baking.
    public static class ContentAuthoringContext
    {
        public const string TemplatePath = "Assets/Landsong/ECSContent/World/GameWorldTemplate.prefab";

        public static GameObject Template()
        {
            var template = AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePath);
            if (template == null)
                throw new InvalidOperationException("缺少正式世界模板：" + TemplatePath);
            return template;
        }

        public static GameContentSetAsset Content()
        {
            var authoring = Template().GetComponent<GameContentSetAuthoring>();
            if (authoring == null || authoring.Content == null)
                throw new InvalidOperationException("正式世界模板缺少内容集引用。");
            return authoring.Content;
        }

        public static T Catalog<T>() where T : ScriptableObject => Content().Get<T>();

        public static void DrawContext()
        {
            try
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField("正式内容集", Content(), typeof(GameContentSetAsset), false);
            }
            catch (Exception error)
            {
                EditorGUILayout.HelpBox(error.Message, MessageType.Error);
            }
        }
    }
}
#endif
