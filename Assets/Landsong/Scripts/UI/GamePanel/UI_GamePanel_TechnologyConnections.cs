using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Text = TMPro.TextMeshProUGUI;
using InputField = TMPro.TMP_InputField;
using Font = TMPro.TMP_FontAsset;

namespace Landsong.ECS.Presentation
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UI_GamePanel_TechnologyConnections : MaskableGraphic
    {
        public struct Edge
        {
            [Sirenix.OdinInspector.LabelText("来源")]
            public Vector2 From;
            [Sirenix.OdinInspector.LabelText("目标")]
            public Vector2 To;
            [Sirenix.OdinInspector.LabelText("颜色")]
            public Color Color;
            [Sirenix.OdinInspector.LabelText("垂直")]
            public bool Vertical;
        }

        public readonly List<Edge> Edges = new List<Edge>();
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            foreach (var edge in Edges)
            {
                var middle = (edge.From.x + edge.To.x) / 2;
                var a = new Vector2(middle, edge.From.y);
                var b = new Vector2(middle, edge.To.y);
                if (edge.Vertical)
                {
                    var y = (edge.From.y + edge.To.y) / 2;
                    a = new Vector2(edge.From.x, y);
                    b = new Vector2(edge.To.x, y);
                }

                Segment(vh, edge.From, a, edge.Color);
                Segment(vh, a, b, edge.Color);
                Segment(vh, b, edge.To, edge.Color);
            }
        }

        static void Segment(VertexHelper vh, Vector2 a, Vector2 b, Color color)
        {
            if ((b - a).sqrMagnitude < .01f)
                return;
            var n = new Vector2(-(b - a).y, (b - a).x).normalized * 1.5f;
            var start = vh.currentVertCount;
            foreach (var p in new[]
            {
                a - n,
                a + n,
                b + n,
                b - n
            }

            )
                vh.AddVert(p, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
