using UnityEngine;

namespace Landsong.BuildingSystem
{
    public readonly struct BuildingPointerHit
    {
        public BuildingPointerHit(BuildingBase building, GameObject target, float distance, bool is2D)
        {
            Building = building;
            Target = target;
            Distance = distance < 0f ? 0f : distance;
            Is2D = is2D;
        }

        public BuildingBase Building { get; }
        public GameObject Target { get; }
        public float Distance { get; }
        public bool Is2D { get; }
        public bool IsValid => Building != null || Target != null;
        public string Source => Is2D ? "Physics2D" : "Physics3D";
    }

    public static class BuildingPointerHitUtility
    {
        public static bool TryGetBuilding(Camera camera, Vector2 screenPosition, out BuildingBase building)
        {
            if (TryGetBuildingHit(camera, screenPosition, out BuildingPointerHit hit))
            {
                building = hit.Building;
                return building != null;
            }

            building = null;
            return false;
        }

        public static bool TryGetBuildingHit(Camera camera, Vector2 screenPosition, out BuildingPointerHit hit)
        {
            if (camera == null)
            {
                hit = default;
                return false;
            }

            Ray ray = camera.ScreenPointToRay(screenPosition);
            return TryGetBuildingHit(ray, camera.farClipPlane, out hit);
        }

        public static bool TryGetBuildingHit(Ray ray, float maxDistance, out BuildingPointerHit hit)
        {
            bool hasHit = false;
            hit = default;

            RaycastHit[] hits3D = Physics.RaycastAll(ray, maxDistance);
            for (int i = 0; i < hits3D.Length; i++)
            {
                Collider hitCollider = hits3D[i].collider;
                BuildingBase building = hitCollider == null ? null : hitCollider.GetComponentInParent<BuildingBase>();
                if (building == null || !CanHitBuilding(building))
                {
                    continue;
                }

                BuildingPointerHit candidate = new BuildingPointerHit(
                    building,
                    hitCollider.gameObject,
                    hits3D[i].distance,
                    false);
                if (!hasHit || candidate.Distance < hit.Distance)
                {
                    hit = candidate;
                    hasHit = true;
                }
            }

            RaycastHit2D[] hits2D = Physics2D.GetRayIntersectionAll(ray, maxDistance);
            for (int i = 0; i < hits2D.Length; i++)
            {
                Collider2D hitCollider = hits2D[i].collider;
                BuildingBase building = hitCollider == null ? null : hitCollider.GetComponentInParent<BuildingBase>();
                if (building == null || !CanHitBuilding(building))
                {
                    continue;
                }

                BuildingPointerHit candidate = new BuildingPointerHit(
                    building,
                    hitCollider.gameObject,
                    hits2D[i].distance,
                    true);
                if (!hasHit || candidate.Distance < hit.Distance)
                {
                    hit = candidate;
                    hasHit = true;
                }
            }

            return hasHit;
        }

        public static bool TryHitObjectCollider(GameObject root, Camera camera, Vector2 screenPosition)
        {
            if (root == null || camera == null)
            {
                return false;
            }

            Ray ray = camera.ScreenPointToRay(screenPosition);
            return TryHitObjectCollider(root, ray, camera.farClipPlane);
        }

        public static bool TryHitObjectCollider(GameObject root, Ray ray, float maxDistance)
        {
            if (root == null)
            {
                return false;
            }

            Collider[] colliders3D = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders3D.Length; i++)
            {
                Collider hitCollider = colliders3D[i];
                if (hitCollider != null && hitCollider.enabled && hitCollider.Raycast(ray, out _, maxDistance))
                {
                    return true;
                }
            }

            RaycastHit2D[] hits2D = Physics2D.GetRayIntersectionAll(ray, maxDistance);
            Transform rootTransform = root.transform;
            for (int i = 0; i < hits2D.Length; i++)
            {
                Collider2D hitCollider = hits2D[i].collider;
                if (hitCollider != null
                    && hitCollider.enabled
                    && hitCollider.transform != null
                    && hitCollider.transform.IsChildOf(rootTransform))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool HasEnabledCollider(GameObject root)
        {
            if (root == null)
            {
                return false;
            }

            Collider[] colliders3D = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders3D.Length; i++)
            {
                if (colliders3D[i] != null && colliders3D[i].enabled)
                {
                    return true;
                }
            }

            Collider2D[] colliders2D = root.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders2D.Length; i++)
            {
                if (colliders2D[i] != null && colliders2D[i].enabled)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CanHitBuilding(BuildingBase building)
        {
            return building != null && building.isActiveAndEnabled && !building.IsDemolishing;
        }
    }
}
