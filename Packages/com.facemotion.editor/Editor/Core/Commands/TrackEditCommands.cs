using FaceMotion.Data;
using FaceMotion.Diagnostics;

namespace FaceMotion.Editor
{
    /// <summary>Removes a track from an animation.</summary>
    public sealed class RemoveTrackCommand : IProjectCommand
    {
        private readonly string _animationId;
        private readonly string _trackId;

        public RemoveTrackCommand(string animationId, string trackId)
        {
            _animationId = animationId;
            _trackId = trackId;
        }

        public string UndoLabel => "Remove Track";

        public bool Validate(FaceMotionProject project, out FaceMotionDiagnostic error)
        {
            if (!project.TryGetAnimation(_animationId, out var animation) || animation.Timeline == null)
            {
                error = CommandDiagnostics.TargetNotFound("animation", _animationId);
                return false;
            }

            if (!animation.Timeline.TryGetTrack(_trackId, out _))
            {
                error = CommandDiagnostics.TargetNotFound("track", _trackId);
                return false;
            }

            error = null;
            return true;
        }

        public void Execute(FaceMotionProject project)
        {
            if (project.TryGetAnimation(_animationId, out var animation) && animation.Timeline != null)
            {
                animation.Timeline.RemoveTrack(_trackId);
            }
        }
    }
}