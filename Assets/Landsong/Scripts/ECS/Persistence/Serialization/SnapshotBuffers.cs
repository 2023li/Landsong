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
    internal static class SnapshotBuffers
    {
        internal static int Count(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > 1000000)
                throw new InvalidDataException("Snapshot count exceeds limits");
            return count;
        }

        internal static T[] Capture<T>(EntityManager em, Entity entity)
            where T : unmanaged, IBufferElementData
        {
            if (!em.HasBuffer<T>(entity))
                return Array.Empty<T>();
            using var data = em.GetBuffer<T>(entity).ToNativeArray(Allocator.Temp);
            return data.ToArray();
        }

        internal static void Write<T>(BinaryWriter writer, T[] values)
            where T : unmanaged
        {
            writer.Write(values.Length);
            foreach (var value in values)
                SnapshotBinary.Write(writer, value);
        }

        internal static T[] Read<T>(BinaryReader reader)
            where T : unmanaged
        {
            var values = new T[Count(reader)];
            for (int i = 0; i < values.Length; i++)
                values[i] = SnapshotBinary.Read<T>(reader);
            return values;
        }

        internal static void Restore<T>(EntityManager em, Entity entity, T[] values)
            where T : unmanaged, IBufferElementData
        {
            if (values.Length == 0 && !em.HasBuffer<T>(entity))
                return;
            EntityState.Buffer<T>(em, entity);
            em.GetBuffer<T>(entity).CopyFrom(values);
        }
    }
}
