using System;
using System.Linq;
using Moyo.Unity;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Landsong.Editor.UI
{
    /// <summary>保存在 Editor 目录的制作配方；正式面板不引用此资产。</summary>
    [CreateAssetMenu(fileName = "UIPreviewRecipe", menuName = "Landsong/UI/编辑预览绑定配方")]
    public sealed class UIPreviewRecipe : ScriptableObject
    {
        [SerializeField, LabelText("目标预制体视图"), Required] private UIViewBase targetView;
        [SerializeField, LabelText("示例数据"), Required] private UIPreviewProfile profile;
        [SerializeField, LabelText("固定文字绑定")] private UIPreviewTextBinding[] texts = Array.Empty<UIPreviewTextBinding>();
        [SerializeField, LabelText("动态列表绑定")] private UIPreviewListBinding[] lists = Array.Empty<UIPreviewListBinding>();

        public UIViewBase TargetView => targetView;

        public void Configure(UIViewBase owner, UIPreviewProfile previewProfile,
            UIPreviewTextBinding[] textBindings, UIPreviewListBinding[] listBindings)
        {
            targetView = owner; profile = previewProfile;
            texts = textBindings ?? Array.Empty<UIPreviewTextBinding>();
            lists = listBindings ?? Array.Empty<UIPreviewListBinding>();
        }

        [Button("刷新预制体示例内容")]
        public void ApplyToPrefab()
        {
            if (Application.isPlaying) throw new InvalidOperationException("不能在真实运行会话中改写编辑预览。");
            if (targetView == null || profile == null) throw new InvalidOperationException("预览配方缺少目标视图或数据。");
            var path = AssetDatabase.GetAssetPath(targetView);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("预览配方必须绑定一个 Prefab 资产内的视图。");
            var sourceRoot = targetView.transform.root;
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == path)
            {
                // 编辑中的未保存内容直接保留在当前 Stage，仅产生可撤销编辑，不覆盖磁盘。
                using (UiPanelLayoutAuthoring.Preserve(stage.prefabContentsRoot.transform as RectTransform))
                    ApplyMapped(sourceRoot, stage.prefabContentsRoot.transform, true);
                EditorSceneManager.MarkSceneDirty(stage.scene);
                return;
            }
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                using (UiPanelLayoutAuthoring.Preserve(contents.transform as RectTransform))
                    ApplyMapped(sourceRoot, contents.transform, false);
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        private void ApplyMapped(Transform sourceRoot, Transform loadedRoot, bool recordUndo)
        {
            var owner = UIPreviewBuilder.MapComponent(targetView, sourceRoot, loadedRoot);
            var mappedTexts = texts.Select(binding => new UIPreviewTextBinding(binding.key,
                UIPreviewBuilder.MapComponent(binding.target, sourceRoot, loadedRoot), binding.runtimeText)).ToArray();
            var mappedLists = lists.Select(binding => new UIPreviewListBinding(binding.key,
                UIPreviewBuilder.MapComponent(binding.container, sourceRoot, loadedRoot),
                UIPreviewBuilder.MapComponent(binding.template, sourceRoot, loadedRoot),
                binding.textTargets.Select(target => UIPreviewBuilder.MapComponent(target, sourceRoot, loadedRoot)).ToArray(),
                binding.columns, binding.layout, binding.textFormats, binding.textKeys)).ToArray();
            UIPreviewBuilder.Apply(owner, profile, mappedTexts, mappedLists, recordUndo);
        }
    }

    [CustomEditor(typeof(UIPreviewProfile))]
    public sealed class UIPreviewProfileEditor : OdinEditor { }
    [CustomEditor(typeof(UIPreviewRecipe))]
    public sealed class UIPreviewRecipeEditor : OdinEditor { }
}
