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
    public sealed class PersonSnapshot : EntitySnapshot
    {
        public Royal Royal;
        public TalentId TalentDefinition;
        public TraitEntry[] Traits = Array.Empty<TraitEntry>();
        public PersonRequestEntry[] PersonRequests = Array.Empty<PersonRequestEntry>();
        public Talent? Talent;
        public PortraitDNA? Portrait;
    }

    internal static class PersonSnapshotStorage
    {
        internal static PersonSnapshot Capture(EntityManager em, Entity entity)
        {
            return new PersonSnapshot
            {
                Identity = em.GetComponentData<Identity>(entity),
                Transform = em.HasComponent<LocalTransform>(entity) ? em.GetComponentData<LocalTransform>(entity) : LocalTransform.Identity,
                Royal = em.GetComponentData<Royal>(entity),
                TalentDefinition = em.HasComponent<TalentDefinitionRef>(entity) ? em.GetComponentData<TalentDefinitionRef>(entity).Definition : default,
                Traits = SnapshotBuffers.Capture<TraitEntry>(em, entity),
                PersonRequests = SnapshotBuffers.Capture<PersonRequestEntry>(em, entity),
                Talent = em.HasComponent<Talent>(entity) ? em.GetComponentData<Talent>(entity) : (Talent? )null,
                Portrait = em.HasComponent<PortraitDNA>(entity) ? em.GetComponentData<PortraitDNA>(entity) : (PortraitDNA? )null,
            };
        }

        internal static void Write(BinaryWriter writer, PersonSnapshot record)
        {
            SnapshotBinary.Write(writer, record.Identity);
            SnapshotBinary.Write(writer, record.Transform);
            SnapshotBinary.Write(writer, record.Royal);
            SnapshotBinary.Write(writer, record.TalentDefinition);
            SnapshotBuffers.Write(writer, record.Traits);
            SnapshotBuffers.Write(writer, record.PersonRequests);
            writer.Write(record.Talent.HasValue);
            if (record.Talent.HasValue)
                SnapshotBinary.Write(writer, record.Talent.Value);
            writer.Write(record.Portrait.HasValue);
            if (record.Portrait.HasValue)
                SnapshotBinary.Write(writer, record.Portrait.Value);
        }

        internal static PersonSnapshot Read(BinaryReader reader)
        {
            return new PersonSnapshot
            {
                Identity = SnapshotBinary.Read<Identity>(reader),
                Transform = SnapshotBinary.Read<LocalTransform>(reader),
                Royal = SnapshotBinary.Read<Royal>(reader),
                TalentDefinition = SnapshotBinary.Read<TalentId>(reader),
                Traits = SnapshotBuffers.Read<TraitEntry>(reader),
                PersonRequests = SnapshotBuffers.Read<PersonRequestEntry>(reader),
                Talent = reader.ReadBoolean() ? SnapshotBinary.Read<Talent>(reader) : (Talent? )null,
                Portrait = reader.ReadBoolean() ? SnapshotBinary.Read<PortraitDNA>(reader) : (PortraitDNA? )null,
            };
        }

        internal static Entity Restore(EntityManager em, Entity root, PersonSnapshot record)
        {
            var entity = record.Talent.HasValue ? TalentEntities.Spawn(em, root, record.TalentDefinition, record.Transform.Position, true) : em.CreateEntity();
            if (!record.Talent.HasValue)
                EntityInstantiation.Initialize(em, root, entity, record.Identity.Name, record.Transform.Position, true);
            EntityState.Set(em, entity, record.Identity);
            EntityState.Set(em, entity, record.Transform);
            EntityState.Set(em, entity, record.Royal);
            // Every royal has this buffer, even when no traits are present.
            EntityState.Buffer<TraitEntry>(em, entity);
            SnapshotBuffers.Restore(em, entity, record.Traits);
            SnapshotBuffers.Restore(em, entity, record.PersonRequests);
            if (record.Talent.HasValue)
                EntityState.Set(em, entity, record.Talent.Value);
            if (record.Portrait.HasValue)
                EntityState.Set(em, entity, record.Portrait.Value);
            return entity;
        }
    }
}
