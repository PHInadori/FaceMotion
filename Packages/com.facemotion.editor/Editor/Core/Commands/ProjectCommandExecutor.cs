using FaceMotion.Data;
using FaceMotion.Diagnostics;

namespace FaceMotion.Editor
{
    /// <summary>Runs a validated command inside an Undo transaction.</summary>
    public static class ProjectCommandExecutor
    {
        public static bool TryExecute(
            IProjectCommand command,
            FaceMotionProject project,
            IUndoTransaction transaction,
            out FaceMotionDiagnostic error)
        {
            error = null;
            if (command == null)
            {
                throw new System.ArgumentNullException(nameof(command));
            }

            if (project == null)
            {
                throw new System.ArgumentNullException(nameof(project));
            }

            if (transaction == null)
            {
                throw new System.ArgumentNullException(nameof(transaction));
            }

            if (!command.Validate(project, out error))
            {
                return false;
            }

            transaction.Begin(project, command.UndoLabel);
            command.Execute(project);
            transaction.Commit();
            return true;
        }
    }
}