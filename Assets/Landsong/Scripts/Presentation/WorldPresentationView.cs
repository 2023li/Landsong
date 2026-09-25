using Landsong.ECS.Definitions;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    public sealed class WorldPresentationView : MonoBehaviour
    {
        [Serializable]
        public sealed class WindSwaySettings
        {
            [Range(0, 20)] public float LightDegrees = 2;
            [Range(0, 20)] public float ModerateDegrees = 5;
            [Range(0, 20)] public float StrongDegrees = 9;
            [Range(0, 10)] public float LightSpeed = 1.4f;
            [Range(0, 10)] public float ModerateSpeed = 2.1f;
            [Range(0, 10)] public float StrongSpeed = 2.8f;
        }

        sealed class View
        {
            public GameObject Object;
            public PresentationActor Actor;
            public WorldVisualCatalog.Model Model;
            public Vector3 Position;
            public LocalTransform SourceTransform;
            public bool HasTransform;
            public float DeathRemaining;
            public bool SawDeath, WasVisible;
        }

        sealed class Effect
        {
            public PresentationEffect Object;
            public float Remaining;
        }

        sealed class PersistentParticleView
        {
            public GameObject Object;
            public ParticleSystem[] Particles;
            public bool Paused;
            public float Remaining;
        }

        readonly Dictionary<Entity, View> views = new Dictionary<Entity, View>();
        readonly Dictionary<Entity, PersistentParticleView> fireMarkers = new Dictionary<Entity, PersistentParticleView>();
        readonly Dictionary<Entity, PersistentParticleView> constructionDust = new Dictionary<Entity, PersistentParticleView>();
        readonly Dictionary<Entity, quaternion> treeBaseRotations = new Dictionary<Entity, quaternion>();
        readonly List<Effect> effects = new List<Effect>();
        readonly Dictionary<ulong, (LifeStage stage, int level, int progress, FixedString64Bytes skin, WorldVisualCatalog.Model model)> buildings = new();
        readonly HashSet<ulong> seenBuildingIds = new();
        readonly List<ulong> staleBuildingIds = new();
        readonly HashSet<ulong> pendingCompletionDust = new();
        readonly HashSet<Entity> seen = new HashSet<Entity>();
        EntityManager em;
        World boundWorld;
        Entity root;
        Phase phase;
        bool initialized;
        readonly HashSet<Entity> visitors = new HashSet<Entity>();
        readonly List<Entity> stale = new List<Entity>();
        readonly Dictionary<Entity, ((FixedString128Bytes definition, LifeStage stage, int level, FixedString64Bytes skin) key, WorldVisualCatalog.Model model, bool road)> selections = new();
        WorldVisualCatalog.Model[] modelDefinitions;
        EntityQuery candidates;
        bool hasQuery;
        [LabelText("世界模型目录"), Required]
        public WorldVisualCatalog Visuals;
        [LabelText("空间特效目录"), Required]
        public EffectCatalog Effects;
        [LabelText("世界叠加层网格"), Required]
        public Mesh OverlayMesh;
        [LabelText("世界叠加层材质"), Required]
        public Material OverlayMaterial;
        [LabelText("世界表现根模板"), Required]
        public Transform WorldRootTemplate;
        [LabelText("建筑起火粒子预制体"), Required]
        public GameObject FirePrefab;
        [LabelText("建筑火焰高度偏移"), MinValue(0)]
        public float FireHeightOffset = 1.3f;
        [LabelText("建筑火焰缩放"), MinValue(0.01f)]
        public float FireScale = 1f;
        [LabelText("施工烟尘粒子预制体"), Required]
        public GameObject ConstructionDustPrefab;
        [LabelText("施工烟尘高度偏移"), MinValue(0)]
        public float ConstructionDustHeightOffset = .15f;
        [LabelText("施工烟尘缩放"), MinValue(0.01f)]
        public float ConstructionDustScale = 1f;
        [LabelText("建筑切换烟尘时长（秒）"), MinValue(0.01f)]
        public float ConstructionDustDuration = .5f;
        [LabelText("树木风摆参数")]
        public WindSwaySettings WindSway = new WindSwaySettings();
        MaterialPropertyBlock overlayProperties;
        float windTime;
        MaterialPropertyBlock OverlayProperties => overlayProperties ??= new MaterialPropertyBlock();
        BuildingRangeOverlayView buildingRangeOverlay;
        BuildingRangeOverlayView BuildingRangeOverlay => buildingRangeOverlay ??= new BuildingRangeOverlayView();
        BuildingRangeOverlayView placementRangeOverlay;
        BuildingRangeOverlayView PlacementRangeOverlay => placementRangeOverlay ??= new BuildingRangeOverlayView();
        AudioRuntime runtime;
        Scene ownerScene;
        Transform worldRoot;
        Transform PresentationRoot
        {
            get
            {
                if (worldRoot == null)
                {
                    worldRoot = Instantiate(WorldRootTemplate);
                    worldRoot.name = "ECS World Presentation";
                    SceneManager.MoveGameObjectToScene(worldRoot.gameObject, ownerScene);
                }

                return worldRoot;
            }
        }

        public int ModelCount => views.Count;
        public int EffectCount => effects.Count;
        public bool IsBound => boundWorld != null && boundWorld.IsCreated && root != Entity.Null && em.Exists(root) && runtime != null && ownerScene.IsValid() && ownerScene.isLoaded;

        public void BindSession(EntityManager manager, Entity simulation, AudioRuntime presentation, Scene scene)
        {
            if (WorldRootTemplate == null || presentation == null || Visuals == null || Effects == null || OverlayMesh == null || OverlayMaterial == null || !scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("世界表现缺少根模板、模型/特效/叠加层配置、音频服务或所属场景配置。");
            if (FirePrefab == null || FirePrefab.GetComponentInChildren<ParticleSystem>(true) == null)
                throw new InvalidOperationException("世界表现缺少包含粒子系统的建筑起火预制体。");
            if (ConstructionDustPrefab == null || ConstructionDustPrefab.GetComponentInChildren<ParticleSystem>(true) == null)
                throw new InvalidOperationException("世界表现缺少包含粒子系统的施工烟尘预制体。");
            if (manager.World == null || !manager.World.IsCreated || simulation == Entity.Null || !manager.Exists(simulation) || !manager.HasComponent<SimulationReady>(simulation))
                throw new InvalidOperationException("世界表现没有有效的游戏会话。");
            if (Visuals.Models == null || Effects.Cues == null)
                throw new InvalidOperationException("模型目录或特效目录数组缺失。");
            foreach (var model in Visuals.Models)
            {
                if (model == null || model.ActorPrefab == null)
                    throw new InvalidOperationException("世界模型目录的模板引用缺失。");
                model.ActorPrefab.ValidateConfiguration();
            }

            foreach (var cue in Effects.Cues)
            {
                if (cue == null)
                    throw new InvalidOperationException("特效目录的提示配置缺失。");
                if (cue.EffectPrefab != null)
                    cue.EffectPrefab.ValidateConfiguration();
            }

            UnbindSession();
            em = manager;
            boundWorld = manager.World;
            root = simulation;
            runtime = presentation;
            ownerScene = scene;
            candidates = em.CreateEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<Identity>(), ComponentType.ReadOnly<LocalTransform>() }, Any = new[] { ComponentType.ReadOnly<Building>(), ComponentType.ReadOnly<Combatant>(), ComponentType.ReadOnly<Opportunity>() } });
            hasQuery = true;
        }

        public void UnbindSession()
        {
            ClearViews();
            if (hasQuery && boundWorld != null && boundWorld.IsCreated)
                candidates.Dispose();
            hasQuery = false;
            root = Entity.Null;
            em = default;
            boundWorld = null;
            runtime = null;
            ownerScene = default;
        }

        public void ClearViews()
        {
            buildingRangeOverlay?.Clear();
            placementRangeOverlay?.Clear();
            if (boundWorld != null && boundWorld.IsCreated)
                foreach (var pair in treeBaseRotations)
                    if (em.Exists(pair.Key) && em.HasComponent<LocalTransform>(pair.Key))
                    {
                        var transform = em.GetComponentData<LocalTransform>(pair.Key);
                        transform.Rotation = pair.Value;
                        em.SetComponentData(pair.Key, transform);
                    }
            treeBaseRotations.Clear();
            foreach (var pair in views)
            {
                if (pair.Value.Object != null)
                    Destroy(pair.Value.Object);
                if (boundWorld != null && boundWorld.IsCreated && em.Exists(pair.Key) && em.HasComponent<ExternalVisual>(pair.Key))
                    em.SetComponentData(pair.Key, new ExternalVisual());
            }

            foreach (var effect in effects)
                if (effect.Object != null)
                    Destroy(effect.Object.gameObject);
            foreach (var marker in fireMarkers.Values)
                ReleasePersistentParticle(marker);
            fireMarkers.Clear();
            foreach (var dust in constructionDust.Values)
                ReleasePersistentParticle(dust);
            constructionDust.Clear();
            effects.Clear();
            views.Clear();
            buildings.Clear();
            seenBuildingIds.Clear();
            staleBuildingIds.Clear();
            pendingCompletionDust.Clear();
            visitors.Clear();
            seen.Clear();
            initialized = false;
            stale.Clear();
            selections.Clear();
            modelDefinitions = null;
            windTime = 0;
            if (worldRoot != null)
                Destroy(worldRoot.gameObject);
            worldRoot = null;
        }

        public ulong BuildingRangeSourceId => buildingRangeOverlay?.SourceId ?? 0;

        public int ShowBuildingRange(Entity building, bool highContrast)
        {
            if (!IsBound)
                return 0;
            return BuildingRangeOverlay.Rebuild(em, root, building, PresentationRoot, OverlayMaterial, highContrast);
        }

        public void SetBuildingRangeVisible(bool visible, bool highContrast)
        {
            if (visible && buildingRangeOverlay != null && buildingRangeOverlay.SourceId != 0 && buildingRangeOverlay.HighContrast != highContrast)
            {
                var building = WorldQueries.Find(em, buildingRangeOverlay.SourceId);
                if (building != Entity.Null)
                    ShowBuildingRange(building, highContrast);
            }

            buildingRangeOverlay?.SetVisible(visible);
        }

        public void ClearBuildingRange() => buildingRangeOverlay?.Clear();

        public void ShowPlacementRange(BuildingId definition, BuildingPlacementState placement, bool highContrast)
        {
            if (IsBound)
                PlacementRangeOverlay.RebuildPreview(em, root, definition, placement, PresentationRoot, OverlayMaterial, highContrast);
        }

        public void ClearPlacementRange() => placementRangeOverlay?.Clear();

        public void DrawOverlay(Camera camera, float3 position, Vector3 size, Color color)
        {
            if (camera == null || OverlayMesh == null || OverlayMaterial == null)
                return;
            if (InterfaceSettings.Current.HighContrast)
            {
                color.a = Mathf.Max(.8f, color.a);
                size.x = Mathf.Max(.16f, size.x);
                size.z = Mathf.Max(.16f, size.z);
            }

            OverlayProperties.Clear();
            OverlayProperties.SetColor("_BaseColor", color);
            Graphics.DrawMesh(OverlayMesh, Matrix4x4.TRS((Vector3)position + Vector3.up * .07f, Quaternion.identity, size), OverlayMaterial, 0, camera, 0, OverlayProperties, ShadowCastingMode.Off, false);
        }

        void OnDisable() => UnbindSession();
        void OnDestroy() => UnbindSession();
        public static ActorPose Pose(EntityManager em, Entity entity, float speed)
        {
            if (em.HasComponent<Dead>(entity) && em.IsComponentEnabled<Dead>(entity) || em.HasComponent<Combatant>(entity) && em.HasComponent<Health>(entity) && em.GetComponentData<Health>(entity).Current <= 0)
                return ActorPose.Dead;
            if (em.HasComponent<Building>(entity))
            {
                var b = em.GetComponentData<Building>(entity);
                BuildingMaintenanceState bMaintenance = em.GetComponentData<BuildingMaintenanceState>(entity);
                return b.Stage == LifeStage.Ruined ? ActorPose.Ruined : b.Stage == LifeStage.Repairing ? ActorPose.Repairing : b.Stage == LifeStage.Construction ? ActorPose.Construction : bMaintenance.Maintained == 0 ? ActorPose.Stopped : ActorPose.Working;
            }

            if (em.HasComponent<VisualState>(entity))
            {
                var visual = em.GetComponentData<VisualState>(entity);
                if (visual.Celebrating != 0)
                    return visual.Celebrating == 1 ? ActorPose.Celebration : visual.Celebrating == 2 ? ActorPose.Guard : ActorPose.Rescue;
            }

            return speed > .02f ? ActorPose.Moving : ActorPose.Idle;
        }

        void LateUpdate()
        {
            if (!EcsSceneFlow.GameReady || !IsBound)
                return;
            var state = em.GetComponentData<Session>(root);
            SimulationControl stateControl = em.GetComponentData<SimulationControl>(root);
            var catalog = Visuals;
            bool modelsChanged = !ReferenceEquals(modelDefinitions, catalog.Models);
            modelDefinitions = catalog.Models;
            if (initialized && phase != state.Phase)
            {
                if (state.Phase == Phase.Celebration)
                    Emit(PresentationCue.Celebration, Vector3.zero, false);
                if (state.Phase == Phase.Report)
                    Emit(PresentationCue.Report, Vector3.zero, false);
            }

            phase = state.Phase;
            bool paused = stateControl.Paused != 0;
            float dt = paused ? 0 : Time.deltaTime;
            windTime += dt;
            TickConstructionDust(dt, paused);
            var wind = em.GetComponentData<SeasonWeatherState>(root);
            seen.Clear();
            seenBuildingIds.Clear();
            using (var entities = candidates.ToEntityArray(Allocator.Temp))
                foreach (var entity in entities)
                {
                    if (em.HasComponent<SimulationOwner>(entity) && em.GetComponentData<SimulationOwner>(entity).Root != root)
                        continue;
                    // This entity owns a baked animated view; do not instantiate a second GameObject model.
                    if (em.HasComponent<AnimatedUnitVisual>(entity))
                        continue;
                    bool isBuilding = em.HasComponent<Building>(entity);
                    seen.Add(entity);
                    var id = em.GetComponentData<Identity>(entity);
                    var transform = em.GetComponentData<LocalTransform>(entity);
                    var b = isBuilding ? em.GetComponentData<Building>(entity) : default;
                    BuildingPlacementState bPlacement = isBuilding ? em.GetComponentData<BuildingPlacementState>(entity) : default(BuildingPlacementState);
                    BuildingAppearanceState bAppearance = isBuilding ? em.GetComponentData<BuildingAppearanceState>(entity) : default(BuildingAppearanceState);
                    BuildingConstructionState bConstruction = isBuilding ? em.GetComponentData<BuildingConstructionState>(entity) : default(BuildingConstructionState);
                    if (em.HasComponent<Opportunity>(entity) && visitors.Add(entity) && initialized)
                        Emit(PresentationCue.Visitor, transform.Position, true);
                    if (isBuilding)
                    {
                        seenBuildingIds.Add(id.Id);
                        if (buildings.TryGetValue(id.Id, out var prior) && !paused)
                        {
                            if (prior.stage != b.Stage && b.Stage == LifeStage.Operational)
                                Emit(PresentationCue.Complete, transform.Position, true);
                            else if (prior.progress != bConstruction.Progress && b.Stage == LifeStage.Construction)
                                Emit(PresentationCue.Build, transform.Position, true);
                        }
                    }

                    var key = (VisualIdentity(entity), isBuilding ? b.Stage : LifeStage.Operational, Mathf.Max(1, b.Level), bAppearance.Skin);
                    if (isBuilding && key.Item1.ToString().StartsWith("b树木", StringComparison.Ordinal))
                        SwayTree(entity, id.Id, wind);
                    if (isBuilding)
                        SetPersistentParticle(entity, em.HasComponent<BuildingFireState>(entity) && em.GetComponentData<BuildingFireState>(entity).Burning != 0,
                            transform.Position, paused, FirePrefab, FireHeightOffset, FireScale, fireMarkers, "Building Fire");
                    bool hadSelection = selections.TryGetValue(entity, out var selection);
                    bool visualStateChanged = hadSelection && !selection.key.Equals(key);
                    if (!hadSelection || modelsChanged || visualStateChanged)
                    {
                        selection = (key, catalog.Select(key.Item1.ToString(), key.Item2, key.Item3, bAppearance.Skin.ToString()), isBuilding && BuildingRoadOps.IsRoad(em, root, em.GetComponentData<BuildingDefinitionRef>(entity).Definition));
                        selections[entity] = selection;
                    }

                    var model = selection.model;
                    views.TryGetValue(entity, out var view);
                    if (isBuilding)
                    {
                        bool hadBuilding = buildings.TryGetValue(id.Id, out var prior);
                        bool changed = hadBuilding && (prior.stage != b.Stage || prior.level != b.Level
                            || !prior.skin.Equals(bAppearance.Skin) || prior.model != model);
                        if (changed && prior.stage == LifeStage.Construction && b.Stage == LifeStage.Operational && state.Phase != Phase.Day)
                            pendingCompletionDust.Add(id.Id);
                        else if (changed || state.Phase == Phase.Day && pendingCompletionDust.Contains(id.Id))
                        {
                            var grid = em.GetComponentData<GridData>(root);
                            PlayConstructionDust(entity, transform.Position, bPlacement.Size, grid.CellSize, paused);
                            pendingCompletionDust.Remove(id.Id);
                        }
                        buildings[id.Id] = (b.Stage, b.Level, bConstruction.Progress, bAppearance.Skin, model);
                    }
                    if (view != null && view.Model != model)
                    {
                        Destroy(view.Object);
                        views.Remove(entity);
                        em.SetComponentData(entity, new ExternalVisual());
                        view = null;
                    }

                    if (model == null || model.ActorPrefab == null)
                        continue;
                    if (view == null)
                    {
                        var actor = Instantiate(model.ActorPrefab, transform.Position, transform.Rotation, PresentationRoot);
                        actor.name = "View · " + id.Id + " · " + id.Name;
                        actor.transform.localScale = model.Scale;
                        view = new View
                        {
                            Object = actor.gameObject,
                            Actor = actor,
                            Model = model,
                            Position = transform.Position,
                        };
                        views.Add(entity, view);
                        EntityState.Set(em, entity, new ExternalVisual { Active = 1 });
                    }

                    float speed = dt > 0 ? Vector3.Distance(view.Position, transform.Position) / dt : 0;
                    view.Position = transform.Position;
                    if (!view.HasTransform || !view.SourceTransform.Equals(transform))
                    {
                        view.HasTransform = true;
                        view.SourceTransform = transform;
                        view.Object.transform.SetPositionAndRotation((Vector3)transform.Position + model.Offset, transform.Rotation);
                        view.Object.transform.localScale = model.Scale;
                        if (selection.road)
                        {
                            var grid = em.GetComponentData<GridData>(root);
                            if (SlopeOps.TryGet(grid, bPlacement.Cell, out var slope))
                            {
                                var gradient = SlopeOps.Gradient(grid, slope);
                                var normal = new Vector3(-gradient.x, 1, -gradient.y).normalized;
                                var rotation = (Quaternion)transform.Rotation;
                                view.Object.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal) * rotation;
                                var local = Quaternion.Inverse(rotation) * new Vector3(gradient.x, 0, gradient.y);
                                view.Object.transform.localScale = Vector3.Scale(model.Scale, new Vector3(Mathf.Sqrt(1 + local.x * local.x), 1, Mathf.Sqrt(1 + local.z * local.z)));
                            }
                        }
                    }


                    var pose = Pose(em, entity, speed);
                    bool visible = !em.HasComponent<VisualState>(entity) || em.GetComponentData<VisualState>(entity).Visible != 0;
                    if (pose == ActorPose.Dead)
                    {
                        if (!view.SawDeath)
                        {
                            view.SawDeath = true;
                            view.DeathRemaining = view.WasVisible ? 1 : 0;
                        }

                        view.DeathRemaining -= dt;
                        visible = view.DeathRemaining > 0;
                    }
                    else
                        view.SawDeath = false;
                    view.WasVisible = visible;
                    if (view.Object.activeSelf != visible)
                        view.Object.SetActive(visible);
                    if (visible)
                        view.Actor.Apply(pose, speed, paused, InterfaceSettings.Current.ReducedMotion, dt);
                }

            stale.Clear();
            foreach (var entity in selections.Keys)
                if (!seen.Contains(entity))
                    stale.Add(entity);
            foreach (var entity in stale)
            {
                RemovePersistentParticle(entity, fireMarkers);
                RemovePersistentParticle(entity, constructionDust);
                if (views.TryGetValue(entity, out var removed))
                {
                    Destroy(removed.Object);
                    views.Remove(entity);
                }

                if (em.Exists(entity) && em.HasComponent<ExternalVisual>(entity))
                    em.SetComponentData(entity, new ExternalVisual());
                visitors.Remove(entity);
                selections.Remove(entity);
            }

            staleBuildingIds.Clear();
            foreach (var id in buildings.Keys)
                if (!seenBuildingIds.Contains(id))
                    staleBuildingIds.Add(id);
            foreach (var id in staleBuildingIds)
            {
                buildings.Remove(id);
                pendingCompletionDust.Remove(id);
            }

            for (int i = effects.Count - 1; i >= 0; i--)
            {
                var effect = effects[i];
                effect.Remaining -= dt;
                if (effect.Object != null)
                    effect.Object.SetPaused(paused);
                if (effect.Remaining <= 0 || InterfaceSettings.Current.ReducedMotion)
                {
                    if (effect.Object != null)
                        Destroy(effect.Object.gameObject);
                    effects.RemoveAt(i);
                }
            }

            initialized = true;
        }

        void SwayTree(Entity building, ulong id, SeasonWeatherState wind)
        {
            if (!em.HasComponent<BuildingVisualSelection>(building))
                return;
            var slot = em.GetComponentData<BuildingVisualSelection>(building).Slot;
            if (slot == Entity.Null || !em.Exists(slot) || !em.HasComponent<LocalTransform>(slot))
                return;
            var transform = em.GetComponentData<LocalTransform>(slot);
            if (!treeBaseRotations.TryGetValue(slot, out var original))
            {
                original = transform.Rotation;
                treeBaseRotations.Add(slot, original);
            }
            var settings = WindSway ?? new WindSwaySettings();
            var amplitude = wind.Wind == WindKind.Calm ? 0 : wind.Wind == WindKind.Light ? settings.LightDegrees : wind.Wind == WindKind.Moderate ? settings.ModerateDegrees : settings.StrongDegrees;
            var direction = Quaternion.Euler(0, wind.WindDegrees, 0) * Vector3.right;
            var phase = id % 97 * .31f;
            var speed = wind.Wind == WindKind.Strong ? settings.StrongSpeed : wind.Wind == WindKind.Moderate ? settings.ModerateSpeed : settings.LightSpeed;
            transform.Rotation = (quaternion)(Quaternion.AngleAxis(Mathf.Sin(windTime * speed + phase) * amplitude, direction) * (Quaternion)original);
            em.SetComponentData(slot, transform);
        }

        void SetPersistentParticle(Entity entity, bool enabled, float3 position, bool paused, GameObject prefab,
            float heightOffset, float scale, Dictionary<Entity, PersistentParticleView> activeViews, string label)
        {
            if (!enabled)
            {
                RemovePersistentParticle(entity, activeViews);
                return;
            }
            var worldPosition = (Vector3)position + Vector3.up * heightOffset;
            if (activeViews.TryGetValue(entity, out var active) && active.Object != null)
            {
                active.Object.transform.position = worldPosition;
                if (active.Paused != paused)
                    SetPersistentParticlePaused(active, paused);
                return;
            }
            var marker = Instantiate(prefab, worldPosition, Quaternion.identity, PresentationRoot);
            marker.name = label + " · " + entity.Index;
            marker.transform.localScale = prefab.transform.localScale * scale;
            var view = new PersistentParticleView { Object = marker, Particles = marker.GetComponentsInChildren<ParticleSystem>(true) };
            foreach (var particle in view.Particles)
                particle.Play(true);
            if (paused)
                SetPersistentParticlePaused(view, true);
            activeViews[entity] = view;
        }

        void PlayConstructionDust(Entity entity, float3 position, int2 footprint, float cellSize, bool paused)
        {
            RemovePersistentParticle(entity, constructionDust);
            if (InterfaceSettings.Current.ReducedMotion)
                return;
            var marker = Instantiate(ConstructionDustPrefab, (Vector3)position + Vector3.up * ConstructionDustHeightOffset,
                Quaternion.identity, PresentationRoot);
            marker.name = "Building View Dust · " + entity.Index;
            marker.transform.localScale = ConstructionDustPrefab.transform.localScale * ConstructionDustScale;
            var dust = new PersistentParticleView
            {
                Object = marker,
                Particles = marker.GetComponentsInChildren<ParticleSystem>(true),
                Remaining = Mathf.Max(.01f, ConstructionDustDuration)
            };
            foreach (var particle in dust.Particles)
            {
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ConfigureConstructionDustFootprint(particle, footprint, cellSize);
                particle.Play(true);
                particle.Emit(Mathf.Clamp(footprint.x * footprint.y * 4, 12, 48));
            }
            if (paused)
                SetPersistentParticlePaused(dust, true);
            constructionDust[entity] = dust;
        }

        void TickConstructionDust(float dt, bool paused)
        {
            stale.Clear();
            foreach (var pair in constructionDust)
            {
                var dust = pair.Value;
                dust.Remaining -= dt;
                if (dust.Object == null || dust.Remaining <= 0 || InterfaceSettings.Current.ReducedMotion)
                    stale.Add(pair.Key);
                else if (dust.Paused != paused)
                    SetPersistentParticlePaused(dust, paused);
            }
            foreach (var entity in stale)
                RemovePersistentParticle(entity, constructionDust);
        }

        public static void ConfigureConstructionDustFootprint(ParticleSystem particle, int2 footprint, float cellSize)
        {
            var shape = particle.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.BoxEdge;
            var size = shape.scale;
            var scale = particle.transform.lossyScale;
            size.x = Mathf.Max(.01f, footprint.x * cellSize / Mathf.Max(.01f, Mathf.Abs(scale.x)));
            size.z = Mathf.Max(.01f, footprint.y * cellSize / Mathf.Max(.01f, Mathf.Abs(scale.z)));
            shape.scale = size;
        }

        static void SetPersistentParticlePaused(PersistentParticleView view, bool paused)
        {
            foreach (var particle in view.Particles)
                if (particle != null)
                {
                    if (paused) particle.Pause(true);
                    else particle.Play(true);
                }
            view.Paused = paused;
        }

        void RemovePersistentParticle(Entity entity, Dictionary<Entity, PersistentParticleView> activeViews)
        {
            if (!activeViews.TryGetValue(entity, out var marker))
                return;
            ReleasePersistentParticle(marker);
            activeViews.Remove(entity);
        }

        static void ReleasePersistentParticle(PersistentParticleView marker)
        {
            if (marker?.Object == null)
                return;
            foreach (var particle in marker.Particles)
                if (particle != null)
                    particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            marker.Object.SetActive(false);
            Destroy(marker.Object);
        }

        FixedString128Bytes VisualIdentity(Entity entity)
        {
            if (em.HasComponent<BuildingDefinitionRef>(entity))
                return BuildingDefinitions.Get(em, root, em.GetComponentData<BuildingDefinitionRef>(entity).Definition).Metadata.Id;
            if (em.HasComponent<SoldierDefinitionRef>(entity))
                return SoldierDefinitions.Get(em, root, em.GetComponentData<SoldierDefinitionRef>(entity).Definition).Metadata.Id;
            if (em.HasComponent<HeroDefinitionRef>(entity))
                return HeroDefinitions.Get(em, root, em.GetComponentData<HeroDefinitionRef>(entity).Definition).Metadata.Id;
            if (em.HasComponent<EnemyDefinitionRef>(entity))
                return EnemyDefinitions.Get(em, root, em.GetComponentData<EnemyDefinitionRef>(entity).Definition).Metadata.Id;
            if (em.HasComponent<OpportunityDefinitionRef>(entity))
                return OpportunityDefinitions.Get(em, root, em.GetComponentData<OpportunityDefinitionRef>(entity).Definition).Metadata.Id;
            return default;
        }

        public void Emit(PresentationCue cue, Vector3 position, bool spatial)
        {
            if (!IsBound)
                return;
            runtime.Play(cue);
            var data = Effects.Find(cue);
            if (!spatial || data?.EffectPrefab == null || InterfaceSettings.Current.ReducedMotion || effects.Count >= 32)
                return;
            var effect = Instantiate(data.EffectPrefab, position, Quaternion.identity, PresentationRoot);
            effects.Add(new Effect { Object = effect, Remaining = data.Lifetime });
        }

        public void Consume(EntityManager manager, Entity simulation, GameEvent message)
        {
            if (!IsBound || manager.World != boundWorld || simulation != root)
                return;
            PresentationCue? cue = message.Kind switch
            {
                EventKind.Damage => PresentationCue.Hit,
                EventKind.Death => PresentationCue.Death,
                EventKind.Ruin => PresentationCue.Ruin,
                EventKind.Reward => PresentationCue.Loot,
                EventKind.TheftPrevented or EventKind.FairyCaught => PresentationCue.Capture,
                EventKind.HeroWakeCost => PresentationCue.HeroWake,
                EventKind.CommandResult when message.Result != ResultCode.Success => PresentationCue.Denied,
                _ => null
            };
            if (message.Kind == EventKind.CommandResult && message.Result == ResultCode.Success)
                cue = (CommandKind)message.Amount switch
                {
                    CommandKind.Build or CommandKind.BuildRoad => PresentationCue.Build,
                    CommandKind.Repair => PresentationCue.Repair,
                    CommandKind.Harvest => PresentationCue.Harvest,
                    CommandKind.Bell => PresentationCue.Bell,
                    CommandKind.ReadIntelligence => PresentationCue.Warning,
                    _ => null
                };
            if (cue == null)
                return;
            var entity = WorldQueries.Find(manager, message.Target);
            bool spatial = entity != Entity.Null && manager.HasComponent<LocalTransform>(entity) && (manager.HasComponent<Building>(entity) || manager.HasComponent<Combatant>(entity));
            var point = spatial ? (Vector3)EntityState.Position(manager, entity) : (Vector3)message.Position;
            // PickUp records position before destroying the loot entity; no payload is reconstructed from inventory.
            if (message.Kind == EventKind.Reward || message.Kind == EventKind.Damage || message.Kind == EventKind.Death || message.Kind == EventKind.TheftPrevented || message.Kind == EventKind.FairyCaught)
                spatial = true;
            Emit(cue.Value, point, spatial);
            if (views.TryGetValue(entity, out var view))
                view.Actor.Cue(cue.Value);
        }
    }
}
