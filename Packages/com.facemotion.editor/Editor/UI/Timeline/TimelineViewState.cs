using UnityEngine;

namespace FaceMotion.Editor.UI.Timeline
{
    /// <summary>Current input interaction being handled by the timeline.</summary>
    public enum TimelineDragMode
    {
        None = 0,
        Scrub = 1,
        MoveKeys = 2,
        BoxSelect = 3,
        Pan = 4
    }

    /// <summary>
    /// Editor-only timeline view state. Never saved into a FaceMotion project; the window
    /// may persist a few fields through EditorPrefs for domain reload continuity.
    /// </summary>
    public sealed class TimelineViewState
    {
        public const float MinZoom = 0.5f;
        public const float MaxZoom = 20f;
        public const float BasePixelsPerSecond = 120f;

        private float _zoom = 1f;

        /// <summary>Zoom multiplier; 1 means BasePixelsPerSecond pixels per second.</summary>
        public float Zoom
        {
            get => _zoom;
            set => _zoom = ClampZoom(value);
        }

        /// <summary>Time at the left edge of the plot area.</summary>
        public float ScrollTime { get; set; }

        public float CurrentTime { get; set; }

        public bool SnapEnabled { get; set; } = true;

        public bool IsPlaying { get; set; }

        /// <summary>EditorApplication.timeSinceStartup captured on the last playback tick.</summary>
        public double LastUpdateRealtime { get; set; }

        public string HoveredKeyId { get; set; }

        public TimelineDragMode DragMode { get; set; }

        public float PixelsPerSecond => BasePixelsPerSecond * _zoom;

        public static float ClampZoom(float zoom)
        {
            if (float.IsNaN(zoom) || float.IsInfinity(zoom))
            {
                return 1f;
            }

            return Mathf.Clamp(zoom, MinZoom, MaxZoom);
        }
    }
}