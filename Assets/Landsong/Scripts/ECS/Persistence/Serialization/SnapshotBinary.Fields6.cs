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
    public static partial class SnapshotBinary
    {
        static void WriteColor32(BinaryWriter writer, Color32 value)
        {
            Write(writer, value.r);
            Write(writer, value.g);
            Write(writer, value.b);
            Write(writer, value.a);
        }

        static Color32 ReadColor32(BinaryReader reader)
        {
            return new Color32
            {
                r = Read<byte>(reader),
                g = Read<byte>(reader),
                b = Read<byte>(reader),
                a = Read<byte>(reader),
            };
        }
    }
}
