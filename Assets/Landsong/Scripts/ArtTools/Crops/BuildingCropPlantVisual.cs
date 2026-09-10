using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.VisualSystem
{
    /// <summary>
    /// 单株作物 Prefab 的自包含表现组件。它保存作物身份、生成参数和生长阶段，
    /// 只接收农田控制器传入的生长百分比，不主动读取玩法状态。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildingCropPlantVisual : MonoBehaviour
    {
        [Serializable]
        public sealed class GrowthStage
        {
            [SerializeField, LabelText("开始生长百分比"), Range(0f, 100f)]
            [Tooltip("达到该百分比后启用本阶段。第一项必须从 0% 开始，后续阈值必须严格递增。")]
            private float startGrowthPercent;

            [SerializeField, LabelText("阶段子对象")]
            [Tooltip("预放在单株作物 Prefab 根节点下的直接子对象。第一阶段默认启用，其余阶段默认关闭。")]
            private GameObject stageRoot;

            public float StartGrowthPercent => Mathf.Clamp(startGrowthPercent, 0f, 100f);
            public GameObject StageRoot => stageRoot;
        }

        public const int MaximumSupportedPlantCount = 500;

        [Header("作物身份与阶段")]
        [SerializeField, LabelText("作物ID")]
        private string cropId = string.Empty;

        [SerializeField, LabelText("百分比生长阶段"), ListDrawerSettings(ShowIndexLabels = true)]
        [Tooltip("阶段数量不固定。按生长百分比从小到大配置，最后一个已达到阈值的阶段会显示。")]
        private GrowthStage[] growthStages = Array.Empty<GrowthStage>();

        [Header("本作物的种植外观")]
        [SerializeField, LabelText("满密度株数"), MinValue(1), MaxValue(MaximumSupportedPlantCount)]
        [Tooltip("农田生成密度为 100% 时，本作物希望使用的固定点数量；实际株数不会超过农田现有点位数。")]
        private int maximumPlantCount = 25;

        [SerializeField, LabelText("生成密度百分比"), Range(1f, 100f)]
        [Tooltip("最终株数 = 满密度株数 × 本百分比，至少生成一株。")]
        private float spawnDensityPercent = 100f;

        private int activeStageIndex = -1;

        public string CropId => string.IsNullOrWhiteSpace(cropId) ? string.Empty : cropId.Trim();
        public IReadOnlyList<GrowthStage> GrowthStages =>
            growthStages ?? Array.Empty<GrowthStage>();
        public int MaximumPlantCount =>
            Mathf.Clamp(maximumPlantCount, 1, MaximumSupportedPlantCount);
        public float SpawnDensityPercent => Mathf.Clamp(spawnDensityPercent, 1f, 100f);
        public int TargetPlantCount => Mathf.Clamp(
            Mathf.RoundToInt(MaximumPlantCount * SpawnDensityPercent * 0.01f),
            1,
            MaximumPlantCount);
        private void OnValidate()
        {
            maximumPlantCount = Mathf.Clamp(
                maximumPlantCount,
                1,
                MaximumSupportedPlantCount);
            spawnDensityPercent = Mathf.Clamp(spawnDensityPercent, 1f, 100f);
        }

        public bool TryValidateConfiguration(out string error)
        {
            if (string.IsNullOrWhiteSpace(CropId))
            {
                error = $"作物 Prefab {name} 缺少 CropId。";
                return false;
            }

            if (!enabled || !gameObject.activeSelf)
            {
                error = $"作物 {CropId} 的 Prefab 根节点和 BuildingCropPlantVisual 必须默认启用。";
                return false;
            }

            if (!TryValidateStages(true, out error))
            {
                return false;
            }

            if (maximumPlantCount < 1 || maximumPlantCount > MaximumSupportedPlantCount)
            {
                error = $"作物 {CropId} 的满密度株数必须位于 1～{MaximumSupportedPlantCount}。";
                return false;
            }

            if (spawnDensityPercent < 1f || spawnDensityPercent > 100f)
            {
                error = $"作物 {CropId} 的生成密度必须位于 1%～100%。";
                return false;
            }

            if (GetComponentInChildren<Collider>(true) != null
                || GetComponentInChildren<Collider2D>(true) != null)
            {
                error = $"作物 {CropId} 的 Prefab 只能包含表现组件，不允许 Collider。";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool ApplyGrowthPercent(float growthPercent, out string error)
        {
            if (!TryValidateStages(false, out error))
            {
                return false;
            }

            var nextStageIndex = ResolveStageIndex(growthPercent);
            if (nextStageIndex < 0)
            {
                error = $"作物 {CropId} 没有可用于 {growthPercent:0.##}% 的表现阶段。";
                return false;
            }

            if (activeStageIndex != nextStageIndex)
            {
                for (var i = 0; i < growthStages.Length; i++)
                {
                    SetActive(growthStages[i].StageRoot, i == nextStageIndex);
                }

                activeStageIndex = nextStageIndex;
            }

            error = string.Empty;
            return true;
        }

        private bool TryValidateStages(bool requireAuthoringDefaults, out string error)
        {
            if (growthStages == null || growthStages.Length == 0)
            {
                error = $"作物 {CropId} 至少需要配置一个百分比表现阶段。";
                return false;
            }

            var usedStageRoots = new HashSet<GameObject>();
            var previousPercent = -1f;
            for (var i = 0; i < growthStages.Length; i++)
            {
                var stage = growthStages[i];
                if (stage == null || stage.StageRoot == null)
                {
                    error = $"作物 {CropId} 的阶段 #{i + 1} 缺少预放阶段子对象。";
                    return false;
                }

                if (i == 0 && !Mathf.Approximately(stage.StartGrowthPercent, 0f))
                {
                    error = $"作物 {CropId} 的第一个表现阶段必须从 0% 开始。";
                    return false;
                }

                if (stage.StartGrowthPercent <= previousPercent + 0.0001f)
                {
                    error = $"作物 {CropId} 的阶段开始百分比必须严格递增。";
                    return false;
                }

                if (!usedStageRoots.Add(stage.StageRoot))
                {
                    error = $"作物 {CropId} 的多个阶段重复引用了 {stage.StageRoot.name}。";
                    return false;
                }

                if (stage.StageRoot == gameObject || stage.StageRoot.transform.parent != transform)
                {
                    error = $"作物 {CropId} 的阶段 {stage.StageRoot.name} 必须是作物 Prefab 根节点的直接子对象。";
                    return false;
                }

                var stageTransform = stage.StageRoot.transform;
                if (stageTransform.localPosition.sqrMagnitude > 0.000001f
                    || Quaternion.Angle(stageTransform.localRotation, Quaternion.identity) > 0.001f
                    || (stageTransform.localScale - Vector3.one).sqrMagnitude > 0.000001f)
                {
                    error = $"作物 {CropId} 的阶段子对象 Transform 必须为 Position=0、Rotation=0、Scale=1：{stage.StageRoot.name}。";
                    return false;
                }

                if (requireAuthoringDefaults
                    && stage.StageRoot.activeSelf != (i == 0))
                {
                    error = $"作物 {CropId} 的第一阶段必须默认启用，其余阶段必须默认关闭：{stage.StageRoot.name}。";
                    return false;
                }

                previousPercent = stage.StartGrowthPercent;
            }

            error = string.Empty;
            return true;
        }

        private int ResolveStageIndex(float growthPercent)
        {
            var normalizedPercent = Mathf.Clamp(growthPercent, 0f, 100f);
            var result = -1;
            for (var i = 0; i < growthStages.Length; i++)
            {
                if (growthStages[i].StartGrowthPercent > normalizedPercent)
                {
                    break;
                }

                result = i;
            }

            return result;
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }
    }
}
