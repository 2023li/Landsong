using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.SceneManagement;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    public sealed class WorldPresentationView : MonoBehaviour
    {
        sealed class View {public GameObject Object;public PresentationActor Actor;public GamePresentationCatalog.Model Model;public Vector3 Position;public float DeathRemaining;public bool SawDeath,WasVisible;}
        sealed class Effect { public PresentationEffect Object; public float Remaining; }
        readonly Dictionary<Entity,View> views=new Dictionary<Entity,View>();readonly List<Effect> effects=new List<Effect>();
        readonly Dictionary<Entity,(LifeStage stage,int level,int progress)> buildings=new Dictionary<Entity,(LifeStage,int,int)>();
        readonly HashSet<Entity> seen=new HashSet<Entity>();EntityManager em;World boundWorld;Entity root;Phase phase;bool initialized;
        readonly HashSet<Entity> visitors = new HashSet<Entity>();
        [LabelText("世界表现根模板"), Required] public Transform WorldRootTemplate;
        PresentationRuntime runtime;
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
        public bool IsBound => boundWorld != null && boundWorld.IsCreated && root != Entity.Null && em.Exists(root)
            && runtime != null && ownerScene.IsValid() && ownerScene.isLoaded;
        public void BindSession(EntityManager manager, Entity simulation, PresentationRuntime presentation, Scene scene)
        {
            if (WorldRootTemplate == null || presentation == null || presentation.Catalog == null || !scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("世界表现缺少根模板、音画服务或所属场景配置。");
            if (manager.World == null || !manager.World.IsCreated || simulation == Entity.Null || !manager.Exists(simulation) || !manager.HasComponent<SimulationReady>(simulation))
                throw new InvalidOperationException("世界表现没有有效的游戏会话。");
            if (presentation.Catalog.Models == null || presentation.Catalog.Cues == null) throw new InvalidOperationException("音画目录的模型或提示数组缺失。");
            foreach (var model in presentation.Catalog.Models)
            {
                if (model == null || model.ActorPrefab == null) throw new InvalidOperationException("音画目录的模型模板引用缺失。");
                model.ActorPrefab.ValidateConfiguration();
            }
            foreach (var cue in presentation.Catalog.Cues)
            {
                if (cue == null) throw new InvalidOperationException("音画目录的提示配置缺失。");
                if (cue.EffectPrefab != null) cue.EffectPrefab.ValidateConfiguration();
            }
            UnbindSession();
            em = manager; boundWorld = manager.World; root = simulation; runtime = presentation; ownerScene = scene;
        }
        public void UnbindSession()
        {
            ClearViews(); root = Entity.Null; em = default; boundWorld = null; runtime = null; ownerScene = default;
        }
        public void ClearViews()
        {
            foreach (var pair in views)
            {
                if (pair.Value.Object != null) Destroy(pair.Value.Object);
                if (boundWorld != null && boundWorld.IsCreated && em.Exists(pair.Key) && em.HasComponent<ExternalVisual>(pair.Key)) em.SetComponentData(pair.Key, new ExternalVisual());
            }
            foreach (var effect in effects) if (effect.Object != null) Destroy(effect.Object.gameObject);
            effects.Clear(); views.Clear(); buildings.Clear(); visitors.Clear(); seen.Clear(); initialized = false;
            if (worldRoot != null) Destroy(worldRoot.gameObject);
            worldRoot = null;
        }
        void OnDisable() => UnbindSession();
        void OnDestroy() => UnbindSession();
        public static ActorPose Pose(EntityManager em,Entity entity,Session state,float speed)
        {
            if(em.HasComponent<Dead>(entity)&&em.IsComponentEnabled<Dead>(entity)||em.HasComponent<Combatant>(entity)&&em.HasComponent<Health>(entity)&&em.GetComponentData<Health>(entity).Current<=0)return ActorPose.Dead;
            if(em.HasComponent<Building>(entity)){var b=em.GetComponentData<Building>(entity);return b.Stage==LifeStage.Ruined?ActorPose.Ruined:b.Stage==LifeStage.Repairing?ActorPose.Repairing:b.Stage==LifeStage.Construction?ActorPose.Construction:b.Maintained==0?ActorPose.Stopped:ActorPose.Working;}
            if(em.HasComponent<VisualState>(entity)){var visual=em.GetComponentData<VisualState>(entity);if(visual.Celebrating!=0)return visual.Celebrating==1?ActorPose.Celebration:visual.Celebrating==2?ActorPose.Guard:ActorPose.Rescue;}
            return speed>.02f?ActorPose.Moving:ActorPose.Idle;
        }
        void LateUpdate()
        {
            if (!EcsSceneFlow.GameReady || !IsBound) return;
            var state = em.GetComponentData<Session>(root); var catalog = runtime.Catalog;
            if(initialized&&phase!=state.Phase){if(state.Phase==Phase.Celebration)Emit(PresentationCue.Celebration,Vector3.zero,false);if(state.Phase==Phase.Report)Emit(PresentationCue.Report,Vector3.zero,false);}
            phase=state.Phase;bool paused=state.Paused!=0;float dt=paused?0:Time.deltaTime;seen.Clear();
            using(var entities=Sim.Entities<Identity>(em))foreach(var entity in entities)
            {
                if (em.HasComponent<SimulationOwner>(entity) && em.GetComponentData<SimulationOwner>(entity).Root != root) continue;
                if(!em.HasComponent<LocalTransform>(entity)||!em.HasComponent<Building>(entity)&&!em.HasComponent<Combatant>(entity)&&!em.HasComponent<Opportunity>(entity))continue;
                seen.Add(entity);var id=em.GetComponentData<Identity>(entity);var transform=em.GetComponentData<LocalTransform>(entity);var b=em.HasComponent<Building>(entity)?em.GetComponentData<Building>(entity):default;
                if(em.HasComponent<Opportunity>(entity)&&visitors.Add(entity)&&initialized)Emit(PresentationCue.Visitor,transform.Position,true);
                if(em.HasComponent<Building>(entity))
                {
                    if(buildings.TryGetValue(entity,out var prior)&&!paused){if(prior.stage!=b.Stage&&b.Stage==LifeStage.Operational)Emit(PresentationCue.Complete,transform.Position,true);else if(prior.progress!=b.Progress&&b.Stage==LifeStage.Construction)Emit(PresentationCue.Build,transform.Position,true);}
                    buildings[entity]=(b.Stage,b.Level,b.Progress);
                }
                var model=catalog.Select(Sim.Definition(em,root,id.Definition).Id.ToString(),em.HasComponent<Building>(entity)?b.Stage:LifeStage.Operational,Mathf.Max(1,b.Level),b.Skin.ToString());
                views.TryGetValue(entity,out var view);if(view!=null&&view.Model!=model){Destroy(view.Object);views.Remove(entity);em.SetComponentData(entity,new ExternalVisual());view=null;}
                if (model == null || model.ActorPrefab == null) continue;
                if (view == null) { var actor = Instantiate(model.ActorPrefab, transform.Position, transform.Rotation, PresentationRoot); actor.name = "View · " + id.Id + " · " + id.Name; actor.transform.localScale = model.Scale; view = new View { Object = actor.gameObject, Actor = actor, Model = model, Position = transform.Position }; views.Add(entity, view); Sim.Set(em, entity, new ExternalVisual { Active = 1 }); }
                float speed=dt>0?Vector3.Distance(view.Position,transform.Position)/dt:0;view.Position=transform.Position;view.Object.transform.SetPositionAndRotation((Vector3)transform.Position+model.Offset,transform.Rotation);
                var pose=Pose(em,entity,state,speed);bool visible=!em.HasComponent<VisualState>(entity)||em.GetComponentData<VisualState>(entity).Visible!=0;
                if(pose==ActorPose.Dead){if(!view.SawDeath){view.SawDeath=true;view.DeathRemaining=view.WasVisible?1:0;}view.DeathRemaining-=dt;visible=view.DeathRemaining>0;}else view.SawDeath=false;
                view.WasVisible=visible;view.Object.SetActive(visible);if(visible)view.Actor.Apply(pose,speed,paused,InterfaceSettings.Current.ReducedMotion,dt);
            }
            foreach(var entity in views.Keys.Where(e=>!seen.Contains(e)).ToArray()){Destroy(views[entity].Object);views.Remove(entity);if(em.Exists(entity)&&em.HasComponent<ExternalVisual>(entity))em.SetComponentData(entity,new ExternalVisual());}foreach(var entity in buildings.Keys.Where(e=>!seen.Contains(e)).ToArray())buildings.Remove(entity);
            visitors.RemoveWhere(e=>!seen.Contains(e));
            for(int i=effects.Count-1;i>=0;i--){var effect=effects[i];effect.Remaining-=dt;if (effect.Object != null) effect.Object.SetPaused(paused);if(effect.Remaining<=0||InterfaceSettings.Current.ReducedMotion){if (effect.Object != null) Destroy(effect.Object.gameObject);effects.RemoveAt(i);}}
            initialized=true;
        }
        public void Emit(PresentationCue cue,Vector3 position,bool spatial)
        {
            if (!IsBound) return;
            runtime.Play(cue); var data = runtime.Catalog.Find(cue);
            if (!spatial || data?.EffectPrefab == null || InterfaceSettings.Current.ReducedMotion || effects.Count >= 32) return;
            var effect = Instantiate(data.EffectPrefab, position, Quaternion.identity, PresentationRoot);
            effects.Add(new Effect { Object = effect, Remaining = data.Lifetime });
        }
        public void Consume(EntityManager manager,Entity simulation,GameEvent message)
        {
            if (!IsBound || manager.World != boundWorld || simulation != root) return;
            PresentationCue? cue=message.Kind switch{EventKind.Damage=>PresentationCue.Hit,EventKind.Death=>PresentationCue.Death,EventKind.Ruin=>PresentationCue.Ruin,EventKind.Reward=>PresentationCue.Loot,EventKind.TheftPrevented or EventKind.FairyCaught=>PresentationCue.Capture,EventKind.HeroWakeCost=>PresentationCue.HeroWake,EventKind.CommandResult when message.Result!=ResultCode.Success=>PresentationCue.Denied,_=>null};
            if(message.Kind==EventKind.CommandResult&&message.Result==ResultCode.Success)cue=(CommandKind)message.Amount switch{CommandKind.Build or CommandKind.BuildRoad=>PresentationCue.Build,CommandKind.Repair=>PresentationCue.Repair,CommandKind.Harvest=>PresentationCue.Harvest,CommandKind.Bell=>PresentationCue.Bell,CommandKind.ReadIntelligence=>PresentationCue.Warning,_=>null};
            if(cue==null)return;var entity=Sim.Find(manager,message.Target);bool spatial=entity!=Entity.Null&&manager.HasComponent<LocalTransform>(entity)&&(manager.HasComponent<Building>(entity)||manager.HasComponent<Combatant>(entity));var point=spatial?(Vector3)Sim.Position(manager,entity):(Vector3)message.Position;
            // PickUp records position before destroying the loot entity; no payload is reconstructed from inventory.
            if(message.Kind==EventKind.Reward||message.Kind==EventKind.Damage||message.Kind==EventKind.Death||message.Kind==EventKind.TheftPrevented||message.Kind==EventKind.FairyCaught)spatial=true;
            Emit(cue.Value,point,spatial);if(views.TryGetValue(entity,out var view))view.Actor.Cue(cue.Value);
        }
    }
}
