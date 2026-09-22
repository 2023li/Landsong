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
    // IO-only records. Gameplay never reads these as live state.
    public abstract class EntitySnapshot
    {
        public Identity Identity;
        public LocalTransform Transform;
    }
}
