using FaceMotion.Data;
using FaceMotion.Diagnostics;

namespace FaceMotion.Editor
{
    /// <summary>Adds a new animation with default timeline settings.</summary>
    public sealed class AddAnimationCommand : IProjectCommand
    {
        private readonly string _displayName;

        public AddAnimationCommand(string displayName)
        {
            _displayName = displayName ?? string.Empty;
        }

        public string UndoLabel => "Add Animation";

        public bool Validate(FaceMotionProject project, out FaceMotionDiagnostic error)
        {
            if (!StableId.IsValid(project.ProjectId))
            {
                error = CommandDiagnostics.InvalidProject(project.ProjectId);
                return false;
            }

            error = null;
            return true;
        }

        public void Execute(FaceMotionProject project)
        {
            project.AddAnimation(FaceMotionAnimationData.Create(_displayName));
        }
    }

    /// <summary>Removes an animation by stable ID.</summary>
    public sealed class RemoveAnimationCommand : IProjectCommand
    {
        private readonly string _animationId;

        public RemoveAnimationCommand(string animationId)
        {
            _animationId = animationId;
        }

        public string UndoLabel => "Remove Animation";

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
            project.RemoveAnimation(_animationId);
        }
    }
}