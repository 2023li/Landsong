using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS
{
    public static class SoldierNaming
    {
        static readonly string[] Surnames =
        {
            "林",
            "陆",
            "沈",
            "赵",
            "陈",
            "江",
            "周",
            "顾",
            "柳",
            "唐",
            "许",
            "韩",
            "卫",
            "徐",
            "秦",
            "苏"
        };
        static readonly string[] GivenNames =
        {
            "长风",
            "远山",
            "星河",
            "青川",
            "明岳",
            "景行",
            "怀安",
            "望舒",
            "秋实",
            "砺锋",
            "凌云",
            "清和",
            "思远",
            "启明",
            "知遥",
            "守宁"
        };
        internal static FixedString128Bytes SoldierName(ulong id)
        {
            uint hash = math.hash(new uint2((uint)id, (uint)(id >> 32) ^ 0x51a7u));
            return new FixedString128Bytes(Surnames[hash % 16] + GivenNames[(hash / 16) % 16]);
        }
    }
}
