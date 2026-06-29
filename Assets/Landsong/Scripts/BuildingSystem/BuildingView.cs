using System;
using UnityEngine;

namespace Landsong.BuildingSystem
{
    [DisallowMultipleComponent]
    public sealed class BuildingView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer[] spriteRenderers = Array.Empty<SpriteRenderer>();
        [SerializeField] private Color placementPreviewColor = new Color(1f, 1f, 1f, 0.45f);

        private Color[] originalColors = Array.Empty<Color>();
        private bool originalColorsCaptured;
        private bool isPlacementPreview;

        public bool IsPlacementPreview => isPlacementPreview;

        public void SetPlacementPreview(bool enabled)
        {
            EnsureSpriteRenderers();
            if (spriteRenderers.Length == 0)
            {
                isPlacementPreview = enabled;
                return;
            }

            if (enabled)
            {
                CaptureOriginalColors();
                ApplyPlacementPreviewColor();
            }
            else
            {
                RestoreOriginalColors();
                originalColors = Array.Empty<Color>();
                originalColorsCaptured = false;
            }

            isPlacementPreview = enabled;
        }

        public void SetPlacementPreviewColor(Color color)
        {
            placementPreviewColor = color;
            if (isPlacementPreview)
            {
                ApplyPlacementPreviewColor();
            }
        }

        private void Reset()
        {
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        private void OnValidate()
        {
            if (spriteRenderers == null || spriteRenderers.Length == 0)
            {
                spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            }
        }

        private void EnsureSpriteRenderers()
        {
            if (HasAnySpriteRenderer())
            {
                return;
            }

            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        private bool HasAnySpriteRenderer()
        {
            if (spriteRenderers == null)
            {
                return false;
            }

            for (var i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private void CaptureOriginalColors()
        {
            if (originalColorsCaptured && originalColors.Length == spriteRenderers.Length)
            {
                return;
            }

            originalColors = new Color[spriteRenderers.Length];
            for (var i = 0; i < spriteRenderers.Length; i++)
            {
                originalColors[i] = spriteRenderers[i] == null ? Color.white : spriteRenderers[i].color;
            }

            originalColorsCaptured = true;
        }

        private void ApplyPlacementPreviewColor()
        {
            for (var i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] != null)
                {
                    spriteRenderers[i].color = placementPreviewColor;
                }
            }
        }

        private void RestoreOriginalColors()
        {
            if (!originalColorsCaptured)
            {
                return;
            }

            for (var i = 0; i < spriteRenderers.Length && i < originalColors.Length; i++)
            {
                if (spriteRenderers[i] != null)
                {
                    spriteRenderers[i].color = originalColors[i];
                }
            }
        }
    }
}
