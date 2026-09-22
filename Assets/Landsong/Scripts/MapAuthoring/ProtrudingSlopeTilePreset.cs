using GiantGrey.TileWorldCreator;
using UnityEngine;

namespace Landsong.GridSystem
{
    [CreateAssetMenu(menuName="Landsong/地图/突出式斜坡 TilePreset",fileName="My斜坡")]
    public sealed class ProtrudingSlopeTilePreset : TilePreset
    {
        [Tooltip("配合 Landsong Slope 构建层使用；普通 Tiles 的邻居规则不能决定坡向和左右端。")] [Sirenix.OdinInspector.LabelText("左端"), Sirenix.OdinInspector.Required] public GameObject Left;
        [Sirenix.OdinInspector.LabelText("中段"), Sirenix.OdinInspector.Required] public GameObject Middle;
        [Sirenix.OdinInspector.LabelText("右端"), Sirenix.OdinInspector.Required] public GameObject Right;
    }
}
