using UnityEngine;

namespace FaceMotion.Editor.UI.Timeline
{
    /// <summary>
    /// Pure time/pixel geometry for the timeline. All mapping between seconds and screen
    /// pixels lives here so the renderer, input handler, and tests share one implementation.
    /// </summary>
    public static class TimelineGeometry
    {
        public const float MinZoom = 0.5f;
        public const float MaxZoom = 20f;
        public const float BasePixelsPerSecond = 120f;
        public const float DefaultLabelWidth = 180f;
        public const float RowHeight = 22f;
        public const float RulerHeight = 24f;

        private static readonly float[] RulerSteps =
        {
            0.001f, 0.002f, 0.005f, 0.01f, 0.02f, 0.05f, 0.1f, 0.2f,
            0.5f, 1f, 2f, 5f, 10f, 15f, 30f, 60f, 120f, 300f, 600f
        };

        public static float PixelsPerSecond(float zoom)
        {
            return BasePixelsPerSecond * TimelineViewState.ClampZoom(zoom);
        }

        public static float TimeToPixel(float time, float scrollTime, float pixelsPerSecond, float plotLeft)
        {
            return plotLeft + (time - scrollTime) * pixelsPerSecond;
        }

        public static float PixelToTime(float x, float scrollTime, float pixelsPerSecond, float plotLeft)
        {
            return scrollTime + (x - plotLeft) / pixelsPerSecond;
        }

        public static float VisibleDuration(float plotWidth, float pixelsPerSecond)
        {
            if (pixelsPerSecond <= 0f)
            {
                return 1f;
            }

            return Mathf.Max(0.001f, plotWidth / pixelsPerSecond);
        }

        /// <summary>Clamps the left-edge scroll time so content cannot scroll past its end.</summary>
        public static float ClampScrollTime(float scrollTime, float duration, float plotWidth, float pixelsPerSecond)
        {
            float visible = VisibleDuration(plotWidth, pixelsPerSecond);
            float maxScroll = Mathf.Max(0f, duration - visible);
            return Mathf.Clamp(scrollTime, 0f, maxScroll);
        }

        /// <summary>Zoom that fits the whole duration into the plot width (clamped to the allowed range).</summary>
        public static float FitZoom(float duration, float plotWidth)
        {
            if (duration <= 0f || plotWidth <= 0f)
            {
                return MinZoom;
            }

            return TimelineViewState.ClampZoom((plotWidth / duration) / BasePixelsPerSecond);
        }

        /// <summary>
        /// Computes the scroll time after a zoom change that keeps the time under the anchor
        /// pixel stationary.
        /// </summary>
        public static float ComputeZoomedScroll(float newZoom, float anchorPixelX, float anchorTime, float plotLeft)
        {
            float newPixelsPerSecond = BasePixelsPerSecond * TimelineViewState.ClampZoom(newZoom);
            return anchorTime - (anchorPixelX - plotLeft) / newPixelsPerSecond;
        }

        /// <summary>Major ruler step in seconds whose pixels stay readable at this scale.</summary>
        public static float GetMajorStep(float pixelsPerSecond)
        {
            float chosen = RulerSteps[0];
            for (int i = 0; i < RulerSteps.Length; i++)
            {
                if (RulerSteps[i] * pixelsPerSecond <= 90f)
                {
                    chosen = RulerSteps[i];
                }
                else
                {
                    break;
                }
            }

            return chosen;
        }

        /// <summary>Minor ruler step derived from the major step and the current scale.</summary>
        public static float GetMinorStep(float pixelsPerSecond)
        {
            float major = GetMajorStep(pixelsPerSecond);
            float[] dividers = { 10f, 5f, 4f, 2f, 1f };
            for (int i = 0; i < dividers.Length; i++)
            {
                float minor = major / dividers[i];
                if (minor * pixelsPerSecond >= 14f)
                {
                    return minor;
                }
            }

            return major;
        }
    }
}