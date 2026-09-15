using System;
using System.Collections.Generic;
using FaceMotion.Timeline;

namespace FaceMotion.Editor
{
    /// <summary>
    /// Applies absolute times to keys of one track and re-sorts by ascending time. Shared by
    /// MoveKeysToTimesCommand and the interactive timeline drag path so both stay identical.
    /// </summary>
    public static class TrackKeyMover
    {
        public static void Move(FaceTrackData track, IReadOnlyList<string> keyIds, IReadOnlyList<float> times)
        {
            if (track == null || keyIds == null || times == null || keyIds.Count != times.Count)
            {
                return;
            }

            if (track.Kind == TrackKind.BlendShape && track.BlendShape != null)
            {
                var keys = (List<FloatKeyframeData>)track.BlendShape.Keys;
                for (int i = 0; i < keyIds.Count; i++)
                {
                    SetFloatTime(keys, keyIds[i], times[i]);
                }

                track.BlendShape.SortKeys();
            }
            else if (TrackKinds.IsTransform(track.Kind) && track.Transform != null)
            {
                var keys = (List<Vector3KeyframeData>)track.Transform.Keys;
                for (int i = 0; i < keyIds.Count; i++)
                {
                    SetVector3Time(keys, keyIds[i], times[i]);
                }

                track.Transform.SortKeys();
            }
        }

        private static void SetFloatTime(List<FloatKeyframeData> keys, string keyId, float time)
        {
            for (int i = 0; i < keys.Count; i++)
            {
                if (keys[i] != null && string.Equals(keys[i].KeyId, keyId, StringComparison.Ordinal))
                {
                    keys[i].Time = time;
                    return;
                }
            }
        }

        private static void SetVector3Time(List<Vector3KeyframeData> keys, string keyId, float time)
        {
            for (int i = 0; i < keys.Count; i++)
            {
                if (keys[i] != null && string.Equals(keys[i].KeyId, keyId, StringComparison.Ordinal))
                {
                    keys[i].Time = time;
                    return;
                }
            }
        }
    }
}