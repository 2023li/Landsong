using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Landsong.ECS.Persistence
{
    internal static class EntitySnapshotStorage
    {
        // Stable disk tags only. Domain identity never uses these tags in gameplay.
        internal static void Capture(BinaryWriter writer, EntityManager em, Entity entity)
        {
            if (em.HasComponent<WorkerCargoDrop>(entity))
            {
                writer.Write((byte)9);
                WorkerCargoDropSnapshotStorage.Capture(writer, em, entity);
                return;
            }
            if (em.HasComponent<Firefighter>(entity))
            {
                writer.Write((byte)8);
                FirefighterSnapshotStorage.Capture(writer, em, entity);
                return;
            }
            if (em.HasComponent<TransportWorker>(entity))
            {
                writer.Write((byte)7);
                TransportWorkerSnapshotStorage.Capture(writer, em, entity);
                return;
            }
            if (em.HasComponent<Building>(entity))
            {
                writer.Write((byte)1);
                BuildingSnapshotStorage.Write(writer, BuildingSnapshotStorage.Capture(em, entity));
                return;
            }

            if (em.HasComponent<Soldier>(entity))
            {
                writer.Write((byte)2);
                SoldierSnapshotStorage.Write(writer, SoldierSnapshotStorage.Capture(em, entity));
                return;
            }

            if (em.HasComponent<Hero>(entity))
            {
                writer.Write((byte)3);
                HeroSnapshotStorage.Write(writer, HeroSnapshotStorage.Capture(em, entity));
                return;
            }

            if (em.HasComponent<Quest>(entity))
            {
                writer.Write((byte)4);
                QuestSnapshotStorage.Write(writer, QuestSnapshotStorage.Capture(em, entity));
                return;
            }

            if (em.HasComponent<Expedition>(entity))
            {
                writer.Write((byte)5);
                ExpeditionSnapshotStorage.Write(writer, ExpeditionSnapshotStorage.Capture(em, entity));
                return;
            }

            if (em.HasComponent<Royal>(entity))
            {
                writer.Write((byte)6);
                PersonSnapshotStorage.Write(writer, PersonSnapshotStorage.Capture(em, entity));
                return;
            }

            throw new InvalidDataException("Persistent entity has no supported domain: " + entity);
        }

        internal static EntitySnapshot Read(BinaryReader reader, int version)
        {
            switch (reader.ReadByte())
            {
                case 1:
                    return BuildingSnapshotStorage.Read(reader, version);
                case 2:
                    return SoldierSnapshotStorage.Read(reader);
                case 3:
                    return HeroSnapshotStorage.Read(reader);
                case 4:
                    return QuestSnapshotStorage.Read(reader);
                case 5:
                    return ExpeditionSnapshotStorage.Read(reader);
                case 6:
                    return PersonSnapshotStorage.Read(reader);
                case 7:
                    return TransportWorkerSnapshotStorage.Read(reader, version);
                case 8:
                    return FirefighterSnapshotStorage.Read(reader);
                case 9:
                    return WorkerCargoDropSnapshotStorage.Read(reader);
                default:
                    throw new InvalidDataException("Unknown persistent entity domain");
            }
        }

        internal static Entity Restore(EntityManager em, Entity root, EntitySnapshot record)
        {
            switch (record)
            {
                case FirefighterSnapshot value:
                    return FirefighterSnapshotStorage.Restore(em, root, value);
                case WorkerCargoDropSnapshot value:
                    return WorkerCargoDropSnapshotStorage.Restore(em, root, value);
                case TransportWorkerSnapshot value:
                    return TransportWorkerSnapshotStorage.Restore(em, root, value);
                case BuildingSnapshot value:
                    return BuildingSnapshotStorage.Restore(em, root, value);
                case SoldierSnapshot value:
                    return SoldierSnapshotStorage.Restore(em, root, value);
                case HeroSnapshot value:
                    return HeroSnapshotStorage.Restore(em, root, value);
                case QuestSnapshot value:
                    return QuestSnapshotStorage.Restore(em, root, value);
                case ExpeditionSnapshot value:
                    return ExpeditionSnapshotStorage.Restore(em, root, value);
                case PersonSnapshot value:
                    return PersonSnapshotStorage.Restore(em, root, value);
                default:
                    throw new InvalidDataException("Unknown persistent entity domain");
            }
        }
    }
}
