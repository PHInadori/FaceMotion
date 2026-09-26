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
        public const float MinimumFitPaddingSeconds = 0.5f;
        public const float MaximumFitPaddingSeconds = 1f;
        public const float FitPaddingRatio = 0.1f;

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

        public static float FitPadding(float duration)
        {
            return Mathf.Clamp(duration * FitPaddingRatio, MinimumFitPaddingSeconds, MaximumFitPaddingSeconds);
        }

        public static Vector2 FitVisibleRange(float duration)
        {
            float validDuration = Mathf.Max(0f, duration);
            return new Vector2(0f, validDuration + FitPadding(validDuration));
        }

        /// <summary>Clamps scroll to the editable duration plus its trailing visual context.</summary>
        public static float ClampScrollTime(float scrollTime, float duration, float plotWidth, float pixelsPerSecond)
        {
            float visible = VisibleDuration(plotWidth, pixelsPerSecond);
            float maxScroll = Mathf.Max(0f, FitVisibleRange(duration).y - visible);
            return Mathf.Clamp(scrollTime, 0f, maxScroll);
        }

        /// <summary>Zoom that fits the editable duration and trailing context into the plot width.</summary>
        public static float FitZoom(float duration, float plotWidth)
        {
            float fitDuration = FitVisibleRange(duration).y;
            if (fitDuration <= 0f || plotWidth <= 0f)
            {
                return MinZoom;
            }

            return TimelineViewState.ClampZoom((plotWidth / fitDuration) / BasePixelsPerSecond);
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

        /// <summary>Returns the first finite tick on <paramref name="step"/>'s lattice at or after the visible start.</summary>
        public static float FirstVisibleTick(float scrollTime, float step)
        {
            if (step <= 0f || float.IsNaN(step) || float.IsInfinity(step))
            {
                return 0f;
            }

            float start = float.IsNaN(scrollTime) || float.IsInfinity(scrollTime) ? 0f : Mathf.Max(0f, scrollTime);
            float tick = Mathf.Ceil((start / step) - 1e-4f) * step;
            return float.IsNaN(tick) || float.IsInfinity(tick) ? 0f : tick;
        }

        public static bool IsMajorTick(float time, float majorStep)
        {
            if (majorStep <= 0f || float.IsNaN(time) || float.IsInfinity(time))
            {
                return false;
            }

            return Mathf.Abs(time - Mathf.Round(time / majorStep) * majorStep) <= Mathf.Max(1e-4f, majorStep * 1e-4f);
        }
    }
}
