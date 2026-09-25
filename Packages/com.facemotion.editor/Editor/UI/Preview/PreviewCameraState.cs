using UnityEngine;

namespace FaceMotion.Editor.UI.Preview
{
    /// <summary>Camera navigation and framing for the isolated preview clone.</summary>
    public sealed class PreviewCameraState
    {
        private const float MinimumDistance = 0.01f;
        private const float MaximumDistance = 10000f;

        public Vector3 Pivot { get; private set; }
        public float Distance { get; private set; } = 2.2f;
        public float Yaw { get; private set; }
        public float Pitch { get; private set; }

        public void Reset(Bounds bounds)
        {
            Pivot = bounds.center;
            Yaw = 0f;
            Pitch = 0f;
            Distance = Mathf.Max(1f, bounds.extents.magnitude * 2.5f);
        }

        public void Fit(Bounds bounds, float verticalFieldOfView, float aspect)
        {
            Pivot = bounds.center;
            float radius = Mathf.Max(0.01f, bounds.extents.magnitude);
            float verticalHalfAngle = Mathf.Deg2Rad * Mathf.Clamp(verticalFieldOfView, 1f, 179f) * 0.5f;
            float horizontalHalfAngle = Mathf.Atan(Mathf.Tan(verticalHalfAngle) * Mathf.Max(0.01f, aspect));
            float limitingHalfAngle = Mathf.Min(verticalHalfAngle, horizontalHalfAngle);
            Distance = Mathf.Clamp(radius / Mathf.Sin(limitingHalfAngle) * 1.2f, MinimumDistance, MaximumDistance);
        }

        public void Orbit(Vector2 delta)
        {
            Yaw += delta.x * 0.35f;
            Pitch = Mathf.Clamp(Pitch - delta.y * 0.35f, -80f, 80f);
        }

        public void Pan(Vector2 delta, float viewportHeight)
        {
            Quaternion rotation = Quaternion.Euler(Pitch, Yaw, 0f);
            float unitsPerPixel = Distance * 1.5f / Mathf.Max(1f, viewportHeight);
            Pivot += (rotation * Vector3.right) * (delta.x * unitsPerPixel);
            Pivot += (rotation * Vector3.up) * (delta.y * unitsPerPixel);
        }

        public void Zoom(float scrollDelta)
        {
            Distance = Mathf.Clamp(Distance * Mathf.Exp(scrollDelta * 0.12f), MinimumDistance, MaximumDistance);
        }

        public void ApplyTo(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            Quaternion rotation = Quaternion.Euler(Pitch, Yaw, 0f);
            camera.transform.position = Pivot + rotation * new Vector3(0f, 0f, Distance);
            camera.transform.rotation = Quaternion.LookRotation(Pivot - camera.transform.position, Vector3.up);
            camera.nearClipPlane = Mathf.Max(0.001f, Distance / 1000f);
            camera.farClipPlane = Mathf.Max(100f, Distance * 10f);
        }

        public static bool TryCalculateBounds(GameObject root, out Bounds bounds)
        {
            bounds = new Bounds();
            if (root == null)
            {
                return false;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool found = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                {
                    continue;
                }

                if (!found)
                {
                    bounds = renderers[i].bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }

            if (!found)
            {
                bounds = new Bounds(root.transform.position, Vector3.one);
            }

            return found;
        }
    }
}
