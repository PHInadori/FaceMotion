using System;
using System.Collections.Generic;
using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Timeline;
using UnityEngine;

namespace FaceMotion.Editor
{
    /// <summary>Identifies one key through its owning track and stable key ID.</summary>
    public struct KeyTarget
    {
        public KeyTarget(string trackId, string keyId)
        {
            TrackId = trackId ?? string.Empty;
            KeyId = keyId ?? string.Empty;
        }

        public string TrackId;

        public string KeyId;
    }

    /// <summary>Removes multiple keys from one animation in a single transaction.</summary>
    public sealed class RemoveKeysCommand : IProjectCommand
    {
        private readonly string _animationId;
        private readonly IReadOnlyList<KeyTarget> _targets;

        public RemoveKeysCommand(string animationId, IReadOnlyList<KeyTarget> targets)
        {
            _animationId = animationId;
            _targets = targets ?? new List<KeyTarget>();
        }

        public string UndoLabel => "Remove Keys";

        public bool Validate(FaceMotionProject project, out FaceMotionDiagnostic error)
        {
            if (!project.TryGetAnimation(_animationId, out var animation) || animation.Timeline == null)
            {
                error = CommandDiagnostics.TargetNotFound("animation", _animationId);
                return false;
            }

            for (int i = 0; i < _targets.Count; i++)
            {
                if (!animation.Timeline.TryGetTrack(_targets[i].TrackId, out var track) || !HasKey(track, _targets[i].KeyId))
                {
                    error = CommandDiagnostics.TargetNotFound("key", _targets[i].KeyId);
                    return false;
                }
            }

            error = null;
            return true;
        }

        public void Execute(FaceMotionProject project)
        {
            if (!project.TryGetAnimation(_animationId, out var animation) || animation.Timeline == null)
            {
                return;
            }

            for (int i = 0; i < _targets.Count; i++)
            {
                if (animation.Timeline.TryGetTrack(_targets[i].TrackId, out var track))
                {
                    RemoveFromTrack(track, _targets[i].KeyId);
                }
            }
        }

        private static bool HasKey(FaceTrackData track, string keyId)
        {
            if (track.Kind == TrackKind.BlendShape && track.BlendShape != null)
            {
                return FindFloatKey(track, keyId) >= 0;
            }

            if (TrackKinds.IsTransform(track.Kind) && track.Transform != null)
            {
                return FindVector3Key(track, keyId) >= 0;
            }

            return false;
        }

        private static void RemoveFromTrack(FaceTrackData track, string keyId)
        {
            if (track.Kind == TrackKind.BlendShape && track.BlendShape != null)
            {
                track.BlendShape.RemoveKey(keyId);
            }
            else if (TrackKinds.IsTransform(track.Kind) && track.Transform != null)
            {
                track.Transform.RemoveKey(keyId);
            }
        }

