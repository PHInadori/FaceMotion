using System.Collections.Generic;
using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Editor.UI.Session;
using UnityEditor;

namespace FaceMotion.Editor.UI.Controllers
{
    /// <summary>Outcome of a controller operation.</summary>
    public sealed class OperationResult
    {
        public bool Succeeded;

        public FaceMotionDiagnostic Error;
    }

    /// <summary>
    /// Runs validated commands through the project command infrastructure inside one Undo
    /// group, then refreshes selection safety and diagnostics. Every user gesture maps to
    /// exactly one call here (or one RunBatch) so 1 action = 1 Undo step.
    /// </summary>
    public static class UICommandRunner
    {
        public static OperationResult Run(FaceMotionEditorSession session, IProjectCommand command)
        {
            var result = new OperationResult();
            FaceMotionProject project = session?.ActiveProject;
            if (project == null)
            {
                result.Error = ControllerDiagnostics.NoProject();
                session?.RefreshAll();
                return result;
            }

            Undo.IncrementCurrentGroup();
            using (var transaction = new UnityUndoTransaction())
            {
                result.Succeeded = ProjectCommandExecutor.TryExecute(command, project, transaction, out result.Error);
            }

            session.RefreshAll();
            return result;
        }

        /// <summary>
        /// Runs a batch of commands inside ONE Undo group (used for multi-track operations
        /// such as moving selected keys across tracks). All commands validate before any
        /// mutation, so a failed batch leaves the project untouched.
        /// </summary>
        public static OperationResult RunBatch(
            FaceMotionEditorSession session,
            IReadOnlyList<IProjectCommand> commands,
            string undoLabel)
        {
            var result = new OperationResult();
            FaceMotionProject project = session?.ActiveProject;
            if (project == null)
            {
                result.Error = ControllerDiagnostics.NoProject();
                session?.RefreshAll();
                return result;
            }

            if (commands != null)
            {
                for (int i = 0; i < commands.Count; i++)
                {
                    if (!commands[i].Validate(project, out result.Error))
                    {
                        session.RefreshAll();
                        return result;
                    }
                }
            }

            Undo.IncrementCurrentGroup();
            using (var transaction = new UnityUndoTransaction())
            {
                transaction.Begin(project, undoLabel ?? "FaceMotion change");
                if (commands != null)
                {
                    for (int i = 0; i < commands.Count; i++)
                    {
                        commands[i].Execute(project);
                    }
                }

                transaction.Commit();
            }

            result.Succeeded = true;
            session.RefreshAll();
            return result;
        }
    }
}