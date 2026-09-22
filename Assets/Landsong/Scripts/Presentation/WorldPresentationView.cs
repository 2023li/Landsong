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

        readonly Dictionary<Entity, View> views = new Dictionary<Entity, View>();
        readonly List<Effect> effects = new List<Effect>();
        readonly Dictionary<Entity, (LifeStage stage, int level, int progress)> buildings = new Dictionary<Entity, (LifeStage, int, int)>();
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
        MaterialPropertyBlock overlayProperties;
        MaterialPropertyBlock OverlayProperties => overlayProperties ??= new MaterialPropertyBlock();
        BuildingRangeOverlayView buildingRangeOverlay;
        BuildingRangeOverlayView BuildingRangeOverlay => buildingRangeOverlay ??= new BuildingRangeOverlayView();
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
            effects.Clear();
            views.Clear();
            buildings.Clear();
            visitors.Clear();
            seen.Clear();
            initialized = false;
            stale.Clear();
            selections.Clear();
            modelDefinitions = null;
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
            seen.Clear();
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
                        if (buildings.TryGetValue(entity, out var prior) && !paused)
                        {
                            if (prior.stage != b.Stage && b.Stage == LifeStage.Operational)
                                Emit(PresentationCue.Complete, transform.Position, true);
                            else if (prior.progress != bConstruction.Progress && b.Stage == LifeStage.Construction)
                                Emit(PresentationCue.Build, transform.Position, true);
                        }

                        buildings[entity] = (b.Stage, b.Level, bConstruction.Progress);
                    }

                    var key = (VisualIdentity(entity), isBuilding ? b.Stage : LifeStage.Operational, Mathf.Max(1, b.Level), bAppearance.Skin);
                    if (!selections.TryGetValue(entity, out var selection) || modelsChanged || !selection.key.Equals(key))
                    {
                        selection = (key, catalog.Select(key.Item1.ToString(), key.Item2, key.Item3, bAppearance.Skin.ToString()), isBuilding && BuildingRoadOps.IsRoad(em, root, em.GetComponentData<BuildingDefinitionRef>(entity).Definition));
                        selections[entity] = selection;
                    }

                    var model = selection.model;
                    views.TryGetValue(entity, out var view);
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
                            Position = transform.Position
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
                if (views.TryGetValue(entity, out var removed))
                {
                    Destroy(removed.Object);
                    views.Remove(entity);
                }

                if (em.Exists(entity) && em.HasComponent<ExternalVisual>(entity))
                    em.SetComponentData(entity, new ExternalVisual());
                buildings.Remove(entity);
                visitors.Remove(entity);
                selections.Remove(entity);
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
