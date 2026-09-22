using Landsong.ECS.Definitions;
using System.Collections.Generic;
using Unity.Entities;
using System;
using System.Linq;
using Landsong.Content;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.InputSystem;
using Text = TMPro.TextMeshProUGUI;
using System.Text;
using Unity.Transforms;
using UnityEngine.EventSystems;
using InputField = TMPro.TMP_InputField;
using Landsong.ECS.Persistence;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_WorldInteraction : MonoBehaviour, IGameWorldUi
    {
        internal GameUiInputContext inputContext;
        internal WorldSelectionState worldSelection;
        internal IntelligenceViewState intelligence;
        internal GameUiRefreshScheduler refresh;
        [Serializable]
        public struct PreviewTemplate
        {
            [Sirenix.OdinInspector.LabelText("定义标识")]
            public string DefinitionId;
            [Sirenix.OdinInspector.LabelText("模板")]
            public BuildingPlacementPreviewBinding Template;
        }

        [Sirenix.OdinInspector.LabelText("预览模板集合")]
        public PreviewTemplate[] PreviewTemplates = Array.Empty<PreviewTemplate>();
        internal UI_GamePanel_BuildingActionBar buildingController;
        internal GameUiCommandWriter commandsController;
        internal UI_GamePanel_Hud hudController;
        internal IGameUiNavigation navigation;
        internal GameUiSessionHandle sessionController;
        internal BuildingId buildDefinition;
        internal int buildRotation;
        internal ulong movingBuilding;
        internal int2? roadStart;
        internal GameObject buildingGhost;
        BuildingPlacementPreviewBinding buildingGhostBinding;
        Vector3 buildingGhostBaseScale;
        public bool HasBuildingPlacement => buildDefinition.IsValid || movingBuilding != 0;

        internal void EndBuildingPlacement()
        {
            buildDefinition = default;
            movingBuilding = 0;
            roadStart = null;
            if (buildingGhost != null)
            {
                buildingGhost.SetActive(false);
                if (Application.isPlaying)
                    Destroy(buildingGhost);
                else
                    DestroyImmediate(buildingGhost);
            }
            buildingGhost = null;
            buildingGhostBinding = null;
            refresh.NextPanel = 0;
            if (buildingController.BuildingHint != null)
            {
                buildingController.BuildingHint.text = "";
                buildingController.BuildingPlacementPanel.SetActive(false);
            }
        }

        internal void BeginMoveBuilding()
        {
            var entity = WorldQueries.Find(sessionController.em, worldSelection.SelectedEntityId);
            var quote = BuildingPlacementCommands.CheckMove(sessionController.em, sessionController.root, entity);
            if (!quote.Allowed)
            {
                hudController.Message.text = quote.Reason;
                return;
            }

            EndBuildingPlacement();
            movingBuilding = worldSelection.SelectedEntityId;
            var b = sessionController.em.GetComponentData<Building>(entity);
            BuildingPlacementState bPlacement = sessionController.em.GetComponentData<BuildingPlacementState>(entity);
            BuildingAppearanceState bAppearance = sessionController.em.GetComponentData<BuildingAppearanceState>(entity);
            buildRotation = bPlacement.Rotation;
            CreateBuildingGhost(sessionController.em.GetComponentData<BuildingDefinitionRef>(entity).Definition, b.Level, bAppearance.Skin.ToString());
            buildingController.BeginPlacementHint("移动建筑");
        }

        internal void BeginBuildingPlacement(BuildingId definition)
        {
            var quote = BuildingPlacementCommands.CheckBuild(sessionController.em, sessionController.root, definition);
            if (!quote.Allowed)
            {
                hudController.Message.text = quote.Reason;
                return;
            }

            EndBuildingPlacement();
            buildDefinition = definition;
            buildRotation = 0;
            CreateBuildingGhost(definition, 1, BuildingDefinitions.Get(sessionController.em, sessionController.root, definition).PlacementAndVisuals.DefaultSkin.ToString());
            buildingController.BeginPlacementHint("放置 " + BuildingDefinitions.Get(sessionController.em, sessionController.root, definition).Metadata.Name.ToString());
        }

        internal void CreateBuildingGhost(BuildingId definition, int level, string skin)
        {
            string id = BuildingDefinitions.Get(sessionController.em, sessionController.root, definition).Metadata.Id.ToString();
            BuildingPlacementPreviewBinding template = null;
            foreach (var entry in PreviewTemplates)
                if (entry.DefinitionId == id)
                {
                    template = entry.Template;
                    break;
                }

            if (template == null)
                throw new InvalidOperationException("建筑放置预览目录缺少配置：" + id);
            var binding = Instantiate(template);
            buildingGhostBinding = binding;
            buildingGhost = binding.gameObject;
            buildingGhost.name = "BuildingPlacementPreview";
            buildingGhost.hideFlags = HideFlags.DontSave;
            binding.Configure(level, skin);
            buildingGhostBaseScale = buildingGhost.transform.localScale;
        }

        internal bool BuildingInput(Mouse mouse, Vector3 point) => BuildingPointer(point, mouse.leftButton.wasPressedThisFrame, mouse.leftButton.wasReleasedThisFrame, mouse.rightButton.wasPressedThisFrame);
        internal bool BuildingPointer(Vector3 point, bool leftPressed, bool leftReleased, bool rightPressed)
        {
            if (inputContext.Policy.Capture().HasOwner(GameUiInputOwner.BuildingConfirmation))
                return true;
            if (!HasBuildingPlacement)
                return false;
            if (sessionController.em.GetComponentData<Session>(sessionController.root).Phase != Phase.Day || intelligence.IsOpen)
            {
                EndBuildingPlacement();
                return true;
            }

            var moving = WorldQueries.Find(sessionController.em, movingBuilding);
            if (movingBuilding != 0 && !BuildingStatus.Operational(sessionController.em, moving))
            {
                EndBuildingPlacement();
                return true;
            }

            if (rightPressed)
            {
                EndBuildingPlacement();
                return true;
            }

            var definition = movingBuilding == 0 ? buildDefinition : sessionController.em.GetComponentData<BuildingDefinitionRef>(moving).Definition;
            var grid = sessionController.em.GetComponentData<GridData>(sessionController.root);
            var cell = GridOps.Cell(grid, point);
            ref var d = ref BuildingDefinitions.Get(sessionController.em, sessionController.root, definition);
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame && d.PlacementAndVisuals.CanRotate)
                buildRotation = (buildRotation + 1) % 4;
            var size = (buildRotation & 1) == 0 ? d.Footprint : d.Footprint.yx;
            var position = GridOps.Position(grid, cell, size);
            if (TerrainConnectionOps.TryGet(sessionController.em, sessionController.root, definition, out _))
                position.y = grid.Origin.y + TerrainConnectionOps.AnchorHeight(sessionController.em, sessionController.root, definition, cell, buildRotation);
            var quote = movingBuilding == 0 ? BuildingPlacementCommands.CheckBuild(sessionController.em, sessionController.root, definition, cell, buildRotation) : BuildingPlacementCommands.CheckMove(sessionController.em, sessionController.root, moving, cell, buildRotation);
            RoadPlan road = null;
            if (movingBuilding == 0 && BuildingRoadOps.IsRoad(sessionController.em, sessionController.root, definition) && roadStart.HasValue)
            {
                road = BuildingRoadOps.Plan(sessionController.em, sessionController.root, definition, roadStart.Value, cell);
                quote = road.Quote;
            }

            if (buildingGhost != null)
            {
                buildingGhost.transform.SetPositionAndRotation((Vector3)position + Vector3.up * .04f, Quaternion.Euler(0, buildRotation * 90, 0));
                buildingGhost.transform.localScale = buildingGhostBaseScale;
                if (BuildingRoadOps.IsRoad(sessionController.em, sessionController.root, definition) && SlopeOps.TryGet(grid, cell, out var slope))
                {
                    var gradient = SlopeOps.Gradient(grid, slope);
                    var rotation = buildingGhost.transform.rotation;
                    buildingGhost.transform.rotation = Quaternion.FromToRotation(Vector3.up, new Vector3(-gradient.x, 1, -gradient.y).normalized) * rotation;
                    var local = Quaternion.Inverse(rotation) * new Vector3(gradient.x, 0, gradient.y);
                    buildingGhost.transform.localScale = Vector3.Scale(buildingGhostBaseScale, new Vector3(Mathf.Sqrt(1 + local.x * local.x), 1, Mathf.Sqrt(1 + local.z * local.z)));
                }

                var tint = new MaterialPropertyBlock();
                tint.SetColor("_BaseColor", quote.Allowed ? new Color(.5f, 1, .65f, .65f) : new Color(1, .35f, .3f, .65f));
                foreach (var part in buildingGhostBinding.Parts)
                    part.Renderer.SetPropertyBlock(tint);
            }

            var action = movingBuilding == 0 ? "放置" : "移动";
            if (buildingController.BuildingHint != null)
                buildingController.BuildingHint.text = $"{action} {d.Metadata.Name} · {size.x}×{size.y} · {buildingController.CostText(quote.Costs)}" + (movingBuilding == 0 ? "" : $" · 经验 -{quote.ExperienceLoss}") + "\n" + (quote.Allowed ? "左键确认位置；R 旋转；右键/Esc 取消" : quote.Reason);
            if (movingBuilding == 0 && BuildingRoadOps.IsRoad(sessionController.em, sessionController.root, definition))
            {
                if (!roadStart.HasValue && leftPressed)
                {
                    roadStart = cell;
                    return true;
                }

                if (roadStart.HasValue && road != null)
                {
                    var finishedDrag = leftReleased && math.any(cell != roadStart.Value);
                    if (finishedDrag || leftPressed)
                    {
                        if (!quote.Allowed)
                        {
                            hudController.Message.text = quote.Reason;
                            return true;
                        }

                        var command = new BuildRoadRequest
                        {
                            Definition = definition,
                            Start = GridOps.Position(grid, roadStart.Value, new int2(1)),
                            End = point
                        };
                        buildingController.ShowBuildingConfirmation("铺设道路 · 新建 " + road.NewCells.Count + " 格（已有道路不收费）", new[] { buildingController.CostText(quote.Costs) }, () =>
                        {
                            SubmitBuilding(command);
                            EndBuildingPlacement();
                        });
                    }
                }

                return true;
            }

            if (!leftPressed)
                return true;
            if (!quote.Allowed)
            {
                hudController.Message.text = quote.Reason;
                return true;
            }

            if (movingBuilding == 0)
                SubmitBuilding(new BuildRequest { Definition = definition, Position = point, Rotation = buildRotation });
            else
            {
                var request = new MoveBuildingRequest
                {
                    Building = movingBuilding,
                    Position = point,
                    Rotation = buildRotation
                };
                buildingController.ShowBuildingConfirmation("确认移动 " + sessionController.em.GetComponentData<Identity>(moving).Name, new[] { "材料：" + buildingController.CostText(quote.Costs), "当前经验减少 " + quote.ExperienceLoss + "，等级/库存/岗位/驻军等身份状态保留。" }, () =>
                {
                    SubmitBuilding(request);
                    EndBuildingPlacement();
                });
            }

            return true;
        }

        internal void SubmitBuilding<T>(T command)
            where T : unmanaged, IGameRequest
        {
            commandsController.TryQueue(command);
        }

        internal void DrawBuildingPlacement(Action<float3, Vector3, Color> draw)
        {
            var grid = sessionController.em.GetComponentData<GridData>(sessionController.root);
            if (!HasBuildingPlacement || Mouse.current == null)
                return;
            if (!GroundPoint(Camera.ScreenPointToRay(Mouse.current.position.ReadValue()), out var point))
                return;
            var moving = WorldQueries.Find(sessionController.em, movingBuilding);
            if (movingBuilding != 0 && moving == Entity.Null)
                return;
            var definition = movingBuilding == 0 ? buildDefinition : sessionController.em.GetComponentData<BuildingDefinitionRef>(moving).Definition;
            var cellAt = GridOps.Cell(grid, point);
            ref var definitionData = ref BuildingDefinitions.Get(sessionController.em, sessionController.root, definition);
            var size = (buildRotation & 1) == 0 ? definitionData.Footprint : definitionData.Footprint.yx;
            var quote = movingBuilding == 0 ? BuildingPlacementCommands.CheckBuild(sessionController.em, sessionController.root, definition, cellAt, buildRotation) : BuildingPlacementCommands.CheckMove(sessionController.em, sessionController.root, moving, cellAt, buildRotation);
            if (TerrainConnectionOps.TryGet(sessionController.em, sessionController.root, definition, out var connection))
            {
                float height = TerrainConnectionOps.AnchorHeight(sessionController.em, sessionController.root, definition, cellAt, buildRotation);
                for (int z = 0; z < definitionData.Footprint.y; z++)
                    for (int x = 0; x < definitionData.Footprint.x; x++)
                    {
                        var cell = TerrainConnectionOps.Port(cellAt, definitionData.Footprint, buildRotation, x, z);
                        var p = GridOps.Position(grid, cell, new int2(1));
                        p.y = grid.Origin.y + height + connection.Rise * TerrainConnectionOps.HeightStep(grid) * z / (definitionData.Footprint.y - 1f);
                        var color = !quote.Allowed ? new Color(1, .2f, .1f, .6f) : z == 0 || z == definitionData.Footprint.y - 1 ? Color.cyan : new Color(.2f, 1, .3f, .5f);
                        draw(p, new Vector3(grid.CellSize, .08f, grid.CellSize), color);
                    }

                return;
            }

            if (roadStart.HasValue && movingBuilding == 0)
            {
                var plan = BuildingRoadOps.Plan(sessionController.em, sessionController.root, definition, roadStart.Value, cellAt);
                foreach (var cell in plan.Path)
                    draw(GridOps.Position(grid, cell, new int2(1)), new Vector3(grid.CellSize, .08f, grid.CellSize), plan.Quote.Allowed ? Color.green : Color.red);
            }
            else
                draw(GridOps.Position(grid, cellAt, size), new Vector3(size.x * grid.CellSize, .08f, size.y * grid.CellSize), quote.Allowed ? new Color(.2f, 1, .3f, .5f) : new Color(1, .2f, .1f, .5f));
        }

        [NonSerialized]
        public Camera Camera;
        internal float clickTime;

        internal void RebuildBuildingRange()
        {
            if (!sessionController.IsBound || WorldPresentation == null)
                return;
            var building = WorldQueries.Find(sessionController.em, worldSelection.SelectedEntityId);
            if (building == Entity.Null || !sessionController.em.HasComponent<Building>(building))
            {
                ClearBuildingRange();
                return;
            }

            buildingController.ResourcePathCellCount = WorldPresentation.ShowBuildingRange(building, InterfaceSettings.Current.HighContrast);
        }

        internal void ClearBuildingRange()
        {
            buildingController.ResourcePathCellCount = 0;
            WorldPresentation?.ClearBuildingRange();
        }
        internal void Input()
        {
            var input = inputContext.Policy.Capture();
            if (!input.CanWorldInput)
            {
                cameraDragging = false;
                touchBlocked = true;
                cameraVelocity = Vector3.zero;
                return;
            }

            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (keyboard != null && input.CanWorldShortcuts)
            {
                if (keyboard[InterfaceSettings.Current.Pause].wasPressedThisFrame)
                    commandsController.TryQueue(new PauseRequest());
                hudController.HeroHotkeys(keyboard);
            }

            if (CameraInput(mouse, keyboard))
                return;
            var ray = Camera.ScreenPointToRay(mouse.position.ReadValue());
            if (!GroundPoint(ray, out var point))
                return;
            if (!input.CanWorldActions)
                return;
            if (BuildingInput(mouse, point))
                return;
            if (mouse.rightButton.wasPressedThisFrame)
            {
                commandsController.TryQueue(new MoveHeroRequest { Destination = point });
            }

            if (!mouse.leftButton.wasPressedThisFrame)
                return;
            WorldClick(point);
        }

        internal bool GroundPoint(Ray ray, out Vector3 point)
        {
            var found = NavigationOps.RaycastSurface(sessionController.em, sessionController.root, ray.origin, ray.direction, out var hit);
            point = hit;
            return found;
        }

        internal Entity Hit(Vector3 point, bool enemiesOnly)
        {
            var nearest = Entity.Null;
            var score = float.MaxValue;
            using var all = WorldQueries.Entities<Identity>(sessionController.em);
            foreach (var e in all)
            {
                if (!sessionController.em.HasComponent<LocalTransform>(e))
                    continue;
                // Restored people have a transform for snapshot symmetry, not a world click target.
                if (!sessionController.em.HasComponent<Building>(e) && !sessionController.em.HasComponent<Combatant>(e) && !sessionController.em.HasComponent<Loot>(e) && !sessionController.em.HasComponent<Opportunity>(e))
                    continue;
                if (enemiesOnly && (!sessionController.em.HasComponent<Combatant>(e) || sessionController.em.GetComponentData<Combatant>(e).Faction != 1 || !EntityState.Alive(sessionController.em, e)))
                    continue;
                if (sessionController.em.HasComponent<VisualState>(e) && sessionController.em.GetComponentData<VisualState>(e).Visible == 0)
                    continue;
                var position = EntityState.Position(sessionController.em, e);
                var delta = math.abs(position.xz - new float2(point.x, point.z));
                var radius = new float2(.8f);
                if (sessionController.em.HasComponent<Loot>(e) || sessionController.em.HasComponent<Opportunity>(e))
                    radius = new float2(1.5f);
                if (sessionController.em.HasComponent<Building>(e))
                    radius = (float2)sessionController.em.GetComponentData<BuildingPlacementState>(e).Size * sessionController.em.GetComponentData<GridData>(sessionController.root).CellSize * .5f;
                if (math.any(delta > radius))
                    continue;
                var value = math.lengthsq(delta) + (sessionController.em.HasComponent<Building>(e) ? 10 : 0);
                if (sessionController.em.HasComponent<Loot>(e) || sessionController.em.HasComponent<Opportunity>(e))
                    value -= 50;
                if (value < score)
                {
                    nearest = e;
                    score = value;
                }
            }

            return nearest;
        }

        [Sirenix.OdinInspector.LabelText("世界表现")]
        public WorldPresentationView WorldPresentation;
        internal CheckpointSystem previewCheckpoint;
        internal WorldPresentationView worldPresentation;
        internal void CaptureSlotPreview(RunArchiveStore store, string run, string slot)
        {
            if (Camera == null)
                return;
            var texture = RenderTexture.GetTemporary(320, 180, 16, RenderTextureFormat.ARGB32);
            var previous = Camera.targetTexture;
            var active = RenderTexture.active;
            Texture2D image = null;
            try
            {
                Camera.targetTexture = texture;
                Camera.Render();
                RenderTexture.active = texture;
                image = new Texture2D(320, 180, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 320, 180), 0, 0);
                image.Apply();
                store.AtomicWrite(store.PreviewPath(run, slot), image.EncodeToPNG());
            }
            finally
            {
                Camera.targetTexture = previous;
                RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(texture);
                if (image != null)
                    Destroy(image);
            }
        }

        internal void LifecycleOnDestroy()
        {
            if (previewCheckpoint != null)
                previewCheckpoint.SlotSaved -= CaptureSlotPreview;
            previewCheckpoint = null;
        }

        internal void ResetSession()
        {
            LifecycleOnDestroy();
            EndBuildingPlacement();
            buildRotation = 0;
            buildingGhostBaseScale = default;
            ClearBuildingRange();
            cameraDragging = pointerClaimed = touchBlocked = touchMoved = false;
            dragLast = touchStart = default;
            touchStarted = pinchDistance = pinchAngle = clickTime = 0;
            touchId = -1;
            cameraVelocity = Vector3.zero;
            pointerHits.Clear();
            worldPresentation = null;
        }

        public void LocateHistory(ulong source, Vector3 oldPosition)
        {
            if (!inputContext.Policy.Capture().CanNavigate)
                return;
            var entity = WorldQueries.Find(sessionController.em, source);
            var point = entity != Entity.Null && sessionController.em.HasComponent<Unity.Transforms.LocalTransform>(entity) ? (Vector3)EntityState.Position(sessionController.em, entity) : oldPosition;
            var direction = Camera.transform.forward;
            float distance = Mathf.Abs(direction.y) > .001f ? (point.y - Camera.transform.position.y) / direction.y : 0;
            Camera.transform.position = ClampCameraPosition(Camera, sessionController.em.GetComponentData<GridData>(sessionController.root), point - direction * Mathf.Max(0, distance));
            if (entity == Entity.Null)
                hudController.Message.text = "来源已不存在，已定位当时的位置。";
        }

        internal bool cameraDragging;
        internal bool pointerClaimed;
        internal bool touchBlocked;
        internal bool touchMoved;
        internal Vector2 dragLast;
        internal Vector2 touchStart;
        internal float touchStarted;
        internal int touchId = -1;
        internal float pinchDistance;
        internal float pinchAngle;
        internal Vector3 cameraVelocity;
        internal readonly List<RaycastResult> pointerHits = new List<RaycastResult>();
        internal bool PointerOnUi(Vector2 position)
        {
            if (inputContext.Events == null)
                return false;
            pointerHits.Clear();
            inputContext.Events.RaycastAll(new PointerEventData(inputContext.Events) { position = position }, pointerHits);
            return pointerHits.Count > 0;
        }

        public static Vector3 ClampCameraPosition(Camera camera, GridData grid, Vector3 proposed)
        {
            // Clamp the camera's ground focus, not its elevated Transform, to the authored map rectangle.
            var direction = camera.transform.forward;
            float t = Mathf.Abs(direction.y) > .001f ? (grid.Origin.y - proposed.y) / direction.y : 0;
            var focus = proposed + direction * Mathf.Max(0, t);
            var min = grid.Origin.xz + (Unity.Mathematics.float2)grid.Value.Value.Min * grid.CellSize;
            var max = min + (Unity.Mathematics.float2)grid.Value.Value.Size * grid.CellSize;
            var clamped = new Vector3(Mathf.Clamp(focus.x, min.x, max.x), focus.y, Mathf.Clamp(focus.z, min.y, max.y));
            return proposed + clamped - focus;
        }

        internal void PanCamera(Vector3 delta)
        {
            var position = ClampCameraPosition(Camera, sessionController.em.GetComponentData<GridData>(sessionController.root), Camera.transform.position + delta);
            Camera.transform.position = position;
        }

        internal void RotateCamera(float degrees)
        {
            var ray = Camera.ViewportPointToRay(new Vector3(.5f, .5f));
            var plane = new Plane(Vector3.up, sessionController.em.GetComponentData<GridData>(sessionController.root).Origin.y * Vector3.up);
            if (!plane.Raycast(ray, out var distance))
                return;
            Camera.transform.RotateAround(ray.GetPoint(distance), Vector3.up, degrees);
            Camera.transform.position = ClampCameraPosition(Camera, sessionController.em.GetComponentData<GridData>(sessionController.root), Camera.transform.position);
        }

        internal void DragCamera(Vector2 delta)
        {
            var right = Camera.transform.right;
            right.y = 0;
            right.Normalize();
            var forward = Vector3.ProjectOnPlane(Camera.transform.up, Vector3.up).normalized;
            PanCamera((-right * delta.x - forward * delta.y) * (Camera.orthographicSize * 2 / Mathf.Max(1, Screen.height)));
            commandsController.TryQueue(new CameraMovedRequest());
        }

        internal void ZoomCamera(float amount)
        {
            var size = Mathf.Clamp(Camera.orthographicSize - amount * InterfaceSettings.Current.ZoomSpeed, 5, 100);
            if (!Mathf.Approximately(size, Camera.orthographicSize))
            {
                Camera.orthographicSize = size;
                commandsController.TryQueue(new CameraZoomedRequest());
            }
        }

        internal bool CameraInput(Mouse mouse, Keyboard keyboard)
        {
            if (Camera == null)
                return true;
            var preferences = InterfaceSettings.Current;
            if (keyboard != null && !inputContext.TextFocused)
            {
                var x = (keyboard[preferences.Right].isPressed ? 1 : 0) - (keyboard[preferences.Left].isPressed ? 1 : 0);
                var y = (keyboard[preferences.Forward].isPressed ? 1 : 0) - (keyboard[preferences.Back].isPressed ? 1 : 0);
                var right = Vector3.ProjectOnPlane(Camera.transform.right, Vector3.up).normalized;
                var forward = Vector3.ProjectOnPlane(Camera.transform.forward, Vector3.up).normalized;
                var wanted = Vector3.ClampMagnitude(right * x + forward * y, 1) * preferences.CameraSpeed;
                var smooth = preferences.ReducedMotion ? 0 : preferences.Smoothing;
                cameraVelocity = smooth <= 0 ? wanted : Vector3.Lerp(cameraVelocity, wanted, 1 - Mathf.Exp(-Time.unscaledDeltaTime / smooth));
                if (cameraVelocity.sqrMagnitude > .001f)
                {
                    PanCamera(cameraVelocity * Time.unscaledDeltaTime);
                    if (x != 0 || y != 0)
                        commandsController.TryQueue(new CameraMovedRequest());
                }

                var rotation = (keyboard[preferences.RotateRight].isPressed ? 1 : 0) - (keyboard[preferences.RotateLeft].isPressed ? 1 : 0);
                if (rotation != 0)
                {
                    RotateCamera(rotation * 60 * Time.unscaledDeltaTime);
                    commandsController.TryQueue(new CameraMovedRequest());
                }
            }
            else
                cameraVelocity = Vector3.zero;
            if (TouchInput())
                return true;
            if (mouse == null)
                return true;
            var position = mouse.position.ReadValue();
            bool over = PointerOnUi(position);
            if (mouse.middleButton.wasPressedThisFrame)
            {
                cameraDragging = !over && !inputContext.TextFocused;
                dragLast = position;
            }

            if (cameraDragging && mouse.middleButton.isPressed)
            {
                DragCamera(position - dragLast);
                dragLast = position;
                return true;
            }

            if (mouse.middleButton.wasReleasedThisFrame)
            {
                cameraDragging = false;
                return true;
            }

            if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)
                pointerClaimed = over;
            if (over || pointerClaimed)
            {
                if (!mouse.leftButton.isPressed && !mouse.rightButton.isPressed && !mouse.leftButton.wasReleasedThisFrame && !mouse.rightButton.wasReleasedThisFrame)
                    pointerClaimed = false;
                return true;
            }

            if (inputContext.TextFocused)
                return true;
            var scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > .01f)
                ZoomCamera(scroll * .03f);
            return false;
        }

        internal bool TouchInput()
        {
            var screen = Touchscreen.current;
            if (screen == null)
                return false;
            var active = new List<UnityEngine.InputSystem.Controls.TouchControl>();
            foreach (var finger in screen.touches)
                if (finger.press.isPressed)
                    active.Add(finger);
            if (active.Count >= 2)
            {
                var a = active[0].position.ReadValue();
                var b = active[1].position.ReadValue();
                var vector = b - a;
                if (touchId != -2)
                {
                    touchBlocked = (touchId >= 0 && touchBlocked) || PointerOnUi(a) || PointerOnUi(b) || HasBuildingPlacement || inputContext.TextFocused;
                    pinchDistance = vector.magnitude;
                    pinchAngle = Mathf.Atan2(vector.y, vector.x) * Mathf.Rad2Deg;
                }

                if (!touchBlocked)
                {
                    ZoomCamera((vector.magnitude - pinchDistance) * Camera.orthographicSize / Mathf.Max(1, Screen.height));
                    var angle = Mathf.Atan2(vector.y, vector.x) * Mathf.Rad2Deg;
                    RotateCamera(-Mathf.DeltaAngle(pinchAngle, angle));
                    pinchAngle = angle;
                }

                pinchDistance = vector.magnitude;
                touchId = -2;
                touchMoved = true;
                return true;
            }

            if (active.Count == 1)
            {
                var finger = active[0];
                var position = finger.position.ReadValue();
                if (touchId == -2)
                {
                    touchBlocked = true;
                    return true;
                }

                if (touchId < 0)
                {
                    touchId = finger.touchId.ReadValue();
                    touchStart = dragLast = position;
                    touchStarted = Time.unscaledTime;
                    touchMoved = false;
                    touchBlocked = PointerOnUi(position) || inputContext.TextFocused;
                }

                if ((position - touchStart).sqrMagnitude > 144)
                    touchMoved = true;
                if (!touchBlocked && touchMoved && !HasBuildingPlacement)
                    DragCamera(position - dragLast);
                dragLast = position;
                return true;
            }

            if (touchId != -1)
            {
                if (!touchBlocked && !touchMoved && !PointerOnUi(dragLast) && GroundPoint(Camera.ScreenPointToRay(dragLast), out var point) && !intelligence.IsOpen)
                {
                    if (Time.unscaledTime - touchStarted > .55f && !HasBuildingPlacement)
                        commandsController.TryQueue(new MoveHeroRequest { Destination = point });
                    else if (!BuildingPointer(point, true, true, false))
                        WorldClick(point);
                }

                touchId = -1;
                touchBlocked = touchMoved = false;
                return true;
            }

            return false;
        }

        internal void WorldClick(Vector3 point)
        {
            var entity = Hit(point, false);
            if (entity == Entity.Null)
            {
                worldSelection.SelectedEntityId = 0;
                refresh.NextPanel = 0;
                return;
            }

            var identity = sessionController.em.GetComponentData<Identity>(entity);
            if (sessionController.em.HasComponent<Loot>(entity) || sessionController.em.HasComponent<Opportunity>(entity))
            {
                commandsController.TryQueue(new PickUpRequest { Loot = identity.Id });
                return;
            }

            if (sessionController.em.HasComponent<Hero>(entity))
            {
                commandsController.TryQueue(new SelectHeroRequest { Hero = identity.Id });
                worldSelection.SelectedEntityId = identity.Id;
                refresh.NextPanel = 0;
                return;
            }

            if (!sessionController.em.HasComponent<Building>(entity))
                return;
            if (worldSelection.SelectedEntityId == identity.Id && Time.unscaledTime - clickTime < .35f && sessionController.em.GetComponentData<Session>(sessionController.root).Phase == Phase.Day)
                commandsController.TryQueue(new HarvestBuildingRequest { Building = identity.Id });
            buildingController.SelectBuilding(identity.Id);
            clickTime = Time.unscaledTime;
            buildingController.DetailsPanel.Name.SetTextWithoutNotify(identity.Name.ToString());
            refresh.NextPanel = 0;
            if (sessionController.em.GetComponentData<Session>(sessionController.root).Phase == Phase.Night && sessionController.em.GetComponentData<BuildingBellStats>(entity).Radius > 0)
                commandsController.TryQueue(new RingBellRequest { Building = identity.Id });
        }

        internal void LifecycleLateUpdate()
        {
            buildingController.RefreshPlacementHint();
            hudController.RefreshFeedbackVisibility();
            hudController.RefreshInterfaceBarrier();
            if (sessionController.root == Entity.Null || !sessionController.em.Exists(sessionController.root) || WorldPresentation == null)
                return;
            var grid = sessionController.em.GetComponentData<GridData>(sessionController.root);
            void Draw(float3 position, Vector3 size, Color color) => WorldPresentation.DrawOverlay(Camera, position, size, color);

            var selectedEntity = WorldQueries.Find(sessionController.em, worldSelection.SelectedEntityId);
            bool rangeVisible = buildingController.showBuildingRange && !HasBuildingPlacement
                && (navigation.Panel == GamePanelId.Building || buildingController.showBuildingActionBar)
                && selectedEntity != Entity.Null && sessionController.em.HasComponent<Building>(selectedEntity)
                && WorldPresentation.BuildingRangeSourceId == worldSelection.SelectedEntityId && !intelligence.IsOpen;
            WorldPresentation.SetBuildingRangeVisible(rangeVisible, InterfaceSettings.Current.HighContrast);
            if (!intelligence.IsOpen)
                DrawBuildingPlacement(Draw);
            if (!intelligence.IsOpen && sessionController.em.GetComponentData<Session>(sessionController.root).Phase == Phase.Night)
                foreach (var wave in sessionController.em.GetBuffer<NightWave>(sessionController.root))
                    if (wave.Warned != 0 && wave.Spawned == 0 && wave.Region >= 0 && wave.Region < sessionController.em.GetBuffer<SpawnRegion>(sessionController.root).Length)
                    {
                        var region = sessionController.em.GetBuffer<SpawnRegion>(sessionController.root)[wave.Region];
                        Draw(region.Center, new Vector3(region.Size.x, .1f, region.Size.z), new Color(1, .65f, .1f, .4f));
                    }

            if (!intelligence.IsOpen && selectedEntity != Entity.Null && sessionController.em.HasComponent<Building>(selectedEntity))
            {
                BuildingPlacementState bPlacement = sessionController.em.GetComponentData<BuildingPlacementState>(selectedEntity);
                Draw(EntityState.Position(sessionController.em, selectedEntity), new Vector3(bPlacement.Size.x * grid.CellSize, .06f, bPlacement.Size.y * grid.CellSize), new Color(.2f, .7f, 1, .4f));
            }

            using (var projectiles = WorldQueries.Entities<Projectile>(sessionController.em))
                foreach (var e in projectiles)
                {
                    var p = sessionController.em.GetComponentData<Projectile>(e);
                    if (p.Mode != ProjectileMode.Ground)
                        continue;
                    float radius = math.max(.2f, p.Radius);
                    var center = p.Landing;
                    center.y = GridOps.Position(grid, GridOps.Cell(grid, center), new int2(1)).y + .08f;
                    for (int i = 0; i < 24; i++)
                    {
                        float angle = i * math.PI / 12;
                        Draw(center + new float3(math.cos(angle), 0, math.sin(angle)) * radius, new Vector3(.15f, .06f, .15f), new Color(1, .2f, .05f, .8f));
                    }
                }

            hudController.DrawIntelligence(Draw);
            hudController.DrawNightResults(Draw);
        }

        public void BindSession()
        {
            LifecycleOnDestroy();
            previewCheckpoint = sessionController.em.World.GetExistingSystemManaged<CheckpointSystem>();
            if (previewCheckpoint != null)
                previewCheckpoint.SlotSaved += CaptureSlotPreview;
            worldPresentation = WorldPresentation;
            cameraDragging = pointerClaimed = touchBlocked = touchMoved = false;
            cameraVelocity = Vector3.zero;
            touchId = -1;
            clickTime = 0;
        }

        Camera IGameWorldUi.Camera => Camera;
    }
}
