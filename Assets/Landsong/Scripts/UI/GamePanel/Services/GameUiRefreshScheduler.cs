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
                if (value <= 0) Invalidate();
            }
        }

        public void Observe(Session state, int settingsRevision, bool hasPresentationEvents, int currentInteractionRevision = 0)
        {
            bool changed = !observed || turn != state.Turn || phase != state.Phase || paused != state.Paused ||
                intelligence != state.IntelligenceMode || checkpoint != state.CheckpointPending || speed != state.NightSpeed ||
                preferencesRevision != settingsRevision || interactionRevision != currentInteractionRevision;
            turn = state.Turn; phase = state.Phase; paused = state.Paused;
            intelligence = state.IntelligenceMode; checkpoint = state.CheckpointPending; speed = state.NightSpeed;
            preferencesRevision = settingsRevision; interactionRevision = currentInteractionRevision; observed = true;
            continuous = state.Paused == 0 && (state.Phase == Phase.Night || state.Phase == Phase.Deployment ||
                state.Phase == Phase.Retreat || state.Phase == Phase.Celebration);
            if (changed) nextHud = 0;
            if (changed || hasPresentationEvents) Invalidate();
        }

        public void Invalidate()
        {
            unchecked { revision++; }
            // Local destructive confirmation content remains held until its explicit choice resets NextPanel.
        }

        public bool TakeHudRefresh(float now)
        {
            if (now < nextHud) return false;
            nextHud = now + HudInterval;
            return true;
        }

        public bool NeedsPanelRefresh(float now, bool hasVisibleContent, bool editing)
        {
            if (held || !hasVisibleContent || editing || now < nextPanel) return false;
            return revision != renderedRevision || continuous;
        }

        public void MarkRendered(float now)
        {
            renderedRevision = revision;
            nextPanel = now + PanelInterval;
        }

        public void Reset()
        {
            revision = 1; renderedRevision = 0;
            nextPanel = nextHud = 0;
            observed = continuous = held = false;
        }
    }
}