        private static int FindFloatKey(FaceTrackData track, string keyId)
        {
            var keys = track.BlendShape.Keys;
            for (int i = 0; i < keys.Count; i++)
            {
                if (keys[i] != null && string.Equals(keys[i].KeyId, keyId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static int FindVector3Key(FaceTrackData track, string keyId)
        {
            var keys = track.Transform.Keys;
            for (int i = 0; i < keys.Count; i++)
            {
                if (keys[i] != null && string.Equals(keys[i].KeyId, keyId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }
    }

    /// <summary>Moves one or more keys of one track to given times. Times are absolute and
    /// sorted afterward; caller handles snapping and duration clamping.</summary>
    public sealed class MoveKeysToTimesCommand : IProjectCommand
    {
        private readonly string _animationId;
        private readonly string _trackId;
        private readonly IReadOnlyList<string> _keyIds;
        private readonly IReadOnlyList<float> _times;

        public MoveKeysToTimesCommand(string animationId, string trackId, IReadOnlyList<string> keyIds, IReadOnlyList<float> times)
        {
            _animationId = animationId;
            _trackId = trackId;
            _keyIds = keyIds ?? new List<string>();
            _times = times ?? new List<float>();
        }

        public string UndoLabel => "Move Keys";

        public bool Validate(FaceMotionProject project, out FaceMotionDiagnostic error)
        {
            if (_keyIds.Count != _times.Count)
            {
                error = CommandDiagnostics.InvalidArgument("Key and time counts do not match.", _trackId);
                return false;
            }

            for (int i = 0; i < _times.Count; i++)
            {
                if (!IsValidTime(_times[i]))
                {
                    error = CommandDiagnostics.InvalidArgument("Key time must be a finite non-negative value.", _trackId);
                    return false;
                }
            }

            if (!project.TryGetAnimation(_animationId, out var animation) || animation.Timeline == null
                || !animation.Timeline.TryGetTrack(_trackId, out var track))
            {
                error = CommandDiagnostics.TargetNotFound("track", _trackId);
                return false;
            }

            for (int i = 0; i < _keyIds.Count; i++)
            {
                if (!HasKey(track, _keyIds[i]))
                {
                    error = CommandDiagnostics.TargetNotFound("key", _keyIds[i]);
                    return false;
                }
            }

            error = null;
            return true;
        }

        public void Execute(FaceMotionProject project)
        {
            if (!project.TryGetAnimation(_animationId, out var animation) || animation.Timeline == null
                || !animation.Timeline.TryGetTrack(_trackId, out var track))
            {
                return;
            }

            TrackKeyMover.Move(track, _keyIds, _times);
        }

        private static bool HasKey(FaceTrackData track, string keyId)
        {
            if (track.Kind == TrackKind.BlendShape && track.BlendShape != null)
            {
                var keys = track.BlendShape.Keys;
                for (int i = 0; i < keys.Count; i++)
                {
                    if (keys[i] != null && string.Equals(keys[i].KeyId, keyId, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
            else if (TrackKinds.IsTransform(track.Kind) && track.Transform != null)
            {
                var keys = track.Transform.Keys;
                for (int i = 0; i < keys.Count; i++)
                {
                    if (keys[i] != null && string.Equals(keys[i].KeyId, keyId, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsValidTime(float time)
        {
            return !float.IsNaN(time) && !float.IsInfinity(time) && time >= 0f;
        }
    }

    /// <summary>Sets one float key's value.</summary>
    public sealed class UpdateFloatKeyValueCommand : IProjectCommand
    {
        private readonly string _animationId;
        private readonly string _trackId;
        private readonly string _keyId;
        private readonly float _value;

        public UpdateFloatKeyValueCommand(string animationId, string trackId, string keyId, float value)
        {
            _animationId = animationId;
            _trackId = trackId;
            _keyId = keyId;
            _value = value;
        }

        public string UndoLabel => "Edit Value";

        public bool Validate(FaceMotionProject project, out FaceMotionDiagnostic error)
        {
            if (float.IsNaN(_value) || float.IsInfinity(_value))
            {
                error = CommandDiagnostics.InvalidArgument("Key value must be finite.", _trackId);
                return false;
            }

            if (!TryGetTrack(project, out var track))
            {
                error = CommandDiagnostics.TargetNotFound("track", _trackId);
                return false;
            }

            if (track.Kind != TrackKind.BlendShape || track.BlendShape == null || FindFloatKey(track) < 0)
            {
                error = CommandDiagnostics.TargetNotFound("key", _keyId);
                return false;
            }

            error = null;
            return true;
        }

        public void Execute(FaceMotionProject project)
        {
            if (TryGetTrack(project, out var track) && track.BlendShape != null)
            {
                var keys = (System.Collections.Generic.List<FloatKeyframeData>)track.BlendShape.Keys;
                int index = FindFloatKey(track);
                if (index >= 0)
                {
                    keys[index].Value = _value;
                }
            }
        }

        private bool TryGetTrack(FaceMotionProject project, out FaceTrackData track)
        {
            track = null;
            return project.TryGetAnimation(_animationId, out var animation) && animation.Timeline != null
                && animation.Timeline.TryGetTrack(_trackId, out track);
        }

        private int FindFloatKey(FaceTrackData track)
        {
            var keys = track.BlendShape.Keys;
            for (int i = 0; i < keys.Count; i++)
            {
                if (keys[i] != null && string.Equals(keys[i].KeyId, _keyId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }
    }

    /// <summary>Sets one vector3 key's value.</summary>
    public sealed class UpdateVector3KeyValueCommand : IProjectCommand
    {
        private readonly string _animationId;
        private readonly string _trackId;
        private readonly string _keyId;
        private readonly Vector3 _value;

        public UpdateVector3KeyValueCommand(string animationId, string trackId, string keyId, Vector3 value)
        {
            _animationId = animationId;
            _trackId = trackId;
            _keyId = keyId;
            _value = value;
        }

        public string UndoLabel => "Edit Value";

        public bool Validate(FaceMotionProject project, out FaceMotionDiagnostic error)
        {
            if (!IsFinite(_value))
            {
                error = CommandDiagnostics.InvalidArgument("Key value must be finite.", _trackId);
                return false;
            }

            if (!TryGetTrack(project, out var track))
            {
                error = CommandDiagnostics.TargetNotFound("track", _trackId);
                return false;
            }

            if (!TrackKinds.IsTransform(track.Kind) || track.Transform == null || FindVector3Key(track) < 0)
            {
                error = CommandDiagnostics.TargetNotFound("key", _keyId);
                return false;
            }

            error = null;
            return true;
        }

        public void Execute(FaceMotionProject project)
        {
            if (TryGetTrack(project, out var track) && track.Transform != null)
            {
                var keys = (System.Collections.Generic.List<Vector3KeyframeData>)track.Transform.Keys;
                int index = FindVector3Key(track);
                if (index >= 0)
                {
                    keys[index].Value = _value;
                }
            }
        }

        private bool TryGetTrack(FaceMotionProject project, out FaceTrackData track)
        {
            track = null;
            return project.TryGetAnimation(_animationId, out var animation) && animation.Timeline != null
                && animation.Timeline.TryGetTrack(_trackId, out track);
        }

        private int FindVector3Key(FaceTrackData track)
        {
            var keys = track.Transform.Keys;
            for (int i = 0; i < keys.Count; i++)
            {
                if (keys[i] != null && string.Equals(keys[i].KeyId, _keyId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }

    /// <summary>Sets one key's interpolation mode.</summary>
    public sealed class SetKeyInterpolationCommand : IProjectCommand
    {
        private readonly string _animationId;
        private readonly string _trackId;
        private readonly string _keyId;
        private readonly InterpolationType _interpolation;

        public SetKeyInterpolationCommand(string animationId, string trackId, string keyId, InterpolationType interpolation)
        {
            _animationId = animationId;
            _trackId = trackId;
            _keyId = keyId;
            _interpolation = interpolation;
        }

        public string UndoLabel => "Edit Interpolation";

        public bool Validate(FaceMotionProject project, out FaceMotionDiagnostic error)
        {
            if (!project.TryGetAnimation(_animationId, out var animation) || animation.Timeline == null
                || !animation.Timeline.TryGetTrack(_trackId, out var track))
            {
                error = CommandDiagnostics.TargetNotFound("track", _trackId);
                return false;
            }

            if (!HasKey(track))
            {
                error = CommandDiagnostics.TargetNotFound("key", _keyId);
                return false;
            }

            error = null;
            return true;
        }

        public void Execute(FaceMotionProject project)
        {
            if (!project.TryGetAnimation(_animationId, out var animation) || animation.Timeline == null
                || !animation.Timeline.TryGetTrack(_trackId, out var track))
            {
                return;
            }

            if (track.Kind == TrackKind.BlendShape && track.BlendShape != null)
            {
                var keys = track.BlendShape.Keys;
                for (int i = 0; i < keys.Count; i++)
                {
                    if (keys[i] != null && string.Equals(keys[i].KeyId, _keyId, StringComparison.Ordinal))
                    {
                        keys[i].Interpolation = _interpolation;
                        return;
                    }
                }
            }
            else if (TrackKinds.IsTransform(track.Kind) && track.Transform != null)
            {
                var keys = track.Transform.Keys;
                for (int i = 0; i < keys.Count; i++)
                {
                    if (keys[i] != null && string.Equals(keys[i].KeyId, _keyId, StringComparison.Ordinal))
                    {
                        keys[i].Interpolation = _interpolation;
                        return;
                    }
                }
            }
        }

        private bool HasKey(FaceTrackData track)
        {
            if (track.Kind == TrackKind.BlendShape && track.BlendShape != null)
            {
                var keys = track.BlendShape.Keys;
                for (int i = 0; i < keys.Count; i++)
                {
                    if (keys[i] != null && string.Equals(keys[i].KeyId, _keyId, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
            else if (TrackKinds.IsTransform(track.Kind) && track.Transform != null)
            {
                var keys = track.Transform.Keys;
                for (int i = 0; i < keys.Count; i++)
                {
                    if (keys[i] != null && string.Equals(keys[i].KeyId, _keyId, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Describes one paste destination. A spec with an empty <see cref="TargetKeyId"/>
    /// inserts a fresh key; a spec carrying an existing key ID overwrites that key's
    /// value and interpolation in place while preserving its identity.
    /// </summary>
    public struct PasteKeySpec
    {
        public string TrackId;
        public TrackKind Kind;
        public float Time;
        public float FloatValue;
        public Vector3 VectorValue;
        public InterpolationType Interpolation;
        public string TargetKeyId;
    }

    /// <summary>
    /// Pastes keys into one animation in a single transaction: every spec without a
    /// target inserts a fresh key, every spec with a target overwrites the existing key
    /// that already occupies that track and timestamp. The whole plan validates before
    /// any mutation, so a rejected paste never changes the project. All resulting key IDs
    /// (inserted and overwritten) are exposed in spec order for selection.
    /// </summary>
    public sealed class PasteKeysCommand : IProjectCommand
    {
        private const float TimeTolerance = 1e-4f;

        private readonly string _animationId;
        private readonly IReadOnlyList<PasteKeySpec> _specs;

        public PasteKeysCommand(string animationId, IReadOnlyList<PasteKeySpec> specs)
        {
            _animationId = animationId;
            _specs = specs ?? new List<PasteKeySpec>();
        }

        /// <summary>Resulting key IDs in spec order: inserted fresh IDs and preserved overwrite IDs.</summary>
        public IReadOnlyList<string> ResultKeyIds { get; private set; }

        public string UndoLabel => "Paste Keys";

        public bool Validate(FaceMotionProject project, out FaceMotionDiagnostic error)
        {
            if (!project.TryGetAnimation(_animationId, out var animation) || animation.Timeline == null)
            {
                error = CommandDiagnostics.TargetNotFound("animation", _animationId);
                return false;
            }

            for (int i = 0; i < _specs.Count; i++)
            {
                var spec = _specs[i];
                if (!animation.Timeline.TryGetTrack(spec.TrackId, out var track))
                {
                    error = CommandDiagnostics.TargetNotFound("track", spec.TrackId);
                    return false;
                }

                if (track.Kind != spec.Kind)
                {
                    error = CommandDiagnostics.KindMismatch("The paste target kind does not match the clipboard.", spec.TrackId);
                    return false;
                }

                if (!IsValidTime(spec.Time))
                {
                    error = CommandDiagnostics.InvalidArgument("Pastable key time must be a finite non-negative value.", spec.TrackId);
                    return false;
                }

                if (!string.IsNullOrEmpty(spec.TargetKeyId) && !HasKey(track, spec.TargetKeyId))
                {
                    error = CommandDiagnostics.TargetNotFound("key", spec.TargetKeyId);
                    return false;
                }

                for (int j = i + 1; j < _specs.Count; j++)
                {
                    var other = _specs[j];
                    if (string.Equals(other.TrackId, spec.TrackId, StringComparison.Ordinal)
                        && Math.Abs(other.Time - spec.Time) <= TimeTolerance)
                    {
                        error = CommandDiagnostics.InvalidArgument(
                            "The paste plan maps two keys onto the same track and timestamp.",
                            spec.TrackId);
                        return false;
                    }
                }
            }

            error = null;
            return true;
        }

        public void Execute(FaceMotionProject project)
        {
            var results = new List<string>();
            if (project.TryGetAnimation(_animationId, out var animation) && animation.Timeline != null)
            {
                var sortedTracks = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < _specs.Count; i++)
                {
                    var spec = _specs[i];
                    if (!animation.Timeline.TryGetTrack(spec.TrackId, out var track) || track.Kind != spec.Kind)
                    {
                        continue;
                    }

                    if (spec.Kind == TrackKind.BlendShape && track.BlendShape != null)
                    {
                        var existing = FindFloatKey(track, spec.TargetKeyId);
                        if (existing != null)
                        {
                            existing.Value = spec.FloatValue;
                            existing.Interpolation = spec.Interpolation;
                            results.Add(existing.KeyId);
                        }
                        else if (string.IsNullOrEmpty(spec.TargetKeyId))
                        {
                            var key = FloatKeyframeData.Create(spec.Time, spec.FloatValue, spec.Interpolation);
                            track.BlendShape.AddKey(key);
                            results.Add(key.KeyId);
                        }
                        else
                        {
                            continue;
                        }
                    }
                    else if (TrackKinds.IsTransform(spec.Kind) && track.Transform != null)
                    {
                        var existing = FindVector3Key(track, spec.TargetKeyId);
                        if (existing != null)
                        {
                            existing.Value = spec.VectorValue;
                            existing.Interpolation = spec.Interpolation;
                            results.Add(existing.KeyId);
                        }
                        else if (string.IsNullOrEmpty(spec.TargetKeyId))
                        {
                            var key = Vector3KeyframeData.Create(spec.Time, spec.VectorValue, spec.Interpolation);
                            track.Transform.AddKey(key);
                            results.Add(key.KeyId);
                        }
                        else
                        {
                            continue;
                        }
                    }
                    else
                    {
                        continue;
                    }

                    sortedTracks.Add(spec.TrackId);
                }

                foreach (var trackId in sortedTracks)
                {
                    if (animation.Timeline.TryGetTrack(trackId, out var track))
                    {
                        if (track.Kind == TrackKind.BlendShape && track.BlendShape != null)
                        {
                            track.BlendShape.SortKeys();
                        }
                        else if (TrackKinds.IsTransform(track.Kind) && track.Transform != null)
                        {
                            track.Transform.SortKeys();
                        }
                    }
                }
            }

            ResultKeyIds = results;
        }

        private static bool HasKey(FaceTrackData track, string keyId)
        {
            return FindFloatKey(track, keyId) != null || FindVector3Key(track, keyId) != null;
        }

        private static FloatKeyframeData FindFloatKey(FaceTrackData track, string keyId)
        {
            if (string.IsNullOrEmpty(keyId) || track.Kind != TrackKind.BlendShape || track.BlendShape == null)
            {
                return null;
            }

            var keys = track.BlendShape.Keys;
            for (int i = 0; i < keys.Count; i++)
            {
                if (keys[i] != null && string.Equals(keys[i].KeyId, keyId, StringComparison.Ordinal))
                {
                    return keys[i];
                }
            }

            return null;
        }

        private static Vector3KeyframeData FindVector3Key(FaceTrackData track, string keyId)
        {
            if (string.IsNullOrEmpty(keyId) || !TrackKinds.IsTransform(track.Kind) || track.Transform == null)
            {
                return null;
            }

            var keys = track.Transform.Keys;
            for (int i = 0; i < keys.Count; i++)
            {
                if (keys[i] != null && string.Equals(keys[i].KeyId, keyId, StringComparison.Ordinal))
                {
                    return keys[i];
                }
            }

            return null;
        }

        private static bool IsValidTime(float time)
        {
            return !float.IsNaN(time) && !float.IsInfinity(time) && time >= 0f;
        }
    }
}