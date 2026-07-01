using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace Landsong.GridSystem
{
    [ExecuteAlways]
    public sealed class GridRenderer : MonoBehaviour
    {
        private const string LineObjectName = "__GridRenderer_Lines";
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ZTestId = Shader.PropertyToID("_ZTest");

        [SerializeField, BoxGroup("引用"), LabelText("网格地图")] private GridMapBehaviour gridMap;
        [SerializeField, BoxGroup("线框"), LabelText("材质")] private Material lineMaterial;
        [SerializeField, BoxGroup("线框"), LabelText("颜色")] private Color lineColor = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField, FormerlySerializedAs("overlayThickness"), Min(0f), BoxGroup("线框"), LabelText("高度偏移")] private float linePlaneOffset = 0.02f;
        [SerializeField, Min(0.001f), BoxGroup("线框"), LabelText("宽度")] private float gridLineWidth = 0.03f;
        [SerializeField, BoxGroup("显示"), LabelText("显示网格线")] private bool showGridLines = true;
        [SerializeField, BoxGroup("显示"), LabelText("每帧强制刷新")] private bool forceRefreshEveryFrame;
        [SerializeField, BoxGroup("渲染"), LabelText("置顶渲染")] private bool renderOnTop = true;
        [SerializeField, BoxGroup("渲染"), LabelText("排序")] private int lineSortingOrder;

        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<int> indices = new List<int>();
        private readonly List<Color> vertexColors = new List<Color>();

        private GameObject lineObject;
        private MeshFilter lineMeshFilter;
        private MeshRenderer lineRenderer;
        private Mesh lineMesh;
        private Material runtimeLineMaterial;
        private bool lineMeshDirty = true;

        public bool GridLinesVisible => showGridLines;

        private void Reset()
        {
            gridMap = GetComponent<GridMapBehaviour>();
        }

        private void OnEnable()
        {
            ResolveReferences();
            EnsureRenderObjects();
            RefreshAll();
        }

        private void LateUpdate()
        {
            ResolveReferences();
            EnsureRenderObjects();

            if (forceRefreshEveryFrame)
            {
                MarkLineDirty();
            }

            if (lineMeshDirty)
            {
                RebuildLineMesh();
            }

            ApplyRendererState();
        }

        private void OnDisable()
        {
            if (lineRenderer != null)
            {
                lineRenderer.enabled = false;
            }
        }

        private void OnDestroy()
        {
            DestroyOwnedObject(lineObject);
            DestroyOwnedObject(lineMesh);
            DestroyOwnedObject(runtimeLineMaterial);
        }

        private void OnValidate()
        {
            linePlaneOffset = Mathf.Max(0f, linePlaneOffset);
            gridLineWidth = Mathf.Max(0.001f, gridLineWidth);
            MarkLineDirty();
            ConfigureRuntimeMaterial(runtimeLineMaterial);
        }

        public void SetGridLinesVisible(bool visible)
        {
            showGridLines = visible;
            ApplyRendererState();
        }

        public void RefreshAll()
        {
            MarkLineDirty();

            if (!isActiveAndEnabled)
            {
                return;
            }

            EnsureRenderObjects();
            RebuildLineMesh();
            ApplyRendererState();
        }

        private void ResolveReferences()
        {
            if (gridMap == null)
            {
                gridMap = GetComponent<GridMapBehaviour>();
            }
        }

        private void EnsureRenderObjects()
        {
            EnsureMesh(ref lineMesh, "Grid Renderer Lines Mesh");

            EnsureRenderObject(
                ref lineObject,
                ref lineMeshFilter,
                ref lineRenderer,
                LineObjectName,
                lineMesh,
                GetLineMaterial(),
                lineSortingOrder);

            ConfigureRuntimeMaterial(runtimeLineMaterial);
        }

        private void EnsureRenderObject(
            ref GameObject renderObject,
            ref MeshFilter meshFilter,
            ref MeshRenderer meshRenderer,
            string objectName,
            Mesh mesh,
            Material material,
            int sortingOrder)
        {
            if (renderObject == null)
            {
                var existing = transform.Find(objectName);
                renderObject = existing != null ? existing.gameObject : new GameObject(objectName);
                renderObject.transform.SetParent(transform, false);
            }

            renderObject.hideFlags = Application.isPlaying
                ? HideFlags.None
                : HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor;
            renderObject.layer = gameObject.layer;
            renderObject.SetActive(true);
            renderObject.transform.localPosition = Vector3.zero;
            renderObject.transform.localRotation = Quaternion.identity;
            renderObject.transform.localScale = Vector3.one;

            if (meshFilter == null)
            {
                meshFilter = renderObject.GetComponent<MeshFilter>();
                if (meshFilter == null)
                {
                    meshFilter = renderObject.AddComponent<MeshFilter>();
                }
            }

            if (meshRenderer == null)
            {
                meshRenderer = renderObject.GetComponent<MeshRenderer>();
                if (meshRenderer == null)
                {
                    meshRenderer = renderObject.AddComponent<MeshRenderer>();
                }
            }

            meshFilter.sharedMesh = mesh;
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            meshRenderer.allowOcclusionWhenDynamic = false;
            meshRenderer.sortingOrder = sortingOrder;
        }

        private static void EnsureMesh(ref Mesh mesh, string meshName)
        {
            if (mesh != null)
            {
                return;
            }

            mesh = new Mesh
            {
                name = meshName,
                hideFlags = HideFlags.HideAndDontSave,
                indexFormat = IndexFormat.UInt32
            };
            mesh.MarkDynamic();
        }

        private Material GetLineMaterial()
        {
            if (lineMaterial != null)
            {
                return lineMaterial;
            }

            if (runtimeLineMaterial == null)
            {
                runtimeLineMaterial = CreateDefaultMaterial("Grid Renderer Lines Material");
            }

            return runtimeLineMaterial;
        }

        private static Material CreateDefaultMaterial(string materialName)
        {
            var shader = Shader.Find("Sprites/Default")
                         ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                         ?? Shader.Find("Landsong/GridVertexColor")
                         ?? Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color");

            if (shader == null)
            {
                Debug.LogWarning("GridRenderer could not find a shader for grid line rendering.");
                return null;
            }

            return new Material(shader)
            {
                name = materialName,
                hideFlags = HideFlags.HideAndDontSave,
                renderQueue = (int)RenderQueue.Transparent
            };
        }

        private void ConfigureRuntimeMaterial(Material material)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty(ColorId))
            {
                material.SetColor(ColorId, Color.white);
            }

            if (material.HasProperty(BaseColorId))
            {
                material.SetColor(BaseColorId, Color.white);
            }

            if (material.HasProperty(ZTestId))
            {
                material.SetInt(ZTestId, renderOnTop ? (int)CompareFunction.Always : (int)CompareFunction.LessEqual);
            }

            material.renderQueue = (int)RenderQueue.Transparent;
        }

        private void RebuildLineMesh()
        {
            lineMeshDirty = false;

            if (lineMesh == null)
            {
                return;
            }

            if (!TryGetLayout(out var layout))
            {
                lineMesh.Clear();
                return;
            }

            vertices.Clear();
            indices.Clear();
            vertexColors.Clear();

            var offset = GetLineOffset(layout);
            var planeNormal = GetPlaneNormal(layout);
            var bounds = gridMap.BaseCellBounds;
            for (var x = bounds.xMin; x <= bounds.xMax; x++)
            {
                AddLineQuad(
                    lineMeshFilter.transform,
                    layout.GridToWorldPoint(x, bounds.yMin) + offset,
                    layout.GridToWorldPoint(x, bounds.yMax) + offset,
                    lineColor,
                    planeNormal);
            }

            for (var y = bounds.yMin; y <= bounds.yMax; y++)
            {
                AddLineQuad(
                    lineMeshFilter.transform,
                    layout.GridToWorldPoint(bounds.xMin, y) + offset,
                    layout.GridToWorldPoint(bounds.xMax, y) + offset,
                    lineColor,
                    planeNormal);
            }

            lineMesh.Clear();
            lineMesh.SetVertices(vertices);
            lineMesh.SetColors(vertexColors);
            lineMesh.SetTriangles(indices, 0);
            lineMesh.RecalculateBounds();
        }

        private void AddLineQuad(Transform targetTransform, Vector3 worldStart, Vector3 worldEnd, Color color, Vector3 planeNormal)
        {
            var direction = worldEnd - worldStart;
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            var side = Vector3.Cross(planeNormal, direction.normalized);
            if (side.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            side = side.normalized * gridLineWidth * 0.5f;
            var startIndex = vertices.Count;
            vertices.Add(targetTransform.InverseTransformPoint(worldStart - side));
            vertices.Add(targetTransform.InverseTransformPoint(worldStart + side));
            vertices.Add(targetTransform.InverseTransformPoint(worldEnd + side));
            vertices.Add(targetTransform.InverseTransformPoint(worldEnd - side));

            for (var i = 0; i < 4; i++)
            {
                vertexColors.Add(color);
            }

            indices.Add(startIndex);
            indices.Add(startIndex + 1);
            indices.Add(startIndex + 2);
            indices.Add(startIndex);
            indices.Add(startIndex + 2);
            indices.Add(startIndex + 3);
        }

        private bool TryGetLayout(out GridLayoutService layout)
        {
            layout = null;

            if (gridMap == null)
            {
                return false;
            }

            if (Application.isPlaying && !gridMap.IsInitialized)
            {
                gridMap.Initialize();
            }

            layout = gridMap.Layout != null ? gridMap.Layout : gridMap.CreateLayoutSnapshot();
            return layout != null;
        }

        private Vector3 GetLineOffset(GridLayoutService layout)
        {
            return GetPlaneNormal(layout) * linePlaneOffset;
        }

        private static Vector3 GetPlaneNormal(GridLayoutService layout)
        {
            return layout.PlaneMode == GridPlaneMode.XZ || layout.PlaneMode == GridPlaneMode.IsometricDiamondXZ
                ? Vector3.up
                : Vector3.forward;
        }

        private void MarkLineDirty()
        {
            lineMeshDirty = true;
        }

        private void ApplyRendererState()
        {
            if (lineRenderer != null)
            {
                lineRenderer.enabled = isActiveAndEnabled && showGridLines && lineMesh != null && lineMesh.vertexCount > 0;
                lineRenderer.sortingOrder = lineSortingOrder;
            }
        }

        private static void DestroyOwnedObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
