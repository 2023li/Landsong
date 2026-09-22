using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AI;

namespace Landsong.Verification
{
    // Experimental connection authored independently of the visual ramp prefab.
    public sealed class TwcLabConnection : MonoBehaviour
    {
        [LabelText("局部入口")] public Vector3 LocalEntry = new Vector3(11.5f, .35f, 6);
        [LabelText("局部出口")] public Vector3 LocalExit = new Vector3(11.5f, 1.35f, 8.5f);
        [LabelText("连接宽度")] [Min(.01f)] public float Width = .6f;
        [LabelText("允许双向通行")] public bool Bidirectional = true;
        NavMeshLinkInstance instance;

        public void Register()
        {
            Unregister();
            var filter = new NavMeshQueryFilter { agentTypeID = 0, areaMask = NavMesh.AllAreas };
            var entry = transform.TransformPoint(LocalEntry);
            var exit = transform.TransformPoint(LocalExit);
            if (!NavMesh.SamplePosition(entry, out var a, .12f, filter)
                || !NavMesh.SamplePosition(exit, out var b, .12f, filter))
                throw new InvalidOperationException("连接端点没有落在指定高度的导航表面上。");
            instance = NavMesh.AddLink(new NavMeshLinkData
            {
                startPosition = a.position, endPosition = b.position, width = Width,
                bidirectional = Bidirectional, area = 0, agentTypeID = 0, costModifier = -1
            });
            if (!instance.valid) throw new InvalidOperationException("显式地表连接创建失败。");
        }

        public void Unregister() { if (instance.valid) instance.Remove(); }
        void OnDisable() => Unregister();
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.magenta;
            var a = transform.TransformPoint(LocalEntry); var b = transform.TransformPoint(LocalExit);
            Gizmos.DrawWireSphere(a, .15f); Gizmos.DrawWireSphere(b, .15f); Gizmos.DrawLine(a, b);
        }
    }
}
