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
            HistoryOps.Ensure(em, root);
            using (var initial = em.GetBuffer<InitialBuilding>(root).ToNativeArray(Allocator.Temp)) foreach (var b in initial)
            {
                if (!GridOps.CanPlace(em, root, b.Definition, b.Cell, b.Rotation)) throw new System.InvalidOperationException("Invalid initial building footprint: " + Sim.Definition(em, root, b.Definition).Id);
                var e = BuildingOps.Create(em, root, b.Definition, b.Cell, b.Rotation, b.Level, true);
                if (!b.Name.IsEmpty) { var id = em.GetComponentData<Identity>(e); id.Name = b.Name; em.SetComponentData(e, id); }
            }
            using (var grants = em.GetBuffer<StartingGrant>(root).ToNativeArray(Allocator.Temp)) foreach (var entry in grants)
            {
                var r = entry.Rule;
                if (r.Kind == RuleKind.RewardItem)
                { if (InventoryOps.Add(em, root, r.Target, r.Amount) != r.Amount) throw new System.InvalidOperationException("Starting resources exceed authored storage capacity."); }
                else Sim.Grant(em, root, r.Target, math.max(1, r.Amount));
            }
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
            HistoryOps.Ensure(em,root);
            var previous=em.GetComponentData<ManualHistoryContext>(root);var source=Sim.Find(em,c.Target);
            em.SetComponentData(root,new ManualHistoryContext {Active=(byte)(HistoryOps.Manual(c.Kind)?1:0),Source=c.Target,Name=source!=Entity.Null?em.GetComponentData<Identity>(source).Name:Sim.ValidDefinition(em,root,c.Definition)?Sim.Definition(em,root,c.Definition).Name:new FixedString128Bytes("全城"),Reason=HistoryOps.ActionName(c.Kind)});
            ResultCode result;
            try { result = ExecuteCore(em, root, c); }
            finally { em.SetComponentData(root,previous); }
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
            switch (c.Kind)
            {
                case CommandKind.MoveInventory: case CommandKind.SortInventory: case CommandKind.StorePendingSlot: case CommandKind.DiscardSlot: case CommandKind.DiscardPending:
                    return InventoryOps.LayoutCommand(em, root, c);
                case CommandKind.ForecastEconomy:
                    try { return EconomyForecastOps.Create(em, root); }
                    catch (System.Exception error) { UnityEngine.Debug.LogException(error); return ResultCode.PreparationFailed; }
                case CommandKind.Build: return BuildingOps.Build(em, root, c);
                case CommandKind.MoveBuilding: return BuildingOps.Move(em, root, target, GridOps.Cell(em.GetComponentData<GridData>(root), c.Position), c.Argument);
                case CommandKind.BuildRoad: return BuildingRoadOps.Build(em, root, c);
                case CommandKind.ChangeBuildingSkin:
                    if (!Sim.Operational(em, target) || !em.HasBuffer<Authoring.BuildingVisualSlot>(target) || c.Text.Length > 60) return ResultCode.InvalidTarget;
                    var matchingSkin = false;
                    foreach (var slot in em.GetBuffer<Authoring.BuildingVisualSlot>(target)) if (slot.Purpose == Authoring.BuildingVisualPurpose.Operational && slot.Skin.ToString() == c.Text.ToString()) { matchingSkin = true; break; }
                    if (!matchingSkin) return ResultCode.InvalidContent;
                    var appearance = em.GetComponentData<Building>(target); appearance.Skin = new FixedString64Bytes(c.Text.ToString()); em.SetComponentData(target, appearance); return ResultCode.Success;
                case CommandKind.ClearCrop:
                    if (!Sim.Operational(em, target)) return ResultCode.InvalidTarget;
                    var cleared = em.GetComponentData<Building>(target); if (cleared.Crop < 0) return ResultCode.Unavailable;
                    cleared.Crop = -1; cleared.CropProgress = 0; cleared.CropSeed = 0; cleared.CropFullCycle = 0; em.SetComponentData(target, cleared); BuildingOps.Changed(em, root); return ResultCode.Success;
                case CommandKind.Harvest:
                    if (!Sim.Operational(em, target)) return ResultCode.InvalidTarget;
                    var harvestState = em.GetComponentData<Building>(target); var harvestId = em.GetComponentData<Identity>(target).Definition;
                    if (harvestState.Crop >= 0) return EconomyOps.HarvestCrop(em, root, target, false);
                    var harvest = Sim.Rule(em, root, harvestId, RuleKind.Harvest, harvestState.Level);
                    if (harvest.Level < 0 || harvestState.HarvestRemaining <= 0) return ResultCode.Unavailable;
                    using (var original = em.GetBuffer<InventorySlot>(root).ToNativeArray(Allocator.Temp))
                    {
                        var success = harvest.Target >= 0 ? InventoryOps.Add(em, root, harvest.Target, harvest.B) == harvest.B : harvestState.HarvestRemaining > 1 || ProgressionOps.Reward(em, root, harvestId);
                        if (!success) { em.GetBuffer<InventorySlot>(root).CopyFrom(original); return ResultCode.NoCapacity; }
                    }
                    harvestState.HarvestRemaining--; em.SetComponentData(target, harvestState);
                    if (harvestState.HarvestRemaining <= 0) { GridOps.Occupy(em, root, target, true); em.DestroyEntity(target); }
                    return ResultCode.Success;
                case CommandKind.Demolish: return BuildingOps.Demolish(em, root, target);
                case CommandKind.Repair: return BuildingOps.Repair(em, root, target);
                case CommandKind.Upgrade: return BuildingOps.Upgrade(em, root, target);
                case CommandKind.Rename:
                    return BuildingOps.Rename(em, target, c.Text);
                case CommandKind.Workers:
                    if (c.Amount > 0 && c.Text.ToString() == "workforce-quote" && Sim.Operational(em, target) && WorkforceOps.Quote(em, root, target).RecruitCost != c.Argument) return ResultCode.Unavailable;
                    return EconomyOps.ChangeWorkers(em, root, target, c.Amount);
                case CommandKind.WorkforceBudget: return c.Argument==1?WorkforceOps.AdjustBudget(em,root,target,c.Amount):c.Argument==0?WorkforceOps.SetBudget(em,root,target,c.Amount):ResultCode.InvalidContent;
                case CommandKind.WorkforceTarget: return WorkforceOps.SetTarget(em, root, target, c.Amount);
                case CommandKind.AutoHarvest:
                    if (!Sim.Operational(em, target)) return ResultCode.InvalidTarget;
                    var auto = em.GetComponentData<Building>(target); auto.AutoHarvest = (byte)(c.Amount > 0 ? 1 : 0); em.SetComponentData(target, auto); return ResultCode.Success;
                case CommandKind.Subsidy: case CommandKind.Offering:
                    if (!Sim.Operational(em, target)) return ResultCode.InvalidTarget;
                    var b = em.GetComponentData<Building>(target);
                    if (c.Kind == CommandKind.Subsidy) return WorkforceOps.SetTarget(em, root, target, c.Amount > 0 ? c.Argument > 0 ? c.Argument : em.GetComponentData<BuildingStats>(target).JobCapacity : 0);
                    b.Offering = (byte)(c.Amount > 0 ? 1 : 0);
                    em.SetComponentData(target, b); return ResultCode.Success;
                case CommandKind.Plant:
                    if (!Sim.Operational(em, target) || !Sim.ValidDefinition(em, root, c.Definition) || Sim.Definition(em, root, c.Definition).Kind != ContentKind.Crop) return ResultCode.InvalidContent;
                    var field = em.GetComponentData<Building>(target); if (field.Crop >= 0) return ResultCode.Busy;
                    var available = false; var site = Sim.Definition(em, root, em.GetComponentData<Identity>(target).Definition);
                    for (var i = 0; i < site.RuleCount; i++) { var r = Sim.GetRule(em, root, site.RuleStart + i); if (r.Kind == RuleKind.Crop && r.Target == c.Definition) available = true; }
                    if (!available || !InventoryOps.Pay(em, root, c.Definition, RuleKind.PlacementCost, 1)) return ResultCode.Unavailable;
                    field.Crop = c.Definition; field.CropProgress = 0; field.CropFullCycle = 1; field.CropSeed = Sim.NextRandom(em, root); em.SetComponentData(target, field); return ResultCode.Success;
                case CommandKind.RecruitSoldier: return MilitaryOps.Recruit(em, root, c, false);
                case CommandKind.RecruitHero: return MilitaryOps.Recruit(em, root, c, true);
                case CommandKind.AssignSoldier: return MilitaryOps.Assign(em, root, c);
                case CommandKind.UnassignSoldier: c.Other = 0; return MilitaryOps.Assign(em, root, c);
                case CommandKind.SwapSoldiers: return MilitaryOps.Swap(em, c);
                case CommandKind.RenameSoldier: case CommandKind.DismissSoldier: return MilitaryOps.SoldierCommand(em, root, c);
                case CommandKind.FillGarrison: return MilitaryOps.Fill(em, root, c.Target);
                case CommandKind.Research: return ProgressionOps.Research(em, root, c.Definition, false);
                case CommandKind.PlanResearch: return ResearchOps.Plan(em, root, c.Definition, c.Text.ToString());
                case CommandKind.CancelResearch: return ProgressionOps.Research(em, root, c.Definition, true);
                case CommandKind.SelectPolicy: return ProgressionOps.Policy(em, root, c.Definition);
                case CommandKind.AcceptQuest: case CommandKind.RejectQuest: case CommandKind.SubmitQuest: case CommandKind.ClaimQuest: case CommandKind.AbandonQuest: return ProgressionOps.QuestCommand(em, root, c);
                case CommandKind.RecruitQuest: return ProgressionOps.Offer(em, root, target, c.Argument, true);
                case CommandKind.StartExpedition: case CommandKind.ClaimExpedition: case CommandKind.AbandonExpedition: return ProgressionOps.ExpeditionCommand(em, root, c);
                case CommandKind.RecruitTalent: case CommandKind.AssignTalent: case CommandKind.DismissTalent: case CommandKind.RefreshTalents: return DynastyOps.TalentCommand(em, root, c);
                case CommandKind.Abdicate: return DynastyOps.Abdicate(em, root, target);
                case CommandKind.DesignateHeir: return CourtOps.Designate(em, root, target);
                case CommandKind.ExecuteHeir: return CourtOps.Execute(em, root, target, c.Argument == 1);
                case CommandKind.RoyalVisit: return CourtOps.Visit(em, root, target, c.Argument);
                case CommandKind.ResolveMarriage: return RoyalFamilyOps.Resolve(em, root, target, c.Argument, c.Other, c.Definition);
                case CommandKind.PrepareMarriage: return RoyalFamilyOps.Prepare(em,root,target);
                case CommandKind.ArrangeMarriage: return RoyalFamilyOps.Arrange(em,root,target,Sim.Find(em,c.Other));
                case CommandKind.RefusePersonRequest: return PersonRequestOps.Refuse(em,root,target,c.Definition);
                case CommandKind.CustomizePortrait: return PortraitOps.Customize(em,root,target,c.Text,(uint)c.Other);
                case CommandKind.GiftPerson: case CommandKind.CompleteSocialTask: case CommandKind.ProposeMarriage: return SocialOps.Command(em, root, c);
                case CommandKind.CancelPolicy:
                    var policies = em.GetBuffer<PolicyChoice>(root);
                    for (var i = policies.Length - 1; i >= 0; i--) if (policies[i].Definition == c.Definition) policies.RemoveAt(i);
                    BuildingOps.Changed(em, root); return ResultCode.Success;
                case CommandKind.Discard: return InventoryOps.Remove(em, root, c.Definition, math.max(0, c.Amount), c.Target) ? ResultCode.Success : ResultCode.InsufficientResources;
                case CommandKind.StorePending:
                    InventoryOps.StoreAllPending(em, root);
                    return ResultCode.Success;
            }
            return ResultCode.Unavailable;
        }
    }
}
