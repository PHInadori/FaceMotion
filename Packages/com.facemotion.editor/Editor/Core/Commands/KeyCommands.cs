using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Timeline;
using UnityEngine;

namespace FaceMotion.Editor
{
    /// <summary>Adds a manual float key to a blend shape track.</summary>
    public sealed class AddFloatKeyCommand : IProjectCommand
    {
        private readonly string _animationId;
        private readonly string _trackId;
        private readonly float _time;
        private readonly float _value;
        private readonly InterpolationType _interpolation;

        public AddFloatKeyCommand(string animationId, string trackId, float time, float value, InterpolationType interpolation = InterpolationType.Linear)
        {
            _animationId = animationId;
            _trackId = trackId;
            _time = time;
            _value = value;
            _interpolation = interpolation;
        }

        public string UndoLabel => "Add Key";

        public bool Validate(FaceMotionProject project, out FaceMotionDiagnostic error)
        {
            if (!IsValidTime(_time))
            {
                error = CommandDiagnostics.InvalidArgument("Key time must be a finite non-negative value.", _trackId);
                return false;
            }

            if (!project.TryGetAnimation(_animationId, out var animation) || animation.Timeline == null
                || !animation.Timeline.TryGetTrack(_trackId, out var track))
            {
                error = CommandDiagnostics.TargetNotFound("track", _trackId);
                return false;
            }

            if (track.Kind != TrackKind.BlendShape || track.BlendShape == null)
            {
                error = CommandDiagnostics.KindMismatch("A float key requires a blend shape track.", _trackId);
                return false;
            }

            error = null;
            return true;
        }

        public void Execute(FaceMotionProject project)
        {
            if (project.TryGetAnimation(_animationId, out var animation)
                && animation.Timeline != null
                && animation.Timeline.TryGetTrack(_trackId, out var track))
            {
                track.BlendShape.AddKey(FloatKeyframeData.Create(_time, _value, _interpolation));
                track.BlendShape.SortKeys();
            }
        }

        private static bool IsValidTime(float time)
        {
            return !float.IsNaN(time) && !float.IsInfinity(time) && time >= 0f;
        }
    }

    /// <summary>Adds a manual vector3 key to a transform track.</summary>
    public sealed class AddVector3KeyCommand : IProjectCommand
    {
        private readonly string _animationId;
        private readonly string _trackId;
        private readonly float _time;
        private readonly Vector3 _value;
        private readonly InterpolationType _interpolation;

        public AddVector3KeyCommand(string animationId, string trackId, float time, Vector3 value, InterpolationType interpolation = InterpolationType.Linear)
        {
            _animationId = animationId;
            _trackId = trackId;
            _time = time;
            _value = value;
            _interpolation = interpolation;
        }

        public string UndoLabel => "Add Key";

        public bool Validate(FaceMotionProject project, out FaceMotionDiagnostic error)
        {
            if (!IsValidTime(_time))
            {
                error = CommandDiagnostics.InvalidArgument("Key time must be a finite non-negative value.", _trackId);
                return false;
            }

            if (!project.TryGetAnimation(_animationId, out var animation) || animation.Timeline == null
                || !animation.Timeline.TryGetTrack(_trackId, out var track))
            {
                error = CommandDiagnostics.TargetNotFound("track", _trackId);
                return false;
            }

            if (!TrackKinds.IsTransform(track.Kind) || track.Transform == null)
            {
                error = CommandDiagnostics.KindMismatch("A vector3 key requires a transform track.", _trackId);
                return false;
            }

            error = null;
            return true;
        }

        public void Execute(FaceMotionProject project)
        {
            if (project.TryGetAnimation(_animationId, out var animation)
                && animation.Timeline != null
                && animation.Timeline.TryGetTrack(_trackId, out var track))
            {
                track.Transform.AddKey(Vector3KeyframeData.Create(_time, _value, _interpolation));
                track.Transform.SortKeys();
            }
        }

        private static bool IsValidTime(float time)
        {
            return !float.IsNaN(time) && !float.IsInfinity(time) && time >= 0f;
        }
    }
}