#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Landsong.ECS.Authoring;
using Landsong.ECS.Presentation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Landsong.ECS.Editor
{
    public sealed class ReleaseContentValidation : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            ValidateCommon();
            if ((report.summary.options & BuildOptions.Development) == 0)
                ValidateRelease();
        }

        public static void ValidateCommon()
        {
            DomainCatalogBuild.Validate();
            DisplayCatalogBuild.Validate();
            ContentAuthoringValidation.ValidateCurrent();
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).ToArray();
            if (!scenes.Select(s => s.path).SequenceEqual(EcsSceneFlow.BuildScenes))
                throw new BuildFailedException("构建场景必须依次为 Boot、Start、LoadingTransition、Game。");
            foreach (var scene in scenes)
                if (!File.Exists(scene.path) || AssetDatabase.GUIDToAssetPath(scene.guid.ToString()) != scene.path)
                    throw new BuildFailedException("场景路径与 GUID 不一致：" + scene.path);
        }

        public static void ValidateRelease()
        {
            var template = AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Landsong/ECSContent/World/GameWorldTemplate.prefab");
            ValidatePortraits(template == null ? null : template.GetComponent<PortraitLibraryAuthoring>()?.Portraits);
        }

        internal static void ValidatePortraits(PortraitConfig config)
        {
            if (config == null || config.Placeholders)
                throw new BuildFailedException("正式构建需要完整美术肖像，并关闭 PortraitConfig.Placeholders。开发构建可继续使用占位素材。");
            using var library = PortraitLibraryBuilder.Build(config);
        }
    }
}
#endif
