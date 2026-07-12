using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.UISystem
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TechnologyTreeConnectionGraphic : MaskableGraphic
    {
        [SerializeField] private List<RectTransform> prerequisiteNodes = new List<RectTransform>();
        [SerializeField] private List<RectTransform> technologyNodes = new List<RectTransform>();
        private readonly Vector3[] worldCorners = new Vector3[4];
        private float lineWidth = 8f;

        public void SetLineWidth(float value)
        {
            lineWidth = Mathf.Max(1f, value);
            SetVerticesDirty();
        }

        public void ClearConnections()
        {
            prerequisiteNodes.Clear();
            technologyNodes.Clear();
            SetVerticesDirty();
        }

        public void AddConnection(RectTransform prerequisite, RectTransform technology)
        {
            if (prerequisite == null || technology == null)
            {
                return;
            }

            prerequisiteNodes.Add(prerequisite);
            technologyNodes.Add(technology);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            var connectionCount = Mathf.Min(prerequisiteNodes.Count, technologyNodes.Count);
            for (var i = 0; i < connectionCount; i++)
            {
                var prerequisite = prerequisiteNodes[i];
                var technology = technologyNodes[i];
                if (prerequisite == null || technology == null)
                {
                    continue;
                }

                var start = GetEdgeCenter(prerequisite, true);
                var end = GetEdgeCenter(technology, false);

                if (Mathf.Abs(start.y - end.y) < 0.5f)
                {
                    AddSegment(vertexHelper, start, end);
                    continue;
                }

                var middleX = Mathf.Lerp(start.x, end.x, 0.5f);
                var firstCorner = new Vector2(middleX, start.y);
                var secondCorner = new Vector2(middleX, end.y);
                AddSegment(vertexHelper, start, firstCorner);
                AddSegment(vertexHelper, firstCorner, secondCorner);
                AddSegment(vertexHelper, secondCorner, end);
            }
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            SetVerticesDirty();
        }

        private Vector2 GetEdgeCenter(RectTransform target, bool rightEdge)
        {
            target.GetWorldCorners(worldCorners);
            var first = rightEdge ? worldCorners[2] : worldCorners[0];
            var second = rightEdge ? worldCorners[3] : worldCorners[1];
            var worldCenter = Vector3.Lerp(first, second, 0.5f);
            return rectTransform.InverseTransformPoint(worldCenter);
        }

        private void AddSegment(VertexHelper vertexHelper, Vector2 start, Vector2 end)
        {
            var direction = end - start;
            if (direction.sqrMagnitude < 0.01f)
            {
                return;
            }

            var normal = new Vector2(-direction.y, direction.x).normalized * (lineWidth * 0.5f);
            var vertex = UIVertex.simpleVert;
            vertex.color = color;

            var quad = new UIVertex[4];
            vertex.position = start - normal;
            quad[0] = vertex;
            vertex.position = start + normal;
            quad[1] = vertex;
            vertex.position = end + normal;
            quad[2] = vertex;
            vertex.position = end - normal;
            quad[3] = vertex;
            vertexHelper.AddUIVertexQuad(quad);
        }

    }
}
