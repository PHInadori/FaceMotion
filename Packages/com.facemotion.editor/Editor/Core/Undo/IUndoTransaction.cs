using System;

namespace FaceMotion.Editor
{
    /// <summary>
    /// Undo boundary around one mutation. Core defines the contract only; UnityEditor is
    /// never referenced here. The Unity layer implements this with RegisterCompleteObjectUndo.
    /// </summary>
    public interface IUndoTransaction : IDisposable
    {
        /// <summary>Records the pre-mutation snapshot. Called before IProjectCommand.Execute.</summary>
        void Begin(FaceMotion.Data.FaceMotionProject project, string undoLabel);

        /// <summary>Finalizes the mutation and marks the project dirty.</summary>
        void Commit();

        /// <summary>Discards the transaction; the recorded snapshot remains usable for Undo.</summary>
        void Rollback();
    }
}