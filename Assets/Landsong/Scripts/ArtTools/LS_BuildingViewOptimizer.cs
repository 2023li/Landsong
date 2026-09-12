using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.VisualSystem
{
    /// <summary>
    /// 将场景中搭建的建筑一键转换成项目可用的纯表现 View Prefab。
    /// 优化制作场景美术，导出 ECS 就绪的视觉 Prefab；LV1 按命名配置选择是否同步主视觉。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LS_BuildingViewOptimizer : MonoBehaviour
    {
        public const string CompletedNameSuffix = "【已完成】";

        [TitleGroup("导出模式")]
        [SerializeField, LabelText("启用 LOD 优化")]
        [Tooltip("默认关闭：保留原始网格、材质、Renderer 和层级，只导出纯表现 Prefab。开启后才生成并简化 LOD0/LOD1/LOD2。")]
        private bool enableLodOptimization;

        [Serializable]
        public sealed class DynamicPartSettings
        {
            [LabelText("动态部件根节点"), Required]
            [Tooltip("例如风车扇叶的旋转轴节点。整个子树不会参与静态网格合并。")]
            public Transform Root;

            [LabelText("自动添加旋转组件")]
            [Tooltip("已有 Animator 或旋转脚本时可以关闭。")]
            public bool AddLocalAxisRotation;

            [LabelText("本地旋转轴"), ShowIf(nameof(AddLocalAxisRotation))]
            public Vector3 LocalAxis = Vector3.forward;

            [LabelText("每秒旋转角度"), ShowIf(nameof(AddLocalAxisRotation))]
            public float DegreesPerSecond = 30f;

            [LabelText("使用不受时间缩放影响的时间"), ShowIf(nameof(AddLocalAxisRotation))]
            public bool UseUnscaledTime;
        }

        [TitleGroup("LOD 参数"), ShowIf(nameof(EnableLodOptimization))]
        [SerializeField, LabelText("LOD0 切换高度"), Range(0f, 1f)]
        private float lod0TransitionHeight = 0.20f;

        [TitleGroup("LOD 参数"), ShowIf(nameof(EnableLodOptimization))]
        [SerializeField, LabelText("LOD1 质量"), Range(0.01f, 1f)]
        private float lod1Quality = 0.70f;

        [TitleGroup("LOD 参数"), ShowIf(nameof(EnableLodOptimization))]
        [SerializeField, LabelText("LOD1 切换高度"), Range(0f, 1f)]
        private float lod1TransitionHeight = 0.08f;

        [TitleGroup("LOD 参数"), ShowIf(nameof(EnableLodOptimization))]
        [SerializeField, LabelText("LOD2 质量"), Range(0.01f, 1f)]
        private float lod2Quality = 0.45f;

        [TitleGroup("LOD 参数"), ShowIf(nameof(EnableLodOptimization))]
        [SerializeField, LabelText("LOD2 / 剔除切换高度"), Range(0f, 1f)]
        private float lod2TransitionHeight = 0.015f;

        [TitleGroup("LOD 参数"), ShowIf(nameof(EnableLodOptimization))]
        [SerializeField, LabelText("全部 LOD 投射阴影")]
        private bool castShadows = true;

        [TitleGroup("LOD 参数"), ShowIf(nameof(EnableLodOptimization))]
        [SerializeField, LabelText("全部 LOD 接收阴影")]
        private bool receiveShadows = true;

        [TitleGroup("网格保护"), ShowIf(nameof(EnableLodOptimization))]
        [SerializeField, LabelText("保留边界边")]
        private bool preserveBorderEdges = true;

        [TitleGroup("网格保护"), ShowIf(nameof(EnableLodOptimization))]
        [SerializeField, LabelText("保留 UV 接缝边")]
        private bool preserveUVSeamEdges = true;

        [TitleGroup("网格保护"), ShowIf(nameof(EnableLodOptimization))]
        [SerializeField, LabelText("保留 UV 翻折边")]
        private bool preserveUVFoldoverEdges = true;

        [TitleGroup("网格保护"), ShowIf(nameof(EnableLodOptimization))]
        [SerializeField, LabelText("保留表面曲率")]
        private bool preserveSurfaceCurvature = true;

        [TitleGroup("动态部件")]
        [SerializeField, LabelText("不参与静态合并的动态部件")]
        [ListDrawerSettings(ShowIndexLabels = true, ListElementLabelName = nameof(DynamicPartSettings.Root))]
        private DynamicPartSettings[] dynamicParts = Array.Empty<DynamicPartSettings>();

        [ShowInInspector, ReadOnly, TitleGroup("上次导出")]
        [LabelText("上次导出预制体路径")] public string LastExportedPrefabPath { get; private set; } = string.Empty;

        public bool EnableLodOptimization => enableLodOptimization;
        public float LOD0TransitionHeight => lod0TransitionHeight;
        public float LOD1Quality => lod1Quality;
        public float LOD1TransitionHeight => lod1TransitionHeight;
        public float LOD2Quality => lod2Quality;
        public float LOD2TransitionHeight => lod2TransitionHeight;
        public bool CastShadows => castShadows;
        public bool ReceiveShadows => receiveShadows;
        public bool PreserveBorderEdges => preserveBorderEdges;
        public bool PreserveUVSeamEdges => preserveUVSeamEdges;
        public bool PreserveUVFoldoverEdges => preserveUVFoldoverEdges;
        public bool PreserveSurfaceCurvature => preserveSurfaceCurvature;
        public IReadOnlyList<DynamicPartSettings> DynamicParts => dynamicParts ?? Array.Empty<DynamicPartSettings>();
        public bool IsMarkedCompleted => HasCompletedNameSuffix(gameObject.name);

        [ShowInInspector, ReadOnly, TitleGroup("制作状态"), LabelText("当前状态")]
        private string CompletionStatus => IsMarkedCompleted ? "已完成" : "制作中";

        private void OnValidate()
        {
            lod0TransitionHeight = Mathf.Clamp01(lod0TransitionHeight);
            lod1TransitionHeight = Mathf.Clamp(lod1TransitionHeight, 0f, lod0TransitionHeight);
            lod2TransitionHeight = Mathf.Clamp(lod2TransitionHeight, 0f, lod1TransitionHeight);
            lod1Quality = Mathf.Clamp01(lod1Quality);
            lod2Quality = Mathf.Clamp(lod2Quality, 0.01f, lod1Quality);
            dynamicParts ??= Array.Empty<DynamicPartSettings>();
        }

        public void SetLastExportedPrefabPath(string assetPath)
        {
            LastExportedPrefabPath = assetPath ?? string.Empty;
        }

        public static bool HasCompletedNameSuffix(string objectName)
        {
            return !string.IsNullOrEmpty(objectName)
                   && objectName.EndsWith(CompletedNameSuffix, StringComparison.Ordinal);
        }

        public static string RemoveCompletedNameSuffix(string objectName)
        {
            if (!HasCompletedNameSuffix(objectName))
            {
                return objectName ?? string.Empty;
            }

            return objectName.Substring(0, objectName.Length - CompletedNameSuffix.Length);
        }

#if UNITY_EDITOR
        [Button("标记已完成", ButtonSizes.Medium), TitleGroup("制作状态")]
        [EnableIf(nameof(CanMarkCompleted))]
        private void MarkCompleted()
        {
            UnityEditor.Undo.RecordObject(gameObject, "标记建筑 View 已完成");
            gameObject.name += CompletedNameSuffix;
            UnityEditor.EditorUtility.SetDirty(gameObject);
        }

        [Button("移除已完成", ButtonSizes.Medium), TitleGroup("制作状态")]
        [EnableIf(nameof(IsMarkedCompleted))]
        private void RemoveCompleted()
        {
            UnityEditor.Undo.RecordObject(gameObject, "移除建筑 View 已完成标记");
            gameObject.name = RemoveCompletedNameSuffix(gameObject.name);
            UnityEditor.EditorUtility.SetDirty(gameObject);
        }

        private bool CanMarkCompleted => !IsMarkedCompleted;

        [Button("导出 ECS 视觉 Prefab（不优化）", ButtonSizes.Large), GUIColor(0.3f, 0.85f, 0.45f)]
        [HideIf(nameof(EnableLodOptimization))]
        public void ExportViewPrefabOnly()
        {
            LS_BuildingViewOptimizationPipeline.Export(this, true);
        }

        [Button("优化并导出 ECS 视觉 Prefab", ButtonSizes.Large), GUIColor(0.3f, 0.85f, 0.45f)]
        [ShowIf(nameof(EnableLodOptimization))]
        public void ExportOptimizedViewPrefab()
        {
            LS_BuildingViewOptimizationPipeline.Export(this, true);
        }
#endif
    }
}
