using System;
using FaceMotion.Data;

namespace FaceMotion.Editor
{
    /// <summary>
    /// One editor operation against a project. Data is never edited directly from UI;
    /// UI and controllers go through commands so validation and Undo stay centralized.
    /// </summary>
    public interface IProjectCommand
    {
        string UndoLabel { get; }

        /// <summary>Validates preconditions before the Undo snapshot is recorded.</summary>
        bool Validate(FaceMotionProject project, out FaceMotion.Diagnostics.FaceMotionDiagnostic error);

        /// <summary>Mutates the project. Runs inside an opened IUndoTransaction.</summary>
        void Execute(FaceMotionProject project);
    }
}