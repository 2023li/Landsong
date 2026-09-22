using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.Verification
{
    // Explicit sources keep decorative ropes, labels and agents out of the lab bake.
    [RequireComponent(typeof(BoxCollider))]
    public sealed class NavigationLabSurface : MonoBehaviour
    {
        [LabelText("导航区域")] [Range(0, 31)] public int NavigationArea;
    }
}
