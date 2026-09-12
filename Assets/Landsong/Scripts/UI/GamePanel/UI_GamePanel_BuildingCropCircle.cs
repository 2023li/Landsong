using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UI_GamePanel_BuildingCropCircle : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            var radius = Mathf.Min(r.width, r.height) / 2;
            vh.AddVert(r.center, color, Vector2.zero);
            for (int i = 0; i <= 32; i++)
            {
                float a = i * Mathf.PI * 2 / 32;
                vh.AddVert(r.center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, color, Vector2.zero);
                if (i > 0)
                    vh.AddTriangle(0, i, i + 1);
            }
        }
    }
}
