using Landsong.ECS.Definitions;
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS
{
    public static class BuildingNaming
    {
        public static ResultCode Rename(EntityManager em, Entity e, FixedString128Bytes name)
        {
            if (e == Entity.Null || !em.Exists(e) || !em.HasComponent<Building>(e))
                return ResultCode.InvalidTarget;
            var clean = BuildingNaming.SanitizeName(name.ToString());
            var id = em.GetComponentData<Identity>(e);
            id.Name = string.IsNullOrWhiteSpace(clean) ? BuildingDefinitions.Get(em, WorldQueries.Root(em), em.GetComponentData<BuildingDefinitionRef>(e).Definition).Metadata.Name : new FixedString128Bytes(clean);
            em.SetComponentData(e, id);
            return ResultCode.Success;
        }

        public static string SanitizeName(string input)
        {
            var result = new StringBuilder();
            var tag = false;
            var count = 0;
            foreach (var c in input ?? "")
            {
                if (c == '<')
                {
                    tag = true;
                    continue;
                }

                if (c == '>')
                {
                    tag = false;
                    continue;
                }

                if (tag || char.IsControl(c) || char.IsSurrogate(c))
                    continue;
                var bytes = Encoding.UTF8.GetByteCount(new[] { c });
                if (count + bytes > 120 || result.Length >= 32)
                    break;
                result.Append(c);
                count += bytes;
            }

            return result.ToString().Trim();
        }
    }
}
