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
    public sealed class SoldierSnapshot : EntitySnapshot
    {
        public SoldierId Definition;
        public Health Health;
        public Soldier Soldier;
        public PortraitDNA? Portrait;
        public SoldierPerson? Person;
    }

    internal static class SoldierSnapshotStorage
    {
        internal static SoldierSnapshot Capture(EntityManager em, Entity entity)
        {
            return new SoldierSnapshot
            {
                Identity = em.GetComponentData<Identity>(entity),
                Transform = em.HasComponent<LocalTransform>(entity) ? em.GetComponentData<LocalTransform>(entity) : LocalTransform.Identity,
                Definition = em.GetComponentData<SoldierDefinitionRef>(entity).Definition,
                Health = em.GetComponentData<Health>(entity),
                Soldier = em.GetComponentData<Soldier>(entity),
                Portrait = em.HasComponent<PortraitDNA>(entity) ? em.GetComponentData<PortraitDNA>(entity) : (PortraitDNA? )null,
                Person = em.HasComponent<SoldierPerson>(entity) ? em.GetComponentData<SoldierPerson>(entity) : (SoldierPerson? )null,
            };
        }

        internal static void Write(BinaryWriter writer, SoldierSnapshot record)
        {
            SnapshotBinary.Write(writer, record.Identity);
            SnapshotBinary.Write(writer, record.Transform);
            SnapshotBinary.Write(writer, record.Definition);
            SnapshotBinary.Write(writer, record.Health);
            SnapshotBinary.Write(writer, record.Soldier);
            writer.Write(record.Portrait.HasValue);
            if (record.Portrait.HasValue)
                SnapshotBinary.Write(writer, record.Portrait.Value);
            writer.Write(record.Person.HasValue);
            if (record.Person.HasValue)
                SnapshotBinary.Write(writer, record.Person.Value);
        }

        internal static SoldierSnapshot Read(BinaryReader reader, int version)
        {
            return new SoldierSnapshot
            {
                Identity = SnapshotBinary.Read<Identity>(reader),
                Transform = SnapshotBinary.Read<LocalTransform>(reader),
                Definition = SnapshotBinary.Read<SoldierId>(reader),
                Health = SnapshotBinary.Read<Health>(reader),
                Soldier = version >= 37 ? SnapshotBinary.Read<Soldier>(reader) : ReadLegacySoldier(reader),
                Portrait = reader.ReadBoolean() ? SnapshotBinary.Read<PortraitDNA>(reader) : (PortraitDNA? )null,
                Person = reader.ReadBoolean() ? SnapshotBinary.Read<SoldierPerson>(reader) : (SoldierPerson? )null,
            };
        }

        static Soldier ReadLegacySoldier(BinaryReader reader) => new Soldier
        {
            Garrison = SnapshotBinary.Read<ulong>(reader),
            Slot = SnapshotBinary.Read<int>(reader),
            PopulationCost = SnapshotBinary.Read<int>(reader),
            PendingSince = SnapshotBinary.Read<int>(reader),
            Experience = SnapshotBinary.Read<int>(reader),
            LastExperienceTurn = SnapshotBinary.Read<int>(reader),
            RecallState = SnapshotBinary.Read<byte>(reader),
            Weapon = SoldierWeaponKind.None
        };

        internal static Entity Restore(EntityManager em, Entity root, SoldierSnapshot record)
        {
            var entity = SoldierEntities.Spawn(em, root, record.Definition, record.Transform.Position, true);
            EntityState.Set(em, entity, record.Identity);
            EntityState.Set(em, entity, record.Transform);
            EntityState.Set(em, entity, record.Health);
            EntityState.Set(em, entity, record.Soldier);
            if (record.Portrait.HasValue)
                EntityState.Set(em, entity, record.Portrait.Value);
            if (record.Person.HasValue)
                EntityState.Set(em, entity, record.Person.Value);
            return entity;
        }
    }
}
