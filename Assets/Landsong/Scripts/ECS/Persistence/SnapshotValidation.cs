using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Landsong.ECS.Definitions;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Persistence
{
    internal static class SnapshotValidation
    {
        internal static void Validate(EntityManager em, Entity root, SnapshotCodec.Snapshot data, bool deferQuestContainerReconciliation = false)
        {
            if (data == null || data.Records == null)
                throw new InvalidDataException("Incomplete snapshot");
            if ((data.Session.Phase != Phase.Day && data.Session.Phase != Phase.Deployment && !(data.Session.Phase == Phase.GameOver && data.Court.Extinction == 1)) || data.Clock.Turn < 1 || data.Random.State == 0 || data.Ids.NextId == 0)
                throw new InvalidDataException("Invalid session");
            if (!math.isfinite(data.Clock.DawnRemaining) || data.Clock.DawnRemaining < 0 || data.Clock.DawnRemaining > em.GetComponentData<NightSettings>(root).DawnSeconds || data.Session.Phase == Phase.Deployment && data.Clock.DawnRemaining != 0)
                throw new InvalidDataException("Invalid dawn clock");
            if (!math.isfinite(data.Clock.DawnSourceNightTime) || data.Clock.DawnSourceNightTime < -1)
                throw new InvalidDataException("Invalid dawn lighting source");
            if (data.Weather.Initialized != 0 && (data.Weather.DayTurn != data.Clock.Turn
                || data.Weather.RandomState == 0 || data.Weather.Season != SeasonWeatherOps.Season(data.Clock.Turn)
                || data.Weather.Weather > WeatherKind.HeavyRain || data.Weather.Wind > WindKind.Strong
                || !math.isfinite(data.Weather.WindDegrees) || data.Weather.WindDegrees < 0 || data.Weather.WindDegrees >= 360
                || !math.isfinite(data.Weather.DayElapsed) || data.Weather.DayElapsed < 0
                || !math.isfinite(data.Weather.NextThunderAt) || data.Weather.NextThunderAt < 0
                || data.Weather.LightningCount > data.Weather.LightningLimit
                || !WeatherKindOps.IsRain(data.Weather.Weather) && data.Weather.LightningLimit != 0))
                throw new InvalidDataException($"Invalid season or weather state: turn={data.Clock.Turn}, day={data.Weather.DayTurn}, season={data.Weather.Season}, weather={data.Weather.Weather}, wind={data.Weather.Wind}, degrees={data.Weather.WindDegrees}, seed={data.Weather.RandomState}, strikes={data.Weather.LightningCount}/{data.Weather.LightningLimit}, next={data.Weather.NextThunderAt}, elapsed={data.Weather.DayElapsed}");
            var ids = new HashSet<ulong>();
            foreach (var record in data.Records)
            {
                if (record == null || record.Identity.Id == 0 || record.Identity.Id > data.Ids.NextId || !ids.Add(record.Identity.Id) || !math.all(math.isfinite(record.Transform.Position)) || !math.all(math.isfinite(record.Transform.Rotation.value)) || !math.isfinite(record.Transform.Scale) || record.Transform.Scale <= 0)
                    throw new InvalidDataException("Invalid entity identity or transform");
                switch (record)
                {
                    case WorkerCargoDropSnapshot drop:
                        WorkerCargoDropSnapshotStorage.Validate(em, root, drop);
                        break;
                    case FirefighterSnapshot firefighter:
                        FirefighterSnapshotStorage.Validate(em, root, data, firefighter);
                        break;
                    case TransportWorkerSnapshot worker:
                        TransportWorkerSnapshotStorage.Validate(em, root, data, worker);
                        break;
                    case BuildingSnapshot building:
                        BuildingStateValidation.Validate(em, root, data, building);
                        break;
                    case SoldierSnapshot soldier:
                        if (!SoldierDefinitions.IsValid(em, root, soldier.Definition))
                            throw new InvalidDataException("Invalid soldier definition");
                        RequireSoldierPrefab(em, root, soldier.Definition);
                        Health(soldier.Health);
                        Portrait(em, root, soldier.Portrait, soldier.Person?.Gender ?? PersonGender.Male);
                        if (soldier.Person.HasValue)
                        {
                            var person = soldier.Person.Value;
                            if (!soldier.Portrait.HasValue || person.Age < 0 || person.Lifespan <= person.Age || person.LastAgeTurn < 0 || person.LastAgeTurn > data.Clock.Turn || person.Incarnation < 0 || person.SpecialAttention > 1 || person.DeathNotified > 1 || (person.Gender != PersonGender.Male && person.Gender != PersonGender.Female))
                                throw new InvalidDataException("Invalid soldier person");
                        }

                        break;
                    case HeroSnapshot hero:
                        if (!HeroDefinitions.IsValid(em, root, hero.Definition))
                            throw new InvalidDataException("Invalid hero definition");
                        RequireHeroPrefab(em, root, hero.Definition);
                        Health(hero.Health);
                        Portrait(em, root, hero.Portrait, PersonGender.Male);
                        break;
                    case QuestSnapshot quest:
                        if (!QuestDefinitions.IsValid(em, root, quest.Definition) || quest.Progress == null)
                            throw new InvalidDataException("Invalid quest definition or progress");
                        QuestOps.ValidateProgress(em, root, quest.Definition, quest.Quest, quest.Progress, data.Clock.Turn);
                        break;
                    case ExpeditionSnapshot expedition:
                        if (!ExpeditionDefinitions.IsValid(em, root, expedition.Definition))
                            throw new InvalidDataException("Invalid expedition definition");
                        break;
                    case PersonSnapshot person:
                        if (person.Traits == null || person.PersonRequests == null || person.PersonRequests.Length > 16 || person.Talent.HasValue != person.TalentDefinition.IsValid || person.Talent.HasValue && !TalentDefinitions.IsValid(em, root, person.TalentDefinition))
                            throw new InvalidDataException("Invalid person records");
                        Portrait(em, root, person.Portrait, person.Royal.Gender);
                        break;
                    default:
                        throw new InvalidDataException("Unknown persistent entity domain");
                }
            }

            ProgressionStateValidation.Validate(em, root, data);
            EconomyStateValidation.Validate(em, root, data);
            InvitationStateValidation.Validate(em, root, data, deferQuestContainerReconciliation);
            CourtStateValidation.Validate(em, root, data);
            NightStateValidation.Validate(em, root, data);
            MilitaryStateValidation.Validate(em, root, data);
            if (data.Tracking.Mode > 2 || data.Tracking.Mode == 2 && data.Tracking.Target != 0)
                throw new InvalidDataException("Invalid quest tracking mode");
            if (data.Tracking.Target != 0 && !data.Records.OfType<QuestSnapshot>().Any(q => q.Identity.Id == data.Tracking.Target && (q.Quest.Status == QuestStatus.Active || q.Quest.Status == QuestStatus.Completed)))
                throw new InvalidDataException("Tracked quest is missing or not trackable");
            if (data.Tracking.Mode != 1 && data.TrackedQuests.Length != 0 || data.Tracking.Mode == 1 && (data.TrackedQuests.Length == 0 || !data.TrackedQuests.Any(q => q.Quest == data.Tracking.Target)))
                throw new InvalidDataException("Invalid tracked quest list");
            var tracked = new HashSet<ulong>();
            foreach (var pin in data.TrackedQuests)
                if (!tracked.Add(pin.Quest) || !data.Records.OfType<QuestSnapshot>().Any(q => q.Identity.Id == pin.Quest && (q.Quest.Status == QuestStatus.Active || q.Quest.Status == QuestStatus.Completed)))
                    throw new InvalidDataException("Tracked quest is missing, duplicated or not trackable");
            var cooldowns = new HashSet<QuestId>();
            foreach (var entry in data.QuestRefreshCooldowns)
                if (!QuestDefinitions.IsValid(em, root, entry.Quest) || entry.NextTurn < 0 || !cooldowns.Add(entry.Quest) || (QuestDefinitions.Get(em, root, entry.Quest).Behavior & QuestBehaviorFlags.Mainline) != 0)
                    throw new InvalidDataException("Invalid quest refresh cooldown");
        }

        static void RequireSoldierPrefab(EntityManager em, Entity root, SoldierId definition)
        {
            if (em.HasBuffer<SoldierPrefab>(root))
                foreach (var entry in em.GetBuffer<SoldierPrefab>(root))
                    if (entry.Definition == definition && entry.Prefab != Entity.Null && em.Exists(entry.Prefab))
                        return;
            throw new InvalidDataException("Missing soldier prefab");
        }

        static void RequireHeroPrefab(EntityManager em, Entity root, HeroId definition)
        {
            if (em.HasBuffer<HeroPrefab>(root))
                foreach (var entry in em.GetBuffer<HeroPrefab>(root))
                    if (entry.Definition == definition && entry.Prefab != Entity.Null && em.Exists(entry.Prefab))
                        return;
            throw new InvalidDataException("Missing hero prefab");
        }

        internal static void Health(Health value)
        {
            if (!math.isfinite(value.Current) || !math.isfinite(value.Maximum) || value.Current < 0 || value.Maximum <= 0 || value.Current > value.Maximum)
                throw new InvalidDataException("Invalid health");
        }

        static void Portrait(EntityManager em, Entity root, PortraitDNA? portrait, PersonGender gender)
        {
            if (!portrait.HasValue)
                return;
            if (!PortraitOps.Ready(em, root))
                throw new InvalidDataException("Invalid portrait owner");
            var library = em.GetComponentData<PortraitLibrary>(root).Value;
            if (!PortraitOps.Valid(ref library.Value, portrait.Value, gender))
                throw new InvalidDataException("Invalid portrait DNA");
        }
    }
}
