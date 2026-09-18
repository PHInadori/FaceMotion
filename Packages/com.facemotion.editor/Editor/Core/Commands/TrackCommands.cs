using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Timeline;

namespace FaceMotion.Editor
{
    /// <summary>Adds a blend shape track to an animation.</summary>
    public sealed class AddBlendShapeTrackCommand : IProjectCommand
    {
        private readonly string _animationId;
        private readonly string _rendererPath;
        private readonly string _blendShapeName;

        public AddBlendShapeTrackCommand(string animationId, string rendererPath, string blendShapeName)
        {
            _animationId = animationId;
            _rendererPath = rendererPath ?? string.Empty;
            _blendShapeName = blendShapeName ?? string.Empty;
        }

        public string UndoLabel => "Add Blend Shape Track";

        public bool Validate(FaceMotionProject project, out FaceMotionDiagnostic error)
        {
            if (!project.TryGetAnimation(_animationId, out var animation) || animation.Timeline == null)
            {
                error = CommandDiagnostics.TargetNotFound("animation", _animationId);
                return false;
            }

            error = null;
            return true;
        }

        public void Execute(FaceMotionProject project)
        {
            if (project.TryGetAnimation(_animationId, out var animation))
            {
                animation.Timeline.AddTrack(FaceTrackData.CreateBlendShape(_rendererPath, _blendShapeName));
            }
        }
    }

    /// <summary>Adds a newly bound blend-shape track with its scene-derived value at frame zero.</summary>
    public sealed class AddBlendShapeTrackWithInitialKeyCommand : IProjectCommand
    {
        private readonly string _animationId;
        private readonly string _rendererPath;
        private readonly string _blendShapeName;
        private readonly float _initialValue;

        public AddBlendShapeTrackWithInitialKeyCommand(string animationId, string rendererPath, string blendShapeName, float initialValue)
        {
            _animationId = animationId;
            _rendererPath = rendererPath ?? string.Empty;
            _blendShapeName = blendShapeName ?? string.Empty;
            _initialValue = initialValue;
        }

        public string CreatedTrackId { get; private set; }
        public string UndoLabel => "Add Blend Shape Track";

        public bool Validate(FaceMotionProject project, out FaceMotionDiagnostic error)
        {
            if (float.IsNaN(_initialValue) || float.IsInfinity(_initialValue))
            {
                error = CommandDiagnostics.InvalidArgument("Initial blend shape value must be finite.", _blendShapeName);
                return false;
            }
            if (!project.TryGetAnimation(_animationId, out var animation) || animation.Timeline == null)
            {
                error = CommandDiagnostics.TargetNotFound("animation", _animationId);
                return false;
            }
            error = null;
            return true;
        }

        public void Execute(FaceMotionProject project)
        {
            if (!project.TryGetAnimation(_animationId, out var animation) || animation.Timeline == null) return;
            var track = FaceTrackData.CreateBlendShape(_rendererPath, _blendShapeName);
            track.BlendShape.AddKey(FloatKeyframeData.Create(0f, _initialValue, InterpolationType.Linear));
            track.BlendShape.SortKeys();
            animation.Timeline.AddTrack(track);
            CreatedTrackId = track.TrackId;
        }
    }

    /// <summary>Adds a transform track (position, rotation, or scale) to an animation.</summary>
    public sealed class AddTransformTrackCommand : IProjectCommand
    {
        private readonly string _animationId;
        private readonly TrackKind _kind;
        private readonly string _transformPath;
        private readonly MotionRotationMode _rotationMode;

        public AddTransformTrackCommand(string animationId, TrackKind kind, string transformPath, MotionRotationMode rotationMode = MotionRotationMode.ShortestQuaternion)
        {
            _animationId = animationId;
            _kind = kind;
            _transformPath = transformPath ?? string.Empty;
            _rotationMode = rotationMode;
        }

        public string UndoLabel => "Add Transform Track";

        public bool Validate(FaceMotionProject project, out FaceMotionDiagnostic error)
        {
            if (!TrackKinds.IsTransform(_kind))
            {
                error = CommandDiagnostics.InvalidArgument($"Track kind {_kind} is not a transform kind.", _animationId);
                return false;
            }

            if (!project.TryGetAnimation(_animationId, out var animation) || animation.Timeline == null)
            {
                error = CommandDiagnostics.TargetNotFound("animation", _animationId);
                return false;
            }

            error = null;
            return true;
        }

        public void Execute(FaceMotionProject project)
        {
            if (project.TryGetAnimation(_animationId, out var animation))
            {
                animation.Timeline.AddTrack(FaceTrackData.CreateTransform(_kind, _transformPath, true, _rotationMode));
            }
        }
    }
}
