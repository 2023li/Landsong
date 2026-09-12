using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct GameLoopSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<Session>();
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager; var root = Sim.Root(em); if (root == Entity.Null) return;
            if (em.GetComponentData<Session>(root).Initialized == 0) Initialize(em, root);
            using var commands = em.GetBuffer<Command>(root).ToNativeArray(Allocator.Temp); em.GetBuffer<Command>(root).Clear();
            foreach (var command in commands)
            {
                var result = Execute(em, root, command);
                em.GetBuffer<GameEvent>(root).Add(new GameEvent { Kind = EventKind.CommandResult, RequestId = command.RequestId, Result = result, Target = command.Target, Definition = command.Definition, Amount=(int)command.Kind, Position=command.Position });
            }
            NightOps.Tick(em, root, SystemAPI.Time.DeltaTime);
            HistoryOps.Trim(em, root);
        }
        public static void Initialize(EntityManager em, Entity root)
        {
            var startingRewards = new System.Collections.Generic.List<RewardGrant>();
            foreach (var entry in em.GetBuffer<StartingGrant>(root))
            {
                var reward = new RewardGrant(entry.Rule.Kind, entry.Rule.Target, entry.Rule.Amount);
                RewardOps.Validate(em, root, reward); startingRewards.Add(reward);
            }
            HistoryOps.Ensure(em, root);
            using (var initial = em.GetBuffer<InitialBuilding>(root).ToNativeArray(Allocator.Temp)) foreach (var b in initial)
            {
                if (!GridOps.CanPlace(em, root, b.Definition, b.Cell, b.Rotation)) throw new System.InvalidOperationException("Invalid initial building footprint: " + Sim.Definition(em, root, b.Definition).Id);
                var e = BuildingOps.Create(em, root, b.Definition, b.Cell, b.Rotation, b.Level, true);
                if (!b.Name.IsEmpty) { var id = em.GetComponentData<Identity>(e); id.Name = b.Name; em.SetComponentData(e, id); }
            }
            if (!RewardOps.ApplyBatch(em, root, startingRewards, false))
                throw new System.InvalidOperationException("Starting resources exceed authored storage capacity.");
            using (var royal = em.GetBuffer<InitialRoyal>(root).ToNativeArray(Allocator.Temp)) foreach (var r in royal)
            { var person = DynastyOps.CreateRoyal(em, root, r.Name, r.Role, r.Age);if(r.Gender!=PersonGender.Unspecified){var p=em.GetComponentData<Royal>(person);p.Gender=r.Gender;em.SetComponentData(person,p);} foreach (var trait in r.Traits) em.GetBuffer<TraitEntry>(person).Add(new TraitEntry { Definition = trait }); }
            CourtOps.Initialize(em, root); SocialOps.EnsureContacts(em, root);
            MilitaryOps.InitializeGarrisons(em, root);
            PortraitOps.EnsurePeople(em,root);PortraitOps.Announce(em,root);
            var s = em.GetComponentData<Session>(root); s.Initialized = 1; em.SetComponentData(root, s); Sim.Set(em, root, new SimulationReady());
            ProgressionOps.DiscoverQuests(em, root); ProgressionOps.EvaluateQuests(em, root); NightOps.Plan(em, root, false); Sim.Emit(em, root, EventKind.DayCheckpoint, "白天节点");
        }
        public static ResultCode Execute(EntityManager em, Entity root, Command c)
        {
            ResultCode result;
            using (HistoryOps.ForCommand(em, root, c)) result = ExecuteCore(em, root, c);
            if (result == ResultCode.Success && c.Kind != CommandKind.ForecastEconomy && c.Kind != CommandKind.Save && c.Kind != CommandKind.Load && c.Kind != CommandKind.Pause && c.Kind != CommandKind.IntelligenceMode && c.Kind != CommandKind.ReadIntelligence && em.GetComponentData<Session>(root).IntelligenceMode == 0 && em.GetComponentData<Session>(root).Phase == Phase.Day) ProgressionOps.EvaluateQuests(em, root);
            return result;
        }
        static ResultCode ExecuteCore(EntityManager em, Entity root, Command c)
        {
            var s = em.GetComponentData<Session>(root); var target = Sim.Find(em, c.Target);
            if (s.Phase == Phase.Ended) return ResultCode.WrongPhase;
            if (s.CheckpointPending != 0 && !(c.Kind == CommandKind.Save && s.Phase != Phase.Day)) return ResultCode.Busy;
            if (c.Kind == CommandKind.IntelligenceMode)
            {
                if (c.Argument != 0 && c.Argument != 1 || c.Argument == 1 && s.Phase == Phase.GameOver) return ResultCode.WrongPhase;
                s.IntelligenceMode = (byte)c.Argument; em.SetComponentData(root, s); return ResultCode.Success;
            }
            if (c.Kind == CommandKind.ReadIntelligence) { IntelOps.MarkRead(em, root, c.Other); return ResultCode.Success; }
            if (s.IntelligenceMode != 0 && c.Kind != CommandKind.Pause && c.Kind != CommandKind.Save && c.Kind != CommandKind.Load && c.Kind != CommandKind.CameraMoved && c.Kind != CommandKind.CameraZoomed) return ResultCode.Busy;
            if (c.Kind == CommandKind.FocusHero) return ResultCode.Unavailable; // First-version heroes receive positional commands only.
            if (c.Kind == CommandKind.Pause) { s.Paused = c.Argument == 1 ? (byte)1 : c.Argument == 2 ? (byte)0 : (byte)(s.Paused == 0 ? 1 : 0); em.SetComponentData(root, s); return ResultCode.Success; }
            if (s.Paused != 0 && c.Kind != CommandKind.Save && c.Kind != CommandKind.Load && c.Kind != CommandKind.CameraMoved && c.Kind != CommandKind.CameraZoomed) return ResultCode.Busy;
            if (c.Kind == CommandKind.TrackQuest) return s.Phase == Phase.Celebration ? ResultCode.WrongPhase : QuestOps.Track(em, root, c);
            if (c.Kind == CommandKind.NightSpeed)
            { if (s.Phase != Phase.Night || s.NightKind != NightKind.Peaceful || c.Amount != 1 && c.Amount != 2) return ResultCode.WrongPhase; s.NightSpeed = (byte)c.Amount; em.SetComponentData(root, s); return ResultCode.Success; }
            if (c.Kind == CommandKind.RetryDay || c.Kind == CommandKind.RetryDusk || c.Kind == CommandKind.EndDynasty)
            {
                if (s.Phase != Phase.GameOver) return ResultCode.WrongPhase;
                if (c.Kind != CommandKind.EndDynasty && (CourtOps.State(em, root).Extinction != 0 || em.HasComponent<RecoveryState>(root) && em.GetComponentData<RecoveryState>(root).Extinction != 0)) return ResultCode.Unavailable;
                s.CheckpointPending = 1; em.SetComponentData(root, s);
                Sim.Emit(em, root, c.Kind == CommandKind.EndDynasty ? EventKind.EndDynasty : EventKind.Retry, "", amount: c.Kind == CommandKind.RetryDusk ? 1 : 0); return ResultCode.Success;
            }
            if (c.Kind == CommandKind.Save || c.Kind == CommandKind.Load)
            {
                if (s.Phase != Phase.Day) return ResultCode.WrongPhase;
                s.CheckpointPending = 1; em.SetComponentData(root, s);
                Sim.Emit(em, root, c.Kind == CommandKind.Save ? EventKind.Save : EventKind.Load, c.Text.ToString(), target:c.Other, amount: c.Argument); return ResultCode.Success;
            }
            if (c.Kind == CommandKind.Advance)
            {
                if (s.Phase == Phase.Day)
                {
                    try { return NightOps.Begin(em, root, c.Argument == 1, c.Other); }
                    catch (System.Exception error) { UnityEngine.Debug.LogException(error); return ResultCode.PreparationFailed; }
                }
                if (s.Phase == Phase.Report) { NightOps.Dawn(em, root); return ResultCode.Success; }
                return ResultCode.WrongPhase;
            }
            if (c.Kind == CommandKind.PickUp)
            {
                if (s.Phase != Phase.Night && s.Phase != Phase.Celebration && s.Phase != Phase.Retreat) return ResultCode.WrongPhase;
                return NightOps.PickUp(em, root, target);
            }
            if (c.Kind == CommandKind.CameraMoved || c.Kind == CommandKind.CameraZoomed)
            { if (s.IntelligenceMode == 0) ProgressionOps.TrackInput(em, root, c.Kind == CommandKind.CameraMoved ? RuleKind.RequireCameraMove : RuleKind.RequireCameraZoom); return ResultCode.Success; }
            if(c.Kind==CommandKind.SetSoldierAttention)return MilitaryOps.SoldierCommand(em,root,c);
            if (c.Kind == CommandKind.RecallGarrison) return MilitaryOps.RecallGarrison(em, root, c.Target, c.Argument == 1);
            if (c.Kind == CommandKind.Bell || c.Kind == CommandKind.WakeHero || c.Kind == CommandKind.SelectHero || c.Kind == CommandKind.MoveHero || c.Kind == CommandKind.FocusHero || c.Kind == CommandKind.Recall)
            {
                if (s.Phase != Phase.Night && s.Phase != Phase.Retreat) return ResultCode.WrongPhase;
                if (c.Kind == CommandKind.WakeHero) return s.Phase != Phase.Night ? ResultCode.Unavailable : MilitaryOps.Wake(em, root, target);
                if (c.Kind == CommandKind.Bell) { if (!Sim.Operational(em, target) || em.GetComponentData<BuildingStats>(target).BellRadius <= 0) return ResultCode.InvalidTarget; MilitaryOps.Bell(em, root, target); return ResultCode.Success; }
                if (c.Kind == CommandKind.SelectHero)
                {
                    if (target != Entity.Null && (!em.HasComponent<Hero>(target) || !Sim.Alive(em, target) || em.GetComponentData<Combatant>(target).Deployed == 0)) return ResultCode.InvalidTarget;
                    s.SelectedHero = target; em.SetComponentData(root, s); return ResultCode.Success;
                }
                if (s.SelectedHero == Entity.Null || !Sim.Alive(em, s.SelectedHero)) return ResultCode.InvalidTarget;
                if (c.Kind == CommandKind.FocusHero && (!Sim.Alive(em, target) || !em.HasComponent<Combatant>(target) || em.GetComponentData<Combatant>(target).Faction != 1)) return ResultCode.InvalidTarget;
                var destination = c.Kind == CommandKind.Recall ? em.GetComponentData<Combatant>(s.SelectedHero).Home : c.Position;
                if (!math.all(math.isfinite(destination))) return ResultCode.InvalidTarget;
                var grid = em.GetComponentData<GridData>(root);
                if (!GridOps.Traversable(grid, em.GetBuffer<Occupancy>(root), GridOps.Cell(grid, destination))) return ResultCode.InvalidTarget;
                using (var reach = new NightSpatialOps.Reach(em, root, Sim.Position(em, s.SelectedHero))) if (!reach.Point(destination)) return ResultCode.InvalidTarget;
                if (c.Kind == CommandKind.MoveHero) { var actor = em.GetComponentData<Combatant>(s.SelectedHero); actor.Home = destination; em.SetComponentData(s.SelectedHero, actor); }
                em.SetComponentData(s.SelectedHero, new NavigationState { Revision = -1 }); em.GetBuffer<Waypoint>(s.SelectedHero).Clear(); em.SetComponentData(s.SelectedHero, new Steering());
                em.SetComponentData(s.SelectedHero, new UnitOrder { Kind = c.Kind == CommandKind.FocusHero ? OrderKind.Focus : c.Kind == CommandKind.Recall ? OrderKind.Recall : OrderKind.Move, Target = target, Destination = destination }); return ResultCode.Success;
            }
            if (s.Phase != Phase.Day) return ResultCode.WrongPhase;
            if (!FeatureOps.Allowed(em, root, c.Kind)) return ResultCode.Unavailable;
            // Permission and deterministic command ordering remain above this boundary.
            // A recognized domain owns its result, including rejection; no fallback executes it twice.
            if (InventoryCommandHandler.TryExecute(em, root, c, out var result) ||
                BuildingCommandHandler.TryExecute(em, root, target, c, out result) ||
                MilitaryCommandHandler.TryExecute(em, root, c, out result) ||
                ProgressionCommandHandler.TryExecute(em, root, target, c, out result) ||
                CourtCommandHandler.TryExecute(em, root, target, c, out result)) return result;
            return ResultCode.Unavailable;
        }
    }
}
