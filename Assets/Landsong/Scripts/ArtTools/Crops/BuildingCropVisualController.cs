using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Landsong.VisualSystem
{
    /// <summary>
    /// 农田级作物表现入口。组件引用作物 Prefab，并把直接子对象中的空点位作为种植位置。
    /// 仅用于编辑器点位和生长阶段预览，不读取或持有 ECS 农田玩法状态。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildingCropVisualController : MonoBehaviour
    {
        [Serializable]
        public sealed class CropPlantPoint
        {
            [SerializeField, LabelText("本地位置")]
            [Tooltip("相对于农田作物控制器的本地坐标。Y 值用于贴合田埂高度。")]
            private Vector3 localPosition;

            [SerializeField, LabelText("Y轴旋转"), Range(0f, 360f)]
            private float yaw;

            [SerializeField, LabelText("统一缩放"), MinValue(0.01f)]
            private float uniformScale = 1f;

            public CropPlantPoint()
            {
            }

            public CropPlantPoint(Vector3 localPosition, float yaw, float uniformScale)
            {
                this.localPosition = localPosition;
                this.yaw = yaw;
                this.uniformScale = uniformScale;
                Normalize();
            }

            public Vector3 LocalPosition => localPosition;
            public float Yaw => Mathf.Repeat(yaw, 360f);
            public float UniformScale => Mathf.Max(0.01f, uniformScale);
            internal bool IsValid =>
                IsFinite(localPosition.x)
                && IsFinite(localPosition.y)
                && IsFinite(localPosition.z)
                && IsFinite(yaw)
                && IsFinite(uniformScale)
                && uniformScale > 0f;

            internal void Normalize()
            {
                yaw = Mathf.Repeat(yaw, 360f);
                uniformScale = Mathf.Max(0.01f, uniformScale);
            }

            internal void Set(Vector3 position, float nextYaw, float nextScale)
            {
                localPosition = position;
                yaw = nextYaw;
                uniformScale = nextScale;
                Normalize();
            }

            private static bool IsFinite(float value)
            {
                return !float.IsNaN(value) && !float.IsInfinity(value);
            }
        }

        [Serializable]
        private sealed class LegacySpawnArea
        {
            [SerializeField] private Vector3 center;
            [SerializeField] private Vector2 size = new Vector2(3f, 0.6f);

            public LegacySpawnArea()
            {
            }

            public LegacySpawnArea(Vector3 center, Vector2 size)
            {
                this.center = center;
                this.size = size;
            }

            public Vector3 Center => center;
            public Vector2 Size => new Vector2(
                Mathf.Max(0.01f, Mathf.Abs(size.x)),
                Mathf.Max(0.01f, Mathf.Abs(size.y)));
        }

        private readonly struct PlantPlacement
        {
            public PlantPlacement(BuildingCropPlantPoint point)
            {
                LocalPosition = point.LocalPosition;
                LocalRotation = point.LocalRotation;
                UniformScale = point.UniformScale;
            }

            public PlantPlacement(CropPlantPoint point)
            {
                LocalPosition = point.LocalPosition;
                LocalRotation = Quaternion.Euler(0f, point.Yaw, 0f);
                UniformScale = point.UniformScale;
            }

            public Vector3 LocalPosition { get; }
            public Quaternion LocalRotation { get; }
            public float UniformScale { get; }
        }

        private const int DefaultMigratedPointCount = 48;

        [Header("农田支持的作物")]
        [SerializeField, LabelText("作物 Prefab"), AssetsOnly]
        [ListDrawerSettings(ShowIndexLabels = true, DraggableItems = true)]
        [Tooltip("每项引用一个带 BuildingCropPlantVisual 的单株作物 Prefab；CropId 由作物 Prefab 自己保存。")]
        private BuildingCropPlantVisual[] cropPrefabs = Array.Empty<BuildingCropPlantVisual>();

        [Header("固定种植点")]
        [SerializeField, HideInInspector]
        [Tooltip("旧版序列化点位，只用于一次性迁移为空子对象。")]
        private List<CropPlantPoint> plantPoints = new List<CropPlantPoint>();

        [SerializeField, HideInInspector]
        private int fixedPointSerializationVersion;

        [SerializeField, HideInInspector]
        private int childPointSerializationVersion;

        [SerializeField, HideInInspector]
        private List<LegacySpawnArea> spawnAreas = new List<LegacySpawnArea>();

        [SerializeField, HideInInspector, FormerlySerializedAs("spawnAreaCenter")]
        private Vector3 legacySpawnAreaCenter;

        [SerializeField, HideInInspector, FormerlySerializedAs("spawnAreaSize")]
        private Vector2 legacySpawnAreaSize = new Vector2(3f, 0.6f);

        [SerializeField, HideInInspector]
        private int spawnAreaSerializationVersion;


        public IReadOnlyList<BuildingCropPlantVisual> CropPrefabs =>
            cropPrefabs ?? Array.Empty<BuildingCropPlantVisual>();
        public int PlantPointCount => GetConfiguredPlantPointCount();
        public int EditablePlantPointCount => CountChildPlantPoints();
        public bool HasLegacyPlantPoints =>
            childPointSerializationVersion < 1 && GetLegacyPlantPointCount() > 0;

        private void OnValidate()
        {
            UpgradeFixedPointsIfNeeded();
            for (var i = 0; i < plantPoints.Count; i++)
            {
                plantPoints[i]?.Normalize();
            }
        }

        public bool TryValidateConfiguration(out string error)
        {
            UpgradeFixedPointsIfNeeded();
            if (!enabled || !gameObject.activeSelf)
            {
                error = "农田的 BuildingCropVisualController 及其根节点必须默认启用。";
                return false;
            }

            if (!TryValidatePlantPoints(out error))
            {
                return false;
            }

            if (cropPrefabs == null || cropPrefabs.Length == 0)
            {
                error = "农田作物控制器至少需要引用一个单株作物 Prefab。";
                return false;
            }

            var cropIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < cropPrefabs.Length; i++)
            {
                var cropPrefab = cropPrefabs[i];
                if (cropPrefab == null)
                {
                    error = $"农田作物 Prefab 列表的第 {i + 1} 项为空。";
                    return false;
                }

                if (!cropPrefab.TryValidateConfiguration(out error))
                {
                    return false;
                }

                if (!cropIds.Add(cropPrefab.CropId))
                {
                    error = $"农田重复引用了 CropId={cropPrefab.CropId} 的作物 Prefab。";
                    return false;
                }

            }

            error = string.Empty;
            return true;
        }

        public bool TryResolveCropPrefab(
            string requestedCropId,
            out BuildingCropPlantVisual cropPrefab)
        {
            var normalizedCropId = string.IsNullOrWhiteSpace(requestedCropId)
                ? string.Empty
                : requestedCropId.Trim();
            if (cropPrefabs != null)
            {
                for (var i = 0; i < cropPrefabs.Length; i++)
                {
                    var candidate = cropPrefabs[i];
                    if (candidate != null
                        && string.Equals(candidate.CropId, normalizedCropId, StringComparison.Ordinal))
                    {
                        cropPrefab = candidate;
                        return true;
                    }
                }
            }

            cropPrefab = null;
            return false;
        }

        private void UpgradeFixedPointsIfNeeded()
        {
            if (plantPoints == null)
            {
                plantPoints = new List<CropPlantPoint>();
            }

            if (fixedPointSerializationVersion >= 1)
            {
                return;
            }

            if (plantPoints.Count == 0)
            {
                var targetCount = ResolveMigrationPointCount();
                GenerateFixedPointsFromLegacyAreas(targetCount);
            }

            fixedPointSerializationVersion = 1;
        }

        private int ResolveMigrationPointCount()
        {
            var result = cropPrefabs == null || cropPrefabs.Length == 0
                ? 1
                : 0;
            if (cropPrefabs != null)
            {
                for (var i = 0; i < cropPrefabs.Length; i++)
                {
                    if (cropPrefabs[i] != null)
                    {
                        result = Mathf.Max(result, cropPrefabs[i].TargetPlantCount);
                    }
                }
            }

            return result > 0 ? result : DefaultMigratedPointCount;
        }

        private void GenerateFixedPointsFromLegacyAreas(int targetCount)
        {
            var areas = new List<LegacySpawnArea>();
            if (spawnAreas != null)
            {
                for (var i = 0; i < spawnAreas.Count; i++)
                {
                    if (spawnAreas[i] != null)
                    {
                        areas.Add(spawnAreas[i]);
                    }
                }
            }

            if (areas.Count == 0)
            {
                areas.Add(new LegacySpawnArea(
                    legacySpawnAreaCenter,
                    legacySpawnAreaSize));
            }

            var totalArea = 0f;
            for (var i = 0; i < areas.Count; i++)
            {
                var size = areas[i].Size;
                totalArea += size.x * size.y;
            }

            var pointCounts = new int[areas.Count];
            for (var pointIndex = 0; pointIndex < targetCount; pointIndex++)
            {
                var ticket = (pointIndex + 0.5f) / targetCount * totalArea;
                for (var areaIndex = 0; areaIndex < areas.Count; areaIndex++)
                {
                    var size = areas[areaIndex].Size;
                    ticket -= size.x * size.y;
                    if (ticket <= 0f || areaIndex == areas.Count - 1)
                    {
                        pointCounts[areaIndex]++;
                        break;
                    }
                }
            }

            for (var areaIndex = 0; areaIndex < areas.Count; areaIndex++)
            {
                AddGridPoints(areas[areaIndex], pointCounts[areaIndex]);
            }
        }

        private void AddGridPoints(LegacySpawnArea area, int count)
        {
            if (count <= 0)
            {
                return;
            }

            var size = area.Size;
            var aspect = size.x / Mathf.Max(0.01f, size.y);
            var columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(count * aspect)));
            var rows = Mathf.Max(1, Mathf.CeilToInt((float)count / columns));
            for (var i = 0; i < count; i++)
            {
                var column = i % columns;
                var row = i / columns;
                var position = new Vector3(
                    area.Center.x + ((column + 0.5f) / columns - 0.5f) * size.x,
                    area.Center.y,
                    area.Center.z + ((row + 0.5f) / rows - 0.5f) * size.y);
                plantPoints.Add(new CropPlantPoint(position, 0f, 1f));
            }
        }

        private bool TryValidatePlantPoints(out string error)
        {
            var childPoints = GetChildPlantPoints();
            if (childPoints.Count > 0)
            {
                for (var childIndex = 0; childIndex < transform.childCount; childIndex++)
                {
                    var child = transform.GetChild(childIndex);
                    if (child.TryGetComponent<BuildingCropPlantPoint>(out _))
                    {
                        continue;
                    }


                    error = "CropVisuals 的制作态直接子对象只能是带 BuildingCropPlantPoint 的空种植点。";
                    return false;
                }

                for (var i = 0; i < childPoints.Count; i++)
                {
                    if (!childPoints[i].TryValidateConfiguration(this, out error))
                    {
                        return false;
                    }
                }

                error = string.Empty;
                return true;
            }

            if (transform.childCount > 0)
            {
                error = "CropVisuals 的直接子对象必须挂有 BuildingCropPlantPoint。";
                return false;
            }

            if (childPointSerializationVersion < 1)
            {
                UpgradeFixedPointsIfNeeded();
                for (var i = 0; i < plantPoints.Count; i++)
                {
                    var point = plantPoints[i];
                    if (point == null || !point.IsValid)
                    {
                        error = $"农田旧固定点 #{i + 1} 的位置、旋转或缩放无效。";
                        return false;
                    }
                }

                if (plantPoints.Count > 0)
                {
                    error = string.Empty;
                    return true;
                }
            }

            error = "农田至少需要一个带 BuildingCropPlantPoint 的空子对象。";
            return false;
        }

        private List<BuildingCropPlantPoint> GetChildPlantPoints()
        {
            var result = new List<BuildingCropPlantPoint>();
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child.TryGetComponent<BuildingCropPlantPoint>(out var point))
                {
                    result.Add(point);
                }
            }

            return result;
        }

        private int CountChildPlantPoints()
        {
            var result = 0;
            for (var i = 0; i < transform.childCount; i++)
            {
                if (transform.GetChild(i).TryGetComponent<BuildingCropPlantPoint>(out _))
                {
                    result++;
                }
            }

            return result;
        }

        private int GetLegacyPlantPointCount()
        {
            UpgradeFixedPointsIfNeeded();
            return plantPoints?.Count ?? 0;
        }

        private int GetConfiguredPlantPointCount()
        {
            var childPointCount = CountChildPlantPoints();
            if (childPointCount > 0 || childPointSerializationVersion >= 1)
            {
                return childPointCount;
            }

            return GetLegacyPlantPointCount();
        }

        private List<PlantPlacement> BuildPlantPlacements(int targetPlantCount)
        {
            var availablePlacements = GetAvailablePlantPlacements();
            var result = new List<PlantPlacement>(targetPlantCount);
            for (var plantIndex = 0; plantIndex < targetPlantCount; plantIndex++)
            {
                var pointIndex = Mathf.Min(
                    availablePlacements.Count - 1,
                    Mathf.FloorToInt(
                        (plantIndex + 0.5f) * availablePlacements.Count / targetPlantCount));
                result.Add(availablePlacements[pointIndex]);
            }

            return result;
        }

        private int ResolveGeneratedPlantCount(BuildingCropPlantVisual cropPrefab)
        {
            if (cropPrefab == null)
            {
                return 0;
            }

            return Mathf.Min(cropPrefab.TargetPlantCount, GetConfiguredPlantPointCount());
        }

        private List<PlantPlacement> GetAvailablePlantPlacements()
        {
            var childPoints = GetChildPlantPoints();
            var result = new List<PlantPlacement>(
                childPoints.Count > 0 ? childPoints.Count : GetLegacyPlantPointCount());
            if (childPoints.Count > 0)
            {
                for (var i = 0; i < childPoints.Count; i++)
                {
                    result.Add(new PlantPlacement(childPoints[i]));
                }

                return result;
            }

            if (childPointSerializationVersion < 1 && plantPoints != null)
            {
                for (var i = 0; i < plantPoints.Count; i++)
                {
                    if (plantPoints[i] != null)
                    {
                        result.Add(new PlantPlacement(plantPoints[i]));
                    }
                }
            }

            return result;
        }

