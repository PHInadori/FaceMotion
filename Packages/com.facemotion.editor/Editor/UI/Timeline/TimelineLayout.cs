using System;
using System.Collections.Generic;
using FaceMotion.Avatar;
using FaceMotion.Data;
using FaceMotion.Timeline;
using UnityEngine;

namespace FaceMotion.Editor.UI.Timeline
{
    /// <summary>One horizontal lane in the timeline.</summary>
    public sealed class TimelineRow
    {
        public FaceTrackData Track;

        public float Y;

        public float Height;

        /// <summary>Binding status for this track, or null when no avatar index is active.</summary>
        public TrackBindingValidation Binding;
    }

    /// <summary>
    /// Captures the track lanes of the currently selected animation plus the geometry that is
    /// stable for the frame the layout was built in. Key pixel positions are derived live from
    /// ScrollTime / PixelsPerSecond so one snapshot serves rendering and hit testing.
    /// </summary>
    public sealed class TimelineLayoutSnapshot
    {
        public readonly List<TimelineRow> Rows = new List<TimelineRow>();

        public float ScrollTime;

        public float PixelsPerSecond;

        public float Duration;

        public float LabelWidth;

        public float PlotLeft => LabelWidth;

        public int RowCount => Rows.Count;

        public float ValidHeight => Rows.Count * TimelineGeometry.RowHeight;
    }

    public static class TimelineLayoutBuilder
    {
        public static TimelineLayoutSnapshot Build(
            FaceMotionAnimationData animation,
            IReadOnlyList<TrackBindingValidation> bindings,
            float labelWidth,
            float rowsTop,
            float scrollTime,
            float zoom,
            float duration)
        {
            var snapshot = new TimelineLayoutSnapshot
            {
                LabelWidth = labelWidth,
                ScrollTime = scrollTime,
                PixelsPerSecond = TimelineGeometry.PixelsPerSecond(zoom),
                Duration = duration
            };

            if (animation == null || animation.Timeline == null)
            {
                return snapshot;
            }

            Dictionary<string, TrackBindingValidation> bindingByTrack = null;
            if (bindings != null && bindings.Count > 0)
            {
                bindingByTrack = new Dictionary<string, TrackBindingValidation>(bindings.Count, StringComparer.Ordinal);
                for (int i = 0; i < bindings.Count; i++)
                {
                    if (bindings[i] != null && bindings[i].TrackId != null)
                    {
                        bindingByTrack[bindings[i].TrackId] = bindings[i];
                    }
                }
            }

            float y = rowsTop;
            foreach (var track in animation.Timeline.Tracks)
            {
                if (track == null)
                {
                    continue;
                }

                TrackBindingValidation binding = null;
                if (bindingByTrack != null && track.TrackId != null
                    && bindingByTrack.TryGetValue(track.TrackId, out var found))
                {
                    binding = found;
                }

                snapshot.Rows.Add(new TimelineRow
                {
                    Track = track,
                    Y = y,
                    Height = TimelineGeometry.RowHeight,
                    Binding = binding
                });

                y += TimelineGeometry.RowHeight;
            }

            return snapshot;
        }
    }

    /// <summary>Pure hit testing shared by the input handler and the view.</summary>
    public static class TimelineHitTest
    {
        // Keep marker rendering compact while providing a small, deliberate pointer cushion.
        public const float KeyHitRadiusPixels = 10f;

        public static TimelineRow FindRowAt(TimelineLayoutSnapshot layout, float y)
        {
            if (layout == null)
            {
                return null;
            }

            for (int i = 0; i < layout.Rows.Count; i++)
            {
                var row = layout.Rows[i];
                bool includesBottom = i == layout.Rows.Count - 1;
                if (y >= row.Y &&
                    (y < row.Y + row.Height ||
                     (includesBottom && y <= row.Y + row.Height)))
                {
                    return row;
                }
            }

            return null;
        }

        public static bool TryFindKeyAt(
            TimelineLayoutSnapshot layout,
            Rect plotRect,
            float x,
            float y,
            out TimelineRow row,
            out string keyId,
            out float time)
        {
            row = null;
            keyId = null;
            time = 0f;
            if (layout == null ||
                x < plotRect.xMin || x > plotRect.xMax ||
                y < plotRect.yMin || y > plotRect.yMax)
            {
                return false;
            }

            TimelineRow candidate = FindRowAt(layout, y);
            if (candidate == null || candidate.Track == null)
            {
                return false;
            }

            float pps = layout.PixelsPerSecond;
            float scroll = layout.ScrollTime;
            float nearestDistance = float.MaxValue;
            string nearestKeyId = null;
            float nearestTime = 0f;
            var track = candidate.Track;
            if (track.Kind == TrackKind.BlendShape && track.BlendShape != null)
            {
                foreach (var key in track.BlendShape.Keys)
                {
                    if (key == null)
                    {
                        continue;
                    }

                    ConsiderKey(
                        key.KeyId,
                        key.Time,
                        TimelineGeometry.TimeToPixel(key.Time, scroll, pps, plotRect.xMin),
                        x,
                        ref nearestDistance,
                        ref nearestKeyId,
                        ref nearestTime);
                }
            }
            else if (TrackKinds.IsTransform(track.Kind) && track.Transform != null)
            {
                foreach (var key in track.Transform.Keys)
                {
                    if (key == null)
                    {
                        continue;
                    }

                    ConsiderKey(
                        key.KeyId,
                        key.Time,
                        TimelineGeometry.TimeToPixel(key.Time, scroll, pps, plotRect.xMin),
                        x,
                        ref nearestDistance,
                        ref nearestKeyId,
                        ref nearestTime);
                }
            }

            if (nearestKeyId == null)
            {
                return false;
            }

            row = candidate;
            keyId = nearestKeyId;
            time = nearestTime;
            return true;
        }

        private static void ConsiderKey(
            string candidateKeyId,
            float candidateTime,
            float candidateX,
            float pointerX,
            ref float nearestDistance,
            ref string nearestKeyId,
            ref float nearestTime)
        {
            float distance = Math.Abs(pointerX - candidateX);
            if (distance > KeyHitRadiusPixels)
            {
                return;
            }

            if (distance < nearestDistance ||
                (Math.Abs(distance - nearestDistance) < 0.001f &&
                 string.CompareOrdinal(candidateKeyId, nearestKeyId) < 0))
            {
                nearestDistance = distance;
                nearestKeyId = candidateKeyId;
                nearestTime = candidateTime;
            }
        }
    }
}
