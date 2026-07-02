using System;
using UnityEngine;

namespace Landsong.BuildingSystem
{
    [Flags]
    public enum BuildingCategory
    {
        [InspectorName("无")]
        None = 0,
        [InspectorName("居住")]
        Housing = 1 << 0,
        [InspectorName("生产")]
        Production = 1 << 1,
        [InspectorName("仓储")]
        Storage = 1 << 2,
        [InspectorName("后勤")]
        后勤 = 1 << 3,
        [InspectorName("通用")]
        通用 = 1 << 4,
        [InspectorName("美化")]
        美化 = 1 << 5,
        [InspectorName("奇迹")]
        奇迹 = 1<< 6,
        [InspectorName("神迹")]
        神迹 = 1<< 7,
        [InspectorName("道路")]
        道路 = 1 << 8,

    }
}
