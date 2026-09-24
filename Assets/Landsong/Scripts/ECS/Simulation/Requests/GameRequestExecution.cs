using Unity.Entities;

namespace Landsong.ECS
{
    public static class GameRequestExecution
    {
        public static ResultCode Execute<T>(EntityManager em, Entity root, T request)
            where T : unmanaged, IGameRequest
        {
            var payload = em.CreateEntity();
            try
            {
                em.AddComponentData(payload, request);
                em.AddComponentData(payload, new SimulationOwner { Root = root });
                return Execute(em, root, new QueuedGameplayRequest { Kind = request.Kind, Target = request.Target, Payload = payload, SourceName = request is IHistoryNamedRequest named ? named.HistoryName(em, root) : default });
            }
            finally
            {
                if (em.Exists(payload))
                    em.DestroyEntity(payload);
            }
        }

        public static ResultCode Execute(EntityManager em, Entity root, QueuedGameplayRequest request)
        {
            if (!em.Exists(request.Payload) || !em.HasComponent<SimulationOwner>(request.Payload) || em.GetComponentData<SimulationOwner>(request.Payload).Root != root)
                return ResultCode.InvalidTarget;
            ResultCode result;
            using (HistoryOps.ForRequest(em, root, request))
                result = ExecuteCore(em, root, request);
            if (result == ResultCode.Success && RefreshesQuests(request.Kind) && em.GetComponentData<IntelligenceModeState>(root).Enabled == 0 && em.GetComponentData<Session>(root).Phase == Phase.Day)
                QuestLifecycle.EvaluateQuests(em, root);
            return result;
        }

        static bool RefreshesQuests(CommandKind kind) => kind != CommandKind.ForecastEconomy && kind != CommandKind.Save && kind != CommandKind.Load && kind != CommandKind.Pause && kind != CommandKind.IntelligenceMode && kind != CommandKind.ReadIntelligence;
        static bool AllowedWhileObserving(CommandKind kind) => kind == CommandKind.Pause || kind == CommandKind.Save || kind == CommandKind.Load || kind == CommandKind.CameraMoved || kind == CommandKind.CameraZoomed;
        static bool NightMilitary(CommandKind kind) => kind == CommandKind.Bell || kind == CommandKind.WakeHero || kind == CommandKind.SelectHero || kind == CommandKind.MoveHero || kind == CommandKind.FocusHero || kind == CommandKind.Recall;
        static ResultCode ExecuteCore(EntityManager em, Entity root, QueuedGameplayRequest request)
        {
            var phase = em.GetComponentData<Session>(root).Phase;
            var payload = request.Payload;
            var kind = request.Kind;
            if (phase == Phase.Ended)
                return ResultCode.WrongPhase;
            if (em.GetComponentData<PersistenceGate>(root).CheckpointPending != 0 && !(kind == CommandKind.Save && phase != Phase.Day))
                return ResultCode.Busy;
            if (em.HasComponent<SetIntelligenceModeRequest>(payload))
                return SessionRequestActions.Intelligence(em, root, em.GetComponentData<SetIntelligenceModeRequest>(payload).Enabled);
            if (em.HasComponent<ReadIntelligenceRequest>(payload))
            {
                IntelOps.MarkRead(em, root, em.GetComponentData<ReadIntelligenceRequest>(payload).ExpectedFingerprint);
                return ResultCode.Success;
            }

            if (em.GetComponentData<IntelligenceModeState>(root).Enabled != 0 && !AllowedWhileObserving(kind))
                return ResultCode.Busy;
            if (kind == CommandKind.FocusHero)
                return ResultCode.Unavailable;
            if (em.HasComponent<PauseRequest>(payload))
                return SessionRequestActions.Pause(em, root, em.GetComponentData<PauseRequest>(payload).Decision);
            if (em.GetComponentData<SimulationControl>(root).Paused != 0 && !AllowedWhileObserving(kind))
                return ResultCode.Busy;
            if (em.HasComponent<TrackQuestRequest>(payload))
            {
                var tracking = em.GetComponentData<TrackQuestRequest>(payload);
                return phase == Phase.Celebration ? ResultCode.WrongPhase : QuestOps.Track(em, root, tracking.Quest, (int)tracking.Mode);
            }

            if (em.HasComponent<SetNightSpeedRequest>(payload))
                return SessionRequestActions.NightSpeed(em, root, em.GetComponentData<SetNightSpeedRequest>(payload).Speed);
            if (em.HasComponent<RetryDayRequest>(payload))
                return SessionRequestActions.Retry(em, root, false, false);
            if (em.HasComponent<RetryDuskRequest>(payload))
                return SessionRequestActions.Retry(em, root, true, false);
            if (em.HasComponent<EndDynastyRequest>(payload))
                return SessionRequestActions.Retry(em, root, false, true);
            if (em.HasComponent<QuickSaveRequest>(payload))
                return SessionRequestActions.Save(em, root, default, 0, 0);
            if (em.HasComponent<CreateSaveRequest>(payload))
                return SessionRequestActions.Save(em, root, em.GetComponentData<CreateSaveRequest>(payload).Label, 0, 1);
            if (em.HasComponent<OverwriteSaveRequest>(payload))
            {
                var save = em.GetComponentData<OverwriteSaveRequest>(payload);
                return SessionRequestActions.Save(em, root, save.SlotId, save.ExpectedStamp, 2);
            }

            if (em.HasComponent<LoadSaveRequest>(payload))
                return SessionRequestActions.Load(em, root, em.GetComponentData<LoadSaveRequest>(payload));
            if (em.HasComponent<AdvanceRequest>(payload))
                return SessionRequestActions.Advance(em, root, em.GetComponentData<AdvanceRequest>(payload));
            if (em.HasComponent<PickUpRequest>(payload))
            {
                var target = WorldQueries.Find(em, em.GetComponentData<PickUpRequest>(payload).Loot);
                return phase != Phase.Night && phase != Phase.Celebration && phase != Phase.Retreat
                    && !(phase == Phase.Day && target != Entity.Null && em.HasComponent<WorkerCargoDrop>(target))
                    ? ResultCode.WrongPhase : NightOps.PickUp(em, root, target);
            }
            if (em.HasComponent<CameraMovedRequest>(payload) || em.HasComponent<CameraZoomedRequest>(payload))
            {
                if (em.GetComponentData<IntelligenceModeState>(root).Enabled == 0)
                    QuestLifecycle.TrackInput(em, root, em.HasComponent<CameraZoomedRequest>(payload));
                return ResultCode.Success;
            }

            if (kind == CommandKind.SetSoldierAttention || kind == CommandKind.RecallGarrison || NightMilitary(kind))
            {
                if (NightMilitary(kind) && phase != Phase.Night)
                    return ResultCode.WrongPhase;
                return MilitaryRequestHandler.TryExecute(em, root, payload, out var military) ? military : ResultCode.Unavailable;
            }

            if (phase != Phase.Day)
                return ResultCode.WrongPhase;
            if (!FeatureOps.Allowed(em, root, kind))
                return ResultCode.Unavailable;
            if (InventoryRequestDispatch.TryExecute(em, root, payload, out var result) || BuildingRequestDispatch.TryExecute(em, root, payload, out result) || ResearchRequestHandler.TryExecute(em, root, payload, out result) || QuestRequestHandler.TryExecute(em, root, payload, out result) || ExpeditionRequestHandler.TryExecute(em, root, payload, out result) || CourtRequestHandler.TryExecute(em, root, payload, out result) || MilitaryRequestHandler.TryExecute(em, root, payload, out result))
                return result;
            return ResultCode.Unavailable;
        }
    }
}
