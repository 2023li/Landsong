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
    public sealed class HeroSnapshot : EntitySnapshot
    {
        public HeroId Definition;
        public Health Health;
        public Hero Hero;
        public PortraitDNA? Portrait;
    }

    internal static class HeroSnapshotStorage
    {
        internal static HeroSnapshot Capture(EntityManager em, Entity entity)
        {
            return new HeroSnapshot
            {
                Identity = em.GetComponentData<Identity>(entity),
                Transform = em.HasComponent<LocalTransform>(entity) ? em.GetComponentData<LocalTransform>(entity) : LocalTransform.Identity,
                Definition = em.GetComponentData<HeroDefinitionRef>(entity).Definition,
                Health = em.GetComponentData<Health>(entity),
                Hero = em.GetComponentData<Hero>(entity),
                Portrait = em.HasComponent<PortraitDNA>(entity) ? em.GetComponentData<PortraitDNA>(entity) : (PortraitDNA? )null,
            };
        }

        internal static void Write(BinaryWriter writer, HeroSnapshot record)
        {
            SnapshotBinary.Write(writer, record.Identity);
            SnapshotBinary.Write(writer, record.Transform);
            SnapshotBinary.Write(writer, record.Definition);
            SnapshotBinary.Write(writer, record.Health);
            SnapshotBinary.Write(writer, record.Hero);
            writer.Write(record.Portrait.HasValue);
            if (record.Portrait.HasValue)
                SnapshotBinary.Write(writer, record.Portrait.Value);
        }

        internal static HeroSnapshot Read(BinaryReader reader)
        {
            return new HeroSnapshot
            {
                Identity = SnapshotBinary.Read<Identity>(reader),
                Transform = SnapshotBinary.Read<LocalTransform>(reader),
                Definition = SnapshotBinary.Read<HeroId>(reader),
                Health = SnapshotBinary.Read<Health>(reader),
                Hero = SnapshotBinary.Read<Hero>(reader),
                Portrait = reader.ReadBoolean() ? SnapshotBinary.Read<PortraitDNA>(reader) : (PortraitDNA? )null,
            };
        }

        internal static Entity Restore(EntityManager em, Entity root, HeroSnapshot record)
        {
            var entity = HeroEntities.Spawn(em, root, record.Definition, record.Transform.Position, true);
            EntityState.Set(em, entity, record.Identity);
            EntityState.Set(em, entity, record.Transform);
            EntityState.Set(em, entity, record.Health);
            EntityState.Set(em, entity, record.Hero);
            if (record.Portrait.HasValue)
                EntityState.Set(em, entity, record.Portrait.Value);
            return entity;
        }
    }
}
