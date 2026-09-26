using System;
using System.Collections.Generic;
using UnityEngine;

namespace FaceMotion.Editor.UI.Support
{
    /// <summary>Shared bounds policy for editable timeline time.</summary>
    public static class TimelineTimeDomain
    {
        public static bool IsEditable(float time, float duration)
        {
            return IsFinite(time) && IsFinite(duration) && duration > 0f && time >= 0f && time <= duration;
        }

        public static float Clamp(float time, float duration)
        {
            return duration > 0f && IsFinite(duration) && IsFinite(time)
                ? Mathf.Clamp(time, 0f, duration)
                : 0f;
        }

        public static float ConstrainGroupDelta(
            IReadOnlyList<float> times,
            float proposedDelta,
            float duration)
        {
            if (times == null || times.Count == 0 || !IsFinite(proposedDelta) || duration <= 0f || !IsFinite(duration))
            {
                return 0f;
            }

            float earliest = float.PositiveInfinity;
            float latest = float.NegativeInfinity;
            for (int i = 0; i < times.Count; i++)
            {
                if (!IsFinite(times[i]))
                {
                    continue;
                }

                earliest = Mathf.Min(earliest, times[i]);
                latest = Mathf.Max(latest, times[i]);
            }

            return ConstrainGroupDelta(earliest, latest, proposedDelta, duration);
        }

        public static float ConstrainGroupDelta(
            float earliest,
            float latest,
            float proposedDelta,
            float duration)
        {
            if (!IsFinite(earliest) || !IsFinite(latest) || earliest > latest || !IsFinite(proposedDelta) || duration <= 0f || !IsFinite(duration))
            {
                return 0f;
            }

            return Mathf.Clamp(proposedDelta, -earliest, duration - latest);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
