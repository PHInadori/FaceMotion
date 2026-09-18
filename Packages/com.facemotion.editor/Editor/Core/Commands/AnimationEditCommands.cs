using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Timeline;

namespace FaceMotion.Editor
{
    /// <summary>Renames an animation's display name.</summary>
    public sealed class RenameAnimationCommand : IProjectCommand
    {
        private readonly string _animationId;
        private readonly string _displayName;

        public RenameAnimationCommand(string animationId, string displayName)
        {
            _animationId = animationId;
            _displayName = displayName == null ? string.Empty : displayName.Trim();
        }

        public string UndoLabel => "Rename Animation";

        public bool Validate(FaceMotionProject project, out FaceMotionDiagnostic error)
        {
            if (!project.TryGetAnimation(_animationId, out _))
            {
                error = CommandDiagnostics.TargetNotFound("animation", _animationId);
                return false;
            }

            if (string.IsNullOrWhiteSpace(_displayName))
            {
                error = CommandDiagnostics.InvalidArgument("Animation name cannot be empty.", _animationId);
                return false;
            }

            error = null;
            return true;
        }

        public void Execute(FaceMotionProject project)
        {
            if (project.TryGetAnimation(_animationId, out var animation))
            {
                animation.DisplayName = _displayName;
            }
        }
    }

    /// <summary>
    /// Duplicates an animation using the project-level provenance semantics. The created
    /// animation ID is exposed for selection.
    /// </summary>
    public sealed class DuplicateAnimationCommand : IProjectCommand
    {
        private readonly string _animationId;

        public DuplicateAnimationCommand(string animationId)
        {
            _animationId = animationId;
        }

        public string CreatedAnimationId { get; private set; }

        public string UndoLabel => "Duplicate Animation";

        public bool Validate(FaceMotionProject project, out FaceMotionDiagnostic error)
        {
            if (!project.TryGetAnimation(_animationId, out _))
            {
                error = CommandDiagnostics.TargetNotFound("animation", _animationId);
                return false;
            }

            error = null;
            return true;
        }

        public void Execute(FaceMotionProject project)
        {
            var duplicate = project.DuplicateAnimation(_animationId);
            if (duplicate != null)
            {
                CreatedAnimationId = duplicate.AnimationId;
            }
        }
    }

    /// <summary>Sets the selected animation's timeline duration.</summary>
    public sealed class SetTimelineDurationCommand : IProjectCommand
    {
        private readonly string _animationId;
        private readonly float _duration;

        public SetTimelineDurationCommand(string animationId, float duration)
        {
            _animationId = animationId;
            _duration = duration;
        }

        public string UndoLabel => "Set Duration";

        public bool Validate(FaceMotionProject project, out FaceMotionDiagnostic error)
        {
            if (!IsValidDuration(_duration))
            {
                error = CommandDiagnostics.InvalidArgument("Duration must be a finite value greater than zero.", _animationId);
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
            if (project.TryGetAnimation(_animationId, out var animation) && animation.Timeline != null)
            {
                animation.Timeline.Duration = _duration;
            }
        }

        private static bool IsValidDuration(float duration)
        {
            return !float.IsNaN(duration) && !float.IsInfinity(duration) && duration > 0f;
        }
    }

    /// <summary>Sets the selected animation's timeline frame rate.</summary>
    public sealed class SetTimelineFrameRateCommand : IProjectCommand
    {
        private readonly string _animationId;
        private readonly float _frameRate;

        public SetTimelineFrameRateCommand(string animationId, float frameRate)
        {
            _animationId = animationId;
            _frameRate = frameRate;
        }

        public string UndoLabel => "Set Frame Rate";

        public bool Validate(FaceMotionProject project, out FaceMotionDiagnostic error)
        {
            if (!IsValidFrameRate(_frameRate))
            {
                error = CommandDiagnostics.InvalidArgument("Frame rate must be a finite value greater than zero.", _animationId);
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
            if (project.TryGetAnimation(_animationId, out var animation) && animation.Timeline != null)
            {
                animation.Timeline.FrameRate = _frameRate;
            }
        }

        private static bool IsValidFrameRate(float frameRate)
        {
            return !float.IsNaN(frameRate) && !float.IsInfinity(frameRate) && frameRate > 0f;
        }
    }

    /// <summary>Sets the selected animation's timeline loop flag.</summary>
    public sealed class SetTimelineLoopCommand : IProjectCommand
    {
        private readonly string _animationId;
        private readonly bool _loop;

        public SetTimelineLoopCommand(string animationId, bool loop)
        {
            _animationId = animationId;
            _loop = loop;
        }

        public string UndoLabel => "Set Loop";

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
            if (project.TryGetAnimation(_animationId, out var animation) && animation.Timeline != null)
            {
                animation.Timeline.Loop = _loop;
            }
        }
    }

    /// <summary>
    /// Finds an existing track in an animation that binds the same identity, so the UI can
    /// reject duplicate tracks on the safe side. Ordinal case-sensitive identity matching
    /// mirrors the resolver contract.
    /// </summary>
    public static class TrackDuplicateFinder
    {
        public static bool HasDuplicate(
            FaceMotionAnimationData animation,
            TrackKind kind,
            string rendererPath,
            string blendShapeName,
            string transformPath)
        {
            return FindDuplicate(animation, kind, rendererPath, blendShapeName, transformPath) != null;
        }

        public static FaceTrackData FindDuplicate(
            FaceMotionAnimationData animation,
            TrackKind kind,
            string rendererPath,
            string blendShapeName,
            string transformPath)
        {
            if (animation == null || animation.Timeline == null)
            {
                return null;
            }

            foreach (var track in animation.Timeline.Tracks)
            {
                if (track == null || track.Kind != kind)
                {
                    continue;
                }

                if (kind == TrackKind.BlendShape)
                {
                    if (track.BlendShape != null
                        && string.Equals(track.BlendShape.RendererPath, rendererPath, System.StringComparison.Ordinal)
                        && string.Equals(track.BlendShape.BlendShapeName, blendShapeName, System.StringComparison.Ordinal))
                    {
                        return track;
                    }
                }
                else if (track.Transform != null
                         && string.Equals(track.Transform.TransformPath, transformPath, System.StringComparison.Ordinal))
                {
                    return track;
                }
            }

            return null;
        }
    }
}
