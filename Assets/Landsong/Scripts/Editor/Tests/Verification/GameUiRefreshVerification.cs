#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using Landsong.ECS.Presentation;

namespace Landsong.ECS.Editor
{
    public static class GameUiRefreshVerification
    {
        public static string Run()
        {
            var report = new StringBuilder();
            int assertions = 0;
            void Check(bool value, string message)
            {
                if (!value)
                    throw new InvalidOperationException(message);
                assertions++;
                report.AppendLine("PASS " + message);
            }

            var clock = new GameUiRefreshScheduler();
            var day = new Session
            {
                Phase = Phase.Day
            };
            GameClock dayClock = new GameClock()
            {
                Turn = 1
            };
            SimulationControl dayControl = new SimulationControl()
            {
            };
            NightRuntimeState dayNight = new NightRuntimeState()
            {
            };
            IntelligenceModeState dayIntelligenceMode = new IntelligenceModeState()
            {
            };
            PersistenceGate dayPersistence = new PersistenceGate()
            {
            };
            clock.Observe(day, dayClock, dayControl, dayNight, dayIntelligenceMode, dayPersistence, 0, false);
            Check(clock.NeedsPanelRefresh(0, true, false), "First visible panel renders");
            clock.MarkRendered(0);
            clock.Observe(day, dayClock, dayControl, dayNight, dayIntelligenceMode, dayPersistence, 0, false);
            Check(!clock.NeedsPanelRefresh(30, true, false), "Idle day does not periodically rebuild unchanged lists");
            var interactionClock = new GameUiRefreshScheduler();
            interactionClock.Observe(day, dayClock, dayControl, dayNight, dayIntelligenceMode, dayPersistence, 0, false);
            interactionClock.MarkRendered(0);
            interactionClock.Observe(day, dayClock, dayControl, dayNight, dayIntelligenceMode, dayPersistence, 0, false, 1);
            Check(interactionClock.NeedsPanelRefresh(1, true, false), "Releasing an interaction flushes deferred rows without a game event");
            clock.Observe(day, dayClock, dayControl, dayNight, dayIntelligenceMode, dayPersistence, 0, true);
            Check(!clock.NeedsPanelRefresh(30, false, false), "Hidden content does not render after a command");
            Check(!clock.NeedsPanelRefresh(30, true, true), "Editing defers content replacement");
            Check(clock.TakeHudRefresh(30), "HUD updates while a list is editing");
            Check(clock.NeedsPanelRefresh(30, true, false), "Deferred revision remains dirty when editing ends");
            clock.MarkRendered(30);
            clock.NextPanel = 0;
            Check(clock.NeedsPanelRefresh(30.01f, true, false), "Explicit selection invalidates immediately");
            clock.MarkRendered(31);
            clock.Observe(day, dayClock, dayControl, dayNight, dayIntelligenceMode, dayPersistence, 1, false);
            Check(clock.NeedsPanelRefresh(32, true, false), "Language and preference changes invalidate visible content");
            clock.MarkRendered(32);
            var night = day;
            GameClock nightClock = dayClock;
            SimulationControl nightControl = dayControl;
            NightRuntimeState nightNight = dayNight;
            IntelligenceModeState nightIntelligenceMode = dayIntelligenceMode;
            PersistenceGate nightPersistence = dayPersistence;
            night.Phase = Phase.Night;
            clock.Observe(night, nightClock, nightControl, nightNight, nightIntelligenceMode, nightPersistence, 1, false);
            clock.MarkRendered(33);
            Check(!clock.NeedsPanelRefresh(33.1f, true, false), "Continuous night updates remain throttled");
            Check(clock.NeedsPanelRefresh(33.3f, true, false), "Visible night content updates without a command event");
            nightControl.Paused = 1;
            clock.Observe(night, nightClock, nightControl, nightNight, nightIntelligenceMode, nightPersistence, 1, false);
            clock.MarkRendered(34);
            Check(!clock.NeedsPanelRefresh(40, true, false), "Paused unchanged content stays stable");
            clock.NextPanel = float.PositiveInfinity;
            Check(!clock.NeedsPanelRefresh(50, true, false), "Local confirmation holds its content");
            clock.Observe(night, nightClock, nightControl, nightNight, nightIntelligenceMode, nightPersistence, 1, true, 2);
            Check(!clock.NeedsPanelRefresh(50, true, false), "Messages and pointer release do not replace an unresolved confirmation");
            clock.NextPanel = 0;
            Check(clock.NeedsPanelRefresh(50, true, false), "Explicit cancellation releases local confirmation hold");
            clock.MarkRendered(50);
            clock.Reset();
            clock.Observe(day, dayClock, dayControl, dayNight, dayIntelligenceMode, dayPersistence, 0, false);
            Check(clock.NeedsPanelRefresh(0, true, false) && clock.TakeHudRefresh(0), "A new session inherits neither clocks nor revisions");
            report.AppendLine("Assertions: " + assertions);
            Directory.CreateDirectory("Library/LandsongEcs");
            File.WriteAllText("Library/LandsongEcs/ui-refresh-verification.txt", report.ToString());
            return report.ToString();
        }
    }
}
#endif