#if UNITY_EDITOR
        private const string EditorPreviewRootPrefix = "[农田固定点预览] ";

        [SerializeField, LabelText("预览作物 Prefab"), AssetsOnly, PropertyOrder(90)]
        [Tooltip("留空时使用作物 Prefab 列表中的第一个有效项。")]
        private BuildingCropPlantVisual editorPreviewCropPrefab;

        [SerializeField, LabelText("预览生长百分比"), Range(0f, 100f), PropertyOrder(91)]
        private float editorPreviewGrowthPercent;

        [NonSerialized]
        private GameObject editorPreviewRoot;

        [Button("预览固定种植点", ButtonSizes.Medium), PropertyOrder(92)]
        [GUIColor(0.45f, 0.85f, 1f)]
        private void PreviewFixedPoints()
        {
            if (Application.isPlaying)
            {
                return;
            }

            ClearEditorPreview();
            var previewCropPrefab = ResolveEditorPreviewCropPrefab();
            if (!TryValidatePlantPoints(out var error)
                || previewCropPrefab == null
                || !previewCropPrefab.TryValidateConfiguration(out error))
            {
                var previewError = string.IsNullOrWhiteSpace(error)
                    ? "没有可用的作物 Prefab。"
                    : error;
                Debug.LogWarning($"无法预览农田作物：{previewError}", this);
                return;
            }

            var configuredPointCount = GetConfiguredPlantPointCount();

            editorPreviewRoot = new GameObject(GetEditorPreviewRootName(previewCropPrefab.CropId))
            {
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
            };
            editorPreviewRoot.transform.SetParent(transform.parent, false);
            editorPreviewRoot.transform.SetLocalPositionAndRotation(
                transform.localPosition,
                transform.localRotation);
            editorPreviewRoot.transform.localScale = transform.localScale;

            var placements = BuildPlantPlacements(ResolveGeneratedPlantCount(previewCropPrefab));
            for (var i = 0; i < placements.Count; i++)
            {
                var instanceObject = PrefabUtility.InstantiatePrefab(
                    previewCropPrefab.gameObject,
                    editorPreviewRoot.transform) as GameObject;
                if (instanceObject == null)
                {
                    instanceObject = Instantiate(
                        previewCropPrefab.gameObject,
                        editorPreviewRoot.transform,
                        false);
                }

                var instance = instanceObject.GetComponent<BuildingCropPlantVisual>();
                var placement = placements[i];
                instanceObject.name = $"Preview_{previewCropPrefab.CropId}_{i + 1:D3}";
                instanceObject.transform.SetLocalPositionAndRotation(
                    placement.LocalPosition,
                    placement.LocalRotation);
                instanceObject.transform.localScale = Vector3.one * placement.UniformScale;
                if (instance == null
                    || !instance.ApplyGrowthPercent(editorPreviewGrowthPercent, out error))
                {
                    ClearEditorPreview();
                    Debug.LogWarning($"无法预览农田作物：{error}", this);
                    return;
                }

                SetPreviewHideFlags(instanceObject);
            }

            SceneView.RepaintAll();
            Debug.Log(
                $"已预览作物 {previewCropPrefab.CropId}：{editorPreviewGrowthPercent:0.##}% 阶段，" +
                $"使用 {placements.Count}/{configuredPointCount} 个固定点。",
                this);
        }

        [Button("清除固定点预览", ButtonSizes.Medium), PropertyOrder(93)]
        private void ClearEditorPreview()
        {
            if (Application.isPlaying)
            {
                return;
            }

            if (editorPreviewRoot != null)
            {
                DestroyImmediate(editorPreviewRoot);
                editorPreviewRoot = null;
            }

            var previewParent = transform.parent;
            if (previewParent != null)
            {
                for (var i = previewParent.childCount - 1; i >= 0; i--)
                {
                    var child = previewParent.GetChild(i);
                    if (child != transform
                        && child.name.StartsWith(EditorPreviewRootPrefix, StringComparison.Ordinal))
                    {
                        DestroyImmediate(child.gameObject);
                    }
                }
            }

            SceneView.RepaintAll();
        }

        private BuildingCropPlantVisual ResolveEditorPreviewCropPrefab()
        {
            if (editorPreviewCropPrefab != null)
            {
                return editorPreviewCropPrefab;
            }

            if (cropPrefabs != null)
            {
                for (var i = 0; i < cropPrefabs.Length; i++)
                {
                    if (cropPrefabs[i] != null)
                    {
                        return cropPrefabs[i];
                    }
                }
            }

            return null;
        }

        private static string GetEditorPreviewRootName(string cropId)
        {
            return $"{EditorPreviewRootPrefix}{cropId}";
        }

        private static void SetPreviewHideFlags(GameObject instance)
        {
            var previewFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            var transforms = instance.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                transforms[i].gameObject.hideFlags = previewFlags;
            }
        }

        public BuildingCropPlantPoint[] GetEditorPlantPoints()
        {
            return GetChildPlantPoints().ToArray();
        }

        public int CreateEditorPlantPointChildrenFromLegacy()
        {
            if (Application.isPlaying)
            {
                return 0;
            }

            var existingPoints = GetChildPlantPoints();
            if (existingPoints.Count > 0)
            {
                Undo.RecordObject(this, "Use Crop Plant Point Children");
                childPointSerializationVersion = 1;
                EditorUtility.SetDirty(this);
                return existingPoints.Count;
            }

            if (transform.childCount > 0)
            {
                Debug.LogWarning("CropVisuals 已包含非点位子对象，无法自动迁移固定点。", this);
                return 0;
            }

            UpgradeFixedPointsIfNeeded();
            Undo.RecordObject(this, "Migrate Crop Plant Points To Children");
            for (var i = 0; i < plantPoints.Count; i++)
            {
                var legacyPoint = plantPoints[i];
                if (legacyPoint == null || !legacyPoint.IsValid)
                {
                    continue;
                }

                CreateEditorPlantPointObject(
                    legacyPoint.LocalPosition,
                    Quaternion.Euler(0f, legacyPoint.Yaw, 0f),
                    legacyPoint.UniformScale,
                    i + 1,
                    "Migrate Crop Plant Point");
            }

            childPointSerializationVersion = 1;
            EditorUtility.SetDirty(this);
            PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            return CountChildPlantPoints();
        }

        public BuildingCropPlantPoint AddEditorPlantPoint()
        {
            if (Application.isPlaying)
            {
                return null;
            }

            if (CountChildPlantPoints() == 0 && childPointSerializationVersion < 1)
            {
                CreateEditorPlantPointChildrenFromLegacy();
            }

            var points = GetChildPlantPoints();
            var position = Vector3.zero;
            var rotation = Quaternion.identity;
            var scale = 1f;
            if (points.Count > 0)
            {
                var source = points[points.Count - 1];
                position = source.LocalPosition + new Vector3(0.25f, 0f, 0f);
                rotation = source.LocalRotation;
                scale = source.UniformScale;
            }

            Undo.RecordObject(this, "Add Crop Plant Point");
            childPointSerializationVersion = 1;
            var createdPoint = CreateEditorPlantPointObject(
                position,
                rotation,
                scale,
                points.Count + 1,
                "Add Crop Plant Point");
            EditorUtility.SetDirty(this);
            PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            return createdPoint;
        }

        private BuildingCropPlantPoint CreateEditorPlantPointObject(
            Vector3 localPosition,
            Quaternion localRotation,
            float uniformScale,
            int pointNumber,
            string undoName)
        {
            var pointObject = new GameObject($"PlantPoint_{pointNumber:D3}");
            Undo.RegisterCreatedObjectUndo(pointObject, undoName);
            pointObject.transform.SetParent(transform, false);
            pointObject.transform.SetLocalPositionAndRotation(localPosition, localRotation);
            pointObject.transform.localScale = Vector3.one * uniformScale;
            return Undo.AddComponent<BuildingCropPlantPoint>(pointObject);
        }
#endif
    }
}
