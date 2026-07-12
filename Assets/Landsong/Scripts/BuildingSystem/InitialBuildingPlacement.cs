using System;
using UnityEngine;

namespace Landsong.BuildingSystem
{
    /// <summary>
    /// 仅保留脚本类型，避免旧地图 Prefab 在人工迁移前出现 Missing Script。
    /// 该组件不再执行任何运行时占格或生成逻辑。
    /// </summary>
    [Obsolete("请把旧初始建筑转换为 MapContentAuthoring 下的 InitialBuildingMarker。")]
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class InitialBuildingPlacement : MonoBehaviour
    {
        [ContextMenu("显示迁移说明")]
        private void ShowMigrationWarning()
        {
            Debug.LogWarning(
                "InitialBuildingPlacement 已停用。请使用 Landsong/地图/地图目录与校验，并把子建筑转换为 InitialBuildingMarker。",
                this);
        }
    }
}
