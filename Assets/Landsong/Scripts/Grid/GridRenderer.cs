using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Landsong.GridSystem
{
    [ExecuteAlways]
    public sealed class GridRenderer : MonoBehaviour
    {
        private const string LineObjectName = "__GridRenderer_Lines";
        private const string OverlayObjectName = "__GridRenderer_Overlay";
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ZTestId = Shader.PropertyToID("_ZTest");

        [SerializeField] private GridMapBehaviour gridMap;
        [SerializeField] private GridPointerProbe pointerProbe;
        [SerializeField] private Material lineMaterial;
        [SerializeField] private Material overlayMaterial;
        [SerializeField] private Color lineColor = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] private Color blockedCellColor = new Color(0.8f, 0.1f, 0.1f, 0.25f);
        [SerializeField] private Color occupiedCellColor = new Color(1f, 0.55f, 0.05f, 0.35f);
        [SerializeField] private Color currentCellColor = new Color(0.15f, 0.75f, 1f, 0.45f);
        [SerializeField, Min(0f)] private float overlayThickness = 0.02f;
        [SerializeField, Min(0.001f)] private float gridLineWidth = 0.03f;
        [SerializeField] private bool showGridLines = true;
        [SerializeField] private bool drawRuntimeCellStates = true;
        [SerializeField] private bool drawOccupiedCells = true;
        [SerializeField] private bool drawPointerCell = true;
        [SerializeField] private bool refreshEveryFrame = true;
        [SerializeField] private bool renderOnTop = true;
        [SerializeField] private int lineSortingOrder;
        [SerializeField] private int overlaySortingOrder = 1;

        private readonly Dictionary<GridPosition, Color> cellColorOverrides = new Dictionary<GridPosition, Color>();
        private readonly Dictionary<GridPosition, Color> visibleCellColors = new Dictionary<GridPosition, Color>();
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<int> indices = new List<int>();
        private readonly List<Color> vertexColors = new List<Color>();

        private GameObject lineObject;
        private GameObject overlayObject;
        private MeshFilter lineMeshFilter;
        private MeshFilter overlayMeshFilter;
        private MeshRenderer lineRenderer;
        private MeshRenderer overlayRenderer;
        private Mesh lineMesh;
        private Mesh overlayMesh;
        private Material runtimeLineMaterial;
        private Material runtimeOverlayMaterial;
        private bool lineMeshDirty = true;
        private bool overlayMeshDirty = true;

        public bool GridLinesVisible => showGridLines;
        public bool OccupiedCellsVisible => drawRuntimeCellStates && drawOccupiedCells;

        private void Reset()
        {
            gridMap = GetComponent<GridMapBehaviour>();
            pointerProbe = GetComponent<GridPointerProbe>();
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

            if (refreshEveryFrame)
            {
                MarkAllDirty();
            }

            if (lineMeshDirty)
            {
                RebuildLineMesh();
            }

            if (overlayMeshDirty)
            {
                RebuildOverlayMesh();
            }

            ApplyRendererState();
        }

        private void OnDisable()
        {
            if (lineRenderer != null)
            {
                lineRenderer.enabled = false;
            }

            if (overlayRenderer != null)
            {
                overlayRenderer.enabled = false;
            }
        }

        private void OnDestroy()
        {
            DestroyOwnedObject(lineObject);
            DestroyOwnedObject(overlayObject);
            DestroyOwnedObject(lineMesh);
            DestroyOwnedObject(overlayMesh);
            DestroyOwnedObject(runtimeLineMaterial);
            DestroyOwnedObject(runtimeOverlayMaterial);
        }

        private void OnValidate()
        {
            overlayThickness = Mathf.Max(0f, overlayThickness);
            gridLineWidth = Mathf.Max(0.001f, gridLineWidth);
            MarkAllDirty();
            ConfigureRuntimeMaterials();
        }

        public void SetGridLinesVisible(bool visible)
        {
            showGridLines = visible;
            ApplyRendererState();
        }

        public void SetOccupiedCellsVisible(bool visible)
        {
            drawRuntimeCellStates = true;
            drawOccupiedCells = visible;
            MarkOverlayDirty();
        }

        public void SetCellColor(GridPosition position, Color color)
        {
            cellColorOverrides[position] = color;
            MarkOverlayDirty();
        }

        public void SetCellsColor(IEnumerable<GridPosition> positions, Color color)
        {
            if (positions == null)
            {
                return;
            }

            foreach (var position in positions)
            {
                cellColorOverrides[position] = color;
            }

            MarkOverlayDirty();
        }

        public void SetFootprintColor(GridFootprint footprint, Color color)
        {
            foreach (var position in footprint.Positions())
            {
                cellColorOverrides[position] = color;
            }

            MarkOverlayDirty();
        }

        public bool ClearCellColor(GridPosition position)
        {
            var removed = cellColorOverrides.Remove(position);
            if (removed)
            {
                MarkOverlayDirty();
            }

            return removed;
        }

        public void ClearFootprintColor(GridFootprint footprint)
        {
            var removed = false;
            foreach (var position in footprint.Positions())
            {
                removed |= cellColorOverrides.Remove(position);
            }

            if (removed)
            {
                MarkOverlayDirty();
            }
        }

        public void ClearAllCellColors()
        {
            if (cellColorOverrides.Count == 0)
            {
                return;
            }

            cellColorOverrides.Clear();
            MarkOverlayDirty();
        }

        public void RefreshOccupiedCells()
        {
            MarkOverlayDirty();
        }

        public void RefreshAll()
        {
            MarkAllDirty();

            if (!isActiveAndEnabled)
            {
                return;
            }

            EnsureRenderObjects();
            RebuildLineMesh();
            RebuildOverlayMesh();
            ApplyRendererState();
        }

        private void ResolveReferences()
        {
            if (gridMap == null)
            {
                gridMap = GetComponent<GridMapBehaviour>();
            }

            if (pointerProbe == null)
            {
                pointerProbe = GetComponent<GridPointerProbe>();
            }
        }

        private void EnsureRenderObjects()
        {
            EnsureMesh(ref lineMesh, "Grid Renderer Lines Mesh");
            EnsureMesh(ref overlayMesh, "Grid Renderer Overlay Mesh");

            EnsureRenderObject(
                ref lineObject,
                ref lineMeshFilter,
                ref lineRenderer,
                LineObjectName,
                lineMesh,
                GetLineMaterial(),
                lineSortingOrder);

            EnsureRenderObject(
                ref overlayObject,
                ref overlayMeshFilter,
                ref overlayRenderer,
                OverlayObjectName,
                overlayMesh,
                GetOverlayMaterial(),
                overlaySortingOrder);

            ConfigureRuntimeMaterials();
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

        private Material GetOverlayMaterial()
        {
            if (overlayMaterial != null)
            {
                return overlayMaterial;
            }

            if (runtimeOverlayMaterial == null)
            {
                runtimeOverlayMaterial = CreateDefaultMaterial("Grid Renderer Overlay Material");
            }

            return runtimeOverlayMaterial;
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
                Debug.LogWarning("GridRenderer could not find a shader for grid rendering.");
                return null;
            }

            return new Material(shader)
            {
                name = materialName,
                hideFlags = HideFlags.HideAndDontSave,
                renderQueue = (int)RenderQueue.Transparent
            };
        }

        private void ConfigureRuntimeMaterials()
        {
            ConfigureRuntimeMaterial(runtimeLineMaterial);
            ConfigureRuntimeMaterial(runtimeOverlayMaterial);
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

            var offset = GetPlaneOffset(layout, 1.5f);
            var planeNormal = GetPlaneNormal(layout);
            for (var x = 0; x <= gridMap.Width; x++)
            {
                AddLineQuad(
                    lineMeshFilter.transform,
                    layout.GridToWorldPoint(x, 0) + offset,
                    layout.GridToWorldPoint(x, gridMap.Height) + offset,
                    lineColor,
                    planeNormal);
            }

            for (var y = 0; y <= gridMap.Height; y++)
            {
                AddLineQuad(
                    lineMeshFilter.transform,
                    layout.GridToWorldPoint(0, y) + offset,
                    layout.GridToWorldPoint(gridMap.Width, y) + offset,
                    lineColor,
                    planeNormal);
            }

            lineMesh.Clear();
            lineMesh.SetVertices(vertices);
            lineMesh.SetColors(vertexColors);
            lineMesh.SetTriangles(indices, 0);
            lineMesh.RecalculateBounds();
        }

        private void RebuildOverlayMesh()
        {
            overlayMeshDirty = false;

            if (overlayMesh == null)
            {
                return;
            }

            if (!TryGetLayout(out var layout))
            {
                overlayMesh.Clear();
                return;
            }

            CollectVisibleCellColors();

            vertices.Clear();
            indices.Clear();
            vertexColors.Clear();

            var offset = GetPlaneOffset(layout, 1f);
            foreach (var pair in visibleCellColors)
            {
                AddCellQuad(overlayMeshFilter.transform, layout, pair.Key, pair.Value, offset);
            }

            overlayMesh.Clear();
            overlayMesh.SetVertices(vertices);
            overlayMesh.SetColors(vertexColors);
            overlayMesh.SetTriangles(indices, 0);
            overlayMesh.RecalculateBounds();
        }

        private void CollectVisibleCellColors()
        {
            visibleCellColors.Clear();

            if (drawRuntimeCellStates && gridMap != null && gridMap.Map != null)
            {
                foreach (var cell in gridMap.Map.Cells)
                {
                    if (!cell.IsBuildable)
                    {
                        visibleCellColors[cell.Position] = blockedCellColor;
                        continue;
                    }

                    if (drawOccupiedCells && cell.IsOccupied)
                    {
                        visibleCellColors[cell.Position] = occupiedCellColor;
                    }
                }
            }

            foreach (var pair in cellColorOverrides)
            {
                if (ContainsGridPosition(pair.Key))
                {
                    visibleCellColors[pair.Key] = pair.Value;
                }
            }

            if (drawPointerCell && pointerProbe != null && pointerProbe.HasCurrentCell && ContainsGridPosition(pointerProbe.CurrentCell))
            {
                visibleCellColors[pointerProbe.CurrentCell] = currentCellColor;
            }
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

        private void AddCellQuad(Transform targetTransform, GridLayoutService layout, GridPosition position, Color color, Vector3 offset)
        {
            var corners = layout.GetCellCorners(position);
            var startIndex = vertices.Count;

            for (var i = 0; i < corners.Length; i++)
            {
                vertices.Add(targetTransform.InverseTransformPoint(corners[i] + offset));
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

        private bool ContainsGridPosition(GridPosition position)
        {
            return gridMap != null
                   && position.X >= 0
                   && position.X < gridMap.Width
                   && position.Y >= 0
                   && position.Y < gridMap.Height;
        }

        private Vector3 GetPlaneOffset(GridLayoutService layout, float scale)
        {
            return GetPlaneNormal(layout) * overlayThickness * scale;
        }

        private static Vector3 GetPlaneNormal(GridLayoutService layout)
        {
            return layout.PlaneMode == GridPlaneMode.XZ || layout.PlaneMode == GridPlaneMode.IsometricDiamondXZ
                ? Vector3.up
                : Vector3.forward;
        }

        private void MarkAllDirty()
        {
            lineMeshDirty = true;
            overlayMeshDirty = true;
        }

        private void MarkOverlayDirty()
        {
            overlayMeshDirty = true;
        }

        private void ApplyRendererState()
        {
            if (lineRenderer != null)
            {
                lineRenderer.enabled = isActiveAndEnabled && showGridLines && lineMesh != null && lineMesh.vertexCount > 0;
                lineRenderer.sortingOrder = lineSortingOrder;
            }

            if (overlayRenderer != null)
            {
                overlayRenderer.enabled = isActiveAndEnabled && overlayMesh != null && overlayMesh.vertexCount > 0;
                overlayRenderer.sortingOrder = overlaySortingOrder;
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
