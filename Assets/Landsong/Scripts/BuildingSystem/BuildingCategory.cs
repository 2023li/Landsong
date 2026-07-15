using System;
using UnityEngine;

namespace Landsong.BuildingSystem
{
    [Flags]
    public enum BuildingCategory
    {
        [InspectorName("无")]
        None = 0,
        [InspectorName("人口")]
        人口 = 1 << 0,
        [InspectorName("农业")]
        农业 = 1 << 1,
        [InspectorName("工业")]
        工业 = 1 << 2,
        [InspectorName("经济")]
        经济 = 1 << 3,
        [InspectorName("科研")]
        科研 = 1 << 4,
        [InspectorName("市政")]
        市政 = 1 << 5,
        [InspectorName("军事")]
        军事 = 1 << 6,
        [InspectorName("交通")]
        交通 = 1 << 7,
        [InspectorName("装饰")]
        装饰 = 1 << 8,
        [InspectorName("奇观")]
        奇观 = 1 << 9
    }
}
