namespace Landsong.ECS.Presentation
{
    /// <summary>Session-owned presentation revisions. No ECS state or prefab references are cached here.</summary>
    public sealed class GameUiRefreshScheduler
    {
        const float PanelInterval = .25f;
        const float HudInterval = .2f;
        ulong revision = 1, renderedRevision;
        float nextPanel, nextHud;
        bool observed, continuous, held;
        int turn, preferencesRevision, interactionRevision;
        Phase phase;
        byte paused, intelligence, checkpoint, speed;
        public float NextPanel
        {
            get => held ? float.PositiveInfinity : nextPanel;
            set
            {
                held = float.IsPositiveInfinity(value);
                nextPanel = value;
                if (value <= 0)
                    Invalidate();
            }
        }

        public void Observe(Session state, GameClock stateClock, SimulationControl stateControl, NightRuntimeState stateNight, IntelligenceModeState stateIntelligenceMode, PersistenceGate statePersistence, int settingsRevision, bool hasPresentationEvents, int currentInteractionRevision = 0)
        {
            bool changed = !observed || turn != stateClock.Turn || phase != state.Phase || paused != stateControl.Paused || intelligence != stateIntelligenceMode.Enabled || checkpoint != statePersistence.CheckpointPending || speed != stateNight.Speed || preferencesRevision != settingsRevision || interactionRevision != currentInteractionRevision;
            turn = stateClock.Turn;
            phase = state.Phase;
            paused = stateControl.Paused;
            intelligence = stateIntelligenceMode.Enabled;
            checkpoint = statePersistence.CheckpointPending;
            speed = stateNight.Speed;
            preferencesRevision = settingsRevision;
            interactionRevision = currentInteractionRevision;
            observed = true;
            continuous = stateControl.Paused == 0 && (state.Phase == Phase.Night || state.Phase == Phase.Deployment || state.Phase == Phase.Retreat || state.Phase == Phase.Celebration || state.Phase == Phase.Returning);
            if (changed)
                nextHud = 0;
            if (changed || hasPresentationEvents)
                Invalidate();
        }

        public void Invalidate()
        {
            unchecked
            {
                revision++;
            }
        // Local destructive confirmation content remains held until its explicit choice resets NextPanel.
        }

        public bool TakeHudRefresh(float now)
        {
            if (now < nextHud)
                return false;
            nextHud = now + HudInterval;
            return true;
        }

        public bool NeedsPanelRefresh(float now, bool hasVisibleContent, bool editing)
        {
            if (held || !hasVisibleContent || editing || now < nextPanel)
                return false;
            return revision != renderedRevision || continuous;
        }

        public void MarkRendered(float now)
        {
            renderedRevision = revision;
            nextPanel = now + PanelInterval;
        }

        public void Reset()
        {
            revision = 1;
            renderedRevision = 0;
            nextPanel = nextHud = 0;
            observed = continuous = held = false;
        }
    }
}
